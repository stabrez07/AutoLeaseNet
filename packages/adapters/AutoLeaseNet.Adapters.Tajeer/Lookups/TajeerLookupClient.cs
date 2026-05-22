using System.Net.Http.Json;
using System.Text.Json;
using AutoLeaseNet.Adapters.Common.Result;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Adapters.Tajeer.Lookups;

/// <summary>
/// Read-only Tajeer lookup endpoints (branches, rent policies, extended coverages, etc.).
/// Lookups are bulk reads with no side effects, so the client is stateless and safe to
/// resolve as scoped/transient.
///
/// Day 3 covers <c>GetAllBranchesAsync</c> only; other lookups arrive on demand.
/// </summary>
public sealed partial class TajeerLookupClient
{
    private const string BranchesPath = "/api/lookups/branches";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TajeerLookupClient> _logger;

    public TajeerLookupClient(IHttpClientFactory httpClientFactory, ILogger<TajeerLookupClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// GET <c>/api/lookups/branches</c> — returns all rental offices/branches available
    /// for contract issuance. 4xx → non-transient failure; 5xx and network errors →
    /// transient (caller / Polly pipeline may retry).
    /// </summary>
    public async Task<IntegrationResult<IReadOnlyList<TajeerBranch>>> GetAllBranchesAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(ServiceCollectionExtensions.TajeerHttpClientName);

        try
        {
            using var response = await client.GetAsync(BranchesPath, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                var isTransient = statusCode >= 500;
                LogNonSuccessStatus(statusCode);
                return IntegrationResult<IReadOnlyList<TajeerBranch>>.Failure(
                    errorCode: $"tajeer.http.{statusCode}",
                    errorMessage: $"Tajeer GET {BranchesPath} returned HTTP {statusCode}.",
                    isTransient: isTransient);
            }

            var branches = await response.Content
                .ReadFromJsonAsync<List<TajeerBranch>>(JsonOptions, ct)
                .ConfigureAwait(false);

            return IntegrationResult<IReadOnlyList<TajeerBranch>>.Success(
                branches ?? new List<TajeerBranch>());
        }
        catch (HttpRequestException ex)
        {
            LogNetworkFailure(ex);
            return IntegrationResult<IReadOnlyList<TajeerBranch>>.Failure(
                errorCode: "tajeer.network",
                errorMessage: $"Network failure calling Tajeer: {ex.Message}",
                isTransient: true);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            LogTimeout(ex);
            return IntegrationResult<IReadOnlyList<TajeerBranch>>.Failure(
                errorCode: "tajeer.timeout",
                errorMessage: "Tajeer request timed out.",
                isTransient: true);
        }
        catch (JsonException ex)
        {
            LogDeserializationFailure(ex);
            return IntegrationResult<IReadOnlyList<TajeerBranch>>.Failure(
                errorCode: "tajeer.deserialization",
                errorMessage: $"Failed to parse Tajeer branches response: {ex.Message}",
                isTransient: false);
        }
    }

    [LoggerMessage(EventId = 3501, Level = LogLevel.Warning,
        Message = "Tajeer GetAllBranches returned non-success status {StatusCode}")]
    partial void LogNonSuccessStatus(int statusCode);

    [LoggerMessage(EventId = 3502, Level = LogLevel.Warning,
        Message = "Tajeer GetAllBranches network failure")]
    partial void LogNetworkFailure(Exception ex);

    [LoggerMessage(EventId = 3503, Level = LogLevel.Warning,
        Message = "Tajeer GetAllBranches timed out")]
    partial void LogTimeout(Exception ex);

    [LoggerMessage(EventId = 3504, Level = LogLevel.Error,
        Message = "Tajeer GetAllBranches returned non-JSON or invalid payload")]
    partial void LogDeserializationFailure(Exception ex);
}
