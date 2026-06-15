namespace AutoLeaseNet.Domain.Zatca;

/// <summary>
/// ZATCA submission lifecycle state machine per Spec 02 §4.5.
/// Draft → Submitted → PendingClearance → Cleared → Finalized (+ error states).
/// </summary>
public enum ZatcaSubmissionStatus
{
    /// <summary>Created; awaiting UBL builder + signing.</summary>
    Draft = 0,

    /// <summary>Submitted to ZATCA endpoint; awaiting clearance response.</summary>
    Submitted = 1,

    /// <summary>Awaiting async clearance poll (Phase 2 feature).</summary>
    PendingClearance = 2,

    /// <summary>ZATCA cleared (invoice compliant + hash verified).</summary>
    Cleared = 3,

    /// <summary>Submission finalized and archived (invoice locked).</summary>
    Finalized = 4,

    /// <summary>Submission failed (network, validation); awaiting retry.</summary>
    SubmissionFailed = 5,

    /// <summary>ZATCA rejected clearance; invoice non-compliant; manual intervention required.</summary>
    ClearanceFailed = 6,
}
