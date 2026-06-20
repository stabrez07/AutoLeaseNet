using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Per-tenant approval-tier configuration (Spec 08, Spec 01 §5.4). A quotation whose
/// <see cref="Quotation.TotalSar"/> is at or above <see cref="MinAmountSar"/> requires
/// the role named by <see cref="RequiredRoleCode"/> to approve at <see cref="TierLevel"/>.
///
/// <para>
/// Thresholds are DB-config-driven and editable via admin endpoints — never hardcode
/// them in code (CLAUDE.md anti-pattern). The evaluator (<see cref="ApprovalTierEvaluator"/>)
/// reads these rows; the <see cref="Quotation"/> aggregate stays config-free and is handed
/// the resolved tier set at submit time so the snapshot is immune to later config edits.
/// </para>
/// </summary>
public sealed class ApprovalTier : Entity
{
    /// <summary>1 through 5 — the order in which this tier must approve.</summary>
    public byte TierLevel { get; private set; }

    /// <summary>Role required to decide this tier (snapshotted onto the approval row at submit).</summary>
    public string RequiredRoleCode { get; private set; } = string.Empty;

    /// <summary>Inclusive lower bound: quotes with <c>TotalSar &gt;= MinAmountSar</c> need this tier.</summary>
    public decimal MinAmountSar { get; private set; }

    /// <summary>Soft-disable a tier without deleting history; inactive tiers are never required.</summary>
    public bool IsActive { get; private set; } = true;

    private ApprovalTier() { }

    public static ApprovalTier Create(
        Guid tenantId,
        byte tierLevel,
        string requiredRoleCode,
        decimal minAmountSar,
        DateTimeOffset nowUtc,
        bool isActive = true)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        if (tierLevel is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(tierLevel), tierLevel, "TierLevel must be between 1 and 5.");
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredRoleCode);
        if (minAmountSar < 0)
            throw new ArgumentOutOfRangeException(nameof(minAmountSar), minAmountSar, "MinAmountSar cannot be negative.");

        return new ApprovalTier
        {
            TenantId = tenantId,
            TierLevel = tierLevel,
            RequiredRoleCode = requiredRoleCode,
            MinAmountSar = minAmountSar,
            IsActive = isActive,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }
}
