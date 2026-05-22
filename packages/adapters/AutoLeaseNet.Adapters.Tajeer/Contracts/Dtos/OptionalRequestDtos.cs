using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

// Lean placeholders for the optional sections of SaveContractRequest. Each one is exercised
// by later workstreams (extra driver / authorization / vehicle rent status) — fields will
// be filled in as those flows land. Keeping the records here (rather than empty in their
// own files) avoids file-per-DTO sprawl until the spec details are needed.

/// <summary>Tajeer V9.7 §6.1 — rentStatus (optional at save, required at issuance/create).</summary>
public sealed record RentStatusDto
{
    [JsonPropertyName("startKm")] public int? StartKm { get; init; }
    [JsonPropertyName("fuelLevelCode")] public int? FuelLevelCode { get; init; }
    [JsonPropertyName("conditionNotes")] public string? ConditionNotes { get; init; }
}

/// <summary>Tajeer V9.7 §6.1 — extraDriver (companion driver, no authorization needed).</summary>
public sealed record ExtraDriverDto
{
    [JsonPropertyName("idNumber")] public required long IdNumber { get; init; }
    [JsonPropertyName("driveLicenseNumber")] public required string DriveLicenseNumber { get; init; }
}

/// <summary>Tajeer V9.7 §6.1 — rentedDriver (driver-with-vehicle case, contract types 3 / 4).</summary>
public sealed record RentedDriverDto
{
    [JsonPropertyName("idNumber")] public required long IdNumber { get; init; }
    [JsonPropertyName("driveLicenseNumber")] public required string DriveLicenseNumber { get; init; }
    [JsonPropertyName("mobile")] public string? Mobile { get; init; }
}

/// <summary>Tajeer V9.7 §6.1 — authorizedDriver (renter delegating driving to a third party).</summary>
public sealed record AuthorizedDriverDto
{
    [JsonPropertyName("idNumber")] public required long IdNumber { get; init; }
    [JsonPropertyName("driveLicenseNumber")] public required string DriveLicenseNumber { get; init; }
}

/// <summary>Tajeer V9.7 §6.1 — authorizationDetails (TAMM authorization metadata).</summary>
public sealed record AuthorizationDetailsDto
{
    [JsonPropertyName("authorizationTypeCode")] public required int AuthorizationTypeCode { get; init; }
    [JsonPropertyName("authorizationNumber")] public string? AuthorizationNumber { get; init; }
}

/// <summary>
/// Tajeer V9.7 §6.1 — addtionalServices [sic] (the misspelling is on Tajeer's side and is
/// preserved in <see cref="SaveContractRequest"/>'s JsonPropertyName). Phase 1 ships an
/// empty shape; bookings of extras (delivery, child seat, etc.) plug in here later.
/// </summary>
public sealed record AdditionalServicesDto
{
    [JsonPropertyName("deliveryRequested")] public bool? DeliveryRequested { get; init; }
    [JsonPropertyName("childSeatCount")] public int? ChildSeatCount { get; init; }
}
