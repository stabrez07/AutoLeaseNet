using AutoLeaseNet.Adapters.Cache.InMemory;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Application.Sales;
using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Sales;

public sealed class QuotationApprovalCommandHandlersTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaa3333-0000-0000-0000-000000000001");
    private static readonly Guid CustomerId = Guid.Parse("bbbb3333-0000-0000-0000-000000000001");
    private static readonly Guid AccountManagerId = Guid.Parse("cccc3333-0000-0000-0000-000000000001");
    private static readonly Guid ApproverId = Guid.Parse("dddd3333-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Submit_handler_routes_quote_to_matching_tiers()
    {
        await using var db = NewDb();
        var quote = NewDraft();
        quote.AddLine(new AddQuotationLineInput
        {
            ItemType = QuotationItemType.VehicleRental,
            Description = "Camry lease",
            Quantity = 1,
            UnitPriceSar = 100_000m,
            DiscountPercent = 0m,
            NowUtc = Now,
        });

        db.Quotations.Add(quote);
        db.ApprovalTiers.AddRange(
            ApprovalTier.Create(TenantId, 1, "APPROVAL_T1", 0m, Now),
            ApprovalTier.Create(TenantId, 2, "APPROVAL_T2", 50_000m, Now),
            ApprovalTier.Create(TenantId, 3, "APPROVAL_T3", 200_000m, Now));
        await db.SaveChangesAsync();

        var handler = new SubmitQuotationForApprovalCommandHandler(
            new EfQuotationRepository(db),
            new EfApprovalTierRepository(db),
            new InMemoryUow(db),
            new InMemoryIdempotencyStore(new MemoryCache(new MemoryCacheOptions())),
            new StubTenantContext(TenantId, ApproverId),
            new FixedClock(Now),
            NullLogger<SubmitQuotationForApprovalCommandHandler>.Instance);

        var result = await handler.Handle(
            new SubmitQuotationForApprovalCommand("idem-submit-1", quote.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(QuotationStatus.PendingApproval);
        result.NextTierLevel.Should().Be(1);
        result.NextRequiredRoleCode.Should().Be("APPROVAL_T1");

        var saved = await db.Quotations.Include(q => q.Approvals).SingleAsync(q => q.Id == quote.Id);
        saved.Approvals.Select(a => a.TierLevel).Should().Equal((byte)1, (byte)2);
    }

    [Fact]
    public async Task Decision_handler_approves_in_sequence_until_quote_is_approved()
    {
        await using var db = NewDb();
        var quote = NewDraft();
        quote.AddLine(new AddQuotationLineInput
        {
            ItemType = QuotationItemType.VehicleRental,
            Description = "Camry lease",
            Quantity = 1,
            UnitPriceSar = 100_000m,
            DiscountPercent = 0m,
            NowUtc = Now,
        });
        quote.SubmitForApproval(
        [
            ApprovalTier.Create(TenantId, 1, "APPROVAL_T1", 0m, Now),
            ApprovalTier.Create(TenantId, 2, "APPROVAL_T2", 50_000m, Now),
        ], Now);

        db.Quotations.Add(quote);
        await db.SaveChangesAsync();

        var handler = new RecordQuotationApprovalDecisionCommandHandler(
            new EfQuotationRepository(db),
            new InMemoryUow(db),
            new InMemoryIdempotencyStore(new MemoryCache(new MemoryCacheOptions())),
            new StubTenantContext(TenantId, ApproverId),
            new FixedClock(Now.AddMinutes(10)),
            NullLogger<RecordQuotationApprovalDecisionCommandHandler>.Instance);

        var first = await handler.Handle(
            new RecordQuotationApprovalDecisionCommand("idem-decide-1", quote.Id, 1, true, "ok"),
            CancellationToken.None);
        var second = await handler.Handle(
            new RecordQuotationApprovalDecisionCommand("idem-decide-2", quote.Id, 2, true, "ok"),
            CancellationToken.None);

        first.Success.Should().BeTrue();
        first.Status.Should().Be(QuotationStatus.PendingApproval);
        second.Success.Should().BeTrue();
        second.Status.Should().Be(QuotationStatus.Approved);

        var saved = await db.Quotations.Include(q => q.Approvals).SingleAsync(q => q.Id == quote.Id);
        saved.Status.Should().Be(QuotationStatus.Approved);
        saved.Approvals.Should().OnlyContain(a => a.Status == QuotationApprovalStatus.Approved);
    }

    private static Quotation NewDraft() =>
        Quotation.CreateDraft(new CreateQuotationInput
        {
            TenantId = TenantId,
            QuoteNumber = "Q-ALN-202606-0100",
            CustomerId = CustomerId,
            AccountManagerId = AccountManagerId,
            QuoteDate = new DateOnly(2026, 6, 16),
            ValidUntilDate = new DateOnly(2026, 6, 30),
            ContractType = QuotationContractType.LongTermLease,
            EstimatedDurationMonths = 12,
            DiscountPercent = 0m,
            NowUtc = Now,
        });

    private static AutoLeaseNetDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AutoLeaseNetDbContext(options);
    }

    private sealed class InMemoryUow(AutoLeaseNetDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class StubTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid? CustomerId => null;
        public Guid? UserId { get; } = userId;
        public string UserType => "INTERNAL_STAFF";
        public IReadOnlyList<Guid> BranchIds => Array.Empty<Guid>();
        public bool IsInternalStaff => true;
        public bool IsSystem => false;
    }
}
