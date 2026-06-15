using AutoLeaseNet.Application.Ports.Integrations;

namespace AutoLeaseNet.Adapters.Zatca.InMemory;

/// <summary>
/// InMemory ZATCA client for development and testing.
/// Returns mock transaction IDs + immediate CLEARED status (happy path).
/// Useful for local dev and integration tests without hitting sandbox.
/// </summary>
public sealed class ZatcaClientInMemory : IZatcaClient
{
    private readonly Dictionary<string, ZatcaSubmissionResult> _submissions = [];

    /// <summary>Simulate ZATCA submission. Always returns success with generated transaction ID.</summary>
    public Task<ZatcaSubmissionResult> SubmitInvoiceAsync(string signedUbl, string invoiceHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedUbl);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceHash);

        var transactionId = $"MOCK-{Guid.NewGuid():N}"[..20];
        var qrCode = $"https://zatca.gov.sa/invoice/{transactionId}";

        var result = new ZatcaSubmissionResult(
            Success: true,
            TransactionId: transactionId,
            ReportingStatus: "CLEARED",
            QrCode: qrCode,
            ErrorMessage: null,
            ReceivedAtUtc: DateTime.UtcNow);

        _submissions[transactionId] = result;

        return Task.FromResult(result);
    }

    /// <summary>Simulate clearance polling. Looks up transaction and returns stored result (Phase 2).</summary>
    public Task<ZatcaClearanceResult> PollClearanceStatusAsync(string zatcaTransactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zatcaTransactionId);

        if (_submissions.TryGetValue(zatcaTransactionId, out var submission))
        {
            var result = new ZatcaClearanceResult(
                Success: submission.Success,
                ReportingStatus: submission.ReportingStatus,
                QrCode: submission.QrCode,
                ErrorMessage: null,
                ReceivedAtUtc: DateTime.UtcNow);

            return Task.FromResult(result);
        }

        // Transaction not found
        return Task.FromResult(new ZatcaClearanceResult(
            Success: false,
            ReportingStatus: null,
            QrCode: null,
            ErrorMessage: $"Transaction {zatcaTransactionId} not found",
            ReceivedAtUtc: DateTime.UtcNow));
    }
}
