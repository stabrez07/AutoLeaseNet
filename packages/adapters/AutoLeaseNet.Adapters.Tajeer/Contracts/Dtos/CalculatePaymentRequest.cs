using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.13 — <c>CalculateContractPayment</c>. Non-destructive preview of the
/// money breakdown for a close (or suspend) operation, computed from the contract's
/// current state plus the caller-supplied return readings. See
/// <see href="../../../../Specs/02-state-machines-and-sagas.md">Spec 02 §6.4</see>.
///
/// <para>
/// Used by the check-in saga to surface "total due at close" to ops BEFORE the
/// destructive <see cref="CloseContractRequest"/> call.
/// </para>
/// </summary>
public sealed record CalculatePaymentRequest
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }

    /// <summary>UTC return moment (the renter handed the keys back). Tajeer format <c>yyyy-MM-ddTHH:mm</c>.</summary>
    [JsonPropertyName("returnDate")] public required string ReturnDate { get; init; }

    [JsonPropertyName("returnedKm")] public required int ReturnedKm { get; init; }

    /// <summary>Tajeer fuel-level code: 1=Full, 2=ThreeQuarter, 3=Half, 4=Quarter, 5=Empty.</summary>
    [JsonPropertyName("returnedFuelLevelCode")] public required int ReturnedFuelLevelCode { get; init; }

    /// <summary>Caller-declared extra-km overage (km beyond the contract allowance).</summary>
    [JsonPropertyName("extraKm")] public int? ExtraKm { get; init; }

    /// <summary>Caller-declared additional charges (damages, cleaning, etc.) — Tajeer applies VAT.</summary>
    [JsonPropertyName("additionalCharges")] public decimal? AdditionalCharges { get; init; }

    /// <summary>Optional discount line. Tajeer caps this server-side per tenant policy.</summary>
    [JsonPropertyName("discountAmount")] public decimal? DiscountAmount { get; init; }
}
