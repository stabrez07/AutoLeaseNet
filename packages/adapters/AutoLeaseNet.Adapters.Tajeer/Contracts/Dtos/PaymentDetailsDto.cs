using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Payment terms attached to the main rental amount. Tajeer V9.7 §6.1 (paymentDetails).
/// Phase 1 captures the subset our `dev/save-contract` flow needs; expand as additional
/// fields are exercised by real-staging payloads.
/// </summary>
public sealed record PaymentDetailsDto
{
    /// <summary>Payment method code from <c>/lookups/payment-method</c> — never hardcode.</summary>
    [JsonPropertyName("paymentMethodCode")] public required int PaymentMethodCode { get; init; }

    [JsonPropertyName("rentAmount")] public required decimal RentAmount { get; init; }

    [JsonPropertyName("paidAmount")] public decimal PaidAmount { get; init; }

    /// <summary>0 = none, 1 = before-VAT discount, 2 = after-VAT discount (per Tajeer lookups).</summary>
    [JsonPropertyName("discountType")] public int? DiscountType { get; init; }

    [JsonPropertyName("discountValue")] public decimal? DiscountValue { get; init; }

    [JsonPropertyName("paymentReference")] public string? PaymentReference { get; init; }
}
