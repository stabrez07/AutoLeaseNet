using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.7 — <c>ExtendContract</c> response. <c>contractStatusCode = 4</c>
/// confirms the EXTENDED transition; money fields echo the additional charges +
/// applied VAT (Tajeer recomputes server-side).
/// </summary>
public sealed record ExtendContractResponse
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }

    /// <summary>Tajeer status code post-extend. 4 = Extended.</summary>
    [JsonPropertyName("contractStatusCode")] public required int ContractStatusCode { get; init; }

    [JsonPropertyName("newContractEndDate")] public required string NewContractEndDate { get; init; }

    /// <summary>Charges incurred for this extension, ex-VAT.</summary>
    [JsonPropertyName("totalDue")] public decimal TotalDue { get; init; }

    [JsonPropertyName("vatAmount")] public decimal VatAmount { get; init; }

    [JsonPropertyName("grandTotal")] public decimal GrandTotal { get; init; }
}
