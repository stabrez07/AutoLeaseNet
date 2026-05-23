using AutoLeaseNet.Application.Lookups;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Lookups;

internal static class LookupGuards
{
    internal static Guid RequireTenant(ITenantContext tenant)
    {
        if (tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException("Lookup query requires an authenticated tenant context.");
        return tenant.TenantId;
    }

    internal static (int page, int size) ClampPaging(int page, int size)
    {
        if (page < 1) page = 1;
        if (size < 1) size = PagedResult<object>.DefaultPageSize;
        if (size > PagedResult<object>.MaxPageSize) size = PagedResult<object>.MaxPageSize;
        return (page, size);
    }
}

public sealed class GetBranchesQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetBranchesQuery, IReadOnlyList<BranchDto>>
{
    public async Task<IReadOnlyList<BranchDto>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = LookupGuards.RequireTenant(tenant);
        return await db.Branches.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.IsActive)
            .OrderBy(b => b.Code)
            .Select(b => new BranchDto(b.Id, b.Code, b.NameEn, b.NameAr,
                b.CityEn, b.CityAr, b.RegionEn, b.RegionAr,
                b.TajeerBranchId, b.IsActive))
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetRentPoliciesQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetRentPoliciesQuery, IReadOnlyList<RentPolicyDto>>
{
    public async Task<IReadOnlyList<RentPolicyDto>> Handle(GetRentPoliciesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = LookupGuards.RequireTenant(tenant);
        return await db.RentPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new RentPolicyDto(p.Id, p.Code, p.NameEn, p.NameAr,
                p.BaseDailyRate, p.BaseHourlyRate,
                p.AllowedKmPerDay, p.AllowedKmPerHour, p.UnlimitedKm,
                p.ExtraKmFee, p.MinRentalDays, p.MaxRentalDays,
                p.TajeerRentPolicyId, p.IsActive))
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetExtendedCoveragesQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetExtendedCoveragesQuery, IReadOnlyList<ExtendedCoverageDto>>
{
    public async Task<IReadOnlyList<ExtendedCoverageDto>> Handle(GetExtendedCoveragesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = LookupGuards.RequireTenant(tenant);
        return await db.ExtendedCoverages.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new ExtendedCoverageDto(c.Id, c.Code, c.NameEn, c.NameAr,
                (int)c.CoverageType, c.DailyRate, c.DeductibleAmount,
                c.TajeerExtendedCoverageId, c.IsActive))
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetCustomersPagedQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetCustomersPagedQuery, PagedResult<CustomerSummaryDto>>
{
    public async Task<PagedResult<CustomerSummaryDto>> Handle(GetCustomersPagedQuery request, CancellationToken cancellationToken)
    {
        var tenantId = LookupGuards.RequireTenant(tenant);
        var (page, size) = LookupGuards.ClampPaging(request.Page, request.PageSize);

        var query = db.Customers.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = $"%{request.Search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.DisplayName, term) ||
                (c.LegalName != null && EF.Functions.Like(c.LegalName, term)) ||
                (c.PersonNameEn != null && EF.Functions.Like(c.PersonNameEn, term)) ||
                (c.CommercialRegistration != null && EF.Functions.Like(c.CommercialRegistration, term)) ||
                (c.PersonIdNumber != null && EF.Functions.Like(c.PersonIdNumber, term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.DisplayName)
            .Skip((page - 1) * size).Take(size)
            .Select(c => new CustomerSummaryDto(c.Id, (int)c.Type, (int)c.Status,
                c.DisplayName, c.DisplayNameAr, c.Email, c.Mobile,
                c.CommercialRegistration, c.VatNumber, c.KycVerified))
            .ToListAsync(cancellationToken);
        return new PagedResult<CustomerSummaryDto>(items, page, size, totalCount);
    }
}

public sealed class GetVehiclesPagedQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetVehiclesPagedQuery, PagedResult<VehicleSummaryDto>>
{
    public async Task<PagedResult<VehicleSummaryDto>> Handle(GetVehiclesPagedQuery request, CancellationToken cancellationToken)
    {
        var tenantId = LookupGuards.RequireTenant(tenant);
        var (page, size) = LookupGuards.ClampPaging(request.Page, request.PageSize);

        var query = db.Vehicles.AsNoTracking().Where(v => v.TenantId == tenantId);
        if (request.Status is int statusCode)
            query = query.Where(v => (int)v.Status == statusCode);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = $"%{request.Search.Trim()}%";
            query = query.Where(v =>
                EF.Functions.Like(v.PlateNumber, term) ||
                EF.Functions.Like(v.Vin, term) ||
                EF.Functions.Like(v.Make, term) ||
                EF.Functions.Like(v.Model, term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(v => v.PlateNumber)
            .Skip((page - 1) * size).Take(size)
            .Select(v => new VehicleSummaryDto(v.Id, (int)v.Status,
                v.PlateNumber, v.PlateLetters, v.PlateTypeCode,
                v.Vin, v.Make, v.Model, v.ModelYear, v.Color,
                (int)v.FuelType, (int)v.BodyType, v.Seats,
                v.CurrentBranchId, v.CurrentKm))
            .ToListAsync(cancellationToken);
        return new PagedResult<VehicleSummaryDto>(items, page, size, totalCount);
    }
}

public sealed class GetDriversPagedQueryHandler(AutoLeaseNetDbContext db, ITenantContext tenant)
    : IRequestHandler<GetDriversPagedQuery, PagedResult<DriverSummaryDto>>
{
    public async Task<PagedResult<DriverSummaryDto>> Handle(GetDriversPagedQuery request, CancellationToken cancellationToken)
    {
        var tenantId = LookupGuards.RequireTenant(tenant);
        var (page, size) = LookupGuards.ClampPaging(request.Page, request.PageSize);

        var query = db.Drivers.AsNoTracking().Where(d => d.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = $"%{request.Search.Trim()}%";
            query = query.Where(d =>
                EF.Functions.Like(d.PersonNameEn, term) ||
                (d.PersonNameAr != null && EF.Functions.Like(d.PersonNameAr, term)) ||
                EF.Functions.Like(d.PersonIdNumber, term) ||
                EF.Functions.Like(d.DriverLicenseNumber, term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(d => d.PersonNameEn)
            .Skip((page - 1) * size).Take(size)
            .Select(d => new DriverSummaryDto(d.Id, (int)d.Status, d.CustomerId,
                d.PersonNameEn, d.PersonNameAr,
                d.IdTypeCode, d.LicenseClass,
                d.LicenseExpiryDate, (int)d.TammAuthorizationStatus))
            .ToListAsync(cancellationToken);
        return new PagedResult<DriverSummaryDto>(items, page, size, totalCount);
    }
}
