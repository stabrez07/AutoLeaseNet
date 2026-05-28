using AutoLeaseNet.Domain.Operations;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for the <see cref="Incident"/> aggregate. Single-table aggregate
/// — no child collections (claim attachments + photos arrive with the Storage adapter
/// later).
/// </summary>
public interface IIncidentRepository
{
    void Add(Incident incident);

    /// <summary>Tenant-scoped lookup (tracked — handlers may mutate).</summary>
    Task<Incident?> GetByIdAsync(Guid tenantId, Guid incidentId, CancellationToken ct);

    /// <summary>
    /// Paged tenant-scoped search ordered by <c>ReportedAtUtc DESC</c>. Filters that
    /// are null apply no narrowing — caller composes any subset.
    /// </summary>
    Task<IncidentSearchResult> SearchAsync(
        Guid tenantId,
        Guid? leaseId,
        Guid? vehicleId,
        IncidentStatus? status,
        IncidentSeverity? severity,
        int page,
        int pageSize,
        CancellationToken ct);
}

/// <summary>Single page of an <see cref="IIncidentRepository.SearchAsync"/> call.</summary>
public sealed record IncidentSearchResult(IReadOnlyList<Incident> Items, int TotalCount);
