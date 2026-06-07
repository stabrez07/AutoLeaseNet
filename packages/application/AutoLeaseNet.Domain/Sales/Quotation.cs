using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Sales Quotation aggregate root (Spec 01 §5.4 fields, Spec 02 §4.1 state machine).
/// Owns its <see cref="QuotationLine"/> and <see cref="QuotationApproval"/> children —
/// they are created and mutated only through this root.
///
/// <para>
/// Lifecycle: <see cref="QuotationStatus.Draft"/> →
/// <see cref="QuotationStatus.PendingApproval"/> | <see cref="QuotationStatus.Approved"/> →
/// <see cref="QuotationStatus.SentToCustomer"/> →
/// <see cref="QuotationStatus.Accepted"/> (triggers lease provisioning); plus the terminal
/// branches <see cref="QuotationStatus.Rejected"/> / <see cref="QuotationStatus.Expired"/> /
/// <see cref="QuotationStatus.Withdrawn"/>.
/// </para>
///
/// <para>
/// Pricing (recomputed on every line mutation while Draft):
/// <c>SubTotalSar = Σ line totals</c>; quote-level <see cref="DiscountPercent"/> applied on
/// the subtotal; <c>VatSar = (SubTotal − discount) × 15%</c> (<see cref="VatRate"/>, KSA
/// standard rate — config in Phase 2); <c>TotalSar = taxable base + VAT</c>.
/// </para>
/// </summary>
public sealed class Quotation : Entity
{
    /// <summary>KSA standard VAT rate. Const for Phase 1; moves to per-tenant config in Phase 2.</summary>
    public const decimal VatRate = 0.15m;

    private readonly List<QuotationLine> _lines = new();
    private readonly List<QuotationApproval> _approvals = new();

    // ─── Identity / references ──────────────────────────────────────────────
    public string QuoteNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid AccountManagerId { get; private set; }

    // ─── Terms ──────────────────────────────────────────────────────────────
    public QuotationStatus Status { get; private set; }
    public DateOnly QuoteDate { get; private set; }
    public DateOnly ValidUntilDate { get; private set; }
    public QuotationContractType ContractType { get; private set; }
    public int EstimatedDurationMonths { get; private set; }
    public string? TermsAndConditionsMd { get; private set; }

    // ─── Pricing (computed) ─────────────────────────────────────────────────
    public decimal SubTotalSar { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal VatSar { get; private set; }
    public decimal TotalSar { get; private set; }

    // ─── Lifecycle stamps ───────────────────────────────────────────────────
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public string? PdfBlobUri { get; private set; }
    public string? AcceptedByCustomerSignature { get; private set; }

    public IReadOnlyCollection<QuotationLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<QuotationApproval> Approvals => _approvals.AsReadOnly();

    private Quotation() { }

    public static Quotation CreateDraft(CreateQuotationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        if (input.CustomerId == Guid.Empty) throw new ArgumentException("CustomerId required.", nameof(input));
        if (input.AccountManagerId == Guid.Empty) throw new ArgumentException("AccountManagerId required.", nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.QuoteNumber);
        if (input.DiscountPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(input), "DiscountPercent must be 0–100.");
        if (input.EstimatedDurationMonths < 0)
            throw new ArgumentOutOfRangeException(nameof(input), "EstimatedDurationMonths cannot be negative.");
        if (input.ValidUntilDate < input.QuoteDate)
            throw new ArgumentException("ValidUntilDate cannot precede QuoteDate.", nameof(input));

        var quotation = new Quotation
        {
            TenantId = input.TenantId,
            QuoteNumber = input.QuoteNumber,
            CustomerId = input.CustomerId,
            AccountManagerId = input.AccountManagerId,
            Status = QuotationStatus.Draft,
            QuoteDate = input.QuoteDate,
            ValidUntilDate = input.ValidUntilDate,
            ContractType = input.ContractType,
            EstimatedDurationMonths = input.EstimatedDurationMonths,
            TermsAndConditionsMd = input.TermsAndConditionsMd,
            DiscountPercent = input.DiscountPercent,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
        quotation.Recompute();
        return quotation;
    }

    /// <summary>Add a priced line. Draft-only; recomputes pricing. LineNumber auto-assigned.</summary>
    public QuotationLine AddLine(AddQuotationLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureDraft(nameof(AddLine));

        var line = QuotationLine.Create(
            TenantId, Id,
            lineNumber: _lines.Count + 1,
            input.ItemType, input.Description, input.VehicleSpecRef,
            input.Quantity, input.UnitPriceSar, input.DiscountPercent,
            input.NowUtc);

        _lines.Add(line);
        Recompute();
        UpdatedAtUtc = input.NowUtc;
        return line;
    }

    /// <summary>Adjust the quote-level discount. Draft-only; recomputes pricing.</summary>
    public void SetDiscountPercent(decimal discountPercent, DateTimeOffset nowUtc)
    {
        EnsureDraft(nameof(SetDiscountPercent));
        if (discountPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(discountPercent), discountPercent, "DiscountPercent must be 0–100.");
        DiscountPercent = discountPercent;
        Recompute();
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Submit for approval. <paramref name="requiredTiers"/> is the resolved tier set from
    /// <see cref="ApprovalTierEvaluator"/> (computed in the app layer against current config,
    /// then snapshotted here so later edits don't touch this quote). With one or more tiers →
    /// <see cref="QuotationStatus.PendingApproval"/> + <see cref="QuotationSubmittedForApprovalDomainEvent"/>;
    /// with none → auto-<see cref="QuotationStatus.Approved"/> + <see cref="QuotationApprovedDomainEvent"/>.
    /// </summary>
    public void SubmitForApproval(IReadOnlyList<ApprovalTier> requiredTiers, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(requiredTiers);
        EnsureDraft(nameof(SubmitForApproval));
        if (_lines.Count == 0 || TotalSar <= 0m)
            throw new InvalidOperationException($"Quotation {Id} cannot be submitted with no priced lines.");

        SubmittedAtUtc = nowUtc;

        if (requiredTiers.Count == 0)
        {
            MarkApproved(nowUtc);
            return;
        }

        foreach (var tier in requiredTiers.OrderBy(t => t.TierLevel))
            _approvals.Add(QuotationApproval.Snapshot(TenantId, Id, tier, nowUtc));

        Status = QuotationStatus.PendingApproval;
        UpdatedAtUtc = nowUtc;
        RaiseDomainEvent(new QuotationSubmittedForApprovalDomainEvent(
            QuotationId: Id,
            TenantId: TenantId,
            CustomerId: CustomerId,
            TotalSar: TotalSar,
            FirstTierLevel: _approvals.Min(a => a.TierLevel),
            SubmittedAtUtc: nowUtc));
    }

    /// <summary>
    /// Record a tier decision (Spec 02 §6.1). Tiers decide in order — the target must be the
    /// lowest-level still-Pending tier. Approving the last tier flips the quote to Approved;
    /// any rejection flips it to Rejected (terminal). Idempotent on re-decision of the same row.
    /// Role/assignment authorisation is enforced one layer up (current DB role state, Spec 08 §11).
    /// </summary>
    public void RecordApproval(byte tierLevel, bool approved, Guid decidedByUserId, string? comment, DateTimeOffset nowUtc)
    {
        if (Status != QuotationStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Quotation {Id} is {Status}; approvals only apply while PendingApproval.");

        var target = _approvals.SingleOrDefault(a => a.TierLevel == tierLevel)
            ?? throw new InvalidOperationException($"Quotation {Id} has no approval tier {tierLevel}.");

        // Re-decision of an already-settled row is idempotent (webhook/retry safety).
        var alreadySettled = target.Status is QuotationApprovalStatus.Approved or QuotationApprovalStatus.Rejected;

        if (!alreadySettled)
        {
            var lowestPending = _approvals
                .Where(a => a.Status == QuotationApprovalStatus.Pending)
                .Min(a => a.TierLevel);
            if (tierLevel != lowestPending)
                throw new InvalidOperationException(
                    $"Tier {tierLevel} cannot decide before tier {lowestPending} on Quotation {Id}.");
        }

        if (approved)
        {
            target.Approve(decidedByUserId, comment, nowUtc);
            if (_approvals.All(a => a.Status == QuotationApprovalStatus.Approved))
                MarkApproved(nowUtc);
        }
        else
        {
            target.Reject(decidedByUserId, comment, nowUtc);
            Status = QuotationStatus.Rejected;
            ClosedAtUtc = nowUtc;
        }

        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Sales rep recalls the quote (Spec 02 §4.1). Allowed from Draft, Approved, SentToCustomer,
    /// or PendingApproval <b>only while no tier has approved yet</b>. Outstanding Pending
    /// approvals flip to Recalled.
    /// </summary>
    public void Recall(DateTimeOffset nowUtc)
    {
        if (Status == QuotationStatus.Withdrawn) return; // idempotent

        var recallable = Status is QuotationStatus.Draft
            or QuotationStatus.PendingApproval
            or QuotationStatus.Approved
            or QuotationStatus.SentToCustomer;
        if (!recallable)
            throw new InvalidOperationException($"Quotation {Id} cannot be recalled from {Status}.");

        if (Status == QuotationStatus.PendingApproval
            && _approvals.Any(a => a.Status == QuotationApprovalStatus.Approved))
            throw new InvalidOperationException(
                $"Quotation {Id} cannot be recalled: a tier has already approved.");

        foreach (var approval in _approvals)
            approval.Recall(nowUtc);

        Status = QuotationStatus.Withdrawn;
        ClosedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Mark the approved quote as sent to the customer. Optionally records the PDF URI.</summary>
    public void MarkSentToCustomer(string? pdfBlobUri, DateTimeOffset nowUtc)
    {
        if (Status == QuotationStatus.SentToCustomer) return; // idempotent
        if (Status != QuotationStatus.Approved)
            throw new InvalidOperationException($"Quotation {Id} must be Approved to send; it is {Status}.");
        Status = QuotationStatus.SentToCustomer;
        SentAtUtc = nowUtc;
        if (pdfBlobUri is not null) PdfBlobUri = pdfBlobUri;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Customer accepts (Spec 02 §4.1) — terminal; triggers lease provisioning downstream.</summary>
    public void Accept(string? customerSignature, DateTimeOffset nowUtc)
    {
        if (Status == QuotationStatus.Accepted) return; // idempotent
        if (Status != QuotationStatus.SentToCustomer)
            throw new InvalidOperationException($"Quotation {Id} must be SentToCustomer to accept; it is {Status}.");
        Status = QuotationStatus.Accepted;
        AcceptedAtUtc = nowUtc;
        AcceptedByCustomerSignature = customerSignature;
        ClosedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Customer rejects a sent quote. Terminal.</summary>
    public void RejectByCustomer(DateTimeOffset nowUtc)
    {
        if (Status == QuotationStatus.Rejected) return; // idempotent
        if (Status != QuotationStatus.SentToCustomer)
            throw new InvalidOperationException($"Quotation {Id} must be SentToCustomer to reject; it is {Status}.");
        Status = QuotationStatus.Rejected;
        ClosedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Daily expiry job (Spec 02 §4.1): a sent, un-accepted quote past ValidUntilDate expires.</summary>
    public void MarkExpired(DateTimeOffset nowUtc)
    {
        if (Status == QuotationStatus.Expired) return; // idempotent
        if (Status != QuotationStatus.SentToCustomer)
            throw new InvalidOperationException($"Quotation {Id} can only expire from SentToCustomer; it is {Status}.");
        Status = QuotationStatus.Expired;
        ClosedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    private void MarkApproved(DateTimeOffset nowUtc)
    {
        Status = QuotationStatus.Approved;
        ApprovedAtUtc = nowUtc;
        RaiseDomainEvent(new QuotationApprovedDomainEvent(
            QuotationId: Id,
            TenantId: TenantId,
            CustomerId: CustomerId,
            TotalSar: TotalSar,
            ApprovedAtUtc: nowUtc));
    }

    private void Recompute()
    {
        SubTotalSar = Math.Round(_lines.Sum(l => l.LineTotalSar), 2, MidpointRounding.AwayFromZero);
        var taxable = Math.Round(SubTotalSar * (1 - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
        VatSar = Math.Round(taxable * VatRate, 2, MidpointRounding.AwayFromZero);
        TotalSar = taxable + VatSar;
    }

    private void EnsureDraft(string operation)
    {
        if (Status != QuotationStatus.Draft)
            throw new InvalidOperationException($"{operation} requires Draft status; Quotation {Id} is {Status}.");
    }
}

/// <summary>Constructor input for <see cref="Quotation.CreateDraft"/>.</summary>
public sealed record CreateQuotationInput
{
    public required Guid TenantId { get; init; }
    public required string QuoteNumber { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid AccountManagerId { get; init; }
    public required DateOnly QuoteDate { get; init; }
    public required DateOnly ValidUntilDate { get; init; }
    public required QuotationContractType ContractType { get; init; }
    public int EstimatedDurationMonths { get; init; }
    public decimal DiscountPercent { get; init; }
    public string? TermsAndConditionsMd { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}

/// <summary>Constructor input for <see cref="Quotation.AddLine"/>.</summary>
public sealed record AddQuotationLineInput
{
    public required QuotationItemType ItemType { get; init; }
    public required string Description { get; init; }
    public string? VehicleSpecRef { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPriceSar { get; init; }
    public decimal DiscountPercent { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}
