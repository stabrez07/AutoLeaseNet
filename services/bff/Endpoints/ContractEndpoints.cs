using System.Globalization;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Contracts;
using AutoLeaseNet.Domain.Sales;
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
                c.MonthlyRentSar,
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
            contract.MonthlyRentSar,
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
}
