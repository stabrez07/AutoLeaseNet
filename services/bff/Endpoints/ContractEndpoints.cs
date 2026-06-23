using System.Globalization;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Contracts;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Domain.Vehicles;
using AutoLeaseNet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Bff.Endpoints;

public static class ContractEndpoints
{
    public static IEndpointRouteBuilder MapContractEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/contracts").WithTags("contracts");

        group.MapGet("", ListContractsAsync).WithName("ListContracts").RequireAuthorization();
        group.MapGet("/{id:guid}", GetContractByIdAsync).WithName("GetContractById").RequireAuthorization();
        group.MapGet("/{id:guid}/lease-agreements", GetContractLeaseAgreementsAsync).WithName("GetContractLeaseAgreements").RequireAuthorization();
        group.MapPost("", CreateContractFromQuotationAsync).WithName("CreateContract").RequireAuthorization();
        group.MapPost("/{id:guid}/allocate-vehicle", AllocateVehicleAsync).WithName("AllocateVehicle").RequireAuthorization();
        group.MapPost("/{id:guid}/create-lease-agreement", CreateLeaseAgreementAsync).WithName("CreateLeaseAgreement").RequireAuthorization();

        return group;
    }

    private static async Task<IResult> ListContractsAsync(
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

        var query = db.Contracts.AsNoTracking().Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContractStatus>(status, true, out var st))
            query = query.Where(c => c.Status == st);

        var total = await query.CountAsync(ct);
        var contracts = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var customerIds = contracts.Select(c => c.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var contractIds = contracts.Select(c => c.Id).ToList();
        var leaseCountMap = await db.Leases.AsNoTracking()
            .Where(l => l.ContractId.HasValue && contractIds.Contains(l.ContractId.Value))
            .GroupBy(l => l.ContractId!.Value)
            .Select(g => new { ContractId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ContractId, x => x.Count, ct);

        var items = contracts.Select(c =>
        {
            var cust = customers.TryGetValue(c.CustomerId, out var cu) ? cu : null;
            leaseCountMap.TryGetValue(c.Id, out var laCount);
            return new
            {
                c.Id,
                c.DisplayId,
                c.ContractNumber,
                c.CustomerId,
                CustomerDisplayName = cust?.DisplayName ?? "—",
                Status = c.Status.ToString(),
                ContractTypeCode = c.ContractTypeCode.ToString(CultureInfo.InvariantCulture),
                c.StartDate,
                c.EndDate,
                c.DurationMonths,
                c.TotalVehicles,
                c.CheckedOutVehicles,
                c.MonthlyRentSar,
                c.BaseAmountSar,
                c.DiscountPercent,
                c.VatPercent,
                c.TotalAmountSar,
                c.TotalContractValueSar,
                LeaseAgreementCount = laCount,
                QuotationId = c.QuotationId ?? Guid.Empty,
                c.CreatedAtUtc,
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var filtered = items.Where(x =>
                x.ContractNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.CustomerDisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            return Results.Ok(new { items = filtered, page, pageSize, totalCount = filtered.Count });
        }

        return Results.Ok(new { items, page, pageSize, totalCount = total });
    }

    private static async Task<IResult> GetContractByIdAsync(
        Guid id,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var contract = await db.Contracts.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Id == id)
            .FirstOrDefaultAsync(ct);

        if (contract is null) return Results.NotFound();

        var lines = await db.ContractLines.AsNoTracking()
            .Where(l => l.ContractId == id)
            .Select(l => new
            {
                l.Id,
                l.Make,
                l.Model,
                l.Year,
                l.Description,
                l.Quantity,
                l.UnitPriceSar,
                l.LineTotalSar,
            })
            .ToListAsync(ct);

        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contract.CustomerId, ct);

        Quotation? quotation = null;
        List<object>? quoteLineItems = null;
        if (contract.QuotationId.HasValue)
        {
            quotation = await db.Quotations.AsNoTracking()
                .Where(q => q.Id == contract.QuotationId.Value)
                .FirstOrDefaultAsync(ct);

            if (quotation is not null)
            {
                var rawQuoteLines = await db.QuotationLines.AsNoTracking()
                    .Where(ql => ql.QuotationId == quotation.Id)
                    .OrderBy(ql => ql.LineNumber)
                    .ToListAsync(ct);

                quoteLineItems = rawQuoteLines.Select(ql => (object)new
                {
                    ql.LineNumber,
                    ItemType = ql.ItemType.ToString(),
                    ql.Description,
                    ql.VehicleSpecRef,
                    ql.Quantity,
                    UnitPriceSar = ql.UnitPriceSar.ToString(CultureInfo.InvariantCulture),
                    DiscountPercent = ql.DiscountPercent.ToString(CultureInfo.InvariantCulture),
                    LineTotalSar = ql.LineTotalSar.ToString(CultureInfo.InvariantCulture),
                }).ToList();
            }
        }

        var leaseAgreements = await db.Leases.AsNoTracking()
            .Where(l => l.ContractId == id && l.TenantId == tenantId)
            .ToListAsync(ct);

        var vehicleIds = leaseAgreements.Where(l => l.VehicleId.HasValue).Select(l => l.VehicleId!.Value).Distinct().ToList();
        var driverIds = leaseAgreements.Where(l => l.PrimaryDriverId.HasValue).Select(l => l.PrimaryDriverId!.Value).Distinct().ToList();
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => vehicleIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var drivers = await db.Drivers.AsNoTracking().Where(d => driverIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);

        var leaseItems = leaseAgreements.Select(l =>
        {
            var veh = l.VehicleId.HasValue && vehicles.TryGetValue(l.VehicleId.Value, out var v) ? v : null;
            var drv = l.PrimaryDriverId.HasValue && drivers.TryGetValue(l.PrimaryDriverId.Value, out var d) ? d : null;
            return new
            {
                l.Id,
                l.DisplayId,
                LeaseNumber = "LA-" + l.DisplayId.ToString(CultureInfo.InvariantCulture),
                VehicleMakeModel = veh != null ? veh.Make + " " + veh.Model : "—",
                VehiclePlate = veh?.PlateNumber ?? "—",
                PrimaryDriverName = drv?.PersonNameEn ?? "—",
                Status = l.Status.ToString(),
                l.ContractStartUtc,
                l.ContractEndUtc,
                RentAmountSar = l.RentAmount,
            };
        }).ToList();

        return Results.Ok(new
        {
            contract.Id,
            contract.DisplayId,
            contract.ContractNumber,
            contract.CustomerId,
            CustomerDisplayName = customer?.DisplayName ?? "—",
            Status = contract.Status.ToString(),
            ContractTypeCode = contract.ContractTypeCode.ToString(CultureInfo.InvariantCulture),
            contract.StartDate,
            contract.EndDate,
            contract.DurationMonths,
            contract.TotalVehicles,
            contract.CheckedOutVehicles,
            AvailableVehicles = contract.TotalVehicles - contract.CheckedOutVehicles,
            contract.MonthlyRentSar,
            contract.BaseAmountSar,
            contract.DiscountPercent,
            contract.DiscountAmountSar,
            contract.NetAmountSar,
            contract.VatPercent,
            contract.VatAmountSar,
            contract.TotalAmountSar,
            contract.TotalContractValueSar,
            contract.PaymentTermsDays,
            contract.Notes,
            QuotationId = contract.QuotationId ?? Guid.Empty,
            QuoteNumber = quotation?.QuoteNumber,
            QuoteDate = quotation?.QuoteDate,
            QuoteValidUntil = quotation?.ValidUntilDate,
            QuoteStatus = quotation?.Status.ToString(),
            QuoteTotalSar = quotation?.TotalSar.ToString(CultureInfo.InvariantCulture),
            QuoteSubTotalSar = quotation?.SubTotalSar.ToString(CultureInfo.InvariantCulture),
            QuoteVatSar = quotation?.VatSar.ToString(CultureInfo.InvariantCulture),
            QuoteDiscountPercent = quotation?.DiscountPercent.ToString(CultureInfo.InvariantCulture),
            TermsAndConditions = quotation?.TermsAndConditionsMd,
            ContractType = quotation?.ContractType.ToString(),
            EstimatedDurationMonths = quotation?.EstimatedDurationMonths,
            QuoteLines = quoteLineItems,
            Lines = lines,
            LeaseAgreements = leaseItems,
            contract.CreatedAtUtc,
        });
    }

    private static async Task<IResult> GetContractLeaseAgreementsAsync(
        Guid id,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var leases = await db.Leases.AsNoTracking()
            .Where(l => l.ContractId == id && l.TenantId == tenantId)
            .ToListAsync(ct);

        var vehicleIds = leases.Where(l => l.VehicleId.HasValue).Select(l => l.VehicleId!.Value).Distinct().ToList();
        var driverIds = leases.Where(l => l.PrimaryDriverId.HasValue).Select(l => l.PrimaryDriverId!.Value).Distinct().ToList();
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => vehicleIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var drivers = await db.Drivers.AsNoTracking().Where(d => driverIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);

        var items = leases.Select(l =>
        {
            var veh = l.VehicleId.HasValue && vehicles.TryGetValue(l.VehicleId.Value, out var v) ? v : null;
            var drv = l.PrimaryDriverId.HasValue && drivers.TryGetValue(l.PrimaryDriverId.Value, out var d) ? d : null;
            return new
            {
                l.Id,
                l.DisplayId,
                LeaseNumber = "LA-" + l.DisplayId.ToString(CultureInfo.InvariantCulture),
                VehicleMakeModel = veh != null ? veh.Make + " " + veh.Model : "—",
                VehiclePlate = veh?.PlateNumber ?? "—",
                PrimaryDriverName = drv?.PersonNameEn ?? "—",
                Status = l.Status.ToString(),
                l.ContractStartUtc,
                l.ContractEndUtc,
                RentAmountSar = l.RentAmount,
            };
        }).ToList();

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateContractFromQuotationAsync(
        HttpContext ctx,
        CreateContractRequest body,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.Problem(title: "Missing Idempotency-Key", statusCode: StatusCodes.Status400BadRequest);

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var quotation = await db.Quotations.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == body.QuotationId && q.TenantId == tenantId, ct);
        if (quotation is null)
            return Results.Problem(title: "quotation.not_found", detail: "Quotation not found.", statusCode: StatusCodes.Status404NotFound);

        // Prevent duplicate: check if a contract already exists for this quotation
        var existing = await db.Contracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.QuotationId == body.QuotationId && c.TenantId == tenantId, ct);
        if (existing is not null)
            return Results.Ok(new { contractId = existing.Id, contractNumber = existing.ContractNumber, alreadyExists = true });

        // Monthly rent = quotation TotalSar / duration months (exact match to quote)
        var durationMonths = quotation.EstimatedDurationMonths > 0 ? quotation.EstimatedDurationMonths : 12;
        var monthlyRent = Math.Round(quotation.TotalSar / durationMonths, 2, MidpointRounding.AwayFromZero);

        var now = DateTimeOffset.UtcNow;
        var contract = Domain.Contracts.Contract.CreateFromQuotation(
            tenantId,
            $"CNT-{now:yyyy}-{(await db.Contracts.CountAsync(c => c.TenantId == tenantId, ct) + 1).ToString("D5", CultureInfo.InvariantCulture)}",
            quotation.CustomerId,
            quotation.Id,
            1, // Long Term Lease
            quotation.DiscountPercent,
            15m, // VAT rate (hardcoded per QuotationPricingCalculator)
            now,
            durationMonths,
            30,
            now);

        // Copy quotation lines as contract vehicle lines
        var quoteLines = await db.QuotationLines.AsNoTracking()
            .Where(ql => ql.QuotationId == quotation.Id)
            .ToListAsync(ct);

        foreach (var ql in quoteLines)
        {
            var parts = ql.VehicleSpecRef?.Split('/') ?? [];
            var make = parts.Length > 0 ? parts[0] : ql.ItemType.ToString();
            var model = parts.Length > 1 ? parts[1] : ql.Description;
            var year = parts.Length > 2 && int.TryParse(parts[2], out var y) ? y : now.Year;
            contract.AddLine(make, model, year, ql.Description, ql.Quantity, ql.UnitPriceSar);
        }

        // If no lines from quote, set totals from quotation directly
        if (quoteLines.Count == 0)
        {
            // Use reflection-free approach: the contract recalculates on AddLine,
            // so add a single summary line
            contract.AddLine("Fleet", "Vehicles", now.Year, "As per quotation", 1, monthlyRent);
        }

        db.Contracts.Add(contract);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { contractId = contract.Id, contractNumber = contract.ContractNumber, alreadyExists = false });
    }

    private static async Task<IResult> AllocateVehicleAsync(
        Guid id,
        HttpContext ctx,
        AllocateVehicleRequest body,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.Problem(title: "Missing Idempotency-Key", statusCode: StatusCodes.Status400BadRequest);

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var contract = await db.Contracts
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        if (contract is null) return Results.NotFound();

        var vehicle = await db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == body.VehicleId && v.TenantId == tenantId, ct);
        if (vehicle is null)
            return Results.Problem(title: "vehicle.not_found", detail: "Vehicle not found.", statusCode: StatusCodes.Status404NotFound);

        if (vehicle.Status != VehicleStatus.Available)
            return Results.Problem(title: "vehicle.not_available", detail: $"Vehicle is not available (current status: {vehicle.Status}).", statusCode: StatusCodes.Status409Conflict);

        var now = DateTimeOffset.UtcNow;
        vehicle.AllocateToContract(contract.CustomerId, contract.Id, now);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { vehicleId = vehicle.Id, contractId = contract.Id, allocated = true });
    }

    private static async Task<IResult> CreateLeaseAgreementAsync(
        Guid id,
        HttpContext ctx,
        CreateLeaseAgreementRequest body,
        AutoLeaseNetDbContext db,
        ITenantContext tenant,
        CancellationToken ct)
    {
        var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.Problem(title: "Missing Idempotency-Key", statusCode: StatusCodes.Status400BadRequest);

        var tenantId = tenant.TenantId;
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var contract = await db.Contracts
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
        if (contract is null) return Results.NotFound();

        var vehicle = await db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == body.VehicleId && v.TenantId == tenantId, ct);
        if (vehicle is null)
            return Results.Problem(title: "vehicle.not_found", detail: "Vehicle not found.", statusCode: StatusCodes.Status404NotFound);

        if (vehicle.AllocatedToContractId != contract.Id)
            return Results.Problem(title: "vehicle.not_allocated", detail: "Vehicle is not allocated to this contract.", statusCode: StatusCodes.Status409Conflict);

        var driver = await db.Drivers.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == body.DriverId && d.TenantId == tenantId, ct);
        if (driver is null)
            return Results.Problem(title: "driver.not_found", detail: "Driver not found.", statusCode: StatusCodes.Status404NotFound);

        var now = DateTimeOffset.UtcNow;
        var checkoutDate = DateTimeOffset.TryParse(body.CheckoutDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed : now;

        var leaseCount = await db.Leases.CountAsync(l => l.TenantId == tenantId, ct);
        var leaseNumber = $"LA-{now:yyyy}-{(leaseCount + 1).ToString("D5", CultureInfo.InvariantCulture)}";

        var lease = Lease.CreatePending(new CreatePendingInput
        {
            TenantId = tenantId,
            CustomerId = contract.CustomerId,
            ContractId = contract.Id,
            VehicleId = vehicle.Id,
            PrimaryDriverId = driver.Id,
            TajeerContractNumber = 1, // Placeholder — no Tajeer call yet
            IssuanceUrl = $"local://lease/{leaseNumber}",
            ContractTypeCode = contract.ContractTypeCode,
            ContractStartUtc = checkoutDate,
            ContractEndUtc = checkoutDate.AddMonths(contract.DurationMonths),
            RentAmount = contract.MonthlyRentSar,
            VatAmount = contract.VatAmountSar,
            TotalAmount = contract.TotalAmountSar,
            PaymentMethodCode = 1, // Default: bank transfer
            NowUtc = now,
        });

        contract.IncrementCheckout();
        vehicle.StartRental(now);

        db.Leases.Add(lease);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            leaseId = lease.Id,
            leaseNumber,
            contractId = contract.Id,
            vehicleId = vehicle.Id,
            driverId = driver.Id,
            status = lease.Status.ToString(),
        });
    }
}

public sealed record CreateContractRequest
{
    public required Guid QuotationId { get; init; }
}

public sealed record AllocateVehicleRequest
{
    public required Guid VehicleId { get; init; }
}

public sealed record CreateLeaseAgreementRequest
{
    public required Guid VehicleId { get; init; }
    public required Guid DriverId { get; init; }
    public required string CheckoutDate { get; init; }
}
