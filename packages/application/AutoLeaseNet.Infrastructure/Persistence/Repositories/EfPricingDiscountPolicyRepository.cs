using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

public sealed class EfPricingDiscountPolicyRepository(AutoLeaseNetDbContext db) : IPricingDiscountPolicyRepository
{
    public void Add(PricingDiscountPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        db.PricingDiscountPolicies.Add(policy);
    }

    public Task<PricingDiscountPolicy?> GetForTenantAsync(Guid tenantId, CancellationToken ct)
    {
        return db.PricingDiscountPolicies
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, ct);
    }
}
