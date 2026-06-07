using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// One tier in a quotation's approval chain (Spec 01 §5.4, Spec 02 §6.1). Snapshotted from
/// <see cref="ApprovalTier"/> at submit time so later config edits don't affect in-flight
/// quotes (<see cref="RequiredRoleCode"/> is frozen here). Owned by, and decided through,
/// the <see cref="Quotation"/> root — callers don't mutate it directly.
/// </summary>
public sealed class QuotationApproval : Entity
{
    public Guid QuotationId { get; private set; }
    public byte TierLevel { get; private set; }

    /// <summary>Role frozen at submit time; the decider must currently hold it (checked one layer up).</summary>
    public string RequiredRoleCode { get; private set; } = string.Empty;

    /// <summary>Set when a specific approver is delegated; otherwise any holder of the role may decide.</summary>
    public Guid? AssignedUserId { get; private set; }

    public QuotationApprovalStatus Status { get; private set; }
    public DateTimeOffset? DecisionAtUtc { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string? Comment { get; private set; }

    private QuotationApproval() { }

    internal static QuotationApproval Snapshot(
        Guid tenantId,
        Guid quotationId,
        ApprovalTier tier,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(tier);
        return new QuotationApproval
        {
            TenantId = tenantId,
            QuotationId = quotationId,
            TierLevel = tier.TierLevel,
            RequiredRoleCode = tier.RequiredRoleCode,
            Status = QuotationApprovalStatus.Pending,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    internal void Approve(Guid decidedByUserId, string? comment, DateTimeOffset nowUtc)
    {
        if (Status == QuotationApprovalStatus.Approved) return; // idempotent
        EnsurePending();
        if (decidedByUserId == Guid.Empty)
            throw new ArgumentException("DecidedByUserId required.", nameof(decidedByUserId));
        Status = QuotationApprovalStatus.Approved;
        DecidedByUserId = decidedByUserId;
        Comment = comment;
        DecisionAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    internal void Reject(Guid decidedByUserId, string? comment, DateTimeOffset nowUtc)
    {
        if (Status == QuotationApprovalStatus.Rejected) return; // idempotent
        EnsurePending();
        if (decidedByUserId == Guid.Empty)
            throw new ArgumentException("DecidedByUserId required.", nameof(decidedByUserId));
        Status = QuotationApprovalStatus.Rejected;
        DecidedByUserId = decidedByUserId;
        Comment = comment;
        DecisionAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    internal void Recall(DateTimeOffset nowUtc)
    {
        if (Status != QuotationApprovalStatus.Pending) return; // only pending rows recall
        Status = QuotationApprovalStatus.Recalled;
        UpdatedAtUtc = nowUtc;
    }

    private void EnsurePending()
    {
        if (Status != QuotationApprovalStatus.Pending)
            throw new InvalidOperationException(
                $"Approval tier {TierLevel} is already {Status}; cannot decide again.");
    }
}
