using AutoLeaseNet.Adapters.Cache.InMemory;
using AutoLeaseNet.Application.Sales;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Sales;

public sealed class RecallQuotationCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0005-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("d4d4d4d4-0000-0000-0000-000000000030");
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 10, 0, 0, TimeSpan.Zero);

    private sealed class Harness : IDisposable
    {
        public AutoLeaseNetDbContext Db { get; }
        public RecallQuotationCommandHandler Sut { get; }
        public Quotation Quotation { get; }

        public Harness(bool submitForApproval = false)
        {
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            var customer = Customer.CreateB2B(new B2BCreateInput
            {
                TenantId = TenantId, LegalName = "Corp", CommercialRegistration = "CR-004", NowUtc = Now,
            });
            Db.Customers.Add(customer);

            Quotation = Quotation.CreateDraft(new CreateQuotationInput
            {
                TenantId = TenantId,
                QuoteNumber = "Q-20260607-0003",
                CustomerId = customer.Id,
                AccountManagerId = UserId,
                QuoteDate = DateOnly.FromDateTime(Now.DateTime),
                ValidUntilDate = DateOnly.FromDateTime(Now.DateTime).AddDays(30),
                ContractType = QuotationContractType.LongTermLease,
                NowUtc = Now,
            });
            Quotation.AddLine(new AddQuotationLineInput
            {
                ItemType = QuotationItemType.VehicleRental, Description = "Camry",
                Quantity = 1, UnitPriceSar = 5_000m, NowUtc = Now,
            });

            if (submitForApproval)
            {
                var tier = ApprovalTier.Create(TenantId, 1, "ROLE_SALES_MGR", 0m, Now);
                Quotation.SubmitForApproval(new[] { tier }, Now);
            }

            Db.Quotations.Add(Quotation);
            Db.SaveChanges();

            var quotationRepo = new EfQuotationRepository(Db);
            var uow = new InMemoryUow(Db);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            IIdempotencyStore idempotency = new InMemoryIdempotencyStore(memoryCache);
            var tenant = new StubTenantContext(TenantId, UserId);
            var clock = new FixedClock(Now);

            Sut = new RecallQuotationCommandHandler(
                quotationRepo, uow, idempotency, tenant, clock,
                NullLogger<RecallQuotationCommandHandler>.Instance);
        }

        public RecallQuotationCommand BuildCommand(string idemKey = "idem-recall") =>
            new(idemKey, Quotation.Id);

        public void Dispose() => Db.Dispose();

        private sealed class InMemoryUow(AutoLeaseNetDbContext db) : IUnitOfWork
        {
            public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        }

        private sealed class StubTenantContext(Guid tenantId, Guid userId) : ITenantContext
        {
            public Guid TenantId { get; } = tenantId;
            public Guid? CustomerId => null;
            public Guid? UserId { get; } = userId;
            public string UserType => "InternalStaff";
            public IReadOnlyList<Guid> BranchIds => Array.Empty<Guid>();
            public bool IsInternalStaff => true;
            public bool IsSystem => false;
        }

        private sealed class FixedClock(DateTimeOffset now) : IClock
        {
            public DateTimeOffset UtcNow { get; } = now;
        }
    }

    [Fact]
    public async Task Handle_recalls_Draft_quotation_to_Withdrawn()
    {
        using var h = new Harness(submitForApproval: false);

        var result = await h.Sut.Handle(h.BuildCommand(), CancellationToken.None);

        result.Success.Should().BeTrue(because: $"error: {result.ErrorCode} — {result.ErrorMessage}");
        result.Status.Should().Be(QuotationStatus.Withdrawn);

        var persisted = await h.Db.Quotations.SingleAsync();
        persisted.Status.Should().Be(QuotationStatus.Withdrawn);
    }

    [Fact]
    public async Task Handle_recalls_PendingApproval_quotation_to_Withdrawn()
    {
        using var h = new Harness(submitForApproval: true);

        var result = await h.Sut.Handle(h.BuildCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(QuotationStatus.Withdrawn);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_unknown_quotation()
    {
        using var h = new Harness();
        var cmd = new RecallQuotationCommand("idem-bad", Guid.NewGuid());

        var result = await h.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("quotation.not_found");
    }

    [Fact]
    public async Task Handle_idempotency_replay_does_not_double_recall()
    {
        using var h = new Harness(submitForApproval: false);
        var cmd = h.BuildCommand("idem-replay-recall");

        var first = await h.Sut.Handle(cmd, CancellationToken.None);
        var second = await h.Sut.Handle(cmd, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Should().BeEquivalentTo(first);
        (await h.Db.Quotations.CountAsync()).Should().Be(1);
    }
}
