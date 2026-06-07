using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Sales;

public sealed class EfQuotationRepositoryTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a11111-0000-0000-0000-000000000001");
    private static readonly Guid CustomerId = Guid.Parse("c1c11111-0000-0000-0000-000000000001");
    private static readonly Guid AccountManagerId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Add_then_get_by_id_and_quote_number_round_trips_the_aggregate()
    {
        await using var db = NewDb();
        var repo = new EfQuotationRepository(db);

        var quotation = NewDraft("Q-TEST-0001");
        quotation.AddLine(new AddQuotationLineInput
        {
            ItemType = QuotationItemType.VehicleRental,
            Description = "Camry 2025",
            Quantity = 2,
            UnitPriceSar = 1_000m,
            DiscountPercent = 5m,
            NowUtc = Now,
        });

        repo.Add(quotation);
        await db.SaveChangesAsync();

        var byId = await repo.GetByIdAsync(TenantId, quotation.Id, CancellationToken.None);
        var byQuoteNumber = await repo.GetByQuoteNumberAsync(TenantId, quotation.QuoteNumber, CancellationToken.None);

        byId.Should().NotBeNull();
        byId!.Lines.Should().HaveCount(1);
        byQuoteNumber.Should().NotBeNull();
        byQuoteNumber!.Id.Should().Be(quotation.Id);
        byQuoteNumber.TotalSar.Should().Be(quotation.TotalSar);
    }

    private static Quotation NewDraft(string quoteNumber) => Quotation.CreateDraft(new CreateQuotationInput
    {
        TenantId = TenantId,
        QuoteNumber = quoteNumber,
        CustomerId = CustomerId,
        AccountManagerId = AccountManagerId,
        QuoteDate = new DateOnly(2026, 6, 7),
        ValidUntilDate = new DateOnly(2026, 6, 21),
        ContractType = QuotationContractType.LongTermLease,
        EstimatedDurationMonths = 12,
        DiscountPercent = 0m,
        NowUtc = Now,
    });

    private static AutoLeaseNetDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AutoLeaseNetDbContext(options);
    }
}

