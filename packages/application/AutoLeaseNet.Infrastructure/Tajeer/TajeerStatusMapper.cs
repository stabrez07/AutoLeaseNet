using AutoLeaseNet.Domain.Leases;

namespace AutoLeaseNet.Infrastructure.Tajeer;

/// <summary>
/// Canonical mapping from Tajeer's <c>contractStatusCode</c> (+ optional suspension /
/// closure reasons) to our local <see cref="LeaseStatus"/>. Per Spec 03 §7.2; Spec 03
/// §1 principle #10 makes this mapper authoritative — inline switches anywhere else are
/// a bug.
/// <para>
/// Tajeer is system of record (CLAUDE.md §5). Local-only refinements (today: promoting
/// Active → Extended when local has &gt;0 extensions) layer on top via
/// <see cref="ApplyLocalRefinements"/>.
/// </para>
/// <para>
/// Lives in Infrastructure rather than the Tajeer adapter because adapters are kept
/// Domain-free; the vendor enum lives in the adapter's <c>GetContractResponse</c>, the
/// translation to a Domain concept lives one layer up.
/// </para>
/// </summary>
public static class TajeerStatusMapper
{
    /// <summary>
    /// Maps the (status, suspension, closure) triple Tajeer surfaces via §6.3 GetContract
    /// (and the same fields on Close/Suspend responses) to <see cref="LeaseStatus"/>.
    /// Throws <see cref="InvalidTajeerStatusException"/> on unknown combinations so the
    /// reconciliation cycle logs the surprising vendor state rather than silently mapping
    /// it to the wrong local status.
    /// </summary>
    public static LeaseStatus FromTajeer(int contractStatusCode, int? suspensionReasonCode, int? closureReasonCode)
    {
        // Permissive on the read side: Tajeer's Suspended/Closed responses may carry a
        // companion reason or may not (depending on the path that drove the transition).
        // We accept either — the reason fields are observed-but-not-required for the mapping.
        return (contractStatusCode, suspensionReasonCode, closureReasonCode) switch
        {
            (1, null, null) => LeaseStatus.PendingIssuance,
            (4, null, null) => LeaseStatus.Active,
            (3, _, null)    => LeaseStatus.Suspended,
            (2, _, _)       => LeaseStatus.Closed,
            (5, _, _)       => LeaseStatus.Cancelled,
            _ => throw new InvalidTajeerStatusException(contractStatusCode, suspensionReasonCode, closureReasonCode),
        };
    }

    /// <summary>
    /// Applies local-only refinements to a Tajeer-derived status. Today only one:
    /// Tajeer keeps an extended contract at <c>contractStatusCode = 4</c> (Issued); we
    /// surface our finer-grained <see cref="LeaseStatus.Extended"/> when the local
    /// extension counter is non-zero.
    /// </summary>
    public static LeaseStatus ApplyLocalRefinements(LeaseStatus tajeerStatus, int localExtensionCount)
    {
        if (tajeerStatus == LeaseStatus.Active && localExtensionCount > 0)
            return LeaseStatus.Extended;
        return tajeerStatus;
    }
}
