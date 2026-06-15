using System.Net.Http.Json;
using System.Text.Json;
using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts;

/// <summary>
/// Real HTTP implementation of <see cref="ITajeerContractClient"/> backed by the named
/// <c>"tajeer"</c> <see cref="HttpClient"/> (resilience + auth headers injected by the
/// pipeline). See <see cref="ServiceCollectionExtensions.AddTajeer"/>.
/// </summary>
public sealed partial class TajeerContractClient : ITajeerContractClient
{
    // Phase 1 endpoint paths — canonical values are confirmed during the first staging
    // round-trip; centralising them here keeps the eventual correction a one-line change.
    private const string SavePath = "/api/contracts/save";
    private const string CalculatePaymentPath = "/api/contracts/calculate-payment";
    private const string ClosePath = "/api/contracts/closure";
    private const string ExtendPath = "/api/contracts/extend";
    private const string SuspendPath = "/api/contracts/suspend";
    private const string CancelPath = "/api/contracts/cancel";
    // Read endpoint per Spec 03 §6.3. RESTful shape — confirmed (or corrected) on the
    // first staging round-trip; centralising the prefix keeps the eventual fix one-line.
    private const string GetPathPrefix = "/api/contracts/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TajeerContractClient> _logger;

    public TajeerContractClient(
        IHttpClientFactory httpClientFactory,
        ILogger<TajeerContractClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IntegrationResult<SaveContractResponse>> SaveAsync(
        SaveContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<SaveContractRequest, SaveContractResponse>(
            HttpMethod.Post, SavePath, request, "SaveContract", ct);
    }

    public Task<IntegrationResult<CalculatePaymentResponse>> CalculatePaymentAsync(
        CalculatePaymentRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<CalculatePaymentRequest, CalculatePaymentResponse>(
            HttpMethod.Put, CalculatePaymentPath, request, "CalculatePayment", ct);
    }

    public Task<IntegrationResult<CloseContractResponse>> CloseAsync(
        CloseContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<CloseContractRequest, CloseContractResponse>(
            HttpMethod.Put, ClosePath, request, "CloseContract", ct);
    }

    public Task<IntegrationResult<ExtendContractResponse>> ExtendAsync(
        ExtendContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<ExtendContractRequest, ExtendContractResponse>(
            HttpMethod.Put, ExtendPath, request, "ExtendContract", ct);
    }

    public Task<IntegrationResult<SuspendContractResponse>> SuspendAsync(
        SuspendContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<SuspendContractRequest, SuspendContractResponse>(
            HttpMethod.Put, SuspendPath, request, "SuspendContract", ct);
    }

    public Task<IntegrationResult<Unit>> CancelAsync(
        CancelContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<CancelContractRequest, Unit>(
            HttpMethod.Put, CancelPath, request, "CancelContract", ct);
    }

    public Task<IntegrationResult<GetContractResponse>> GetAsync(
        long contractNumber,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contractNumber);
        var path = GetPathPrefix + contractNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return SendNoBodyAsync<GetContractResponse>(HttpMethod.Get, path, "GetContract", ct);
    }

    /// <summary>
    /// Shared request/response/error-mapping spine. Every Tajeer contract method maps
    /// failures identically — vendor envelope on 2xx, vendor envelope on 4xx, HTTP-only
    /// transient on 5xx/408/429, then network/timeout/JSON parse below.
    /// </summary>
    private async Task<IntegrationResult<TResponse>> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest body,
        string operationName,
        CancellationToken ct)
        where TRequest : class
        where TResponse : class
    {
        var client = _httpClientFactory.CreateClient(ServiceCollectionExtensions.TajeerHttpClientName);

        try
        {
            using var request = new HttpRequestMessage(method, path)
            {
                Content = JsonContent.Create(body, options: JsonOptions),
            };
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);

            var statusCode = (int)response.StatusCode;
            var rawBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // Defensive: Tajeer occasionally returns 200 + error envelope. Inspect the
            // body first regardless of HTTP status — only treat a clean 2xx with no
            // errorKey as success. (Spec 03 §8.1 Q4.)
            var vendorError = TryReadErrorEnvelope(rawBody);
            if (vendorError is { HasError: true })
            {
                LogVendorBusinessError(operationName, statusCode, vendorError.ErrorKey!, vendorError.ErrorCode ?? 0);
                return IntegrationResult<TResponse>.Failure(
                    errorCode: $"tajeer.vendor.{vendorError.ErrorKey}",
                    errorMessage: vendorError.RawMessage ?? vendorError.Message ?? "Tajeer business error.",
                    isTransient: false);
            }

            if (!response.IsSuccessStatusCode)
            {
                var isTransient = statusCode >= 500 || statusCode == 408 || statusCode == 429;
                LogNonSuccessStatus(operationName, statusCode);
                return IntegrationResult<TResponse>.Failure(
                    errorCode: $"tajeer.http.{statusCode}",
                    errorMessage: $"Tajeer {method} {path} returned HTTP {statusCode}.",
                    isTransient: isTransient);
            }

            if (typeof(TResponse) == typeof(Unit))
            {
                return IntegrationResult<TResponse>.Success((TResponse)(object)Unit.Value);
            }

            var parsed = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
            if (parsed is null)
            {
                LogEmptyBody(operationName);
                return IntegrationResult<TResponse>.Failure(
                    errorCode: "tajeer.deserialization",
                    errorMessage: $"Tajeer returned an empty/null body on {operationName}.",
                    isTransient: false);
            }
            return IntegrationResult<TResponse>.Success(parsed);
        }
        catch (HttpRequestException ex)
        {
            LogNetworkFailure(operationName, ex);
            return IntegrationResult<TResponse>.Failure(
                errorCode: "tajeer.network",
                errorMessage: $"Network failure calling Tajeer: {ex.Message}",
                isTransient: true);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            LogTimeout(operationName, ex);
            return IntegrationResult<TResponse>.Failure(
                errorCode: "tajeer.timeout",
                errorMessage: $"Tajeer {operationName} timed out.",
                isTransient: true);
        }
        catch (JsonException ex)
        {
            LogDeserializationFailure(operationName, ex);
            return IntegrationResult<TResponse>.Failure(
                errorCode: "tajeer.deserialization",
                errorMessage: $"Failed to parse Tajeer {operationName} response: {ex.Message}",
                isTransient: false);
        }
    }

    /// <summary>
    /// No-body sibling of <see cref="SendAsync{TRequest, TResponse}"/> for GET endpoints.
    /// Shares the same vendor-error / transient / parse mapping; only the request
    /// construction differs (no <see cref="JsonContent"/>).
    /// </summary>
    private async Task<IntegrationResult<TResponse>> SendNoBodyAsync<TResponse>(
        HttpMethod method,
        string path,
        string operationName,
        CancellationToken ct)
        where TResponse : class
    {
        var client = _httpClientFactory.CreateClient(ServiceCollectionExtensions.TajeerHttpClientName);

        try
        {
            using var request = new HttpRequestMessage(method, path);
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);

            var statusCode = (int)response.StatusCode;
            var rawBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            var vendorError = TryReadErrorEnvelope(rawBody);
            if (vendorError is { HasError: true })
            {
                LogVendorBusinessError(operationName, statusCode, vendorError.ErrorKey!, vendorError.ErrorCode ?? 0);
                return IntegrationResult<TResponse>.Failure(
                    errorCode: $"tajeer.vendor.{vendorError.ErrorKey}",
                    errorMessage: vendorError.RawMessage ?? vendorError.Message ?? "Tajeer business error.",
                    isTransient: false);
            }

            if (!response.IsSuccessStatusCode)
            {
                // 404 on a read is a vendor-level "not found" — non-transient, surfaced
                // distinctly so callers (reconciliation) can treat it as a drift signal
                // rather than a retryable infrastructure blip.
                if (statusCode == 404)
                {
                    LogNonSuccessStatus(operationName, statusCode);
                    return IntegrationResult<TResponse>.Failure(
                        errorCode: "tajeer.vendor.contract.not_found",
                        errorMessage: $"Tajeer {method} {path} returned HTTP 404 (contract not found).",
                        isTransient: false);
                }

                var isTransient = statusCode >= 500 || statusCode == 408 || statusCode == 429;
                LogNonSuccessStatus(operationName, statusCode);
                return IntegrationResult<TResponse>.Failure(
                    errorCode: $"tajeer.http.{statusCode}",
                    errorMessage: $"Tajeer {method} {path} returned HTTP {statusCode}.",
                    isTransient: isTransient);
            }

            var parsed = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
            if (parsed is null)
            {
                LogEmptyBody(operationName);
                return IntegrationResult<TResponse>.Failure(
                    errorCode: "tajeer.deserialization",
                    errorMessage: $"Tajeer returned an empty/null body on {operationName}.",
                    isTransient: false);
            }
            return IntegrationResult<TResponse>.Success(parsed);
        }
        catch (HttpRequestException ex)
        {
            LogNetworkFailure(operationName, ex);
            return IntegrationResult<TResponse>.Failure(
                errorCode: "tajeer.network",
                errorMessage: $"Network failure calling Tajeer: {ex.Message}",
                isTransient: true);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            LogTimeout(operationName, ex);
            return IntegrationResult<TResponse>.Failure(
                errorCode: "tajeer.timeout",
                errorMessage: $"Tajeer {operationName} timed out.",
                isTransient: true);
        }
        catch (JsonException ex)
        {
            LogDeserializationFailure(operationName, ex);
            return IntegrationResult<TResponse>.Failure(
                errorCode: "tajeer.deserialization",
                errorMessage: $"Failed to parse Tajeer {operationName} response: {ex.Message}",
                isTransient: false);
        }
    }

    private static TajeerErrorEnvelope? TryReadErrorEnvelope(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return null;
        try
        {
            return JsonSerializer.Deserialize<TajeerErrorEnvelope>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            // Body isn't JSON (e.g. an HTML 502 page). Not an error envelope.
            return null;
        }
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning,
        Message = "Tajeer {Operation} returned vendor business error {ErrorKey} (vendor code {VendorCode}) on HTTP {StatusCode}")]
    partial void LogVendorBusinessError(string operation, int statusCode, string errorKey, int vendorCode);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Warning,
        Message = "Tajeer {Operation} returned non-success status {StatusCode}")]
    partial void LogNonSuccessStatus(string operation, int statusCode);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Warning,
        Message = "Tajeer {Operation} network failure")]
    partial void LogNetworkFailure(string operation, Exception ex);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Warning,
        Message = "Tajeer {Operation} timed out")]
    partial void LogTimeout(string operation, Exception ex);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Error,
        Message = "Tajeer {Operation} returned non-JSON or invalid payload")]
    partial void LogDeserializationFailure(string operation, Exception ex);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Error,
        Message = "Tajeer {Operation} returned empty body on 2xx")]
    partial void LogEmptyBody(string operation);
}
