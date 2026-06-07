namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Commercial shape of the lease a quotation proposes (Spec 01 §5.4 <c>ContractType</c>).
/// Numeric values are stable persisted contract; never renumber.
/// </summary>
public enum QuotationContractType
{
    Daily = 1,
    Hourly = 2,
    LongTermLease = 3,
}
