using AutoLeaseNet.Adapters.Cache.InMemory;
using AutoLeaseNet.Application.Sales;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Sales;

public sealed class RecordApprovalDecisionCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0004-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("d4d4d4d4-0000-0000-0000-000000000030");
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 10, 0, 0, TimeSpan.Zero);

    private sealed class Harness : IDisposable
    {
        public AutoLeaseNetDbContext Db { get; }
        public RecordApprovalDecisionCommandHandler Sut { get; }
        public Quotation Quotation { get; }

        public Harness(bool twoTiers = false)
        {
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            var customer = Customer.CreateB2B(new B2BCreateInput
            {
                TenantId = TenantId, LegalName = "Corp", CommercialRegistration = "CR-003", NowUtc = Now,
            });
            Db.Customers.Add(customer);

            Quotation = Quotation.CreateDraft(new CreateQuotationInput
            {
                TenantId = TenantId,
                QuoteNumber = "Q-20260607-0002",
                CustomerId = customer.Id,
                AccountManagerId = UserId,
                QuoteDate = DateOnly.FromDateTime(Now.DateTime),
                ValidUntilDate = DateOnly.FromDateTime(Now.DateTime).AddDays(30),
                ContractType = QuotationContractType.LongTermLease,
                NowUtc = Now,
            });
            Quotation.AddLine(new AddQuotationLineInput
            {
                ItemType = QuotationItemType.VehicleRental, Description = "Camry",
                Quantity = 1, UnitPriceSar = 5_000m, NowUtc = Now,
            });

            var tier1 = ApprovalTier.Create(TenantId, 1, "ROLE_SALES_MGR", 0m, Now);
            var tiers = twoTiers
                ? new[] { tier1, ApprovalTier.Create(TenantId, 2, "ROLE_DIRECTOR", 0m, Now) }
                : new[] { tier1 };
            Quotation.SubmitForApproval(tiers, Now);

            Db.Quotations.Add(Quotation);
            Db.SaveChanges();

            var quotationRepo = new EfQuotationRepository(Db);
            var uow = new InMemoryUow(Db);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            IIdempotencyStore idempotency = new InMemoryIdempotencyStore(memoryCache);
            var tenant = new StubTenantContext(TenantId, UserId);
            var clock = new FixedClock(Now);

            Sut = new RecordApprovalDecisionCommandHandler(
                quotationRepo, uow, idempotency, tenant, clock,
                NullLogger<RecordApprovalDecisionCommandHandler>.Instance);
        }

        public RecordApprovalDecisionCommand BuildApprove(byte tierLevel, string idemKey = "idem-approve") =>
            new() { IdempotencyKey = idemKey, QuotationId = Quotation.Id, TierLevel = tierLevel, Approved = true };

        public RecordApprovalDecisionCommand BuildReject(byte tierLevel, string idemKey = "idem-reject") =>
            new() { IdempotencyKey = idemKey, QuotationId = Quotation.Id, TierLevel = tierLevel, Approved = false, Notes = "Too expensive" };

        public void Dispose() => Db.Dispose();

        private sealed class InMemoryUow(AutoLeaseNetDbContext db) : IUnitOfWork
        {
            public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        }

        private sealed class StubTenantContext(Guid tenantId, Guid userId) : ITenantContext
        {
            public Guid TenantId { get; } = tenantId;
            public Guid? CustomerId => null;
            public Guid? UserId { get; } = userId;
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
    public async Task Handle_single_tier_approval_flips_quotation_to_Approved()
    {
        using var h = new Harness(twoTiers: false);

        var result = await h.Sut.Handle(h.BuildApprove(1), CancellationToken.None);

        result.Success.Should().BeTrue(because: $"error: {result.ErrorCode} — {result.ErrorMessage}");
        result.Status.Should().Be(QuotationStatus.Approved);

        var persisted = await h.Db.Quotations.Include(q => q.Approvals).SingleAsync();
        persisted.Status.Should().Be(QuotationStatus.Approved);
    }

    [Fact]
    public async Task Handle_two_tier_approval_first_tier_stays_PendingApproval()
    {
        using var h = new Harness(twoTiers: true);

        var result = await h.Sut.Handle(h.BuildApprove(1), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(QuotationStatus.PendingApproval, because: "second tier still pending");
    }

    [Fact]
    public async Task Handle_two_tier_full_approval_moves_to_Approved()
    {
        using var h = new Harness(twoTiers: true);

        await h.Sut.Handle(h.BuildApprove(1, "idem-t1"), CancellationToken.None);
        var result = await h.Sut.Handle(h.BuildApprove(2, "idem-t2"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(QuotationStatus.Approved);
    }

    [Fact]
    public async Task Handle_rejection_flips_quotation_to_Rejected()
    {
        using var h = new Harness(twoTiers: false);

        var result = await h.Sut.Handle(h.BuildReject(1), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(QuotationStatus.Rejected);

        var persisted = await h.Db.Quotations.SingleAsync();
        persisted.Status.Should().Be(QuotationStatus.Rejected);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_unknown_quotation()
    {
        using var h = new Harness();
        var cmd = new RecordApprovalDecisionCommand
        {
            IdempotencyKey = "idem-bad", QuotationId = Guid.NewGuid(), TierLevel = 1, Approved = true,
        };

        var result = await h.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("quotation.not_found");
    }

    [Fact]
    public async Task Handle_idempotency_replay_does_not_double_decide()
    {
        using var h = new Harness(twoTiers: false);
        var cmd = h.BuildApprove(1, "idem-replay-approve");

        var first = await h.Sut.Handle(cmd, CancellationToken.None);
        var second = await h.Sut.Handle(cmd, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Should().BeEquivalentTo(first);
    }
}
