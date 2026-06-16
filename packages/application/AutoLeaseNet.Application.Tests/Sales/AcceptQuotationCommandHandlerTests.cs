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

public sealed class AcceptQuotationCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaa4400-0000-0000-0000-000000000001");
    private static readonly Guid CustomerId = Guid.Parse("bbbb4400-0000-0000-0000-000000000001");
    private static readonly Guid AccountManagerId = Guid.Parse("cccc4400-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Accept_handler_transitions_sent_quote_to_accepted()
    {
        var quote = NewSentToCustomerQuote();

        var quotations = Substitute.For<IQuotationRepository>();
        quotations.GetByIdAsync(TenantId, quote.Id, Arg.Any<CancellationToken>())
            .Returns(quote);

        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var handler = BuildHandler(quotations, uow);

        var result = await handler.Handle(
            new AcceptQuotationCommand(
                QuotationId: quote.Id,
                CustomerSignature: "Ahmad Al-Harbi",
                IdempotencyKey: "accept-idem-01"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(QuotationStatus.Accepted.ToString());
        result.AcceptedAtUtc.Should().Be(Now);
        quote.Status.Should().Be(QuotationStatus.Accepted);
        quote.AcceptedAtUtc.Should().Be(Now);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accept_handler_returns_error_when_quote_not_sent_to_customer()
    {
        // Quote is still Draft — cannot accept
        var quote = NewDraft();

        var quotations = Substitute.For<IQuotationRepository>();
        quotations.GetByIdAsync(TenantId, quote.Id, Arg.Any<CancellationToken>())
            .Returns(quote);

        var uow = Substitute.For<IUnitOfWork>();

        var handler = BuildHandler(quotations, uow);

        var result = await handler.Handle(
            new AcceptQuotationCommand(
                QuotationId: quote.Id,
                CustomerSignature: null,
                IdempotencyKey: "accept-idem-02"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("quotation.invalid_transition");
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accept_handler_replays_idempotent_result_on_duplicate_key()
    {
        var quote = NewSentToCustomerQuote();

        var quotations = Substitute.For<IQuotationRepository>();
        quotations.GetByIdAsync(TenantId, quote.Id, Arg.Any<CancellationToken>())
            .Returns(quote);

        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var idempotencyStore = new InMemoryIdempotencyStore(new MemoryCache(new MemoryCacheOptions()));
        var handler = BuildHandler(quotations, uow, idempotencyStore);

        var cmd = new AcceptQuotationCommand(quote.Id, "sig", "accept-idem-dup");
        var first = await handler.Handle(cmd, CancellationToken.None);
        var second = await handler.Handle(cmd, CancellationToken.None);

        first.Should().BeEquivalentTo(second);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>()); // only once
    }

    [Fact]
    public async Task Accept_handler_returns_error_when_quote_not_found()
    {
        var quotations = Substitute.For<IQuotationRepository>();
        quotations.GetByIdAsync(TenantId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Quotation?)null);

        var uow = Substitute.For<IUnitOfWork>();
        var handler = BuildHandler(quotations, uow);

        var result = await handler.Handle(
            new AcceptQuotationCommand(Guid.NewGuid(), null, "accept-idem-03"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("quotation.not_found");
    }

    private static AcceptQuotationCommandHandler BuildHandler(
        IQuotationRepository quotations,
        IUnitOfWork uow,
        InMemoryIdempotencyStore? store = null)
    {
        store ??= new InMemoryIdempotencyStore(new MemoryCache(new MemoryCacheOptions()));
        return new AcceptQuotationCommandHandler(
            quotations,
            uow,
            store,
            new StubTenantContext(TenantId),
            new FixedClock(Now),
            NullLogger<AcceptQuotationCommandHandler>.Instance);
    }

    private static Quotation NewDraft() =>
        Quotation.CreateDraft(new CreateQuotationInput
        {
            TenantId = TenantId,
            QuoteNumber = "Q-ALN-202606-0200",
            CustomerId = CustomerId,
            AccountManagerId = AccountManagerId,
            QuoteDate = new DateOnly(2026, 6, 1),
            ValidUntilDate = new DateOnly(2026, 6, 30),
            ContractType = QuotationContractType.LongTermLease,
            EstimatedDurationMonths = 12,
            DiscountPercent = 0m,
            NowUtc = Now,
        });

    private static Quotation NewSentToCustomerQuote()
    {
        var q = NewDraft();
        q.AddLine(new AddQuotationLineInput
        {
            ItemType = QuotationItemType.VehicleRental,
            Description = "Camry lease",
            Quantity = 1,
            UnitPriceSar = 50_000m,
            DiscountPercent = 0m,
            NowUtc = Now,
        });
        // Auto-approve (no tiers required for 50k below typical threshold) then send
        q.SubmitForApproval(Array.Empty<ApprovalTier>(), Now);
        q.MarkSentToCustomer(null, Now.AddMinutes(5));
        return q;
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class StubTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid? CustomerId => null;
        public Guid? UserId => Guid.Parse("eeee4400-0000-0000-0000-000000000001");
        public string UserType => "CUSTOMER";
        public IReadOnlyList<Guid> BranchIds => Array.Empty<Guid>();
        public bool IsInternalStaff => false;
        public bool IsSystem => false;
    }
}
