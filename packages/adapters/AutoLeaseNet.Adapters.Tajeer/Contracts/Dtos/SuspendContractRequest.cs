using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.10 — <c>SuspendContract</c>. Transitions the contract to
/// <c>contractStatusCode = 3</c> (Suspended). Spec 02 §768 notes Tajeer doesn't
/// allow SUSPENDED → ACTIVE; only SUSPENDED → CLOSED. The reason code feeds the
/// reconciliation report.
/// </summary>
public sealed record SuspendContractRequest
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }

    /// <summary>Tajeer suspension reason code (e.g. NON_TRAFFIC_DAMAGE).</summary>
    [JsonPropertyName("suspensionReasonCode")] public required int SuspensionReasonCode { get; init; }

    /// <summary>Optional ops note (max 130 chars per Tajeer).</summary>
    [JsonPropertyName("suspensionNotes")] public string? SuspensionNotes { get; init; }

    /// <summary>UTC moment ops marked the contract suspended. Tajeer format <c>yyyy-MM-ddTHH:mm</c>.</summary>
    [JsonPropertyName("suspendedAt")] public required string SuspendedAt { get; init; }
}
