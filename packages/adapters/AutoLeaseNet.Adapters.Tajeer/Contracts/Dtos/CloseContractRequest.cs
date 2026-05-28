using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.6 — <c>CloseContract</c>. Destructive vendor commit that flips a
/// Tajeer contract to <c>contractStatusCode = 2</c> (Closed). Returned amounts on the
/// matching <see cref="CloseContractResponse"/> should agree with the preceding
/// <see cref="CalculatePaymentResponse"/> (Tajeer recomputes server-side).
/// </summary>
public sealed record CloseContractRequest
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }

    [JsonPropertyName("closureMainReasonCode")] public required int ClosureMainReasonCode { get; init; }
    [JsonPropertyName("closureSubReasonCode")] public int? ClosureSubReasonCode { get; init; }

    /// <summary>UTC return moment, Tajeer format <c>yyyy-MM-ddTHH:mm</c>.</summary>
    [JsonPropertyName("returnDate")] public required string ReturnDate { get; init; }

    [JsonPropertyName("returnedKm")] public required int ReturnedKm { get; init; }
    [JsonPropertyName("returnedFuelLevelCode")] public required int ReturnedFuelLevelCode { get; init; }

    /// <summary>Free-form return condition note (max 130 chars per Tajeer).</summary>
    [JsonPropertyName("returnConditionNotes")] public string? ReturnConditionNotes { get; init; }
    [JsonPropertyName("damagesObserved")] public string? DamagesObserved { get; init; }

    [JsonPropertyName("finalPaidAmount")] public required decimal FinalPaidAmount { get; init; }
    [JsonPropertyName("discountAmount")] public decimal? DiscountAmount { get; init; }
}
