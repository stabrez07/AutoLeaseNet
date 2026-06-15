using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.8 — cancel a saved contract before issuance.
/// </summary>
public sealed record CancelContractRequest
{
    [JsonPropertyName("contractNumber")]
    public required long ContractNumber { get; init; }
}
