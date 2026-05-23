using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.RentPolicies;

/// <summary>
/// Rent policy — pricing + terms template that Sales picks from when issuing a contract.
/// Maps 1:1 to a Tajeer rent policy via <see cref="TajeerRentPolicyId"/>.
/// </summary>
public sealed class RentPolicy : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string? DescriptionEn { get; private set; }
    public string? DescriptionAr { get; private set; }

    public decimal BaseDailyRate { get; private set; }
    public decimal? BaseHourlyRate { get; private set; }
    public int AllowedKmPerDay { get; private set; }
    public int AllowedKmPerHour { get; private set; }
    public bool UnlimitedKm { get; private set; }
    public decimal LateHourFee { get; private set; }
    public decimal ExtraKmFee { get; private set; }
    public int MinRentalDays { get; private set; }
    public int? MaxRentalDays { get; private set; }
    public decimal? SecurityDeposit { get; private set; }

    public int TajeerRentPolicyId { get; private set; }
    public bool IsActive { get; private set; }

    private RentPolicy() { }

    public static RentPolicy Create(RentPolicyCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.NameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.NameAr);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.BaseDailyRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.TajeerRentPolicyId);

        return new RentPolicy
        {
            TenantId = input.TenantId,
            Code = input.Code,
            NameEn = input.NameEn,
            NameAr = input.NameAr,
            DescriptionEn = input.DescriptionEn,
            DescriptionAr = input.DescriptionAr,
            BaseDailyRate = input.BaseDailyRate,
            BaseHourlyRate = input.BaseHourlyRate,
            AllowedKmPerDay = input.AllowedKmPerDay,
            AllowedKmPerHour = input.AllowedKmPerHour,
            UnlimitedKm = input.UnlimitedKm,
            LateHourFee = input.LateHourFee,
            ExtraKmFee = input.ExtraKmFee,
            MinRentalDays = input.MinRentalDays,
            MaxRentalDays = input.MaxRentalDays,
            SecurityDeposit = input.SecurityDeposit,
            TajeerRentPolicyId = input.TajeerRentPolicyId,
            IsActive = true,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }

    public void Deactivate(DateTimeOffset nowUtc) { IsActive = false; UpdatedAtUtc = nowUtc; }
}

public sealed record RentPolicyCreateInput
{
    public required Guid TenantId { get; init; }
    public required string Code { get; init; }
    public required string NameEn { get; init; }
    public required string NameAr { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionAr { get; init; }
    public required decimal BaseDailyRate { get; init; }
    public decimal? BaseHourlyRate { get; init; }
    public int AllowedKmPerDay { get; init; } = 300;
    public int AllowedKmPerHour { get; init; }
    public bool UnlimitedKm { get; init; }
    public decimal LateHourFee { get; init; }
    public decimal ExtraKmFee { get; init; }
    public int MinRentalDays { get; init; } = 1;
    public int? MaxRentalDays { get; init; }
    public decimal? SecurityDeposit { get; init; }
    public required int TajeerRentPolicyId { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}
