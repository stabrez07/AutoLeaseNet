using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Zatca;

/// <summary>
/// ZATCA submission aggregate root per Spec 02 §4.5. One per invoice; tracks clearance state + transaction IDs.
/// Lifecycle: Create (Draft) → BuildAndSign → Submit → ReceiveClearance → Finalize.
/// </summary>
public sealed class ZatcaSubmission : Entity
{
    /// <summary>The invoice this submission is for (1:1 relationship).</summary>
    public Guid InvoiceId { get; private set; }

    /// <summary>Current submission state per Spec 02 §4.5.</summary>
    public ZatcaSubmissionStatus Status { get; private set; }

    /// <summary>Canonical UBL 2.1 XML (before signing).</summary>
    public string? UblXml { get; private set; }

    /// <summary>Signed UBL XML (ECDSA P-256 signature embedded).</summary>
    public string? SignedUblXml { get; private set; }

    /// <summary>SHA-256 hash of canonical UBL (for ZATCA submission).</summary>
    public string? InvoiceHash { get; private set; }

    /// <summary>ZATCA-assigned transaction ID (returned on successful submission).</summary>
    public string? ZatcaTransactionId { get; private set; }

    /// <summary>ZATCA-assigned reporting status (e.g., "CLEARED", "REJECTED").</summary>
    public string? ZatcaReportingStatus { get; private set; }

    /// <summary>Clearance timestamp from ZATCA response.</summary>
    public DateTimeOffset? ClearedAtUtc { get; private set; }

    /// <summary>Last error message (submission or clearance failure).</summary>
    public string? LastErrorMessage { get; private set; }

    /// <summary>Submission attempt count (for retry logic).</summary>
    public int SubmissionAttempts { get; private set; }

    private ZatcaSubmission() { }

    /// <summary>Factory: create draft submission for invoice. Pending UBL + signing.</summary>
    public static ZatcaSubmission CreateForInvoice(Guid tenantId, Guid invoiceId)
    {
        if (invoiceId == Guid.Empty)
            throw new ArgumentException("Invoice ID cannot be empty.", nameof(invoiceId));

        return new ZatcaSubmission
        {
            TenantId = tenantId,
            InvoiceId = invoiceId,
            Status = ZatcaSubmissionStatus.Draft,
            SubmissionAttempts = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Record UBL + signing completion. State: Draft → Submitted (ready to POST).</summary>
    public void MarkBuiltAndSigned(string ublXml, string signedUbl, string invoiceHash, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ublXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(signedUbl);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceHash);

        if (Status != ZatcaSubmissionStatus.Draft)
            throw new InvalidOperationException($"Cannot build+sign submission with status {Status}.");

        UblXml = ublXml;
        SignedUblXml = signedUbl;
        InvoiceHash = invoiceHash;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Record submission to ZATCA endpoint (before clearance response).</summary>
    public void MarkSubmitted(DateTimeOffset nowUtc)
    {
        if (Status != ZatcaSubmissionStatus.Draft && Status != ZatcaSubmissionStatus.SubmissionFailed)
            throw new InvalidOperationException($"Cannot submit with status {Status}.");

        Status = ZatcaSubmissionStatus.Submitted;
        SubmissionAttempts++;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Record ZATCA clearance success. State: Submitted → Cleared.</summary>
    public void MarkCleared(string zatcaTransactionId, string reportingStatus, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zatcaTransactionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportingStatus);

        if (Status != ZatcaSubmissionStatus.Submitted && Status != ZatcaSubmissionStatus.PendingClearance)
            throw new InvalidOperationException($"Cannot clear submission with status {Status}.");

        Status = ZatcaSubmissionStatus.Cleared;
        ZatcaTransactionId = zatcaTransactionId;
        ZatcaReportingStatus = reportingStatus;
        ClearedAtUtc = nowUtc;
        LastErrorMessage = null;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Record submission or clearance failure. Awaiting retry or manual intervention.</summary>
    public void MarkFailed(bool isSubmissionPhase, string errorMessage, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        Status = isSubmissionPhase ? ZatcaSubmissionStatus.SubmissionFailed : ZatcaSubmissionStatus.ClearanceFailed;
        LastErrorMessage = errorMessage;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Mark submission as finalized (no further changes allowed).</summary>
    public void MarkFinalized(DateTimeOffset nowUtc)
    {
        if (Status != ZatcaSubmissionStatus.Cleared)
            throw new InvalidOperationException($"Can only finalize cleared submissions; current status: {Status}.");

        Status = ZatcaSubmissionStatus.Finalized;
        UpdatedAtUtc = nowUtc;
    }
}
