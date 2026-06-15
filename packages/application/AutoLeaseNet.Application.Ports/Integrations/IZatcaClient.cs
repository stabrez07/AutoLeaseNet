namespace AutoLeaseNet.Application.Ports.Integrations;

/// <summary>
/// Port for ZATCA e-invoicing service per Spec 02 §4.5, Spec 03 §8.2.
/// Submits signed UBL invoices for clearance; receives transaction IDs + clearing status.
/// Implementations: ZatcaClient (real HTTP), ZatcaClientInMemory (test/dev).
/// </summary>
public interface IZatcaClient
{
    /// <summary>
    /// Submit signed UBL invoice for ZATCA clearance.
    /// </summary>
    /// <param name="signedUbl">UBL XML with ECDSA signature embedded.</param>
    /// <param name="invoiceHash">SHA-256 hash of canonical UBL (for verification).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ZatcaSubmissionResult with transaction ID + clearing status.</returns>
    Task<ZatcaSubmissionResult> SubmitInvoiceAsync(string signedUbl, string invoiceHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Poll clearance status for previously submitted invoice (Phase 2 feature).
    /// </summary>
    /// <param name="zatcaTransactionId">Transaction ID from initial submission.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ZatcaClearanceResult with status + timestamp.</returns>
    Task<ZatcaClearanceResult> PollClearanceStatusAsync(string zatcaTransactionId, CancellationToken cancellationToken = default);
}

/// <summary>Result from ZATCA submission endpoint.</summary>
public sealed record ZatcaSubmissionResult(
    bool Success,
    string? TransactionId,
    string? ReportingStatus,
    string? QrCode,
    string? ErrorMessage,
    DateTime ReceivedAtUtc)
{
    /// <summary>Shortcut: check if submission succeeded and invoice is cleared.</summary>
    public bool IsCleared => Success && ReportingStatus == "CLEARED";
}

/// <summary>Result from ZATCA clearance polling endpoint (Phase 2).</summary>
public sealed record ZatcaClearanceResult(
    bool Success,
    string? ReportingStatus,
    string? QrCode,
    string? ErrorMessage,
    DateTime ReceivedAtUtc);
