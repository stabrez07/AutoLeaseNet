using AutoLeaseNet.Domain.Sales;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for per-tenant quotation approval-tier configuration.
/// </summary>
public interface IApprovalTierRepository
{
    void Add(ApprovalTier tier);

    Task<bool> AnyAsync(Guid tenantId, CancellationToken ct);

    Task<IReadOnlyList<ApprovalTier>> GetActiveForTenantAsync(Guid tenantId, CancellationToken ct);
}
