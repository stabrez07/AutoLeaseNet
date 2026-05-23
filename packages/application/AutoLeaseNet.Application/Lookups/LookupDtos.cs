namespace AutoLeaseNet.Application.Lookups;

/// <summary>Lightweight read-models the BFF returns from the lookup endpoints.</summary>

public sealed record BranchDto(
    Guid Id, string Code, string NameEn, string NameAr,
    string? CityEn, string? CityAr, string? RegionEn, string? RegionAr,
    int TajeerBranchId, bool IsActive);

public sealed record RentPolicyDto(
    Guid Id, string Code, string NameEn, string NameAr,
    decimal BaseDailyRate, decimal? BaseHourlyRate,
    int AllowedKmPerDay, int AllowedKmPerHour, bool UnlimitedKm,
    decimal ExtraKmFee, int MinRentalDays, int? MaxRentalDays,
    int TajeerRentPolicyId, bool IsActive);

public sealed record ExtendedCoverageDto(
    Guid Id, string Code, string NameEn, string NameAr,
    int CoverageType, decimal DailyRate, decimal DeductibleAmount,
    int TajeerExtendedCoverageId, bool IsActive);

public sealed record CustomerSummaryDto(
    Guid Id, int Type, int Status,
    string DisplayName, string? DisplayNameAr,
    string? Email, string? Mobile,
    string? CommercialRegistration, string? VatNumber,
    bool KycVerified);

public sealed record VehicleSummaryDto(
    Guid Id, int Status,
    string PlateNumber, string PlateLetters, int PlateTypeCode,
    string Vin, string Make, string Model, int ModelYear, string? Color,
    int FuelType, int BodyType, int Seats,
    Guid CurrentBranchId, int CurrentKm);

public sealed record DriverSummaryDto(
    Guid Id, int Status, Guid? CustomerId,
    string PersonNameEn, string? PersonNameAr,
    int IdTypeCode, int LicenseClass,
    DateOnly LicenseExpiryDate, int TammAuthorizationStatus);
