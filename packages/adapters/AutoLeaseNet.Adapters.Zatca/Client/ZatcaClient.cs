using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Zatca.Dtos;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Adapters.Zatca.Client;

/// <summary>
/// Real HTTP <see cref="IZatcaClient"/> backed by the named <c>"zatca"</c>
/// <see cref="HttpClient"/> (resilience + auth header injected by the pipeline). See
/// <see cref="ServiceCollectionExtensions.AddZatca"/>.
///
/// <para>
/// <b>Phase-1 stub.</b> <see cref="SubmitInvoiceAsync"/> returns
/// <c>zatca.not_yet_implemented</c> rather than throwing, so a misconfigured composition
/// root that lands on Real instead of InMemory fails loudly at the saga rather than as a
/// 500 in the BFF. Week-4 replaces the body with UBL 2.1 + ECDSA P-256 + TLV QR + a real
/// POST to <c>{BaseUrl}/invoices/clearance/single</c> (Tax) or
/// <c>/invoices/reporting/single</c> (Simplified).
/// </para>
/// </summary>
public sealed partial class ZatcaClient : IZatcaClient
{
    public const string ErrorCodeNotImplemented = "zatca.not_yet_implemented";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ZatcaClient> _logger;

    public ZatcaClient(IHttpClientFactory httpClientFactory, ILogger<ZatcaClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IntegrationResult<SubmitInvoiceResponse>> SubmitInvoiceAsync(
        SubmitInvoiceRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        LogStubCalled(request.Uuid);

        return Task.FromResult(IntegrationResult<SubmitInvoiceResponse>.Failure(
            errorCode: ErrorCodeNotImplemented,
            errorMessage: "Real ZATCA clearance lands in the Week-4 workstream — switch Zatca:Mode to InMemory for tests/dev.",
            isTransient: false));
    }

    [LoggerMessage(EventId = 5001, Level = LogLevel.Warning,
        Message = "Real ZatcaClient.SubmitInvoiceAsync called for invoice {Uuid} but Phase-1 stub is wired — Week-4 workstream will replace with the real UBL 2.1 + clearance flow.")]
    partial void LogStubCalled(Guid uuid);
}
