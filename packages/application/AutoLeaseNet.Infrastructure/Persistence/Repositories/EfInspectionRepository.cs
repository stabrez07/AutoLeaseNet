using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IInspectionRepository"/>. <c>GetByIdAsync</c>
/// eager-loads photos + damage markers because the UI never renders an inspection without
/// them; <c>SearchAsync</c> returns the aggregate without children to keep the list
/// response small (counts are projected in the DTO via the existing collection sizes).
/// </summary>
public sealed class EfInspectionRepository(AutoLeaseNetDbContext db) : IInspectionRepository
{
    public void Add(Inspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        db.Inspections.Add(inspection);
    }

    public Task<Inspection?> GetByIdAsync(Guid tenantId, Guid inspectionId, CancellationToken ct)
    {
        return db.Inspections
            .Include(i => i.Photos)
            .Include(i => i.DamageMarkers)
            .SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == inspectionId, ct);
    }

    public async Task<InspectionSearchResult> SearchAsync(
        Guid tenantId,
        Guid? vehicleId,
        Guid? leaseId,
        InspectionType? type,
        InspectionStatus? status,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var query = db.Inspections.AsNoTracking().Where(i => i.TenantId == tenantId);
        if (vehicleId is { } v) query = query.Where(i => i.VehicleId == v);
        if (leaseId is { } l) query = query.Where(i => i.LeaseId == l);
        if (type is { } t) query = query.Where(i => i.Type == t);
        if (status is { } s) query = query.Where(i => i.Status == s);

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .Include(i => i.Photos)
            .Include(i => i.DamageMarkers)
            .OrderByDescending(i => i.PerformedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new InspectionSearchResult(items, total);
    }
}
