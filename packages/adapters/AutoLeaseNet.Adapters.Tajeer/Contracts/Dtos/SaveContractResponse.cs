using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.1 — Save Contract response. Per
/// <see href="../../../../Specs/03-tajeer-adapter-design.md">Spec 03 §6.3</see>.
///
/// <para>
/// The <c>issuanceURL</c> is the link the renter follows to complete (issue) the saved
/// contract — format <c>{IssuanceUrlBase}/#/public-contract/{contractNumber}/{token}</c>
/// (see <see cref="Configuration.TajeerOptions.IssuanceUrlBase"/>).
/// </para>
/// </summary>
public sealed record SaveContractResponse
{
    [JsonPropertyName("contractNumber")] public required long ContractNumber { get; init; }

    [JsonPropertyName("token")] public required string Token { get; init; }

    [JsonPropertyName("issuanceURL")] public required string IssuanceUrl { get; init; }

    [JsonPropertyName("mainPaymentDetails")] public required PaymentSummary MainPaymentDetails { get; init; }

    [JsonPropertyName("otherPaymentDetails")] public required PaymentSummary OtherPaymentDetails { get; init; }

    [JsonPropertyName("totalPaymentDetails")] public required PaymentSummary TotalPaymentDetails { get; init; }
}

/// <summary>Money breakdown returned alongside every Save Contract response.</summary>
public sealed record PaymentSummary
{
    [JsonPropertyName("paid")] public decimal Paid { get; init; }
    [JsonPropertyName("remaining")] public decimal Remaining { get; init; }
    [JsonPropertyName("total")] public decimal Total { get; init; }
    [JsonPropertyName("vat")] public decimal Vat { get; init; }
}
