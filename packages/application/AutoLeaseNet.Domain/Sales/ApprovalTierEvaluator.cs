namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Pure function (Spec 08 §3): given a quote total and the tenant's configured tiers,
/// returns the ordered list of tiers whose threshold the total meets — i.e. the tiers
/// that must approve, lowest level first. Inactive tiers are ignored.
///
/// <para>
/// The result is what <see cref="Quotation.SubmitForApproval"/> snapshots into
/// <see cref="QuotationApproval"/> rows. Lives in the domain (not an adapter) because
/// it is pure business logic with no I/O.
/// </para>
/// </summary>
public static class ApprovalTierEvaluator
{
    /// <summary>
    /// Required tiers for <paramref name="totalSar"/>, ordered by <see cref="ApprovalTier.TierLevel"/>.
    /// A tier is required when active and <c>totalSar &gt;= MinAmountSar</c>. Empty result
    /// means the quote clears with no approval needed.
    /// </summary>
    public static IReadOnlyList<ApprovalTier> RequiredTiers(decimal totalSar, IEnumerable<ApprovalTier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        return tiers
            .Where(t => t.IsActive && totalSar >= t.MinAmountSar)
            .OrderBy(t => t.TierLevel)
            .ToList();
    }
}
