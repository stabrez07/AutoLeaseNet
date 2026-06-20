using AutoLeaseNet.Domain.Pricing;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for tenant discount policy setup.
/// </summary>
public interface IPricingDiscountPolicyRepository
{
    void Add(PricingDiscountPolicy policy);

    Task<PricingDiscountPolicy?> GetForTenantAsync(Guid tenantId, CancellationToken ct);
}
