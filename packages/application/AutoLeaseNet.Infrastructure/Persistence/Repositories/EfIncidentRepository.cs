using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IIncidentRepository"/>. Single-table aggregate
/// — no eager-load complexity. <c>GetByIdAsync</c> returns tracked entities so handlers
/// can mutate-and-save in the same unit of work.
/// </summary>
public sealed class EfIncidentRepository(AutoLeaseNetDbContext db) : IIncidentRepository
{
    public void Add(Incident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        db.Incidents.Add(incident);
    }

    public Task<Incident?> GetByIdAsync(Guid tenantId, Guid incidentId, CancellationToken ct)
    {
        return db.Incidents.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == incidentId, ct);
    }

    public async Task<IncidentSearchResult> SearchAsync(
        Guid tenantId,
        Guid? leaseId,
        Guid? vehicleId,
        IncidentStatus? status,
        IncidentSeverity? severity,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var query = db.Incidents.AsNoTracking().Where(i => i.TenantId == tenantId);
        if (leaseId is { } l) query = query.Where(i => i.LeaseId == l);
        if (vehicleId is { } v) query = query.Where(i => i.VehicleId == v);
        if (status is { } s) query = query.Where(i => i.Status == s);
        if (severity is { } sev) query = query.Where(i => i.Severity == sev);

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(i => i.ReportedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new IncidentSearchResult(items, total);
    }
}
