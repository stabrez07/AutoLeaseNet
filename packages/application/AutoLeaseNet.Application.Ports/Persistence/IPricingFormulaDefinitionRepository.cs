using AutoLeaseNet.Domain.Pricing;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for configurable pricing formulas.
/// </summary>
public interface IPricingFormulaDefinitionRepository
{
    void Add(PricingFormulaDefinition formulaDefinition);

    Task<PricingFormulaDefinition?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);

    Task<IReadOnlyList<PricingFormulaDefinition>> GetActiveForTenantAsync(Guid tenantId, CancellationToken ct);
}
