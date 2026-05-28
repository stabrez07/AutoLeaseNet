using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.7 — <c>ExtendContract</c>. Pushes the contract end date forward
/// (state transition ACTIVE → EXTENDED, or EXTENDED → EXTENDED). Spec 02 §4.2 caps
/// total extensions at 25 — the domain enforces that locally on <c>Lease.IncrementExtension</c>.
/// </summary>
public sealed record ExtendContractRequest
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }

    /// <summary>UTC new contract end. Tajeer format <c>yyyy-MM-ddTHH:mm</c>; must be strictly &gt; current end.</summary>
    [JsonPropertyName("newContractEndDate")] public required string NewContractEndDate { get; init; }

    /// <summary>Tajeer extension reason code (optional — defaults applied server-side).</summary>
    [JsonPropertyName("extensionReasonCode")] public int? ExtensionReasonCode { get; init; }

    /// <summary>Additional charges collected at extend time (cleaning, top-up, etc.).</summary>
    [JsonPropertyName("additionalChargesAmount")] public decimal? AdditionalChargesAmount { get; init; }

    /// <summary>Payment method for the additional charges (Tajeer payment-method code).</summary>
    [JsonPropertyName("paymentMethodCode")] public int? PaymentMethodCode { get; init; }
}
