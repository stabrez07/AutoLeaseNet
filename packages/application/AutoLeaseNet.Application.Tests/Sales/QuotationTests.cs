using AutoLeaseNet.Domain.Sales;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Sales;

/// <summary>
/// Domain coverage for <see cref="Quotation"/>: pricing recompute, the Spec 02 §4.1 state
/// machine (submit / tiered approval / recall / send / accept / expire), tier-ordering and
/// snapshot rules, and idempotent re-entry.
/// </summary>
public sealed class QuotationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaa3333-0000-0000-0000-000000000001");
    private static readonly Guid CustomerId = Guid.Parse("bbbb3333-0000-0000-0000-000000000001");
    private static readonly Guid AccountManagerId = Guid.Parse("cccc3333-0000-0000-0000-000000000001");
    private static readonly Guid ApproverId = Guid.Parse("dddd3333-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    private static Quotation NewDraft(decimal discountPercent = 0m) =>
        Quotation.CreateDraft(new CreateQuotationInput
        {
            TenantId = TenantId,
            QuoteNumber = "Q-ALN-202606-0001",
            CustomerId = CustomerId,
            AccountManagerId = AccountManagerId,
            QuoteDate = new DateOnly(2026, 6, 7),
            ValidUntilDate = new DateOnly(2026, 6, 21),
            ContractType = QuotationContractType.LongTermLease,
            EstimatedDurationMonths = 12,
            DiscountPercent = discountPercent,
            NowUtc = Now,
        });

    private static void AddRentalLine(Quotation q, decimal unitPrice, int qty = 1, decimal lineDiscount = 0m) =>
        q.AddLine(new AddQuotationLineInput
        {
            ItemType = QuotationItemType.VehicleRental,
            Description = "Toyota Camry 2025 — 12mo",
            Quantity = qty,
            UnitPriceSar = unitPrice,
            DiscountPercent = lineDiscount,
            NowUtc = Now,
        });

    private static ApprovalTier Tier(byte level, decimal min) =>
        ApprovalTier.Create(TenantId, level, $"ROLE_T{level}", min, Now);

    // ─── Creation + pricing ─────────────────────────────────────────────────

    [Fact]
    public void CreateDraft_starts_Draft_with_zero_totals()
    {
        var q = NewDraft();

        q.Status.Should().Be(QuotationStatus.Draft);
        q.SubTotalSar.Should().Be(0m);
        q.TotalSar.Should().Be(0m);
        q.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CreateDraft_rejects_ValidUntil_before_QuoteDate()
    {
        var act = () => Quotation.CreateDraft(new CreateQuotationInput
        {
            TenantId = TenantId, QuoteNumber = "Q-1", CustomerId = CustomerId,
            AccountManagerId = AccountManagerId,
            QuoteDate = new DateOnly(2026, 6, 7), ValidUntilDate = new DateOnly(2026, 6, 6),
            ContractType = QuotationContractType.Daily, NowUtc = Now,
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddLine_computes_subtotal_vat_and_total_at_15_percent()
    {
        var q = NewDraft();
        AddRentalLine(q, unitPrice: 1000m, qty: 10); // 10,000 subtotal

        q.SubTotalSar.Should().Be(10_000m);
        q.VatSar.Should().Be(1_500m);
        q.TotalSar.Should().Be(11_500m);
    }

    [Fact]
    public void Line_level_discount_reduces_line_total()
    {
        var q = NewDraft();
        AddRentalLine(q, unitPrice: 1000m, qty: 10, lineDiscount: 10m); // 9,000 net

        q.SubTotalSar.Should().Be(9_000m);
        q.TotalSar.Should().Be(10_350m); // 9000 * 1.15
    }

    [Fact]
    public void Quote_level_discount_applies_on_subtotal_before_vat()
    {
        var q = NewDraft(discountPercent: 20m);
        AddRentalLine(q, unitPrice: 1000m, qty: 10); // 10,000 subtotal

        q.SubTotalSar.Should().Be(10_000m);
        q.VatSar.Should().Be(1_200m);       // (10000 - 20%) * 15%
        q.TotalSar.Should().Be(9_200m);     // 8000 + 1200
    }

    [Fact]
    public void AddLine_after_submit_throws()
    {
        var q = NewDraft();
        AddRentalLine(q, 1000m);
        q.SubmitForApproval([Tier(1, 0m)], Now);

        var act = () => AddRentalLine(q, 500m);
        act.Should().Throw<InvalidOperationException>();
    }

    // ─── Submit ─────────────────────────────────────────────────────────────

    [Fact]
    public void Submit_with_no_lines_throws()
    {
        var q = NewDraft();
        var act = () => q.SubmitForApproval([Tier(1, 0m)], Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Submit_with_tiers_goes_PendingApproval_snapshots_rows_and_raises_event()
    {
        var q = NewDraft();
        AddRentalLine(q, 30_000m); // total 34,500

        q.SubmitForApproval([Tier(2, 50_000m), Tier(1, 0m)], Now);

        q.Status.Should().Be(QuotationStatus.PendingApproval);
        q.Approvals.Select(a => a.TierLevel).Should().Equal((byte)1, (byte)2);
        q.Approvals.Should().OnlyContain(a => a.Status == QuotationApprovalStatus.Pending);
        q.Approvals.First().RequiredRoleCode.Should().Be("ROLE_T1"); // snapshotted
        var evt = q.DomainEvents.OfType<QuotationSubmittedForApprovalDomainEvent>().Single();
        evt.FirstTierLevel.Should().Be(1);
        evt.TotalSar.Should().Be(q.TotalSar);
    }

    [Fact]
    public void Submit_with_no_required_tiers_auto_approves_and_raises_approved_event()
    {
        var q = NewDraft();
        AddRentalLine(q, 1000m);

        q.SubmitForApproval([], Now);

        q.Status.Should().Be(QuotationStatus.Approved);
        q.ApprovedAtUtc.Should().Be(Now);
        q.Approvals.Should().BeEmpty();
        q.DomainEvents.OfType<QuotationApprovedDomainEvent>().Should().ContainSingle();
    }

    // ─── Approval chain ─────────────────────────────────────────────────────

    private static Quotation PendingTwoTier()
    {
        var q = NewDraft();
        AddRentalLine(q, 100_000m);
        q.SubmitForApproval([Tier(1, 0m), Tier(2, 50_000m)], Now);
        return q;
    }

    [Fact]
    public void Approving_all_tiers_in_order_reaches_Approved()
    {
        var q = PendingTwoTier();

        q.RecordApproval(1, approved: true, ApproverId, "ok", Now);
        q.Status.Should().Be(QuotationStatus.PendingApproval); // tier 2 still pending

        q.RecordApproval(2, approved: true, ApproverId, "ok", Now);
        q.Status.Should().Be(QuotationStatus.Approved);
        q.DomainEvents.OfType<QuotationApprovedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Approving_tier_2_before_tier_1_throws()
    {
        var q = PendingTwoTier();

        var act = () => q.RecordApproval(2, approved: true, ApproverId, null, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Any_tier_rejection_rejects_the_quote()
    {
        var q = PendingTwoTier();

        q.RecordApproval(1, approved: false, ApproverId, "too risky", Now);

        q.Status.Should().Be(QuotationStatus.Rejected);
        q.Approvals.Single(a => a.TierLevel == 1).Status.Should().Be(QuotationApprovalStatus.Rejected);
    }

    [Fact]
    public void Re_approving_a_settled_tier_is_idempotent()
    {
        var q = PendingTwoTier();
        q.RecordApproval(1, approved: true, ApproverId, "ok", Now);

        var act = () => q.RecordApproval(1, approved: true, ApproverId, "ok again", Now);

        act.Should().NotThrow();
        q.Status.Should().Be(QuotationStatus.PendingApproval);
    }

    [Fact]
    public void RecordApproval_when_not_pending_throws()
    {
        var q = NewDraft();
        AddRentalLine(q, 1000m);
        q.SubmitForApproval([], Now); // auto-approved

        var act = () => q.RecordApproval(1, approved: true, ApproverId, null, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    // ─── Recall ─────────────────────────────────────────────────────────────

    [Fact]
    public void Recall_before_any_approval_withdraws_and_recalls_pending_rows()
    {
        var q = PendingTwoTier();

        q.Recall(Now);

        q.Status.Should().Be(QuotationStatus.Withdrawn);
        q.Approvals.Should().OnlyContain(a => a.Status == QuotationApprovalStatus.Recalled);
    }

    [Fact]
    public void Recall_after_a_tier_approved_throws()
    {
        var q = PendingTwoTier();
        q.RecordApproval(1, approved: true, ApproverId, "ok", Now);

        var act = () => q.Recall(Now);
        act.Should().Throw<InvalidOperationException>();
    }

    // ─── Send / accept / reject / expire ────────────────────────────────────

    private static Quotation Approved()
    {
        var q = NewDraft();
        AddRentalLine(q, 1000m);
        q.SubmitForApproval([], Now);
        return q;
    }

    [Fact]
    public void Send_then_accept_walks_to_Accepted()
    {
        var q = Approved();

        q.MarkSentToCustomer("blob://quote.pdf", Now);
        q.Status.Should().Be(QuotationStatus.SentToCustomer);
        q.PdfBlobUri.Should().Be("blob://quote.pdf");

        q.Accept("sig", Now);
        q.Status.Should().Be(QuotationStatus.Accepted);
        q.AcceptedByCustomerSignature.Should().Be("sig");
    }

    [Fact]
    public void Cannot_send_before_approved()
    {
        var q = NewDraft();
        AddRentalLine(q, 1000m);

        var act = () => q.MarkSentToCustomer(null, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Accept_is_idempotent()
    {
        var q = Approved();
        q.MarkSentToCustomer(null, Now);
        q.Accept("sig", Now);

        var act = () => q.Accept("sig", Now);
        act.Should().NotThrow();
        q.Status.Should().Be(QuotationStatus.Accepted);
    }

    [Fact]
    public void Expire_only_from_SentToCustomer()
    {
        var sent = Approved();
        sent.MarkSentToCustomer(null, Now);
        sent.MarkExpired(Now);
        sent.Status.Should().Be(QuotationStatus.Expired);

        var draft = NewDraft();
        var act = () => draft.MarkExpired(Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Customer_reject_from_sent_is_terminal()
    {
        var q = Approved();
        q.MarkSentToCustomer(null, Now);

        q.RejectByCustomer(Now);

        q.Status.Should().Be(QuotationStatus.Rejected);
    }
}
