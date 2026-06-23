namespace AutoLeaseNet.Domain.Shared;

/// <summary>
/// Unified pricing calculation engine used across Quotation, Contract, and Lease Agreement.
/// Single source of truth for: Base → Discount → Net → VAT → Total.
/// </summary>
public static class LeasingPricingEngine
{
    public static PricingResult Calculate(
        decimal baseAmountSar,
        decimal discountPercent,
        decimal vatPercent = 15m,
        decimal surchargeSar = 0m)
    {
        if (discountPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(discountPercent));
        if (vatPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(vatPercent));

        var discountAmount = Math.Round(baseAmountSar * (discountPercent / 100m), 2, MidpointRounding.AwayFromZero);
        var netAmount = Math.Round(baseAmountSar - discountAmount, 2, MidpointRounding.AwayFromZero);
        var subtotalBeforeTax = Math.Round(netAmount + surchargeSar, 2, MidpointRounding.AwayFromZero);
        var vatAmount = Math.Round(subtotalBeforeTax * (vatPercent / 100m), 2, MidpointRounding.AwayFromZero);
        var totalAmount = subtotalBeforeTax + vatAmount;

        return new PricingResult(baseAmountSar, discountPercent, discountAmount, netAmount, surchargeSar, vatPercent, vatAmount, totalAmount);
    }

    public static PricingResult CalculateFromLines(
        IEnumerable<ILineItem> lines,
        decimal discountPercent,
        decimal vatPercent = 15m,
        decimal surchargeSar = 0m)
    {
        var baseAmount = Math.Round(lines.Sum(l => l.LineTotalSar), 2, MidpointRounding.AwayFromZero);
        return Calculate(baseAmount, discountPercent, vatPercent, surchargeSar);
    }
}

public interface ILineItem
{
    int Quantity { get; }
    decimal UnitPriceSar { get; }
    decimal LineTotalSar { get; }
}

public sealed record PricingResult(
    decimal BaseAmountSar,
    decimal DiscountPercent,
    decimal DiscountAmountSar,
    decimal NetAmountSar,
    decimal SurchargeSar,
    decimal VatPercent,
    decimal VatAmountSar,
    decimal TotalAmountSar)
{
    public decimal MonthlyAmount(int durationMonths) =>
        durationMonths > 0 ? Math.Round(TotalAmountSar / durationMonths, 2, MidpointRounding.AwayFromZero) : 0m;
}
