using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Tajeer V9.7 §6.1 — Save Contract request body. Per
/// <see href="../../../../Specs/03-tajeer-adapter-design.md">Spec 03 §6.2</see>.
///
/// Field names mirror Tajeer's payload exactly, including the documented misspelling
/// <c>addtionalServices</c> (do NOT "fix" it on our side — Tajeer's API expects that key).
/// </summary>
public sealed record SaveContractRequest
{
    [JsonPropertyName("renter")]
    public required RenterDto Renter { get; init; }

    [JsonPropertyName("paymentDetails")]
    public required PaymentDetailsDto PaymentDetails { get; init; }

    [JsonPropertyName("vehicleDetails")]
    public required VehicleDetailsDto VehicleDetails { get; init; }

    [JsonPropertyName("rentStatus")]
    public RentStatusDto? RentStatus { get; init; }

    [JsonPropertyName("extraDriver")]
    public ExtraDriverDto? ExtraDriver { get; init; }

    [JsonPropertyName("rentedDriver")]
    public RentedDriverDto? RentedDriver { get; init; }

    [JsonPropertyName("authorizedDriver")]
    public AuthorizedDriverDto? AuthorizedDriver { get; init; }

    [JsonPropertyName("authorizationDetails")]
    public AuthorizationDetailsDto? AuthorizationDetails { get; init; }

    // Tajeer's spec misspells "additional" — preserve.
    [JsonPropertyName("addtionalServices")]
    public AdditionalServicesDto? AdditionalServices { get; init; }

    [JsonPropertyName("extendedCoverageId")]
    public int? ExtendedCoverageId { get; init; }

    [JsonPropertyName("workingBranchId")]
    public required int WorkingBranchId { get; init; }

    [JsonPropertyName("rentPolicyId")]
    public required int RentPolicyId { get; init; }

    /// <summary>Format: <c>yyyy-MM-ddTHH:mm</c> per Tajeer V9.7 §11.2.</summary>
    [JsonPropertyName("contractStartDate")]
    public required string ContractStartDate { get; init; }

    /// <summary>Format: <c>yyyy-MM-ddTHH:mm</c> per Tajeer V9.7 §11.2.</summary>
    [JsonPropertyName("contractEndDate")]
    public required string ContractEndDate { get; init; }

    [JsonPropertyName("allowedKmPerHour")]
    public int AllowedKmPerHour { get; init; }

    [JsonPropertyName("allowedKmPerDay")]
    public int AllowedKmPerDay { get; init; }

    [JsonPropertyName("unlimitedKm")]
    public bool UnlimitedKm { get; init; }

    [JsonPropertyName("receiveBranchId")]
    public required int ReceiveBranchId { get; init; }

    [JsonPropertyName("returnBranchId")]
    public required int ReturnBranchId { get; init; }

    /// <summary>1=daily, 2=hourly, 3=daily with driver, 4=hourly with driver. See Spec 03 §7.5.</summary>
    [JsonPropertyName("contractTypeCode")]
    public required int ContractTypeCode { get; init; }

    /// <summary>0–24 hours.</summary>
    [JsonPropertyName("allowedLateHours")]
    public int AllowedLateHours { get; init; }

    [JsonPropertyName("operatorId")]
    public required long OperatorId { get; init; }
}
