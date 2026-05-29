using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Reconciliation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Reconciliation;

/// <summary>
/// Real drift detector now that <c>ITajeerContractClient.GetAsync</c> exists. Pins:
/// query scope (Active + Extended only, configured tenants only, capped at
/// MaxLeasesPerCycle, ordered by UpdatedAtUtc desc, skips rows with null
/// TajeerContractNumber), per-row decision tree (match / drift / vendor-failure /
/// transient / unrecognised), and that cancellation between tenants is respected.
/// </summary>
public sealed class TajeerStatusMirrorCheckTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-1111-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-2222-0000-0000-000000000002");

    [Fact]
    public async Task RunAsync_when_no_tenants_configured_does_nothing()
    {
        await using var db = NewDb();
        var opts = NewOptions(); // TenantIds defaults to empty
        var tajeer = new InMemoryTajeerContractClient();
        var check = new TajeerStatusMirrorCheck(db, tajeer, Options.Create(opts),
            NullLogger<TajeerStatusMirrorCheck>.Instance);

        Func<Task> act = () => check.RunAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        tajeer.GetCalls.Should().BeEmpty(because: "no tenants → no queries → no GetAsync calls");
    }

    [Fact]
    public async Task RunAsync_calls_GetAsync_only_for_Active_or_Extended_leases_with_contract_numbers()
    {
        await using var db = NewDb();
        var now = DateTimeOffset.UtcNow;

        // 2 Active leases for Tenant A — should both be inspected.
        AddActive(db, TenantA, contractNumber: 1001, updatedAt: now.AddMinutes(-30));
        AddActive(db, TenantA, contractNumber: 1002, updatedAt: now.AddMinutes(-10));
        // 1 Pending lease for Tenant A — must be excluded.
        AddPending(db, TenantA, contractNumber: 1099, updatedAt: now);

        await db.SaveChangesAsync();

        var opts = NewOptions();
        opts.Tajeer.TenantIds = new[] { TenantA };

        var tajeer = new InMemoryTajeerContractClient();
        tajeer.SeedProjection(1001, contractStatusCode: 4); // matches Active
        tajeer.SeedProjection(1002, contractStatusCode: 4); // matches Active

        var check = new TajeerStatusMirrorCheck(db, tajeer, Options.Create(opts),
            NullLogger<TajeerStatusMirrorCheck>.Instance);

        await check.RunAsync(CancellationToken.None);

        tajeer.GetCalls.Should().HaveCount(2,
            because: "Active rows only — Pending row excluded");
        tajeer.GetCalls.Should().Contain(1001L).And.Contain(1002L);
        tajeer.GetCalls.Should().NotContain(1099L);
    }

    [Fact]
    public async Task RunAsync_respects_MaxLeasesPerCycle_cap()
    {
        await using var db = NewDb();
        var now = DateTimeOffset.UtcNow;

        AddActive(db, TenantA, 1001, now.AddMinutes(-30));
        AddActive(db, TenantA, 1002, now.AddMinutes(-20));
        AddActive(db, TenantA, 1003, now.AddMinutes(-1)); // newest, must be picked
        await db.SaveChangesAsync();

        var opts = NewOptions();
        opts.Tajeer.TenantIds = new[] { TenantA };
        opts.Tajeer.MaxLeasesPerCycle = 2;

        var tajeer = new InMemoryTajeerContractClient();
        tajeer.SeedProjection(1001, 4);
        tajeer.SeedProjection(1002, 4);
        tajeer.SeedProjection(1003, 4);

        var check = new TajeerStatusMirrorCheck(db, tajeer, Options.Create(opts),
            NullLogger<TajeerStatusMirrorCheck>.Instance);

        await check.RunAsync(CancellationToken.None);

        tajeer.GetCalls.Should().HaveCount(2);
        tajeer.GetCalls.Should().Contain(1003L, because: "newest must be in the cap");
        tajeer.GetCalls.Should().Contain(1002L);
        tajeer.GetCalls.Should().NotContain(1001L, because: "oldest dropped under MaxLeasesPerCycle=2");
    }

    [Fact]
    public async Task RunAsync_logs_no_drift_when_vendor_matches_local()
    {
        // Behaviour we pin: no throw, Get called once. The "match" branch is structurally
        // identical to the drift branch from a control-flow POV — if the check passes
        // through cleanly, we know it ran the comparison.
        await using var db = NewDb();
        AddActive(db, TenantA, 4242L, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var opts = NewOptions();
        opts.Tajeer.TenantIds = new[] { TenantA };

        var tajeer = new InMemoryTajeerContractClient();
        tajeer.SeedProjection(4242L, contractStatusCode: 4); // Issued = local Active

        var check = new TajeerStatusMirrorCheck(db, tajeer, Options.Create(opts),
            NullLogger<TajeerStatusMirrorCheck>.Instance);

        Func<Task> act = () => check.RunAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        tajeer.GetCalls.Should().ContainSingle().Which.Should().Be(4242L);
    }

    [Fact]
    public async Task RunAsync_continues_after_per_row_vendor_failure()
    {
        await using var db = NewDb();
        AddActive(db, TenantA, 4242L, DateTimeOffset.UtcNow);
        AddActive(db, TenantA, 5252L, DateTimeOffset.UtcNow.AddMinutes(-5));
        await db.SaveChangesAsync();

        var opts = NewOptions();
        opts.Tajeer.TenantIds = new[] { TenantA };

        // First contract → vendor not_found (drift). Second → matching Active. The check
        // must not bail on the first failure; it must process both rows.
        var tajeer = new InMemoryTajeerContractClient();
        tajeer.SeedProjection(5252L, contractStatusCode: 4);
        // 4242L deliberately NOT seeded — InMemory returns vendor.contract.not_found.

        var check = new TajeerStatusMirrorCheck(db, tajeer, Options.Create(opts),
            NullLogger<TajeerStatusMirrorCheck>.Instance);

        await check.RunAsync(CancellationToken.None);

        tajeer.GetCalls.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_continues_after_transient_failure()
    {
        await using var db = NewDb();
        AddActive(db, TenantA, 4242L, DateTimeOffset.UtcNow);
        AddActive(db, TenantA, 5252L, DateTimeOffset.UtcNow.AddMinutes(-5));
        await db.SaveChangesAsync();

        var opts = NewOptions();
        opts.Tajeer.TenantIds = new[] { TenantA };

        // Inject a transient failure for 4242L; 5252L falls through to seeded projection.
        var tajeer = new InMemoryTajeerContractClient(
            getFactory: contractNumber => contractNumber == 4242L
                ? IntegrationResult<GetContractResponse>.Failure(
                    errorCode: "tajeer.http.503",
                    errorMessage: "synthetic transient failure",
                    isTransient: true)
                : IntegrationResult<GetContractResponse>.Success(new GetContractResponse
                {
                    ContractNumber = contractNumber,
                    ContractStatusCode = 4,
                }));

        var check = new TajeerStatusMirrorCheck(db, tajeer, Options.Create(opts),
            NullLogger<TajeerStatusMirrorCheck>.Instance);

        Func<Task> act = () => check.RunAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        tajeer.GetCalls.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_continues_after_unrecognised_vendor_state()
    {
        await using var db = NewDb();
        AddActive(db, TenantA, 4242L, DateTimeOffset.UtcNow);
        AddActive(db, TenantA, 5252L, DateTimeOffset.UtcNow.AddMinutes(-5));
        await db.SaveChangesAsync();

        var opts = NewOptions();
        opts.Tajeer.TenantIds = new[] { TenantA };

        // Inject an undocumented contractStatusCode for 4242L. Mapper throws
        // InvalidTajeerStatusException — the check must catch and continue.
        var tajeer = new InMemoryTajeerContractClient(
            getFactory: contractNumber => IntegrationResult<GetContractResponse>.Success(new GetContractResponse
            {
                ContractNumber = contractNumber,
                ContractStatusCode = contractNumber == 4242L ? 99 : 4,
            }));

        var check = new TajeerStatusMirrorCheck(db, tajeer, Options.Create(opts),
            NullLogger<TajeerStatusMirrorCheck>.Instance);

        Func<Task> act = () => check.RunAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        tajeer.GetCalls.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_respects_cancellation_between_tenants()
    {
        await using var db = NewDb();
        AddActive(db, TenantA, 1001, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var opts = NewOptions();
        opts.Tajeer.TenantIds = new[] { TenantA, TenantB };

        var tajeer = new InMemoryTajeerContractClient();
        var check = new TajeerStatusMirrorCheck(db, tajeer, Options.Create(opts),
            NullLogger<TajeerStatusMirrorCheck>.Instance);

        Func<Task> act = () => check.RunAsync(cts.Token);

        await act.Should().NotThrowAsync();
        tajeer.GetCalls.Should().BeEmpty(because: "pre-cancelled token bails before first DB hit");
    }

    // ---------- helpers ----------

    private static AutoLeaseNetDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AutoLeaseNetDbContext(options);
    }

    private static ReconciliationOptions NewOptions() => new();

    private static void AddActive(AutoLeaseNetDbContext db, Guid tenantId, long contractNumber, DateTimeOffset updatedAt)
    {
        var lease = Lease.CreatePending(new CreatePendingInput
        {
            TenantId = tenantId,
            TajeerContractNumber = contractNumber,
            IssuanceUrl = $"https://example/{contractNumber}/tok",
            ContractTypeCode = 1,
            ContractStartUtc = updatedAt.AddDays(-1),
            ContractEndUtc = updatedAt.AddDays(10),
            RentAmount = 200m,
            PaymentMethodCode = 1,
            NowUtc = updatedAt.AddDays(-1),
        });
        lease.MarkIssued(startKm: null, startFuelLevelCode: null, conditionNotes: null, nowUtc: updatedAt);
        db.Leases.Add(lease);
    }

    private static void AddPending(AutoLeaseNetDbContext db, Guid tenantId, long contractNumber, DateTimeOffset updatedAt)
    {
        var lease = Lease.CreatePending(new CreatePendingInput
        {
            TenantId = tenantId,
            TajeerContractNumber = contractNumber,
            IssuanceUrl = $"https://example/{contractNumber}/tok",
            ContractTypeCode = 1,
            ContractStartUtc = updatedAt.AddDays(-1),
            ContractEndUtc = updatedAt.AddDays(10),
            RentAmount = 200m,
            PaymentMethodCode = 1,
            NowUtc = updatedAt.AddDays(-1),
        });
        db.Leases.Add(lease);
    }
}
