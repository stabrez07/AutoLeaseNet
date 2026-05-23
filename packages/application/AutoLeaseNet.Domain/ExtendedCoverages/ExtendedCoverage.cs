using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.ExtendedCoverages;

public enum CoverageType
{
    PartialCdw = 1,
    FullCdw = 2,
    SuperCdw = 3,
    TheftProtection = 4,
    RoadsideAssistance = 5,
}

/// <summary>
/// Optional insurance coverage add-ons offered at contract issuance. Maps 1:1 to a Tajeer
/// extended coverage via <see cref="TajeerExtendedCoverageId"/>.
/// </summary>
public sealed class ExtendedCoverage : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string? DescriptionEn { get; private set; }
    public string? DescriptionAr { get; private set; }
    public CoverageType CoverageType { get; private set; }
    public decimal DailyRate { get; private set; }
    public decimal DeductibleAmount { get; private set; }
    public int TajeerExtendedCoverageId { get; private set; }
    public bool IsActive { get; private set; }

    private ExtendedCoverage() { }

    public static ExtendedCoverage Create(ExtendedCoverageCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.NameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.NameAr);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.DailyRate);
        ArgumentOutOfRangeException.ThrowIfNegative(input.DeductibleAmount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.TajeerExtendedCoverageId);

        return new ExtendedCoverage
        {
            TenantId = input.TenantId,
            Code = input.Code,
            NameEn = input.NameEn,
            NameAr = input.NameAr,
            DescriptionEn = input.DescriptionEn,
            DescriptionAr = input.DescriptionAr,
            CoverageType = input.CoverageType,
            DailyRate = input.DailyRate,
            DeductibleAmount = input.DeductibleAmount,
            TajeerExtendedCoverageId = input.TajeerExtendedCoverageId,
            IsActive = true,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }

    public void Deactivate(DateTimeOffset nowUtc) { IsActive = false; UpdatedAtUtc = nowUtc; }
}

public sealed record ExtendedCoverageCreateInput
{
    public required Guid TenantId { get; init; }
    public required string Code { get; init; }
    public required string NameEn { get; init; }
    public required string NameAr { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }
    public required CoverageType CoverageType { get; init; }
    public required decimal DailyRate { get; init; }
    public decimal DeductibleAmount { get; init; }
    public required int TajeerExtendedCoverageId { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}
