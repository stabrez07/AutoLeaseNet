using AutoLeaseNet.Domain.Sales;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for per-tenant quotation approval-tier configuration.
/// </summary>
public interface IApprovalTierRepository
{
    void Add(ApprovalTier tier);

    Task<bool> AnyAsync(Guid tenantId, CancellationToken ct);

    /// <summary>Returns all active tiers for the tenant, ordered by <see cref="ApprovalTier.TierLevel"/>.</summary>
    Task<IReadOnlyList<ApprovalTier>> GetAllActiveAsync(Guid tenantId, CancellationToken ct);
}
