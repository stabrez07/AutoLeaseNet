using System.Text.Json.Serialization;

namespace AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;

/// <summary>
/// Renter (lessee) details. Tajeer V9.7 §6.1.1 — required for every save. ID/passport
/// combinations vary by <c>idTypeCode</c> (1=Saudi, 2=Iqama, 3=GCC, 4=Visitor — see Spec 03 §7.6).
/// </summary>
public sealed record RenterDto
{
    [JsonPropertyName("personAddress")] public required string PersonAddress { get; init; }

    /// <summary>Required for GCC / Visitor renters.</summary>
    [JsonPropertyName("email")] public string? Email { get; init; }

    [JsonPropertyName("mobile")] public required string Mobile { get; init; }

    /// <summary>1=Saudi National, 2=Iqama, 3=GCC, 4=Visitor.</summary>
    [JsonPropertyName("idTypeCode")] public required int IdTypeCode { get; init; }

    [JsonPropertyName("idNumber")] public required long IdNumber { get; init; }

    [JsonPropertyName("passportNumber")] public string? PassportNumber { get; init; }

    /// <summary>Yakeen Hijri birth date (YYYYMMDD as int) — populated for Saudi renters.</summary>
    [JsonPropertyName("hijriBirthDate")] public int? HijriBirthDate { get; init; }

    /// <summary>Gregorian birth date <c>yyyy-MM-dd</c>.</summary>
    [JsonPropertyName("birthDate")] public string? BirthDate { get; init; }

    [JsonPropertyName("nationalityCode")] public int? NationalityCode { get; init; }

    [JsonPropertyName("driveLicenseNumber")] public string? DriveLicenseNumber { get; init; }

    [JsonPropertyName("licenseExpiryDate")] public string? LicenseExpiryDate { get; init; }

    [JsonPropertyName("issuePlaceId")] public long? IssuePlaceId { get; init; }

    [JsonPropertyName("idCopyNumber")] public int? IdCopyNumber { get; init; }

    [JsonPropertyName("idExpiryDate")] public string? IdExpiryDate { get; init; }
}
