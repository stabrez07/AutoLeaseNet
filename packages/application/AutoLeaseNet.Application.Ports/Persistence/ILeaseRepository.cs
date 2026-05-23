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

    /// <summary>
    /// Cross-tenant lookup used by the unauthenticated Tajeer webhook receiver — the
    /// webhook arrives without a tenant claim, so the receiver resolves the owning tenant
    /// from the contract number, then performs the actual state transition under that
    /// tenant. Phase 2 multi-tenant will encode the tenant in the registered webhook URL
    /// and retire this method. RLS in Week 2 Day 9 will require this method to bypass via
    /// `SESSION_CONTEXT` admin flag.
    /// </summary>
    Task<Lease?> GetByTajeerContractNumberAcrossTenantsAsync(
        long tajeerContractNumber,
        CancellationToken ct);
}
