using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Zatca;
using Microsoft.EntityFrameworkCore;

namespace AutoLeaseNet.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IZatcaChainStateRepository"/>. Treats the
/// <see cref="ZatcaChainState"/> aggregate as a single-row-per-tenant store; the
/// primary key on <c>TenantId</c> guarantees no operator-level dedup is needed.
///
/// <para>
/// <c>GetOrCreateAsync</c> intentionally does the "create-if-missing" inline rather
/// than via a separate factory call — saga code stays a single line:
/// <c>var state = await repo.GetOrCreateAsync(tenantId, ct);</c>.
/// </para>
/// </summary>
public sealed class EfZatcaChainStateRepository(AutoLeaseNetDbContext db) : IZatcaChainStateRepository
{
    public async Task<ZatcaChainState> GetOrCreateAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));

        var existing = await db.ZatcaChainStates
            .FirstOrDefaultAsync(z => z.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        if (existing is not null) return existing;

        var created = ZatcaChainState.ForNewTenant(tenantId, DateTimeOffset.UtcNow);
        db.ZatcaChainStates.Add(created);
        return created;
    }

    public void Save(ZatcaChainState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var entry = db.Entry(state);
        if (entry.State == EntityState.Detached)
        {
            // GetOrCreateAsync returned a tracked instance; a detached one means the
            // caller built it manually. Treat as upsert — Update covers both insert
            // (new PK) and update (existing PK) because TenantId is the key.
            db.ZatcaChainStates.Update(state);
        }
        // Tracked instances commit automatically on SaveChangesAsync — no-op here.
    }
}
