using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Branches;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Drivers;
using AutoLeaseNet.Domain.ExtendedCoverages;
using AutoLeaseNet.Domain.RentPolicies;
using AutoLeaseNet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

public sealed class EfCustomerRepository(AutoLeaseNetDbContext db) : ICustomerRepository
{
    public void Add(Customer customer) { ArgumentNullException.ThrowIfNull(customer); db.Customers.Add(customer); }

    public Task<Customer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Customers.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public Task<bool> AnyAsync(Guid tenantId, CancellationToken ct) =>
        db.Customers.AnyAsync(c => c.TenantId == tenantId, ct);

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(Guid tenantId, int page, int pageSize, string? search, CancellationToken ct)
    {
        var q = db.Customers.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.DisplayName.Contains(search) || (c.Mobile != null && c.Mobile.Contains(search)));
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(c => c.DisplayName)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task UpdateAsync(Customer customer, CancellationToken ct) { db.Customers.Update(customer); return Task.CompletedTask; }
}

public sealed class EfVehicleRepository(AutoLeaseNetDbContext db) : IVehicleRepository
{
    public void Add(Vehicle vehicle) { ArgumentNullException.ThrowIfNull(vehicle); db.Vehicles.Add(vehicle); }

    public Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Vehicles.SingleOrDefaultAsync(v => v.TenantId == tenantId && v.Id == id, ct);

    public Task<Vehicle?> GetByPlateAsync(Guid tenantId, string plateNumber, string plateLetters, CancellationToken ct) =>
        db.Vehicles.SingleOrDefaultAsync(
            v => v.TenantId == tenantId && v.PlateNumber == plateNumber && v.PlateLetters == plateLetters,
            ct);

    public Task<Vehicle?> FindAvailableReplacementAsync(
        Guid tenantId,
        Guid excludedVehicleId,
        Guid preferredBranchId,
        BodyType bodyType,
        int seats,
        CancellationToken ct)
    {
        return db.Vehicles
            .Where(v =>
                v.TenantId == tenantId &&
                v.Id != excludedVehicleId &&
                v.Status == VehicleStatus.Available &&
                v.BodyType == bodyType &&
                v.Seats == seats)
            .OrderByDescending(v => v.CurrentBranchId == preferredBranchId)
            .ThenBy(v => v.CurrentKm)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetPagedAsync(Guid tenantId, int page, int pageSize, string? search, int? statusFilter, CancellationToken ct)
    {
        var q = db.Vehicles.AsNoTracking().Where(v => v.TenantId == tenantId);
        if (statusFilter.HasValue) q = q.Where(v => (int)v.Status == statusFilter.Value);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(v => v.PlateNumber.Contains(search) || v.Make.Contains(search) || v.Model.Contains(search) || v.Vin.Contains(search));
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(v => v.PlateNumber).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class EfDriverRepository(AutoLeaseNetDbContext db) : IDriverRepository
{
    public void Add(Driver driver) { ArgumentNullException.ThrowIfNull(driver); db.Drivers.Add(driver); }

    public Task<Driver?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Drivers.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id, ct);

    public Task<Driver?> GetByIdNumberAsync(Guid tenantId, string personIdNumber, CancellationToken ct) =>
        db.Drivers.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.PersonIdNumber == personIdNumber, ct);

    public async Task<(IReadOnlyList<Driver> Items, int TotalCount)> GetPagedAsync(Guid tenantId, int page, int pageSize, string? search, CancellationToken ct)
    {
        var q = db.Drivers.AsNoTracking().Where(d => d.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(d => d.PersonNameEn.Contains(search) || d.DriverLicenseNumber.Contains(search));
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(d => d.PersonNameEn).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class EfBranchRepository(AutoLeaseNetDbContext db) : IBranchRepository
{
    public void Add(Branch branch) { ArgumentNullException.ThrowIfNull(branch); db.Branches.Add(branch); }

    public Task<Branch?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Branches.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.Id == id, ct);

    public Task<Branch?> GetByTajeerBranchIdAsync(Guid tenantId, int tajeerBranchId, CancellationToken ct) =>
        db.Branches.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.TajeerBranchId == tajeerBranchId, ct);

    public async Task<(IReadOnlyList<Branch> Items, int TotalCount)> GetPagedAsync(Guid tenantId, int page, int pageSize, string? search, CancellationToken ct)
    {
        var q = db.Branches.AsNoTracking().Where(b => b.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(b => b.Code.Contains(search) || b.NameEn.Contains(search) || b.NameAr.Contains(search));
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(b => b.Code).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class EfRentPolicyRepository(AutoLeaseNetDbContext db) : IRentPolicyRepository
{
    public void Add(RentPolicy policy) { ArgumentNullException.ThrowIfNull(policy); db.RentPolicies.Add(policy); }

    public Task<RentPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.RentPolicies.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public Task<RentPolicy?> GetByTajeerRentPolicyIdAsync(Guid tenantId, int tajeerId, CancellationToken ct) =>
        db.RentPolicies.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.TajeerRentPolicyId == tajeerId, ct);
}

public sealed class EfExtendedCoverageRepository(AutoLeaseNetDbContext db) : IExtendedCoverageRepository
{
    public void Add(ExtendedCoverage coverage) { ArgumentNullException.ThrowIfNull(coverage); db.ExtendedCoverages.Add(coverage); }

    public Task<ExtendedCoverage?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.ExtendedCoverages.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public Task<ExtendedCoverage?> GetByTajeerIdAsync(Guid tenantId, int tajeerId, CancellationToken ct) =>
        db.ExtendedCoverages.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.TajeerExtendedCoverageId == tajeerId, ct);
}
