using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Vehicle identification — Tajeer accepts either an internal vehicleId or the plate
/// triple (number + letters + type). Per V9.7 §6.1 / Spec 03 §11.1 (plate format helpers).
/// </summary>
public sealed record VehicleDetailsDto
{
    /// <summary>Tajeer's internal vehicle id when known (preferred — avoids plate lookup).</summary>
    [JsonPropertyName("vehicleId")] public long? VehicleId { get; init; }

    /// <summary>Numeric portion of the plate (e.g. <c>"1234"</c>).</summary>
    [JsonPropertyName("plateNumber")] public string? PlateNumber { get; init; }

    /// <summary>Letters portion in Tajeer's Arabic-letter format (see Spec 03 §11.1).</summary>
    [JsonPropertyName("plateLetters")] public string? PlateLetters { get; init; }

    /// <summary>Plate type code (private, taxi, public-transport, …) — Tajeer lookup.</summary>
    [JsonPropertyName("plateTypeCode")] public int? PlateTypeCode { get; init; }

    [JsonPropertyName("currentKm")] public int? CurrentKm { get; init; }
}
