using AutoLeaseNet.Adapters.Cache.InMemory;
using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using AutoLeaseNet.Application.Leases;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Leases;

/// <summary>
/// Day-20 — SuspendLeaseCommandHandler tests.
/// </summary>
public sealed class SuspendLeaseCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    private sealed class Harness : IDisposable
    {
        public AutoLeaseNetDbContext Db { get; }
        public SuspendLeaseCommandHandler Sut { get; }
        public Lease Lease { get; }
        public InMemoryTajeerContractClient Tajeer { get; }

        public Harness(
            LeaseStatus startingStatus = LeaseStatus.Active,
            Func<SuspendContractRequest, IntegrationResult<SuspendContractResponse>>? suspendFactory = null)
        {
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            Lease = Lease.CreatePending(new CreatePendingInput
            {
                TenantId = TenantId,
                TajeerContractNumber = 7000,
                IssuanceUrl = "https://x/y",
                ContractTypeCode = 1,
                ContractStartUtc = Now.AddDays(-3),
                ContractEndUtc = Now.AddDays(2),
                RentAmount = 500m,
                PaymentMethodCode = 1,
                NowUtc = Now.AddDays(-3),
            });
            if (startingStatus is LeaseStatus.Active or LeaseStatus.Extended or LeaseStatus.Closed)
                Lease.MarkIssued(0, 4, null, Now.AddDays(-3).AddMinutes(30));
            if (startingStatus is LeaseStatus.Extended)
                Lease.IncrementExtension(Now.AddDays(5), Now.AddDays(-1));
            if (startingStatus is LeaseStatus.Closed)
                Lease.MarkClosed(1, null, 100, 3, "ok", null, Now.AddDays(-1));

            Db.Leases.Add(Lease); Db.SaveChanges();

            Tajeer = new InMemoryTajeerContractClient(suspendFactory: suspendFactory);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            Sut = new SuspendLeaseCommandHandler(
                new EfLeaseRepository(Db),
                new InMemoryUow(Db),
                new InMemoryIdempotencyStore(memoryCache),
                new StubTenantContext(TenantId),
                new FixedClock(Now),
                Tajeer,
                NullLogger<SuspendLeaseCommandHandler>.Instance);
        }

        public SuspendLeaseCommand BuildCommand(string idempotencyKey = "idem-suspend", int reason = 7) => new()
        {
            IdempotencyKey = idempotencyKey,
            LeaseId = Lease.Id,
            SuspensionReasonCode = reason,
            Notes = "body shop",
        };

        public void Dispose() => Db.Dispose();

        private sealed class InMemoryUow(AutoLeaseNetDbContext db) : IUnitOfWork
        {
            public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        }

        private sealed class StubTenantContext(Guid tenantId) : ITenantContext
        {
            public Guid TenantId { get; } = tenantId;
            public Guid? CustomerId => null;
            public Guid? UserId { get; } = Guid.Parse("d4d4d4d4-0000-0000-0000-000000000030");
            public string UserType => "InternalStaff";
            public IReadOnlyList<Guid> BranchIds => Array.Empty<Guid>();
            public bool IsInternalStaff => true;
            public bool IsSystem => false;
        }

        private sealed class FixedClock(DateTimeOffset now) : IClock
        {
            public DateTimeOffset UtcNow { get; } = now;
        }
    }

    [Fact]
    public async Task Handle_happy_path_calls_Tajeer_then_marks_lease_Suspended()
    {
        using var h = new Harness();

        var result = await h.Sut.Handle(h.BuildCommand(), CancellationToken.None);

        result.Success.Should().BeTrue(because: $"error was {result.ErrorCode} — {result.ErrorMessage}");
        result.LeaseStatus.Should().Be(nameof(LeaseStatus.Suspended));
        result.SuspensionReasonCode.Should().Be(7);
        h.Tajeer.SuspendCalls.Should().HaveCount(1);
        h.Tajeer.SuspendCalls[0].ContractNumber.Should().Be(7000);

        var lease = await h.Db.Leases.SingleAsync();
        lease.Status.Should().Be(LeaseStatus.Suspended);
        lease.SuspensionReasonCode.Should().Be(7);
    }

    [Fact]
    public async Task Handle_short_circuits_before_Tajeer_when_lease_already_Closed()
    {
        using var h = new Harness(startingStatus: LeaseStatus.Closed);

        var result = await h.Sut.Handle(h.BuildCommand("k-closed"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("lease.invalid_state_for_suspend");
        h.Tajeer.SuspendCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_aborts_on_Tajeer_non_transient_failure_without_mutating_local()
    {
        using var h = new Harness(
            suspendFactory: _ => IntegrationResult<SuspendContractResponse>.Failure(
                "tajeer.vendor.server.error.contract.not_active", "Not active", isTransient: false));

        var result = await h.Sut.Handle(h.BuildCommand("k-tajeer-fail"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.suspend.failure");
        var lease = await h.Db.Leases.SingleAsync();
        lease.Status.Should().Be(LeaseStatus.Active, because: "Tajeer failure must keep local state untouched");
    }

    [Fact]
    public async Task Handle_idempotency_replay_returns_cached_envelope()
    {
        using var h = new Harness();
        var cmd = h.BuildCommand("k-replay");

        var first = await h.Sut.Handle(cmd, CancellationToken.None);
        var second = await h.Sut.Handle(cmd, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Should().BeEquivalentTo(first);
        h.Tajeer.SuspendCalls.Should().HaveCount(1);
    }
}
