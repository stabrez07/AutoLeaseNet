using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Customers;

/// <summary>
/// Customer aggregate root. Two flavours via <see cref="CustomerType"/>:
/// <list type="bullet">
///   <item>B2B (Fleet account) — has commercial registration, VAT, credit limit.</item>
///   <item>B2C (individual lessee) — has personal id (Saudi national / Iqama / GCC / Visitor).</item>
/// </list>
/// PII fields (<see cref="PersonIdNumber"/>, future IBAN) will move to SQL Server Always
/// Encrypted columns in Week 2 Day 9; today they're plain strings with
/// <see cref="PiiOptedOut"/> as the future Right-To-Be-Forgotten flag.
/// </summary>
public sealed class Customer : Entity
{
    public CustomerType Type { get; private set; }
    public CustomerStatus Status { get; private set; }

    // ─── Shared identity / contact ──────────────────────────────────────────
    public string DisplayName { get; private set; } = string.Empty;
    public string? DisplayNameAr { get; private set; }
    public string? Email { get; private set; }
    public string? Mobile { get; private set; }
    public string? NationalAddress { get; private set; }
    public PreferredLanguage PreferredLanguage { get; private set; }

    // ─── B2B ────────────────────────────────────────────────────────────────
    public string? LegalName { get; private set; }
    public string? LegalNameAr { get; private set; }
    public string? CommercialRegistration { get; private set; }
    public string? VatNumber { get; private set; }
    public string? BillingAddress { get; private set; }
    public decimal? CreditLimit { get; private set; }
    public string? CreditCurrency { get; private set; }

    // ─── B2C ────────────────────────────────────────────────────────────────
    public string? PersonNameEn { get; private set; }
    public string? PersonNameAr { get; private set; }
    /// <summary>1=Saudi National, 2=Iqama, 3=GCC, 4=Visitor — per Spec 03 §7.6.</summary>
    public int? IdTypeCode { get; private set; }
    public string? PersonIdNumber { get; private set; } // Always Encrypted in Week 2
    public DateOnly? DateOfBirth { get; private set; }
    public string? NationalityCode { get; private set; }

    // ─── KYC ────────────────────────────────────────────────────────────────
    public bool KycVerified { get; private set; }
    public DateTimeOffset? KycVerifiedAtUtc { get; private set; }
    public string? KycVerifiedBy { get; private set; }

    public bool PiiOptedOut { get; private set; }

    private Customer() { }

    /// <summary>B2B factory — Fleet account creation.</summary>
    public static Customer CreateB2B(B2BCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.LegalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.CommercialRegistration);

        return new Customer
        {
            TenantId = input.TenantId,
            Type = CustomerType.B2B,
            Status = CustomerStatus.Active,
            DisplayName = input.LegalName,
            DisplayNameAr = input.LegalNameAr,
            LegalName = input.LegalName,
            LegalNameAr = input.LegalNameAr,
            CommercialRegistration = input.CommercialRegistration,
            VatNumber = input.VatNumber,
            Email = input.Email,
            Mobile = input.Mobile,
            NationalAddress = input.NationalAddress,
            BillingAddress = input.BillingAddress,
            CreditLimit = input.CreditLimit,
            CreditCurrency = input.CreditCurrency,
            PreferredLanguage = input.PreferredLanguage,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }

    /// <summary>B2C factory — individual lessee creation.</summary>
    public static Customer CreateB2C(B2CCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.PersonNameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.PersonIdNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(input.IdTypeCode, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(input.IdTypeCode, 4);

        return new Customer
        {
            TenantId = input.TenantId,
            Type = CustomerType.B2C,
            Status = CustomerStatus.Active,
            DisplayName = input.PersonNameEn,
            DisplayNameAr = input.PersonNameAr,
            PersonNameEn = input.PersonNameEn,
            PersonNameAr = input.PersonNameAr,
            IdTypeCode = input.IdTypeCode,
            PersonIdNumber = input.PersonIdNumber,
            DateOfBirth = input.DateOfBirth,
            NationalityCode = input.NationalityCode,
            Email = input.Email,
            Mobile = input.Mobile,
            NationalAddress = input.NationalAddress,
            PreferredLanguage = input.PreferredLanguage,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }

    public void MarkKycVerified(string verifiedBy, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedBy);
        KycVerified = true;
        KycVerifiedAtUtc = nowUtc;
        KycVerifiedBy = verifiedBy;
        UpdatedAtUtc = nowUtc;
    }

    public void Suspend(DateTimeOffset nowUtc)
    {
        if (Status == CustomerStatus.Closed)
            throw new InvalidOperationException($"Customer {Id} is Closed; cannot Suspend.");
        Status = CustomerStatus.Suspended;
        UpdatedAtUtc = nowUtc;
    }

    public void Reactivate(DateTimeOffset nowUtc)
    {
        if (Status != CustomerStatus.Suspended)
            throw new InvalidOperationException($"Customer {Id} must be Suspended to Reactivate (was {Status}).");
        Status = CustomerStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Close(DateTimeOffset nowUtc)
    {
        Status = CustomerStatus.Closed;
        UpdatedAtUtc = nowUtc;
    }

    public void OptOutOfPii(DateTimeOffset nowUtc)
    {
        PiiOptedOut = true;
        UpdatedAtUtc = nowUtc;
    }
}

public sealed record B2BCreateInput
{
    public required Guid TenantId { get; init; }
    public required string LegalName { get; init; }
    public string? LegalNameAr { get; init; }
    public required string CommercialRegistration { get; init; }
    public string? VatNumber { get; init; }
    public string? Email { get; init; }
    public string? Mobile { get; init; }
    public string? NationalAddress { get; init; }
    public string? BillingAddress { get; init; }
    public decimal? CreditLimit { get; init; }
    public string? CreditCurrency { get; init; }
    public PreferredLanguage PreferredLanguage { get; init; } = PreferredLanguage.Ar;
    public required DateTimeOffset NowUtc { get; init; }
}

public sealed record B2CCreateInput
{
    public required Guid TenantId { get; init; }
    public required string PersonNameEn { get; init; }
    public string? PersonNameAr { get; init; }
    public required int IdTypeCode { get; init; }
    public required string PersonIdNumber { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? NationalityCode { get; init; }
    public string? Email { get; init; }
    public string? Mobile { get; init; }
    public string? NationalAddress { get; init; }
    public PreferredLanguage PreferredLanguage { get; init; } = PreferredLanguage.Ar;
    public required DateTimeOffset NowUtc { get; init; }
}
