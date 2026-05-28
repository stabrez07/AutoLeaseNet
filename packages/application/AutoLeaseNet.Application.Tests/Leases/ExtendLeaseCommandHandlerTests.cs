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
/// Day-20 — ExtendLeaseCommandHandler tests. Each test owns its own EF InMemory
/// DbContext + harness; Tajeer is the InMemory adapter with optional override
/// factories for negative-path tests.
/// </summary>
public sealed class ExtendLeaseCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    private sealed class Harness : IDisposable
    {
        public AutoLeaseNetDbContext Db { get; }
        public ExtendLeaseCommandHandler Sut { get; }
        public Lease Lease { get; }
        public InMemoryTajeerContractClient Tajeer { get; }

        public Harness(
            LeaseStatus startingStatus = LeaseStatus.Active,
            Func<ExtendContractRequest, IntegrationResult<ExtendContractResponse>>? extendFactory = null,
            int extensionCountSeed = 0)
        {
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            Lease = Lease.CreatePending(new CreatePendingInput
            {
                TenantId = TenantId,
                TajeerContractNumber = 6000,
                IssuanceUrl = "https://x/y",
                ContractTypeCode = 1,
                ContractStartUtc = Now.AddDays(-3),
                ContractEndUtc = Now.AddDays(2),
                RentAmount = 500m,
                PaymentMethodCode = 1,
                NowUtc = Now.AddDays(-3),
            });
            Lease.MarkIssued(0, 4, null, Now.AddDays(-3).AddMinutes(30));
            for (var i = 0; i < extensionCountSeed; i++)
            {
                Lease.IncrementExtension(Now.AddDays(2 + i + 1), Now.AddDays(-2));
            }
            if (startingStatus is LeaseStatus.Suspended) Lease.MarkSuspended(1, Now.AddDays(-1));

            Db.Leases.Add(Lease); Db.SaveChanges();

            Tajeer = new InMemoryTajeerContractClient(extendFactory: extendFactory);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            Sut = new ExtendLeaseCommandHandler(
                new EfLeaseRepository(Db),
                new InMemoryUow(Db),
                new InMemoryIdempotencyStore(memoryCache),
                new StubTenantContext(TenantId),
                new FixedClock(Now),
                Tajeer,
                NullLogger<ExtendLeaseCommandHandler>.Instance);
        }

        public ExtendLeaseCommand BuildCommand(string idempotencyKey = "idem-extend", DateTimeOffset? newEnd = null) => new()
        {
            IdempotencyKey = idempotencyKey,
            LeaseId = Lease.Id,
            NewContractEndUtc = newEnd ?? Lease.ContractEndUtc.AddDays(3),
            AdditionalCharges = 100m,
            PaymentMethodCode = 1,
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
    public async Task Handle_happy_path_calls_Tajeer_then_IncrementsExtension()
    {
        using var h = new Harness();
        var cmd = h.BuildCommand();

        var result = await h.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeTrue(because: $"error was {result.ErrorCode} — {result.ErrorMessage}");
        result.LeaseStatus.Should().Be(nameof(LeaseStatus.Extended));
        result.ExtensionCount.Should().Be(1);
        result.Charges.Should().NotBeNull();
        result.Charges!.GrandTotal.Should().Be(115m, because: "InMemory adapter applies 15% VAT to 100 charges");

        h.Tajeer.ExtendCalls.Should().HaveCount(1);
        h.Tajeer.ExtendCalls[0].ContractNumber.Should().Be(6000);
        h.Tajeer.ExtendCalls[0].NewContractEndDate.Should().StartWith(cmd.NewContractEndUtc.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        var lease = await h.Db.Leases.SingleAsync();
        lease.ExtensionCount.Should().Be(1);
        lease.ContractEndUtc.Should().Be(cmd.NewContractEndUtc);
    }

    [Fact]
    public async Task Handle_aborts_on_Tajeer_transient_failure_without_mutating_local()
    {
        using var h = new Harness(
            extendFactory: _ => IntegrationResult<ExtendContractResponse>.Failure(
                "tajeer.http.503", "down", isTransient: true));

        var result = await h.Sut.Handle(h.BuildCommand("k-tajeer-fail"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("tajeer.extend.transient");
        var lease = await h.Db.Leases.SingleAsync();
        lease.ExtensionCount.Should().Be(0);
        lease.Status.Should().Be(LeaseStatus.Active);
    }

    [Fact]
    public async Task Handle_short_circuits_before_Tajeer_when_extensions_exhausted()
    {
        using var h = new Harness(extensionCountSeed: Lease.MaxExtensions);

        var result = await h.Sut.Handle(h.BuildCommand("k-exhausted"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("lease.extensions_exhausted");
        h.Tajeer.ExtendCalls.Should().BeEmpty(because: "guard runs before any Tajeer call");
    }

    [Fact]
    public async Task Handle_rejects_non_monotonic_NewContractEndUtc()
    {
        using var h = new Harness();
        var cmd = h.BuildCommand("k-back-in-time", newEnd: h.Lease.ContractEndUtc); // same date

        var result = await h.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("lease.invalid_new_end_date");
        h.Tajeer.ExtendCalls.Should().BeEmpty();
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
        h.Tajeer.ExtendCalls.Should().HaveCount(1, because: "replay must not re-hit Tajeer");
    }
}
