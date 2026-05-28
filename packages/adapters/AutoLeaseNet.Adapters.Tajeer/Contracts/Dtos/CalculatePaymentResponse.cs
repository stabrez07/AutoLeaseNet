using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.13 — <c>CalculateContractPayment</c> response. Money breakdown that
/// feeds the ops preview screen and the eventual <see cref="CloseContractRequest"/>.
/// All amounts are in SAR.
/// </summary>
public sealed record CalculatePaymentResponse
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }

    /// <summary>Base rent for the contract period (un-discounted, ex-VAT).</summary>
    [JsonPropertyName("rentAmount")] public decimal RentAmount { get; init; }

    /// <summary>Already paid by the renter (paid-on-issuance + any top-ups).</summary>
    [JsonPropertyName("paidAmount")] public decimal PaidAmount { get; init; }

    /// <summary>Hours past the contract end (Tajeer rounds per its own policy).</summary>
    [JsonPropertyName("lateHoursFee")] public decimal LateHoursFee { get; init; }

    /// <summary>Kilometres over the contract allowance × per-km rate.</summary>
    [JsonPropertyName("extraKmFee")] public decimal ExtraKmFee { get; init; }

    /// <summary>Damages, cleaning, refuelling — pass-through from the request.</summary>
    [JsonPropertyName("damagesFee")] public decimal DamagesFee { get; init; }

    /// <summary>Discount applied (if any) — server-validated.</summary>
    [JsonPropertyName("discountAmount")] public decimal DiscountAmount { get; init; }

    /// <summary>Sum of fees minus discount, ex-VAT.</summary>
    [JsonPropertyName("totalDue")] public decimal TotalDue { get; init; }

    /// <summary>VAT computed on <see cref="TotalDue"/>.</summary>
    [JsonPropertyName("vatAmount")] public decimal VatAmount { get; init; }

    /// <summary>Total - paid + VAT — what ops collects at the counter.</summary>
    [JsonPropertyName("grandTotal")] public decimal GrandTotal { get; init; }
}
