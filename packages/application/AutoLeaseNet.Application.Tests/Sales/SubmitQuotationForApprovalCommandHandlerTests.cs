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

public sealed class SubmitQuotationForApprovalCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0003-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("d4d4d4d4-0000-0000-0000-000000000030");
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 10, 0, 0, TimeSpan.Zero);

    private sealed class Harness : IDisposable
    {
        // HandlerDb is a fresh DbContext sharing the same InMemory store as the setup Db.
        // This matches production behaviour (each request scope gets a new DbContext) and
        // avoids EF change-tracker conflicts when new child entities are added to an
        // already-saved aggregate root.
        private readonly string _dbName = Guid.NewGuid().ToString();
        private readonly AutoLeaseNetDbContext _setupDb;
        public AutoLeaseNetDbContext Db { get; }
        public SubmitQuotationForApprovalCommandHandler Sut { get; }
        public Quotation Quotation { get; }

        public Harness(decimal lineUnitPrice = 2_000m, bool seedTiers = true)
        {
            _setupDb = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options);

            var customer = Customer.CreateB2B(new B2BCreateInput
            {
                TenantId = TenantId, LegalName = "Corp", CommercialRegistration = "CR-002", NowUtc = Now,
            });
            _setupDb.Customers.Add(customer);

            Quotation = Quotation.CreateDraft(new CreateQuotationInput
            {
                TenantId = TenantId,
                QuoteNumber = "Q-20260607-0001",
                CustomerId = customer.Id,
                AccountManagerId = UserId,
                QuoteDate = DateOnly.FromDateTime(Now.DateTime),
                ValidUntilDate = DateOnly.FromDateTime(Now.DateTime).AddDays(30),
                ContractType = QuotationContractType.LongTermLease,
                EstimatedDurationMonths = 12,
                NowUtc = Now,
            });
            Quotation.AddLine(new AddQuotationLineInput
            {
                ItemType = QuotationItemType.VehicleRental,
                Description = "Camry",
                Quantity = 1,
                UnitPriceSar = lineUnitPrice,
                NowUtc = Now,
            });
            _setupDb.Quotations.Add(Quotation);

            if (seedTiers)
            {
                var tier1 = ApprovalTier.Create(TenantId, 1, "ROLE_SALES_MGR", 0m, Now);
                var tier2 = ApprovalTier.Create(TenantId, 2, "ROLE_DIRECTOR", 50_000m, Now);
                _setupDb.ApprovalTiers.AddRange(tier1, tier2);
            }
            _setupDb.SaveChanges();

            // Fresh DbContext for the handler — avoids tracked-entity conflicts
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options);

            var quotationRepo = new EfQuotationRepository(Db);
            var approvalTierRepo = new EfApprovalTierRepository(Db);
            var uow = new InMemoryUow(Db);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            IIdempotencyStore idempotency = new InMemoryIdempotencyStore(memoryCache);
            var tenant = new StubTenantContext(TenantId, UserId);
            var clock = new FixedClock(Now);

            Sut = new SubmitQuotationForApprovalCommandHandler(
                quotationRepo, approvalTierRepo, uow, idempotency, tenant, clock,
                NullLogger<SubmitQuotationForApprovalCommandHandler>.Instance);
        }

        public SubmitQuotationForApprovalCommand BuildCommand(string idemKey = "idem-submit") =>
            new(idemKey, Quotation.Id);

        public void Dispose()
        {
            Db.Dispose();
            _setupDb.Dispose();
        }

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
    public async Task Handle_submits_quotation_above_tier2_threshold_and_moves_to_PendingApproval()
    {
        // 60,000 SAR × 1.15 VAT = 69,000 — above tier2 threshold (50,000)
        using var h = new Harness(lineUnitPrice: 60_000m);

        var result = await h.Sut.Handle(h.BuildCommand(), CancellationToken.None);

        result.Success.Should().BeTrue(because: $"error: {result.ErrorCode} — {result.ErrorMessage}");
        result.Status.Should().Be(QuotationStatus.PendingApproval);
        result.RequiredTierLevels.Should().BeEquivalentTo(new byte[] { 1, 2 }, o => o.WithStrictOrdering());

        var persisted = await h.Db.Quotations.Include(q => q.Approvals).SingleAsync();
        persisted.Status.Should().Be(QuotationStatus.PendingApproval);
        persisted.Approvals.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_submits_quotation_below_tier2_threshold_moves_to_PendingApproval_with_single_tier()
    {
        // 1,000 SAR — only tier1 applies (min=0)
        using var h = new Harness(lineUnitPrice: 1_000m);

        var result = await h.Sut.Handle(h.BuildCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(QuotationStatus.PendingApproval);
        result.RequiredTierLevels.Should().BeEquivalentTo(new byte[] { 1 });
    }

    [Fact]
    public async Task Handle_with_no_tiers_auto_approves_quotation()
    {
        using var h = new Harness(lineUnitPrice: 2_000m, seedTiers: false);

        var result = await h.Sut.Handle(h.BuildCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(QuotationStatus.Approved);
        result.RequiredTierLevels.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_returns_not_found_for_unknown_quotation_id()
    {
        using var h = new Harness();
        var cmd = new SubmitQuotationForApprovalCommand("idem-bad", Guid.NewGuid());

        var result = await h.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("quotation.not_found");
    }

    [Fact]
    public async Task Handle_idempotency_replay_returns_cached_result()
    {
        using var h = new Harness(lineUnitPrice: 2_000m, seedTiers: false);
        var cmd = h.BuildCommand("idem-replay-submit");

        var first = await h.Sut.Handle(cmd, CancellationToken.None);
        var second = await h.Sut.Handle(cmd, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Should().BeEquivalentTo(first);
    }
}
