using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using AutoLeaseNet.Application.Ports.Integrations;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Adapters.Zatca;

/// <summary>
/// Real ZATCA e-invoicing client. Posts signed UBL to ZATCA Fatoorah sandbox/production.
/// Per Spec 02 §4.5, Spec 03 §8.2 — KSA regulatory requirement.
/// Phase 1: synchronous submission (happy path).
/// Phase 2: async polling + retry pipeline (Polly).
/// </summary>
public sealed class ZatcaClient : IZatcaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZatcaClient> _logger;

    public ZatcaClient(HttpClient httpClient, ILogger<ZatcaClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Submit signed UBL to ZATCA. Returns transaction ID + reporting status.</summary>
    public async Task<ZatcaSubmissionResult> SubmitInvoiceAsync(string signedUbl, string invoiceHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signedUbl);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceHash);

        try
        {
            // Prepare HTTP request (ZATCA expects XML with auth header)
            var content = new StringContent(signedUbl, Encoding.UTF8, "application/xml");
            var request = new HttpRequestMessage(HttpMethod.Post, "/clearance")
            {
                Content = content
            };

            // Phase 1: Basic auth (Phase 2: OAuth2)
            request.Headers.Add("Authorization", $"Bearer {GetAuthToken()}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMsg = $"ZATCA submission failed: HTTP {response.StatusCode}. Response: {errorContent}";
                _logger.LogError(errorMsg);
                return new ZatcaSubmissionResult(false, null, null, null, errorMsg, DateTime.UtcNow);
            }

            var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);
            var (transactionId, reportingStatus, qrCode) = ParseZatcaResponse(responseXml);

            _logger.LogInformation("ZATCA submission successful. TransactionId: {TransactionId}, Status: {ReportingStatus}", transactionId, reportingStatus);

            return new ZatcaSubmissionResult(
                Success: true,
                TransactionId: transactionId,
                ReportingStatus: reportingStatus,
                QrCode: qrCode,
                ErrorMessage: null,
                ReceivedAtUtc: DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            var errorMsg = $"ZATCA submission exception: {ex.Message}";
            _logger.LogError(ex, errorMsg);
            return new ZatcaSubmissionResult(false, null, null, null, errorMsg, DateTime.UtcNow);
        }
    }

    /// <summary>Poll clearance status (Phase 2 enhancement).</summary>
    public async Task<ZatcaClearanceResult> PollClearanceStatusAsync(string zatcaTransactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zatcaTransactionId);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/clearance/{zatcaTransactionId}");
            request.Headers.Add("Authorization", $"Bearer {GetAuthToken()}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ZatcaClearanceResult(false, null, null, errorContent, DateTime.UtcNow);
            }

            var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);
            var (_, reportingStatus, qrCode) = ParseZatcaResponse(responseXml);

            return new ZatcaClearanceResult(true, reportingStatus, qrCode, null, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZATCA polling exception for transaction {TransactionId}", zatcaTransactionId);
            return new ZatcaClearanceResult(false, null, null, ex.Message, DateTime.UtcNow);
        }
    }

    /// <summary>Parse ZATCA response XML. Extracts transaction ID, status, and QR code.</summary>
    private static (string? TransactionId, string? ReportingStatus, string? QrCode) ParseZatcaResponse(string responseXml)
    {
        try
        {
            var root = XDocument.Parse(responseXml).Root;
            if (root == null)
                return (null, null, null);

            var transactionId = root.Element("TransactionId")?.Value;
            var reportingStatus = root.Element("ReportingStatus")?.Value;
            var qrCode = root.Element("QRCode")?.Value;

            return (transactionId, reportingStatus, qrCode);
        }
        catch
        {
            return (null, null, null);
        }
    }

    /// <summary>Get auth token for ZATCA request (Phase 1: placeholder; Phase 2: OAuth2).</summary>
    private static string GetAuthToken()
    {
        // Phase 1: hardcoded token (from sandbox CSID)
        // Phase 2: fetch from OAuth2 endpoint with CSID credentials
        return "sandbox-token-placeholder";
    }
}
