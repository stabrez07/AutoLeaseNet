using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Branches;

/// <summary>
/// Branch (rental office) aggregate. Maps 1:1 to a Tajeer branch via
/// <see cref="TajeerBranchId"/>; <see cref="TajeerOperatorId"/> is the default operator
/// id we pass when issuing contracts through this branch.
/// </summary>
public sealed class Branch : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string? CityEn { get; private set; }
    public string? CityAr { get; private set; }
    public string? RegionEn { get; private set; }
    public string? RegionAr { get; private set; }
    public string? LicenseNumber { get; private set; }
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string? PhoneNumber { get; private set; }

    public int TajeerBranchId { get; private set; }
    public long TajeerOperatorId { get; private set; }

    /// <summary>Free-form JSON describing working hours per day-of-week.</summary>
    public string? WorkingHoursJson { get; private set; }

    public bool IsActive { get; private set; }

    private Branch() { }

    public static Branch Create(BranchCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.NameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.NameAr);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.TajeerBranchId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.TajeerOperatorId);

        return new Branch
        {
            TenantId = input.TenantId,
            Code = input.Code,
            NameEn = input.NameEn,
            NameAr = input.NameAr,
            CityEn = input.CityEn,
            CityAr = input.CityAr,
            RegionEn = input.RegionEn,
            RegionAr = input.RegionAr,
            LicenseNumber = input.LicenseNumber,
            Address = input.Address,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            PhoneNumber = input.PhoneNumber,
            TajeerBranchId = input.TajeerBranchId,
            TajeerOperatorId = input.TajeerOperatorId,
            WorkingHoursJson = input.WorkingHoursJson,
            IsActive = true,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }

    public void Deactivate(DateTimeOffset nowUtc) { IsActive = false; UpdatedAtUtc = nowUtc; }
    public void Activate(DateTimeOffset nowUtc) { IsActive = true; UpdatedAtUtc = nowUtc; }
}

public sealed record BranchCreateInput
{
    public required Guid TenantId { get; init; }
    public required string Code { get; init; }
    public required string NameEn { get; init; }
    public required string NameAr { get; init; }
    public string? CityEn { get; init; }
    public string? CityAr { get; init; }
    public string? RegionEn { get; init; }
    public string? RegionAr { get; init; }
    public string? LicenseNumber { get; init; }
    public string? Address { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public string? PhoneNumber { get; init; }
    public required int TajeerBranchId { get; init; }
    public required long TajeerOperatorId { get; init; }
    public string? WorkingHoursJson { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}
