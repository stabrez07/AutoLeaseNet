using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer's standard business-error body. Returned on 4xx responses AND occasionally on
/// 200 OK (per Spec 03 §8.1 Q4 — defensive parsing: every response with <c>errorKey</c>
/// set is treated as a business error regardless of HTTP status).
/// </summary>
public sealed record TajeerErrorEnvelope
{
    [JsonPropertyName("errorKey")] public string? ErrorKey { get; init; }

    [JsonPropertyName("errorCode")] public int? ErrorCode { get; init; }

    [JsonPropertyName("rawMessage")] public string? RawMessage { get; init; }

    /// <summary>Tajeer occasionally uses <c>message</c> instead of <c>rawMessage</c>.</summary>
    [JsonPropertyName("message")] public string? Message { get; init; }

    /// <summary>True when this envelope carries an error signal we should surface.</summary>
    [JsonIgnore]
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorKey);
}
