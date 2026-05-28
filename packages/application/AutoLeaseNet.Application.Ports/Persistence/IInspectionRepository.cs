using AutoLeaseNet.Domain.Operations;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for the <see cref="Inspection"/> aggregate. Photos and damage
/// markers are loaded eagerly because the UI always renders the inspection detail
/// view alongside them; if we ever ship a list view that doesn't need children,
/// add a no-children overload then.
/// </summary>
public interface IInspectionRepository
{
    void Add(Inspection inspection);

    /// <summary>Tenant-scoped lookup with photos + damage markers eagerly loaded.</summary>
    Task<Inspection?> GetByIdAsync(Guid tenantId, Guid inspectionId, CancellationToken ct);

    /// <summary>
    /// Paged tenant-scoped search ordered by <c>PerformedAtUtc DESC</c>. Filters that are
    /// null apply no narrowing — caller composes any subset.
    /// </summary>
    Task<InspectionSearchResult> SearchAsync(
        Guid tenantId,
        Guid? vehicleId,
        Guid? leaseId,
        InspectionType? type,
        InspectionStatus? status,
        int page,
        int pageSize,
        CancellationToken ct);
}

/// <summary>Single page of an <see cref="IInspectionRepository.SearchAsync"/> call.</summary>
public sealed record InspectionSearchResult(IReadOnlyList<Inspection> Items, int TotalCount);
