using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.10 — <c>SuspendContract</c> response. Minimal — the vendor confirms
/// the new status code + the moment it stamped on the suspension.
/// </summary>
public sealed record SuspendContractResponse
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }

    /// <summary>Tajeer status code post-suspend. 3 = Suspended.</summary>
    [JsonPropertyName("contractStatusCode")] public required int ContractStatusCode { get; init; }

    [JsonPropertyName("suspendedAt")] public string? SuspendedAt { get; init; }
}
