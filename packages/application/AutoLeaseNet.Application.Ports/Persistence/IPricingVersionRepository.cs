using AutoLeaseNet.Domain.Pricing;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for versioned tenant pricing setup.
/// </summary>
public interface IPricingVersionRepository
{
    void Add(PricingVersion pricingVersion);

    Task<PricingVersion?> GetByIdAsync(Guid tenantId, Guid pricingVersionId, CancellationToken ct);

    Task<PricingVersion?> GetActiveForAsync(Guid tenantId, DateTimeOffset atUtc, CancellationToken ct);

    Task<IReadOnlyList<PricingVersion>> ListForTenantAsync(Guid tenantId, CancellationToken ct);
}
