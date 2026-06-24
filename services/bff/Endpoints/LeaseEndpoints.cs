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

        var query = from l in db.Leases.Where(l => l.TenantId == tenantId)
                    join c in db.Customers on l.CustomerId equals c.Id into cg
                    from c in cg.DefaultIfEmpty()
                    join v in db.Vehicles on l.VehicleId equals v.Id into vg
                    from v in vg.DefaultIfEmpty()
                    join d in db.Drivers on l.PrimaryDriverId equals d.Id into dg
                    from d in dg.DefaultIfEmpty()
                    join b in db.Branches on l.WorkingBranchId equals b.Id into bg
                    from b in bg.DefaultIfEmpty()
                    select new
                    {
                        l.Id, l.DisplayId, l.Status, l.ContractId,
                        l.ContractStartUtc, l.ContractEndUtc, l.ContractTypeCode,
                        RentAmountSar = l.RentAmount, l.CreatedAtUtc,
                        CustomerDisplayName = c != null ? c.DisplayName : "—",
                        VehicleMakeModel = v != null ? v.Make + " " + v.Model : "—",
                        VehiclePlate = v != null ? v.PlateNumber : "—",
                        PrimaryDriverName = d != null ? d.PersonNameEn : (string?)null,
                        WorkingBranchName = b != null ? b.NameEn : (string?)null,
                        CustomerId = l.CustomerId,
                        VehicleId = l.VehicleId,
                    };

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Domain.Leases.LeaseStatus>(status, true, out var st))
            query = query.Where(x => x.Status == st);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.CustomerDisplayName.Contains(s) ||
                x.VehicleMakeModel.Contains(s) ||
                x.VehiclePlate.Contains(s));
        }

        var total = await query.CountAsync(ct);
        var raw = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var items = raw.Select(x => new
        {
            x.Id, x.DisplayId,
            LeaseNumber = "LA-" + x.DisplayId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ContractId = x.ContractId ?? Guid.Empty,
            x.CustomerDisplayName, x.VehicleMakeModel, x.VehiclePlate,
            Status = x.Status.ToString(),
            x.ContractStartUtc, x.ContractEndUtc,
            ContractTypeCode = x.ContractTypeCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            x.RentAmountSar, x.PrimaryDriverName, x.WorkingBranchName,
        }).ToList();

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

        var result = await (
            from l in db.Leases.Where(l => l.TenantId == tenantId && l.Id == id)
            join c in db.Customers on l.CustomerId equals c.Id into cg from c in cg.DefaultIfEmpty()
            join v in db.Vehicles on l.VehicleId equals v.Id into vg from v in vg.DefaultIfEmpty()
            join d in db.Drivers on l.PrimaryDriverId equals d.Id into dg from d in dg.DefaultIfEmpty()
            join b in db.Branches on l.WorkingBranchId equals b.Id into bg from b in bg.DefaultIfEmpty()
            join ct2 in db.Contracts on l.ContractId equals ct2.Id into ctg from ct2 in ctg.DefaultIfEmpty()
            select new
            {
                l.Id, l.DisplayId, l.ContractId, l.CustomerId, l.VehicleId,
                l.PrimaryDriverId, l.Status, l.ContractTypeCode,
                l.ContractStartUtc, l.ContractEndUtc, l.RentAmount, l.PaidAmount,
                l.VatAmount, l.TotalAmount, l.RemainingAmount, l.AllowedKmPerDay,
                l.PaymentMethodCode, l.RentPolicyId, l.CreatedAtUtc,
                CustomerDisplayName = c != null ? c.DisplayName : "—",
                VehiclePlate = v != null ? v.PlateNumber : "—",
                VehicleMakeModel = v != null ? v.Make + " " + v.Model : "—",
                PrimaryDriverName = d != null ? d.PersonNameEn : (string?)null,
                WorkingBranchCode = b != null ? b.Code : "—",
                WorkingBranchName = b != null ? b.NameEn : "—",
                QuotationId = ct2 != null ? ct2.QuotationId : (Guid?)null,
            }
        ).FirstOrDefaultAsync(ct);

        if (result is null) return Results.NotFound();

        return Results.Ok(new
        {
            result.Id, result.DisplayId,
            LeaseNumber = "LA-" + result.DisplayId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ContractId = result.ContractId ?? Guid.Empty,
            QuotationId = result.QuotationId ?? Guid.Empty,
            CustomerId = result.CustomerId ?? Guid.Empty,
            result.CustomerDisplayName,
            VehicleId = result.VehicleId ?? Guid.Empty,
            result.VehiclePlate, result.VehicleMakeModel,
            result.PrimaryDriverId, result.PrimaryDriverName,
            Status = result.Status.ToString(),
            ContractTypeCode = result.ContractTypeCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.ContractStartUtc, result.ContractEndUtc,
            RentAmountSar = result.RentAmount,
            result.WorkingBranchCode, result.WorkingBranchName,
            result.CreatedAtUtc,
            RentPolicyId = result.RentPolicyId ?? Guid.Empty,
            PaidAmountSar = result.PaidAmount,
            VatAmountSar = result.VatAmount,
            TotalAmountSar = result.TotalAmount,
            RemainingAmountSar = result.RemainingAmount,
            AllowedKmPerDay = result.AllowedKmPerDay,
            PaymentMethodCode = result.PaymentMethodCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
