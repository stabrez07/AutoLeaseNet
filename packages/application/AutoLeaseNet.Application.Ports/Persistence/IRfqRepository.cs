using AutoLeaseNet.Domain.Sales;

namespace AutoLeaseNet.Application.Ports.Persistence;

public interface IRfqRepository
{
    void Add(Rfq rfq);

    Task<Rfq?> GetByIdAsync(Guid tenantId, Guid rfqId, CancellationToken ct);

    Task<IReadOnlyList<Rfq>> GetByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct);

    Task<int> GetNextSequenceAsync(Guid tenantId, CancellationToken ct);
}
