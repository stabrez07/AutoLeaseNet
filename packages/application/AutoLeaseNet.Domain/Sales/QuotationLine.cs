using AutoLeaseNet.Domain.Shared;

namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// A priced line on a <see cref="Quotation"/> (Spec 01 §5.4 <c>QuotationLine</c>).
/// <see cref="LineTotalSar"/> is computed and never set directly:
/// <c>Quantity × UnitPriceSar × (1 − DiscountPercent/100)</c>, rounded to 2 dp.
/// Lines are owned by, and only mutated through, the <see cref="Quotation"/> root.
/// </summary>
public sealed class QuotationLine : Entity
{
    public Guid QuotationId { get; private set; }
    public int LineNumber { get; private set; }
    public QuotationItemType ItemType { get; private set; }
    public string Description { get; private set; } = string.Empty;

    /// <summary>Free-text spec ("Toyota Camry 2025") or a pre-allocated VehicleId string.</summary>
    public string? VehicleSpecRef { get; private set; }

    public int Quantity { get; private set; }
    public decimal UnitPriceSar { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal LineTotalSar { get; private set; }

    private QuotationLine() { }

    internal static QuotationLine Create(
        Guid tenantId,
        Guid quotationId,
        int lineNumber,
        QuotationItemType itemType,
        string description,
        string? vehicleSpecRef,
        int quantity,
        decimal unitPriceSar,
        decimal discountPercent,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (lineNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(lineNumber), lineNumber, "LineNumber must be >= 1.");
        if (quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be >= 1.");
        if (unitPriceSar < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPriceSar), unitPriceSar, "UnitPriceSar cannot be negative.");
        if (discountPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(discountPercent), discountPercent, "DiscountPercent must be 0–100.");

        var gross = quantity * unitPriceSar;
        var net = Math.Round(gross * (1 - discountPercent / 100m), 2, MidpointRounding.AwayFromZero);

        return new QuotationLine
        {
            TenantId = tenantId,
            QuotationId = quotationId,
            LineNumber = lineNumber,
            ItemType = itemType,
            Description = description,
            VehicleSpecRef = vehicleSpecRef,
            Quantity = quantity,
            UnitPriceSar = unitPriceSar,
            DiscountPercent = discountPercent,
            LineTotalSar = net,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }
}
