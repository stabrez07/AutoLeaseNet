using AutoLeaseNet.Domain.Sales;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Sales;

public sealed class QuotationPricingCalculatorTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaa3333-0000-0000-0000-000000000001");
    private static readonly Guid CustomerId = Guid.Parse("bbbb3333-0000-0000-0000-000000000001");
    private static readonly Guid AccountManagerId = Guid.Parse("cccc3333-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    private static Quotation QuoteWithSingleLine(decimal unitPrice, int quantity)
    {
        var q = Quotation.CreateDraft(new CreateQuotationInput
        {
            TenantId = TenantId,
            QuoteNumber = "Q-ALN-202606-0101",
            CustomerId = CustomerId,
            AccountManagerId = AccountManagerId,
            QuoteDate = new DateOnly(2026, 6, 7),
            ValidUntilDate = new DateOnly(2026, 6, 21),
            ContractType = QuotationContractType.LongTermLease,
            EstimatedDurationMonths = 12,
            DiscountPercent = 0m,
            NowUtc = Now,
        });

        q.AddLine(new AddQuotationLineInput
        {
            ItemType = QuotationItemType.VehicleRental,
            Description = "Toyota Camry 2025 — 12mo",
            Quantity = quantity,
            UnitPriceSar = unitPrice,
            DiscountPercent = 0m,
            NowUtc = Now,
        });

        return q;
    }

    [Fact]
    public void Calculate_supports_10_percent_discount_formula()
    {
        var q = QuoteWithSingleLine(unitPrice: 1000m, quantity: 10); // base = 10,000

        var result = QuotationPricingCalculator.Calculate(
            q.Lines,
            quoteDiscountPercent: 10m,
            vatPercent: 15m);

        result.BaseAmountSar.Should().Be(10_000m);
        result.DiscountAmountSar.Should().Be(1_000m);
        result.NetAmountSar.Should().Be(9_000m);
        result.SubTotalBeforeTaxSar.Should().Be(9_000m);
        result.VatSar.Should().Be(1_350m);
        result.TotalSar.Should().Be(10_350m);
    }

    [Fact]
    public void Calculate_supports_20_percent_discount_formula()
    {
        var q = QuoteWithSingleLine(unitPrice: 1000m, quantity: 10); // base = 10,000

        var result = QuotationPricingCalculator.Calculate(
            q.Lines,
            quoteDiscountPercent: 20m,
            vatPercent: 15m);

        result.BaseAmountSar.Should().Be(10_000m);
        result.DiscountAmountSar.Should().Be(2_000m);
        result.NetAmountSar.Should().Be(8_000m);
        result.SubTotalBeforeTaxSar.Should().Be(8_000m);
        result.VatSar.Should().Be(1_200m);
        result.TotalSar.Should().Be(9_200m);
    }

    [Fact]
    public void Calculate_includes_surcharge_before_tax()
    {
        var q = QuoteWithSingleLine(unitPrice: 1000m, quantity: 1);

        var result = QuotationPricingCalculator.Calculate(
            q.Lines,
            quoteDiscountPercent: 0m,
            vatPercent: 15m,
            surchargeSar: 100m);

        result.SubTotalBeforeTaxSar.Should().Be(1_100m);
        result.VatSar.Should().Be(165m);
        result.TotalSar.Should().Be(1_265m);
    }
}
