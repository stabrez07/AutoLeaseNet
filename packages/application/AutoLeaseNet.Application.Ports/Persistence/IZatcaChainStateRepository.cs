using AutoLeaseNet.Domain.Zatca;

namespace AutoLeaseNet.Application.Ports.Persistence;

/// <summary>
/// Persistence port for the per-tenant <see cref="ZatcaChainState"/> aggregate-of-one.
/// One row per tenant (TenantId is the primary key) — there is no scenario in which a
/// tenant has two chains.
///
/// <para>
/// <see cref="GetOrCreateAsync"/> is the only read path the saga needs: load the
/// existing row or initialise a fresh "no clearance yet" row in the same call. This
/// matches the Outbox / Reconciliation single-row-per-tenant pattern.
/// </para>
/// </summary>
public interface IZatcaChainStateRepository
{
    /// <summary>
    /// Loads the chain state for <paramref name="tenantId"/>; creates and tracks a fresh
    /// row if none exists yet. The caller commits via <see cref="IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    Task<ZatcaChainState> GetOrCreateAsync(Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Marks <paramref name="state"/> for upsert. The implementation must handle both
    /// "newly created" (from a prior <see cref="GetOrCreateAsync"/> on a fresh tenant)
    /// and "updated" cases without the caller needing to distinguish.
    /// </summary>
    void Save(ZatcaChainState state);
}
