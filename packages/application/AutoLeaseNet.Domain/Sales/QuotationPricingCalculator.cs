namespace AutoLeaseNet.Domain.Sales;

/// <summary>
/// Internal pricing calculator for quotation totals.
/// Formula parity is kept with the migration spreadsheet while calculations run in-app.
/// </summary>
public static class QuotationPricingCalculator
{
    public static QuotationPricingResult Calculate(
        IEnumerable<QuotationLine> lines,
        decimal quoteDiscountPercent,
        decimal vatPercent,
        decimal surchargeSar = 0m)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (quoteDiscountPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quoteDiscountPercent), quoteDiscountPercent, "DiscountPercent must be 0-100.");
        if (vatPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(vatPercent), vatPercent, "VatPercent must be 0-100.");
        if (surchargeSar < 0)
            throw new ArgumentOutOfRangeException(nameof(surchargeSar), surchargeSar, "Surcharge cannot be negative.");

        // BaseAmount (quote-level in current model) = sum of line net totals.
        var baseAmount = Math.Round(lines.Sum(l => l.LineTotalSar), 2, MidpointRounding.AwayFromZero);

        // DiscountAmount = BaseAmount * DiscountPercent.
        var discountAmount = Math.Round(baseAmount * (quoteDiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);

        // NetAmount = BaseAmount - DiscountAmount.
        var netAmount = Math.Round(baseAmount - discountAmount, 2, MidpointRounding.AwayFromZero);

        // SubtotalBeforeTax = NetAmount + Surcharges.
        var subtotalBeforeTax = Math.Round(netAmount + surchargeSar, 2, MidpointRounding.AwayFromZero);

        // VatAmount = SubtotalBeforeTax * VatPercent.
        var vatAmount = Math.Round(subtotalBeforeTax * (vatPercent / 100m), 2, MidpointRounding.AwayFromZero);

        // GrandTotal = SubtotalBeforeTax + VatAmount.
        var grandTotal = subtotalBeforeTax + vatAmount;

        return new QuotationPricingResult(
            BaseAmountSar: baseAmount,
            DiscountAmountSar: discountAmount,
            NetAmountSar: netAmount,
            SurchargeSar: surchargeSar,
            SubTotalBeforeTaxSar: subtotalBeforeTax,
            VatSar: vatAmount,
            TotalSar: grandTotal);
    }
}

public sealed record QuotationPricingResult(
    decimal BaseAmountSar,
    decimal DiscountAmountSar,
    decimal NetAmountSar,
    decimal SurchargeSar,
    decimal SubTotalBeforeTaxSar,
    decimal VatSar,
    decimal TotalSar);
