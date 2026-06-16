using AutoLeaseNet.Adapters.Cache.InMemory;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Application.Sales;
using AutoLeaseNet.Domain.Sales;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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

        var quotations = Substitute.For<IQuotationRepository>();
        quotations.GetByIdAsync(TenantId, quote.Id, Arg.Any<CancellationToken>())
            .Returns(quote);

        var tiers = new[]
        {
            ApprovalTier.Create(TenantId, 1, "APPROVAL_T1", 0m, Now),
            ApprovalTier.Create(TenantId, 2, "APPROVAL_T2", 50_000m, Now),
            ApprovalTier.Create(TenantId, 3, "APPROVAL_T3", 200_000m, Now),
        };
        var approvalTiers = Substitute.For<IApprovalTierRepository>();
        approvalTiers.GetActiveForTenantAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(tiers);

        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var handler = new SubmitQuotationForApprovalCommandHandler(
            quotations,
            approvalTiers,
            uow,
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
        quote.Approvals.Select(a => a.TierLevel).Should().Equal((byte)1, (byte)2);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Decision_handler_approves_in_sequence_until_quote_is_approved()
    {
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

        var quotations = Substitute.For<IQuotationRepository>();
        quotations.GetByIdAsync(TenantId, quote.Id, Arg.Any<CancellationToken>())
            .Returns(quote);

        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var handler = new RecordQuotationApprovalDecisionCommandHandler(
            quotations,
            uow,
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
        quote.Approvals.Should().OnlyContain(a => a.Status == QuotationApprovalStatus.Approved);
        await uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
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
