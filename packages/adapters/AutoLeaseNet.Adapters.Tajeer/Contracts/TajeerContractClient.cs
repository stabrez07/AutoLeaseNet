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
    // Phase 1 endpoint — the canonical path is confirmed during Day 5 smoke against
    // staging; centralising it here makes the eventual correction a one-line change.
    private const string SavePath = "/api/contracts/save";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Tajeer fields use camelCase already, but defending against null-on-required
        // and DTOs that flow strings-as-numbers happens at the response level via
        // record validation. Keep the defaults simple here.
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TajeerContractClient> _logger;

    public TajeerContractClient(
        IHttpClientFactory httpClientFactory,
        ILogger<TajeerContractClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IntegrationResult<SaveContractResponse>> SaveAsync(
        SaveContractRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = _httpClientFactory.CreateClient(ServiceCollectionExtensions.TajeerHttpClientName);

        try
        {
            using var response = await client
                .PostAsJsonAsync(SavePath, request, JsonOptions, ct)
                .ConfigureAwait(false);

            var statusCode = (int)response.StatusCode;
            var rawBody = await response.Content
                .ReadAsStringAsync(ct)
                .ConfigureAwait(false);

            // Defensive: Tajeer occasionally returns 200 + error envelope. Inspect the body
            // first regardless of HTTP status — only treat a clean 2xx with no errorKey as
            // success. (Spec 03 §8.1 Q4.)
            var vendorError = TryReadErrorEnvelope(rawBody);
            if (vendorError is { HasError: true })
            {
                LogVendorBusinessError(statusCode, vendorError.ErrorKey!, vendorError.ErrorCode ?? 0);
                return IntegrationResult<SaveContractResponse>.Failure(
                    errorCode: $"tajeer.vendor.{vendorError.ErrorKey}",
                    errorMessage: vendorError.RawMessage ?? vendorError.Message ?? "Tajeer business error.",
                    isTransient: false);
            }

            if (!response.IsSuccessStatusCode)
            {
                var isTransient = statusCode >= 500
                    || statusCode == 408   // Request Timeout
                    || statusCode == 429;  // Too Many Requests
                LogNonSuccessStatus(statusCode);
                return IntegrationResult<SaveContractResponse>.Failure(
                    errorCode: $"tajeer.http.{statusCode}",
                    errorMessage: $"Tajeer POST {SavePath} returned HTTP {statusCode}.",
                    isTransient: isTransient);
            }

            var parsed = JsonSerializer.Deserialize<SaveContractResponse>(rawBody, JsonOptions);
            if (parsed is null)
            {
                LogEmptyBody();
                return IntegrationResult<SaveContractResponse>.Failure(
                    errorCode: "tajeer.deserialization",
                    errorMessage: "Tajeer returned an empty/null body on Save Contract.",
                    isTransient: false);
            }
            return IntegrationResult<SaveContractResponse>.Success(parsed);
        }
        catch (HttpRequestException ex)
        {
            LogNetworkFailure(ex);
            return IntegrationResult<SaveContractResponse>.Failure(
                errorCode: "tajeer.network",
                errorMessage: $"Network failure calling Tajeer: {ex.Message}",
                isTransient: true);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            LogTimeout(ex);
            return IntegrationResult<SaveContractResponse>.Failure(
                errorCode: "tajeer.timeout",
                errorMessage: "Tajeer SaveContract timed out.",
                isTransient: true);
        }
        catch (JsonException ex)
        {
            LogDeserializationFailure(ex);
            return IntegrationResult<SaveContractResponse>.Failure(
                errorCode: "tajeer.deserialization",
                errorMessage: $"Failed to parse Tajeer SaveContract response: {ex.Message}",
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
        Message = "Tajeer SaveContract returned vendor business error {ErrorKey} (vendor code {VendorCode}) on HTTP {StatusCode}")]
    partial void LogVendorBusinessError(int statusCode, string errorKey, int vendorCode);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Warning,
        Message = "Tajeer SaveContract returned non-success status {StatusCode}")]
    partial void LogNonSuccessStatus(int statusCode);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Warning,
        Message = "Tajeer SaveContract network failure")]
    partial void LogNetworkFailure(Exception ex);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Warning,
        Message = "Tajeer SaveContract timed out")]
    partial void LogTimeout(Exception ex);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Error,
        Message = "Tajeer SaveContract returned non-JSON or invalid payload")]
    partial void LogDeserializationFailure(Exception ex);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Error,
        Message = "Tajeer SaveContract returned empty body on 2xx")]
    partial void LogEmptyBody();
}
