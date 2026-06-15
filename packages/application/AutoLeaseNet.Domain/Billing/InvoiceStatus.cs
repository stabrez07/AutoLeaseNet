namespace AutoLeaseNet.Domain.Billing;

/// <summary>
/// Invoice lifecycle state machine per Spec 02 §4.4.
/// Draft → Submitted → Cleared → Finalized (+ error states).
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Created from lease; awaiting submission to ZATCA.</summary>
    Draft = 0,

    /// <summary>Submitted to ZATCA for clearance.</summary>
    Submitted = 1,

    /// <summary>ZATCA cleared (compliant); QR code generated.</summary>
    Cleared = 2,

    /// <summary>Invoice finalized and archived (no further changes).</summary>
    Finalized = 3,

    /// <summary>Submission failed; awaiting retry or manual intervention.</summary>
    SubmissionFailed = 4,

    /// <summary>ZATCA rejected (non-compliance); must be corrected and resubmitted.</summary>
    ClearanceFailed = 5,

    /// <summary>Voided (credit memo issued or lease cancelled).</summary>
    Voided = 6,
}
