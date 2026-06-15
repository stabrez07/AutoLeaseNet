using AutoLeaseNet.Domain.Sales;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for the <see cref="Quotation"/> aggregate.
/// </summary>
public interface IQuotationRepository
{
    void Add(Quotation quotation);

    Task<Quotation?> GetByIdAsync(Guid tenantId, Guid quotationId, CancellationToken ct);

    Task<Quotation?> GetByQuoteNumberAsync(Guid tenantId, string quoteNumber, CancellationToken ct);

    Task<IReadOnlyList<Quotation>> GetPendingApprovalsForTenantAsync(Guid tenantId, CancellationToken ct);
}
