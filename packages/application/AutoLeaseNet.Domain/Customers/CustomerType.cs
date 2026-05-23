namespace AutoLeaseNet.Domain.Customers;

/// <summary>
/// B2B (Fleet account with VAT registration) vs B2C (individual lessee). Drives
/// validation rules at create time and downstream billing flows.
/// </summary>
public enum CustomerType
{
    B2B = 1,
    B2C = 2,
}

public enum CustomerStatus
{
    Active = 1,
    Suspended = 2,
    Closed = 3,
}

public enum PreferredLanguage
{
    Ar = 1,
    En = 2,
}
