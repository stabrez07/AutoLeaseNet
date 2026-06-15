using System.Security.Cryptography;
using System.Text;
using AutoLeaseNet.Application.Ports.Integrations;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Zatca;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Application.Billing;

/// <summary>
/// Command: Submit invoice to ZATCA for clearance.
/// Workflow: (1) Build UBL, (2) Sign XML, (3) Submit to ZATCA, (4) Update submission state.
/// Per Spec 02 §4.5, Spec 03 §8.2.
/// </summary>
public sealed record SubmitInvoiceToZatcaCommand(
    Guid TenantId,
    Guid InvoiceId) : IRequest<SubmitInvoiceToZatcaResult>;

/// <summary>Result of ZATCA submission.</summary>
public sealed record SubmitInvoiceToZatcaResult(
    bool Success,
    string? Message,
    string? TransactionId);

/// <summary>
/// Orchestrates ZATCA submission: UBL builder → signer → HTTP submission → state update.
/// Idempotent via Redis cache (key: `tenant:{tenantId:N}:zatca-submit:{invoiceId:N}`, TTL: 24h).
/// </summary>
public sealed class SubmitInvoiceToZatcaCommandHandler : IRequestHandler<SubmitInvoiceToZatcaCommand, SubmitInvoiceToZatcaResult>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IZatcaSubmissionRepository _submissionRepository;
    private readonly IZatcaClient _zatcaClient;
    private readonly IDistributedCache _cache;
    private readonly IClock _clock;
    private readonly ILogger<SubmitInvoiceToZatcaCommandHandler> _logger;

    public SubmitInvoiceToZatcaCommandHandler(
        IInvoiceRepository invoiceRepository,
        IZatcaSubmissionRepository submissionRepository,
        IZatcaClient zatcaClient,
        IDistributedCache cache,
        IClock clock,
        ILogger<SubmitInvoiceToZatcaCommandHandler> logger)
    {
        _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
        _submissionRepository = submissionRepository ?? throw new ArgumentNullException(nameof(submissionRepository));
        _zatcaClient = zatcaClient ?? throw new ArgumentNullException(nameof(zatcaClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SubmitInvoiceToZatcaResult> Handle(SubmitInvoiceToZatcaCommand request, CancellationToken cancellationToken)
    {
        var idempotencyKey = $"tenant:{request.TenantId:N}:zatca-submit:{request.InvoiceId:N}";

        // Check idempotency cache
        var cachedResult = await _cache.GetStringAsync(idempotencyKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedResult))
        {
            _logger.LogInformation("ZATCA submission idempotent cache hit. InvoiceId: {InvoiceId}", request.InvoiceId);
            return DeserializeResult(cachedResult);
        }

        try
        {
            // Retrieve invoice
            var invoice = await _invoiceRepository.GetByIdAsync(request.TenantId, request.InvoiceId, cancellationToken);
            if (invoice == null)
                return FailResult("Invoice not found.", idempotencyKey);

            // Check or create submission
            var submission = await _submissionRepository.GetByInvoiceIdAsync(request.InvoiceId, cancellationToken);
            if (submission == null)
            {
                submission = ZatcaSubmission.CreateForInvoice(request.TenantId, request.InvoiceId);
                await _submissionRepository.CreateAsync(submission, cancellationToken);
            }

            // Build UBL
            var invoiceHash = ComputeInvoiceHash(invoice);
            var ublXml = UblXmlBuilder.BuildUblXml(invoice, invoiceHash);

            // Phase 1: Skip actual signing; use UBL as-is for submission
            // Phase 2: Inject IEcdsaSigner from infrastructure to sign the UBL
            var signedUblXml = ublXml;

            // Update submission with built/signed data
            submission.MarkBuiltAndSigned(ublXml, signedUblXml, invoiceHash, _clock.UtcNow);
            submission.MarkSubmitted(_clock.UtcNow);
            await _submissionRepository.UpdateAsync(submission, cancellationToken);

            // Submit to ZATCA
            var zatcaResult = await _zatcaClient.SubmitInvoiceAsync(signedUblXml, invoiceHash, cancellationToken);

            if (!zatcaResult.Success)
            {
                submission.MarkFailed(isSubmissionPhase: true, zatcaResult.ErrorMessage ?? "Unknown error", _clock.UtcNow);
                await _submissionRepository.UpdateAsync(submission, cancellationToken);
                _logger.LogError("ZATCA submission failed. InvoiceId: {InvoiceId}, Error: {Error}", request.InvoiceId, zatcaResult.ErrorMessage);
                return FailResult(zatcaResult.ErrorMessage ?? "ZATCA submission failed.", idempotencyKey);
            }

            // ZATCA accepted; update submission
            submission.MarkCleared(zatcaResult.TransactionId ?? "UNKNOWN", zatcaResult.ReportingStatus ?? "CLEARED", _clock.UtcNow);
            await _submissionRepository.UpdateAsync(submission, cancellationToken);

            // Mark invoice as submitted (Phase 1: defer clearance finalization to Phase 2 polling)
            invoice.MarkSubmitted(_clock.UtcNow);
            await _invoiceRepository.UpdateAsync(invoice, cancellationToken);

            var result = new SubmitInvoiceToZatcaResult(
                Success: true,
                Message: "Invoice submitted to ZATCA successfully.",
                TransactionId: zatcaResult.TransactionId);

            // Cache result for idempotency
            await _cache.SetStringAsync(idempotencyKey, SerializeResult(result), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            }, cancellationToken);

            _logger.LogInformation("ZATCA submission successful. InvoiceId: {InvoiceId}, TransactionId: {TransactionId}", request.InvoiceId, zatcaResult.TransactionId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZATCA submission exception. InvoiceId: {InvoiceId}", request.InvoiceId);
            return FailResult($"Exception: {ex.Message}", idempotencyKey);
        }
    }

    private SubmitInvoiceToZatcaResult FailResult(string message, string idempotencyKey)
    {
        var result = new SubmitInvoiceToZatcaResult(false, message, null);
        // Cache failure for 1 hour (shorter than success TTL to allow retry)
        _ = _cache.SetStringAsync(idempotencyKey, SerializeResult(result), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        });
        return result;
    }

    private static string ComputeInvoiceHash(Domain.Billing.Invoice invoice)
    {
        // Phase 1: simple hash of invoice number + amounts
        // Phase 2: canonical UBL XML hash
        var data = $"{invoice.InvoiceNumber}:{invoice.TotalSar}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash);
    }

    private static string SerializeResult(SubmitInvoiceToZatcaResult result)
    {
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    private static SubmitInvoiceToZatcaResult DeserializeResult(string json)
    {
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return System.Text.Json.JsonSerializer.Deserialize<SubmitInvoiceToZatcaResult>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize cached result.");
    }
}
