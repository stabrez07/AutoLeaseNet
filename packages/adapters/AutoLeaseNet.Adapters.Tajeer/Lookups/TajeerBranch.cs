using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Lookups;

/// <summary>
/// Branch (rental office) returned by Tajeer's <c>GET /api/lookups/branches</c> endpoint.
/// Unknown fields are ignored by System.Text.Json's default tolerant reader, so adding new
/// vendor fields won't break parsing — Day 3 smoke test will reveal the canonical shape
/// and we expand as needed.
/// </summary>
public sealed record TajeerBranch(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("nameAr")] string? NameAr,
    [property: JsonPropertyName("nameEn")] string? NameEn,
    [property: JsonPropertyName("cityAr")] string? CityAr,
    [property: JsonPropertyName("cityEn")] string? CityEn,
    [property: JsonPropertyName("regionAr")] string? RegionAr,
    [property: JsonPropertyName("regionEn")] string? RegionEn,
    [property: JsonPropertyName("licenseNumber")] string? LicenseNumber,
    [property: JsonPropertyName("isActive")] bool? IsActive);
