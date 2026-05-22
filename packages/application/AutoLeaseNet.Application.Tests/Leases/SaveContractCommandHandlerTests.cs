using AutoLeaseNet.Adapters.Cache.InMemory;
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
/// T5.1 — SaveContractCommandHandler exercised against InMemory Tajeer + EF Core
/// InMemory provider + InMemory idempotency store. Asserts the full happy path
/// (Tajeer success → Lease row written + cached) and the key failure modes (Tajeer
/// vendor error → no row written; idempotency replay returns cached result without
/// re-calling Tajeer; missing tenant throws).
/// </summary>
public sealed class SaveContractCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static SaveContractRequest MinimalRequest() => new()
    {
        Renter = new RenterDto { PersonAddress = "Riyadh", Mobile = "0501234567", IdTypeCode = 1, IdNumber = 1234567890 },
        PaymentDetails = new PaymentDetailsDto { PaymentMethodCode = 1, RentAmount = 200m, PaidAmount = 50m },
        VehicleDetails = new VehicleDetailsDto { VehicleId = 4242 },
        WorkingBranchId = 1,
        RentPolicyId = 1,
        ContractStartDate = "2026-05-23T10:00",
        ContractEndDate = "2026-05-25T10:00",
        ReceiveBranchId = 1,
        ReturnBranchId = 1,
        ContractTypeCode = 1,
        OperatorId = 99,
    };

    private sealed class Harness : IDisposable
    {
        public AutoLeaseNetDbContext Db { get; }
        public InMemoryTajeerContractClient Tajeer { get; }
        public SaveContractCommandHandler Sut { get; }
        public IIdempotencyStore Idempotency { get; }

        public Harness(InMemoryTajeerContractClient? tajeer = null, Guid? tenantId = null)
        {
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options);
            Tajeer = tajeer ?? new InMemoryTajeerContractClient();
            var leases = new EfLeaseRepository(Db);
            var uow = new InMemoryUow(Db);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            Idempotency = new InMemoryIdempotencyStore(memoryCache);
            var tenant = new StubTenantContext(tenantId ?? TenantId);
            var clock = new FixedClock(new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero));
            Sut = new SaveContractCommandHandler(
                Tajeer, leases, uow, Idempotency, tenant, clock,
                NullLogger<SaveContractCommandHandler>.Instance);
        }

        public void Dispose() => Db.Dispose();

        private sealed class InMemoryUow(AutoLeaseNetDbContext db) : IUnitOfWork
        {
            public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        }

        private sealed class StubTenantContext(Guid tenantId) : ITenantContext
        {
            public Guid TenantId { get; } = tenantId;
            public Guid? CustomerId => null;
            public Guid? UserId => null;
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
    public async Task Handle_persists_Lease_in_PendingIssuance_on_Tajeer_success()
    {
        using var harness = new Harness();

        var result = await harness.Sut.Handle(
            new SaveContractCommand("idem-001", CustomerId: null, MinimalRequest()),
            CancellationToken.None);

        result.Success.Should().BeTrue(because: $"error was {result.ErrorCode} — {result.ErrorMessage}");
        result.LeaseId.Should().NotBeNull();
        result.TajeerContractNumber.Should().BeGreaterThan(0);
        result.IssuanceUrl.Should().StartWith("https://inmemory.tajeer.local/#/public-contract/");

        var savedLease = await harness.Db.Leases.SingleAsync();
        savedLease.Id.Should().Be(result.LeaseId!.Value);
        savedLease.TenantId.Should().Be(TenantId);
        savedLease.Status.Should().Be(LeaseStatus.PendingIssuance);
        savedLease.TajeerContractNumber.Should().Be(result.TajeerContractNumber);
        savedLease.IssuanceUrl.Should().Be(result.IssuanceUrl);

        harness.Tajeer.SaveCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_does_not_persist_Lease_when_Tajeer_returns_business_error()
    {
        var failing = new InMemoryTajeerContractClient(
            _ => AutoLeaseNet.Adapters.Common.Result.IntegrationResult<SaveContractResponse>.Failure(
                errorCode: "tajeer.vendor.server.error.renter.mobile.invalid",
                errorMessage: "Mobile invalid",
                isTransient: false));
        using var harness = new Harness(failing);

        var result = await harness.Sut.Handle(
            new SaveContractCommand("idem-002", CustomerId: null, MinimalRequest()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.LeaseId.Should().BeNull();
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.renter.mobile.invalid");
        result.IsTransient.Should().BeFalse();

        (await harness.Db.Leases.CountAsync()).Should().Be(0, because: "failed Saves must not write any Lease row");
    }

    [Fact]
    public async Task Handle_replays_cached_result_for_same_idempotency_key_without_calling_Tajeer()
    {
        using var harness = new Harness();

        var first = await harness.Sut.Handle(
            new SaveContractCommand("idem-replay", CustomerId: null, MinimalRequest()),
            CancellationToken.None);
        var second = await harness.Sut.Handle(
            new SaveContractCommand("idem-replay", CustomerId: null, MinimalRequest()),
            CancellationToken.None);

        second.Should().BeEquivalentTo(first);
        harness.Tajeer.SaveCalls.Should().HaveCount(1, because: "the second call is served from the idempotency cache");
        (await harness.Db.Leases.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_different_tenants_share_no_idempotency_state()
    {
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");

        // Use one shared Tajeer fake so we can count cross-tenant calls.
        var sharedTajeer = new InMemoryTajeerContractClient();

        using var harnessA = new Harness(tajeer: sharedTajeer, tenantId: tenantA);
        using var harnessB = new Harness(tajeer: sharedTajeer, tenantId: tenantB);

        await harnessA.Sut.Handle(new SaveContractCommand("same-key", null, MinimalRequest()), CancellationToken.None);
        await harnessB.Sut.Handle(new SaveContractCommand("same-key", null, MinimalRequest()), CancellationToken.None);

        sharedTajeer.SaveCalls.Should().HaveCount(2, because: "the idempotency key is namespaced by tenant; tenants must not collide");
    }

    [Fact]
    public async Task Handle_throws_when_tenant_context_is_empty()
    {
        using var harness = new Harness(tenantId: Guid.Empty);

        var act = () => harness.Sut.Handle(
            new SaveContractCommand("idem-x", null, MinimalRequest()),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*authenticated tenant context*");
    }
}
