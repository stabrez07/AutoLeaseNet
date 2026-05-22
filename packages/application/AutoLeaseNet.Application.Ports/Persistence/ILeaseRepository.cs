using AutoLeaseNet.Domain.Leases;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for the <see cref="Lease"/> aggregate. Phase 1 / Week 1 exposes only
/// the methods needed by the SaveContract use case and the inbound Tajeer webhook (Week 1
/// Day 6). Query-heavy reads will land as Week 2 features need them.
/// </summary>
public interface ILeaseRepository
{
    void Add(Lease lease);

    Task<Lease?> GetByTajeerContractNumberAsync(
        Guid tenantId,
        long tajeerContractNumber,
        CancellationToken ct);
}
