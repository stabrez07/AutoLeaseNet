namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Category of a <see cref="QuotationLine"/> (Spec 01 §5.4 <c>ItemType</c>).
/// Numeric values are stable persisted contract; never renumber.
/// </summary>
public enum QuotationItemType
{
    VehicleRental = 1,
    Insurance = 2,
    AdditionalDriver = 3,
    Gps = 4,
    Other = 5,
}
