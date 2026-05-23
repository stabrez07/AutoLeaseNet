using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Drivers;

public enum DriverStatus
{
    Active = 1,
    Suspended = 2,
    Banned = 3,
}

public enum TammAuthorizationStatus
{
    NotRequested = 0,
    Pending = 1,
    Authorized = 2,
    Rejected = 3,
}

/// <summary>
/// Driver aggregate root. A driver may be:
/// <list type="bullet">
///   <item>affiliated with a B2B <c>CustomerId</c> (fleet driver pool), or</item>
///   <item>unaffiliated (freelance / hired-by-the-day) — <see cref="CustomerId"/> is null.</item>
/// </list>
/// Carries TAMM authorisation state (KSA government driver-delegation flow) and license
/// expiry so the BI dashboard can surface "expiring in 30 days" reports without recomputing.
/// </summary>
public sealed class Driver : Entity
{
    public DriverStatus Status { get; private set; }
    public Guid? CustomerId { get; private set; }

    public string PersonNameEn { get; private set; } = string.Empty;
    public string? PersonNameAr { get; private set; }
    /// <summary>1=Saudi National, 2=Iqama, 3=GCC, 4=Visitor.</summary>
    public int IdTypeCode { get; private set; }
    public string PersonIdNumber { get; private set; } = string.Empty; // Always Encrypted in Week 2
    public DateOnly? DateOfBirth { get; private set; }
    public string? NationalityCode { get; private set; }

    public string DriverLicenseNumber { get; private set; } = string.Empty; // Always Encrypted in Week 2
    /// <summary>Tajeer / MOI license class (1=light vehicle, 2=heavy, 3=motorcycle, …).</summary>
    public int LicenseClass { get; private set; }
    public long? LicenseIssuePlaceId { get; private set; }
    public DateOnly? LicenseIssueDate { get; private set; }
    public DateOnly LicenseExpiryDate { get; private set; }

    public string? Mobile { get; private set; }
    public string? Email { get; private set; }
    public string? NationalAddress { get; private set; }

    public TammAuthorizationStatus TammAuthorizationStatus { get; private set; }
    public string? TammAuthorizationRef { get; private set; }
    public DateTimeOffset? TammAuthorizedAtUtc { get; private set; }

    public bool DefensiveDrivingCertHeld { get; private set; }
    public int AccidentCountLast3Yrs { get; private set; }

    public bool PiiOptedOut { get; private set; }

    private Driver() { }

    public static Driver Create(DriverCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.PersonNameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.PersonIdNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.DriverLicenseNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(input.IdTypeCode, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(input.IdTypeCode, 4);

        return new Driver
        {
            TenantId = input.TenantId,
            CustomerId = input.CustomerId,
            Status = DriverStatus.Active,
            PersonNameEn = input.PersonNameEn,
            PersonNameAr = input.PersonNameAr,
            IdTypeCode = input.IdTypeCode,
            PersonIdNumber = input.PersonIdNumber,
            DateOfBirth = input.DateOfBirth,
            NationalityCode = input.NationalityCode,
            DriverLicenseNumber = input.DriverLicenseNumber,
            LicenseClass = input.LicenseClass,
            LicenseIssuePlaceId = input.LicenseIssuePlaceId,
            LicenseIssueDate = input.LicenseIssueDate,
            LicenseExpiryDate = input.LicenseExpiryDate,
            Mobile = input.Mobile,
            Email = input.Email,
            NationalAddress = input.NationalAddress,
            TammAuthorizationStatus = TammAuthorizationStatus.NotRequested,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }

    /// <summary>True when <see cref="LicenseExpiryDate"/> is within <paramref name="days"/> of today.</summary>
    public bool IsLicenseExpiringSoon(DateOnly today, int days = 30)
    {
        return LicenseExpiryDate <= today.AddDays(days);
    }

    public void MarkTammAuthorizationPending(string reference, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        TammAuthorizationStatus = TammAuthorizationStatus.Pending;
        TammAuthorizationRef = reference;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkTammAuthorized(DateTimeOffset nowUtc)
    {
        if (TammAuthorizationStatus != TammAuthorizationStatus.Pending)
            throw new InvalidOperationException($"Driver {Id} TAMM authorisation must be Pending to mark Authorized (was {TammAuthorizationStatus}).");
        TammAuthorizationStatus = TammAuthorizationStatus.Authorized;
        TammAuthorizedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkTammRejected(DateTimeOffset nowUtc)
    {
        TammAuthorizationStatus = TammAuthorizationStatus.Rejected;
        UpdatedAtUtc = nowUtc;
    }

    public void Suspend(DateTimeOffset nowUtc) { Status = DriverStatus.Suspended; UpdatedAtUtc = nowUtc; }
    public void Reactivate(DateTimeOffset nowUtc)
    {
        if (Status != DriverStatus.Suspended)
            throw new InvalidOperationException($"Driver {Id} must be Suspended to Reactivate (was {Status}).");
        Status = DriverStatus.Active; UpdatedAtUtc = nowUtc;
    }
    public void Ban(DateTimeOffset nowUtc) { Status = DriverStatus.Banned; UpdatedAtUtc = nowUtc; }

    public void RecordAccident(DateTimeOffset nowUtc)
    {
        AccidentCountLast3Yrs++;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkDefensiveDrivingCertHeld(DateTimeOffset nowUtc)
    {
        DefensiveDrivingCertHeld = true;
        UpdatedAtUtc = nowUtc;
    }

    public void OptOutOfPii(DateTimeOffset nowUtc) { PiiOptedOut = true; UpdatedAtUtc = nowUtc; }
}

public sealed record DriverCreateInput
{
    public required Guid TenantId { get; init; }
    public Guid? CustomerId { get; init; }
    public required string PersonNameEn { get; init; }
    public string? PersonNameAr { get; init; }
    public required int IdTypeCode { get; init; }
    public required string PersonIdNumber { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? NationalityCode { get; init; }
    public required string DriverLicenseNumber { get; init; }
    public int LicenseClass { get; init; } = 1;
    public long? LicenseIssuePlaceId { get; init; }
    public DateOnly? LicenseIssueDate { get; init; }
    public required DateOnly LicenseExpiryDate { get; init; }
    public string? Mobile { get; init; }
    public string? Email { get; init; }
    public string? NationalAddress { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}
