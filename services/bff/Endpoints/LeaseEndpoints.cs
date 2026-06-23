using AutoLeaseNet.Application.Leases;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Operations;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Bff.Endpoints;

/// <summary>
/// Lease endpoints: list + operations (check-in, extend, suspend).
/// </summary>
public static class LeaseEndpoints
{
    public static IEndpointRouteBuilder MapLeaseEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/leases").WithTags("leases");

        group.MapGet("", ListLeasesAsync)
            .WithName("ListLeases")
            .RequireAuthorization();

        group.MapGet("/{id:guid}", GetLeaseByIdAsync)
            .WithName("GetLeaseById")
            .RequireAuthorization();

        group.MapGet("/{id:guid}/damages", (Guid id) => Results.Ok(Array.Empty<object>()))
            .WithName("GetLeaseDamages")
            .RequireAuthorization();

        group.MapGet("/{id:guid}/violations", (Guid id) => Results.Ok(Array.Empty<object>()))
            .WithName("GetLeaseViolations")
            .RequireAuthorization();

        group.MapGet("/{id:guid}/payments", GetLeasePaymentsAsync)
            .WithName("GetLeasePayments")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/check-in", CheckInAsync)
            .WithName("CheckInLease")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/extend", ExtendAsync)
            .WithName("ExtendLease")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/suspend", SuspendAsync)
            .WithName("SuspendLease")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/activate", ActivateAsync)
            .WithName("ActivateLease")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/switch-vehicle", SwitchVehicleAsync)
            .WithName("SwitchLeaseVehicle")
            .RequireAuthorization();

        return group;
    }

    private static async Task<IResult> ListLeasesAsync(
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? status = null)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var query = db.Leases.AsNoTracking().Where(l => l.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Domain.Leases.LeaseStatus>(status, true, out var st))
            query = query.Where(l => l.Status == st);

        var total = await query.CountAsync(ct);
        var leases = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var customerIds = leases.Where(l => l.CustomerId.HasValue).Select(l => l.CustomerId!.Value).Distinct().ToList();
        var vehicleIds = leases.Where(l => l.VehicleId.HasValue).Select(l => l.VehicleId!.Value).Distinct().ToList();
        var driverIds = leases.Where(l => l.PrimaryDriverId.HasValue).Select(l => l.PrimaryDriverId!.Value).Distinct().ToList();
        var branchIds = leases.Where(l => l.WorkingBranchId.HasValue).Select(l => l.WorkingBranchId!.Value).Distinct().ToList();

        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => vehicleIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var drivers = await db.Drivers.AsNoTracking().Where(d => driverIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var branches = await db.Branches.AsNoTracking().Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);

        var items = leases.Select(l =>
        {
            var cust = l.CustomerId.HasValue && customers.TryGetValue(l.CustomerId.Value, out var c) ? c : null;
            var veh = l.VehicleId.HasValue && vehicles.TryGetValue(l.VehicleId.Value, out var v) ? v : null;
            var drv = l.PrimaryDriverId.HasValue && drivers.TryGetValue(l.PrimaryDriverId.Value, out var d) ? d : null;
            var br = l.WorkingBranchId.HasValue && branches.TryGetValue(l.WorkingBranchId.Value, out var b) ? b : null;
            return new
            {
                l.Id,
                l.DisplayId,
                LeaseNumber = "LA-" + l.DisplayId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ContractId = l.ContractId ?? Guid.Empty,
                CustomerDisplayName = cust?.DisplayName ?? "—",
                VehicleMakeModel = veh != null ? veh.Make + " " + veh.Model : "—",
                VehiclePlate = veh?.PlateNumber ?? "—",
                Status = l.Status.ToString(),
                l.ContractStartUtc,
                l.ContractEndUtc,
                ContractTypeCode = l.ContractTypeCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                RentAmountSar = l.RentAmount,
                PrimaryDriverName = drv?.PersonNameEn,
                WorkingBranchName = br?.NameEn,
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var filtered = items.Where(x =>
                (x.CustomerDisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (x.VehicleMakeModel.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (x.VehiclePlate.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
            return Results.Ok(new { items = filtered, page, pageSize, totalCount = filtered.Count });
        }

        return Results.Ok(new { items, page, pageSize, totalCount = total });
    }

    private static async Task<IResult> GetLeaseByIdAsync(
        Guid id,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var lease = await db.Leases.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.Id == id)
            .FirstOrDefaultAsync(ct);

        if (lease is null) return Results.NotFound();

        var cust = lease.CustomerId.HasValue
            ? await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == lease.CustomerId.Value, ct)
            : null;
        var veh = lease.VehicleId.HasValue
            ? await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == lease.VehicleId.Value, ct)
            : null;
        var drv = lease.PrimaryDriverId.HasValue
            ? await db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == lease.PrimaryDriverId.Value, ct)
            : null;
        var br = lease.WorkingBranchId.HasValue
            ? await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == lease.WorkingBranchId.Value, ct)
            : null;

        // Lookup quotationId via contract
        Guid quotationId = Guid.Empty;
        if (lease.ContractId.HasValue)
        {
            var parentContract = await db.Contracts.AsNoTracking()
                .Where(c => c.Id == lease.ContractId.Value)
                .Select(c => new { c.QuotationId })
                .FirstOrDefaultAsync(ct);
            if (parentContract?.QuotationId != null) quotationId = parentContract.QuotationId.Value;
        }

        return Results.Ok(new
        {
            lease.Id,
            lease.DisplayId,
            LeaseNumber = "LA-" + lease.DisplayId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ContractId = lease.ContractId ?? Guid.Empty,
            QuotationId = quotationId,
            CustomerId = lease.CustomerId ?? Guid.Empty,
            CustomerDisplayName = cust?.DisplayName ?? "—",
            VehicleId = lease.VehicleId ?? Guid.Empty,
            VehiclePlate = veh?.PlateNumber ?? "—",
            VehicleMakeModel = veh != null ? veh.Make + " " + veh.Model : "—",
            PrimaryDriverId = lease.PrimaryDriverId,
            PrimaryDriverName = drv?.PersonNameEn,
            Status = lease.Status.ToString(),
            ContractTypeCode = lease.ContractTypeCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            lease.ContractStartUtc,
            lease.ContractEndUtc,
            RentAmountSar = lease.RentAmount,
            WorkingBranchCode = br?.Code ?? "—",
            WorkingBranchName = br?.NameEn ?? "—",
            lease.CreatedAtUtc,
            RentPolicyId = lease.RentPolicyId ?? Guid.Empty,
            PaidAmountSar = lease.PaidAmount,
            VatAmountSar = lease.VatAmount,
            TotalAmountSar = lease.TotalAmount,
            RemainingAmountSar = lease.RemainingAmount,
            AllowedKmPerDay = lease.AllowedKmPerDay,
            PaymentMethodCode = lease.PaymentMethodCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            IssuedAtUtc = (string?)null,
            SuspendedAtUtc = (string?)null,
            ResumedAtUtc = (string?)null,
            ClosedAtUtc = (string?)null,
            CancelledAtUtc = (string?)null,
            ZatcaSubmissionStatus = (string?)null,
            ZatcaInvoiceNumber = (string?)null,
            Inspections = Array.Empty<object>(),
            Incidents = Array.Empty<object>(),
        });
    }

    private static async Task<IResult> GetLeasePaymentsAsync(
        Guid id,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var lease = await db.Leases.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.Id == id)
            .FirstOrDefaultAsync(ct);
        if (lease is null) return Results.NotFound();

        var leaseInvoiceIds = await db.Invoices.AsNoTracking()
            .Where(i => i.LeaseId == id && i.TenantId == tenantId)
            .Select(i => i.Id)
            .ToListAsync(ct);

        var allocPaymentIds = await db.PaymentAllocations.AsNoTracking()
            .Where(a => leaseInvoiceIds.Contains(a.InvoiceId))
            .Select(a => a.AdvancePaymentId)
            .Distinct()
            .ToListAsync(ct);

        var customerPaymentIds = lease.CustomerId.HasValue
            ? await db.AdvancePayments.AsNoTracking()
                .Where(p => p.CustomerId == lease.CustomerId.Value && p.TenantId == tenantId)
                .Select(p => p.Id)
                .ToListAsync(ct)
            : new List<Guid>();

        var paymentIds = allocPaymentIds.Union(customerPaymentIds).Distinct().ToList();

        var payments = await db.AdvancePayments.AsNoTracking()
            .Where(p => paymentIds.Contains(p.Id))
            .Join(db.Customers.AsNoTracking(), p => p.CustomerId, c => c.Id, (p, c) => new { p, c })
            .Select(x => new
            {
                x.p.Id,
                x.p.DisplayId,
                x.p.CustomerId,
                CustomerDisplayName = x.c.DisplayName,
                x.p.Amount,
                x.p.PaymentMethod,
                x.p.ReceivedDate,
                x.p.ReferenceNumber,
                x.p.Notes,
                x.p.RemainingBalance,
                x.p.CreatedAtUtc,
            })
            .OrderByDescending(p => p.ReceivedDate)
            .ToListAsync(ct);

        var allAllocs = await db.PaymentAllocations.AsNoTracking()
            .Where(a => paymentIds.Contains(a.AdvancePaymentId))
            .Select(a => new
            {
                a.Id,
                a.AdvancePaymentId,
                a.InvoiceId,
                a.InvoiceNumber,
                a.AllocatedAmountSar,
                a.AllocatedAtUtc,
            })
            .ToListAsync(ct);

        var items = payments.Select(p => new
        {
            p.Id,
            p.DisplayId,
            p.CustomerId,
            p.CustomerDisplayName,
            p.Amount,
            p.PaymentMethod,
            p.ReceivedDate,
            p.ReferenceNumber,
            p.Notes,
            p.RemainingBalance,
            Allocations = allAllocs.Where(a => a.AdvancePaymentId == p.Id).ToList(),
            p.CreatedAtUtc,
        }).ToList();

        return Results.Ok(items);
    }

    private static async Task<IResult> ExtendAsync(
        HttpContext ctx,
        IMediator mediator,
        Guid id,
        ExtendLeaseRequest body,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "POST /leases/{id}/extend requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (body is null)
        {
            return Results.Problem(
                title: "Missing request body",
                detail: "POST /leases/{id}/extend requires a JSON body with at least newContractEndUtc.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new ExtendLeaseCommand
        {
            IdempotencyKey = idempotencyKey,
            LeaseId = id,
            NewContractEndUtc = body.NewContractEndUtc,
            ExtensionReasonCode = body.ExtensionReasonCode,
            AdditionalCharges = body.AdditionalCharges,
            PaymentMethodCode = body.PaymentMethodCode,
        };

        var result = await mediator.Send(command, ct);
        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "lease.not_found" => StatusCodes.Status404NotFound,
                "tajeer.extend.transient" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status422UnprocessableEntity,
            };
            return Results.Problem(title: result.ErrorCode ?? "lease.extend.error", detail: result.ErrorMessage, statusCode: status);
        }

        return Results.Ok(new
        {
            leaseId = result.LeaseId,
            status = result.LeaseStatus,
            newContractEndUtc = result.NewContractEndUtc,
            extensionCount = result.ExtensionCount,
            charges = result.Charges is null ? null : new
            {
                totalDue = result.Charges.TotalDue,
                vatAmount = result.Charges.VatAmount,
                grandTotal = result.Charges.GrandTotal,
            },
        });
    }

    private static async Task<IResult> SuspendAsync(
        HttpContext ctx,
        IMediator mediator,
        Guid id,
        SuspendLeaseRequest body,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "POST /leases/{id}/suspend requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (body is null)
        {
            return Results.Problem(
                title: "Missing request body",
                detail: "POST /leases/{id}/suspend requires a JSON body with at least suspensionReasonCode.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new SuspendLeaseCommand
        {
            IdempotencyKey = idempotencyKey,
            LeaseId = id,
            SuspensionReasonCode = body.SuspensionReasonCode,
            Notes = body.Notes,
        };

        var result = await mediator.Send(command, ct);
        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "lease.not_found" => StatusCodes.Status404NotFound,
                "tajeer.suspend.transient" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status422UnprocessableEntity,
            };
            return Results.Problem(title: result.ErrorCode ?? "lease.suspend.error", detail: result.ErrorMessage, statusCode: status);
        }

        return Results.Ok(new
        {
            leaseId = result.LeaseId,
            status = result.LeaseStatus,
            suspensionReasonCode = result.SuspensionReasonCode,
            suspendedAtUtc = result.SuspendedAtUtc,
        });
    }

    private static async Task<IResult> CheckInAsync(
        HttpContext ctx,
        IMediator mediator,
        Guid id,
        CheckInLeaseRequest body,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "POST /leases/{id}/check-in requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (body is null)
        {
            return Results.Problem(
                title: "Missing request body",
                detail: "POST /leases/{id}/check-in requires a JSON body with at least odometerKm + fuelLevel + closureMainReasonCode.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new CheckInLeaseCommand
        {
            IdempotencyKey = idempotencyKey,
            LeaseId = id,
            OdometerKm = body.OdometerKm,
            FuelLevel = body.FuelLevel,
            AcCondition = body.AcCondition,
            RadioStereoCondition = body.RadioStereoCondition,
            ScreenCondition = body.ScreenCondition,
            SpeedometerCondition = body.SpeedometerCondition,
            KeysCondition = body.KeysCondition,
            CarSeatsCondition = body.CarSeatsCondition,
            SafetyTriangleCondition = body.SafetyTriangleCondition,
            FireExtinguisherCondition = body.FireExtinguisherCondition,
            FirstAidKitCondition = body.FirstAidKitCondition,
            SpareTireToolsCondition = body.SpareTireToolsCondition,
            TiresCondition = body.TiresCondition,
            SpareTireCondition = body.SpareTireCondition,
            Notes = body.Notes,
            SketchInfoJson = body.SketchInfoJson,
            DamagesObserved = body.DamagesObserved,
            ReturnConditionNotes = body.ReturnConditionNotes,
            ClosureMainReasonCode = body.ClosureMainReasonCode,
            ClosureSubReasonCode = body.ClosureSubReasonCode,
            ExtraKm = body.ExtraKm,
            AdditionalCharges = body.AdditionalCharges,
            DiscountAmount = body.DiscountAmount,
            FinalPaidAmount = body.FinalPaidAmount,
        };

        var result = await mediator.Send(command, ct);
        if (!result.Success)
        {
            var status = result.ErrorCode switch
            {
                "lease.not_found" => StatusCodes.Status404NotFound,
                "tajeer.calculate.transient" or "tajeer.close.transient" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status422UnprocessableEntity,
            };
            return Results.Problem(title: result.ErrorCode ?? "lease.check_in.error", detail: result.ErrorMessage, statusCode: status);
        }

        return Results.Ok(new
        {
            leaseId = result.LeaseId,
            inspectionId = result.InspectionId,
            status = result.LeaseStatus,
            payment = result.Payment is null ? null : new
            {
                rentAmount = result.Payment.RentAmount,
                paidAmount = result.Payment.PaidAmount,
                lateHoursFee = result.Payment.LateHoursFee,
                extraKmFee = result.Payment.ExtraKmFee,
                damagesFee = result.Payment.DamagesFee,
                discountAmount = result.Payment.DiscountAmount,
                totalDue = result.Payment.TotalDue,
                vatAmount = result.Payment.VatAmount,
                grandTotal = result.Payment.GrandTotal,
                finalPaidAmount = result.Payment.FinalPaidAmount,
            },
        });
    }

    private static async Task<IResult> ActivateAsync(
        Guid id,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var lease = await db.Leases
            .Where(l => l.TenantId == tenantId && l.Id == id)
            .FirstOrDefaultAsync(ct);

        if (lease is null) return Results.NotFound();

        if (lease.Status == Domain.Leases.LeaseStatus.PendingIssuance)
        {
            // Use the domain method for the canonical PendingIssuance -> Active transition
            lease.MarkIssued(startKm: null, startFuelLevelCode: null, conditionNotes: null, DateTimeOffset.UtcNow);
        }
        else if (lease.Status == Domain.Leases.LeaseStatus.Draft)
        {
            // Draft has no domain method — use EF Entry to set properties directly
            var now = DateTimeOffset.UtcNow;
            db.Entry(lease).Property("Status").CurrentValue = Domain.Leases.LeaseStatus.Active;
            db.Entry(lease).Property("IssuedAtUtc").CurrentValue = (DateTimeOffset?)now;
            db.Entry(lease).Property("UpdatedAtUtc").CurrentValue = now;
        }
        else
        {
            return Results.Problem(
                title: "lease.invalid_status",
                detail: $"Cannot activate lease from status '{lease.Status}'. Lease must be in Draft or PendingIssuance status.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            leaseId = lease.Id,
            status = "Active",
            issuedAtUtc = lease.IssuedAtUtc,
        });
    }

    private static async Task<IResult> SwitchVehicleAsync(
        HttpContext ctx,
        Guid id,
        SwitchVehicleRequest body,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Missing Idempotency-Key",
                detail: "POST /leases/{id}/switch-vehicle requires an 'Idempotency-Key' header.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (body is null)
        {
            return Results.Problem(
                title: "Missing request body",
                detail: "POST /leases/{id}/switch-vehicle requires a JSON body with newVehicleId, reason, and odometer.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var lease = await db.Leases
            .Where(l => l.TenantId == tenantId && l.Id == id)
            .FirstOrDefaultAsync(ct);

        if (lease is null) return Results.NotFound();

        if (lease.Status == Domain.Leases.LeaseStatus.Closed || lease.Status == Domain.Leases.LeaseStatus.Cancelled)
        {
            return Results.Problem(
                title: "lease.invalid_status",
                detail: $"Cannot switch vehicle on lease with status '{lease.Status}'.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        // Look up previous vehicle plate before switching
        var previousVehiclePlate = "—";
        if (lease.VehicleId.HasValue)
        {
            var prevVeh = await db.Vehicles.AsNoTracking()
                .Where(v => v.Id == lease.VehicleId.Value)
                .FirstOrDefaultAsync(ct);
            if (prevVeh is not null) previousVehiclePlate = prevVeh.PlateNumber;
        }

        // Look up the new vehicle to confirm it exists and get its plate
        var newVehicle = await db.Vehicles.AsNoTracking()
            .Where(v => v.Id == body.NewVehicleId && v.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (newVehicle is null)
        {
            return Results.Problem(
                title: "vehicle.not_found",
                detail: $"Vehicle {body.NewVehicleId} not found for tenant.",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Update VehicleId via EF Entry (private setter)
        db.Entry(lease).Property("VehicleId").CurrentValue = (Guid?)body.NewVehicleId;
        db.Entry(lease).Property("UpdatedAtUtc").CurrentValue = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            success = true,
            leaseId = lease.Id,
            newVehiclePlate = newVehicle.PlateNumber,
            previousVehiclePlate,
        });
    }
}

public sealed record CheckInLeaseRequest
{
    public required int OdometerKm { get; init; }
    public required FuelLevel FuelLevel { get; init; }
    public byte? AcCondition { get; init; }
    public byte? RadioStereoCondition { get; init; }
    public byte? ScreenCondition { get; init; }
    public byte? SpeedometerCondition { get; init; }
    public byte? KeysCondition { get; init; }
    public byte? CarSeatsCondition { get; init; }
    public byte? SafetyTriangleCondition { get; init; }
    public byte? FireExtinguisherCondition { get; init; }
    public byte? FirstAidKitCondition { get; init; }
    public byte? SpareTireToolsCondition { get; init; }
    public byte? TiresCondition { get; init; }
    public byte? SpareTireCondition { get; init; }
    public string? Notes { get; init; }
    public string? SketchInfoJson { get; init; }
    public string? DamagesObserved { get; init; }
    public string? ReturnConditionNotes { get; init; }
    public required int ClosureMainReasonCode { get; init; }
    public int? ClosureSubReasonCode { get; init; }

    /// <summary>Caller-declared extra-km overage. Optional — Tajeer can compute from contract allowance.</summary>
    public int? ExtraKm { get; init; }
    /// <summary>Caller-declared additional charges (damages, cleaning, refuelling, etc.).</summary>
    public decimal? AdditionalCharges { get; init; }
    /// <summary>Discount applied at close — Tajeer validates server-side.</summary>
    public decimal? DiscountAmount { get; init; }
    /// <summary>What ops actually collected at the counter — passed to Tajeer's CloseContract.</summary>
    public decimal? FinalPaidAmount { get; init; }
}

public sealed record ExtendLeaseRequest
{
    /// <summary>New UTC contract end — must be strictly after the current one.</summary>
    public required DateTimeOffset NewContractEndUtc { get; init; }
    public int? ExtensionReasonCode { get; init; }
    public decimal? AdditionalCharges { get; init; }
    public int? PaymentMethodCode { get; init; }
}

public sealed record SuspendLeaseRequest
{
    public required int SuspensionReasonCode { get; init; }
    public string? Notes { get; init; }
}

public sealed record SwitchVehicleRequest
{
    public required Guid NewVehicleId { get; init; }
    public required string Reason { get; init; }
    public required int Odometer { get; init; }
    public string? Notes { get; init; }
}
