using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

public sealed class EfPricingVersionRepository(AutoLeaseNetDbContext db) : IPricingVersionRepository
{
    public void Add(PricingVersion pricingVersion)
    {
        ArgumentNullException.ThrowIfNull(pricingVersion);
        db.PricingVersions.Add(pricingVersion);
    }

    public Task<PricingVersion?> GetByIdAsync(Guid tenantId, Guid pricingVersionId, CancellationToken ct)
    {
        return db.PricingVersions
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == pricingVersionId, ct);
    }

    public Task<PricingVersion?> GetActiveForAsync(Guid tenantId, DateTimeOffset atUtc, CancellationToken ct)
    {
        return db.PricingVersions
            .Where(x => x.TenantId == tenantId && x.Status == PricingVersionStatus.Published)
            .Where(x => x.EffectiveFromUtc <= atUtc)
            .Where(x => x.EffectiveToUtc == null || x.EffectiveToUtc >= atUtc)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<PricingVersion>> ListForTenantAsync(Guid tenantId, CancellationToken ct)
    {
        return await db.PricingVersions
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
