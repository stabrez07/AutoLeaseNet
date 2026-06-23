using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Customers;

public enum AccountStatus
{
    Active = 1,
    Inactive = 2,
}

public sealed class Account : Entity
{
    public Guid CustomerId { get; private set; }

    public string NatureOfBusiness { get; private set; } = string.Empty;

    // Customer company contact
    public string CustomerContactNameEn { get; private set; } = string.Empty;
    public string? CustomerContactNameAr { get; private set; }
    public string? CustomerContactPosition { get; private set; }
    public string? CustomerContactMobile { get; private set; }
    public string? CustomerContactEmail { get; private set; }

    // Our company account holder
    public string AccountHolderNameEn { get; private set; } = string.Empty;
    public string? AccountHolderNameAr { get; private set; }
    public string? AccountHolderPosition { get; private set; }
    public string? AccountHolderMobile { get; private set; }
    public string? AccountHolderEmail { get; private set; }

    // Address
    public string? Street { get; private set; }
    public string? City { get; private set; }
    public string? Region { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Country { get; private set; }

    public AccountStatus Status { get; private set; }

    private Account() { }

    public static Account Create(AccountCreateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(input));
        if (input.CustomerId == Guid.Empty) throw new ArgumentException("CustomerId required.", nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.CustomerContactNameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.AccountHolderNameEn);

        return new Account
        {
            TenantId = input.TenantId,
            CustomerId = input.CustomerId,
            NatureOfBusiness = input.NatureOfBusiness ?? string.Empty,
            CustomerContactNameEn = input.CustomerContactNameEn,
            CustomerContactNameAr = input.CustomerContactNameAr,
            CustomerContactPosition = input.CustomerContactPosition,
            CustomerContactMobile = input.CustomerContactMobile,
            CustomerContactEmail = input.CustomerContactEmail,
            AccountHolderNameEn = input.AccountHolderNameEn,
            AccountHolderNameAr = input.AccountHolderNameAr,
            AccountHolderPosition = input.AccountHolderPosition,
            AccountHolderMobile = input.AccountHolderMobile,
            AccountHolderEmail = input.AccountHolderEmail,
            Street = input.Street,
            City = input.City,
            Region = input.Region,
            PostalCode = input.PostalCode,
            Country = input.Country,
            Status = AccountStatus.Active,
            CreatedAtUtc = input.NowUtc,
            UpdatedAtUtc = input.NowUtc,
        };
    }

    public void Update(AccountUpdateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.NatureOfBusiness is not null) NatureOfBusiness = input.NatureOfBusiness;
        if (input.CustomerContactNameEn is not null) CustomerContactNameEn = input.CustomerContactNameEn;
        CustomerContactNameAr = input.CustomerContactNameAr ?? CustomerContactNameAr;
        CustomerContactPosition = input.CustomerContactPosition ?? CustomerContactPosition;
        CustomerContactMobile = input.CustomerContactMobile ?? CustomerContactMobile;
        CustomerContactEmail = input.CustomerContactEmail ?? CustomerContactEmail;
        if (input.AccountHolderNameEn is not null) AccountHolderNameEn = input.AccountHolderNameEn;
        AccountHolderNameAr = input.AccountHolderNameAr ?? AccountHolderNameAr;
        AccountHolderPosition = input.AccountHolderPosition ?? AccountHolderPosition;
        AccountHolderMobile = input.AccountHolderMobile ?? AccountHolderMobile;
        AccountHolderEmail = input.AccountHolderEmail ?? AccountHolderEmail;
        Street = input.Street ?? Street;
        City = input.City ?? City;
        Region = input.Region ?? Region;
        PostalCode = input.PostalCode ?? PostalCode;
        Country = input.Country ?? Country;
        UpdatedAtUtc = input.NowUtc;
    }

    public void Activate(DateTimeOffset nowUtc)
    {
        Status = AccountStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Deactivate(DateTimeOffset nowUtc)
    {
        Status = AccountStatus.Inactive;
        UpdatedAtUtc = nowUtc;
    }
}

public sealed record AccountCreateInput
{
    public required Guid TenantId { get; init; }
    public required Guid CustomerId { get; init; }
    public string? NatureOfBusiness { get; init; }
    public required string CustomerContactNameEn { get; init; }
    public string? CustomerContactNameAr { get; init; }
    public string? CustomerContactPosition { get; init; }
    public string? CustomerContactMobile { get; init; }
    public string? CustomerContactEmail { get; init; }
    public required string AccountHolderNameEn { get; init; }
    public string? AccountHolderNameAr { get; init; }
    public string? AccountHolderPosition { get; init; }
    public string? AccountHolderMobile { get; init; }
    public string? AccountHolderEmail { get; init; }
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? Region { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}

public sealed record AccountUpdateInput
{
    public string? NatureOfBusiness { get; init; }
    public string? CustomerContactNameEn { get; init; }
    public string? CustomerContactNameAr { get; init; }
    public string? CustomerContactPosition { get; init; }
    public string? CustomerContactMobile { get; init; }
    public string? CustomerContactEmail { get; init; }
    public string? AccountHolderNameEn { get; init; }
    public string? AccountHolderNameAr { get; init; }
    public string? AccountHolderPosition { get; init; }
    public string? AccountHolderMobile { get; init; }
    public string? AccountHolderEmail { get; init; }
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? Region { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
}
