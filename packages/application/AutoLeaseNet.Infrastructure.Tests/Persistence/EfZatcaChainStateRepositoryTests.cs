using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Domain.Zatca;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Persistence;

/// <summary>
/// Round-trip + single-row-per-tenant contract for <see cref="EfZatcaChainStateRepository"/>.
/// Uses EF Core InMemory — same provider every BFF test factory uses, so any future
/// move to SQL would surface the same dual-write semantics via the existing migration.
/// </summary>
public sealed class EfZatcaChainStateRepositoryTests
{
    private static readonly Guid TenantA = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("b2b2b2b2-0002-0000-0000-000000000002");

    [Fact]
    public async Task GetOrCreateAsync_creates_fresh_row_when_none_exists_for_tenant()
    {
        await using var db = NewDb();
        var repo = new EfZatcaChainStateRepository(db);

        var state = await repo.GetOrCreateAsync(TenantA, CancellationToken.None);

        state.TenantId.Should().Be(TenantA);
        state.LastClearedInvoiceHash.Should().BeNull();
        state.LastClearedAtUtc.Should().BeNull();

        // Not yet persisted — UnitOfWork commits in production. Saving here mirrors the saga.
        await db.SaveChangesAsync();
        (await db.ZatcaChainStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_returns_existing_row_on_second_call_for_same_tenant()
    {
        await using var db = NewDb();
        var repo = new EfZatcaChainStateRepository(db);

        var first = await repo.GetOrCreateAsync(TenantA, CancellationToken.None);
        first.AdvanceTo("hash-1", new DateTimeOffset(2026, 5, 29, 9, 0, 0, TimeSpan.Zero));
        await db.SaveChangesAsync();

        var second = await repo.GetOrCreateAsync(TenantA, CancellationToken.None);

        second.LastClearedInvoiceHash.Should().Be("hash-1");
        (await db.ZatcaChainStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Round_trip_persists_AdvanceTo_state_across_a_fresh_DbContext()
    {
        var dbName = Guid.NewGuid().ToString();
        var advanceAt = new DateTimeOffset(2026, 5, 29, 10, 30, 0, TimeSpan.Zero);

        await using (var db = NewDb(dbName))
        {
            var repo = new EfZatcaChainStateRepository(db);
            var state = await repo.GetOrCreateAsync(TenantA, CancellationToken.None);
            state.AdvanceTo("hash-persisted", advanceAt);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb(dbName))
        {
            var reloaded = await db.ZatcaChainStates.AsNoTracking()
                .FirstAsync(z => z.TenantId == TenantA);
            reloaded.LastClearedInvoiceHash.Should().Be("hash-persisted");
            reloaded.LastClearedAtUtc.Should().Be(advanceAt);
        }
    }

    [Fact]
    public async Task Two_distinct_tenants_get_two_distinct_rows()
    {
        await using var db = NewDb();
        var repo = new EfZatcaChainStateRepository(db);

        await repo.GetOrCreateAsync(TenantA, CancellationToken.None);
        await repo.GetOrCreateAsync(TenantB, CancellationToken.None);
        await db.SaveChangesAsync();

        (await db.ZatcaChainStates.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Reset_clears_persisted_state()
    {
        await using var db = NewDb();
        var repo = new EfZatcaChainStateRepository(db);

        var state = await repo.GetOrCreateAsync(TenantA, CancellationToken.None);
        state.AdvanceTo("hash-2", new DateTimeOffset(2026, 5, 29, 9, 0, 0, TimeSpan.Zero));
        await db.SaveChangesAsync();

        state.Reset(new DateTimeOffset(2026, 5, 29, 11, 0, 0, TimeSpan.Zero));
        await db.SaveChangesAsync();

        var reloaded = await db.ZatcaChainStates.AsNoTracking()
            .FirstAsync(z => z.TenantId == TenantA);
        reloaded.LastClearedInvoiceHash.Should().BeNull();
        reloaded.LastClearedAtUtc.Should().BeNull();
    }

    private static AutoLeaseNetDbContext NewDb(string? dbName = null)
    {
        var opts = new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new AutoLeaseNetDbContext(opts);
    }
}
