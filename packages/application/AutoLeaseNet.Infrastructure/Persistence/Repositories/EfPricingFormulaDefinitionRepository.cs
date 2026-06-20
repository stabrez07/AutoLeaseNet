using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

public sealed class EfPricingFormulaDefinitionRepository(AutoLeaseNetDbContext db) : IPricingFormulaDefinitionRepository
{
    public void Add(PricingFormulaDefinition formulaDefinition)
    {
        ArgumentNullException.ThrowIfNull(formulaDefinition);
        db.PricingFormulaDefinitions.Add(formulaDefinition);
    }

    public Task<PricingFormulaDefinition?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return db.PricingFormulaDefinitions
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code, ct);
    }

    public async Task<IReadOnlyList<PricingFormulaDefinition>> GetActiveForTenantAsync(Guid tenantId, CancellationToken ct)
    {
        return await db.PricingFormulaDefinitions
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Code)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
