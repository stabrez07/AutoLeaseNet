using AutoLeaseNet.Adapters.Cache.InMemory;
using AutoLeaseNet.Application.Sales;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Sales;
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
using NSubstitute;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Sales;

public sealed class CreateQuotationCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0002-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("d4d4d4d4-0000-0000-0000-000000000030");
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 10, 0, 0, TimeSpan.Zero);

    private sealed class Harness : IDisposable
    {
        public AutoLeaseNetDbContext Db { get; }
        public CreateQuotationCommandHandler Sut { get; }
        public Customer Customer { get; }

        public Harness()
        {
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            Customer = Customer.CreateB2B(new B2BCreateInput
            {
                TenantId = TenantId,
                LegalName = "Fleet Co",
                CommercialRegistration = "CR-001",
                NowUtc = Now,
            });
            Db.Customers.Add(Customer);
            Db.SaveChanges();

            var quotationRepo = new EfQuotationRepository(Db);
            var customerRepo = new EfCustomerRepository(Db);
            var uow = new InMemoryUow(Db);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            IIdempotencyStore idempotency = new InMemoryIdempotencyStore(memoryCache);
            var tenant = new StubTenantContext(TenantId, UserId);
            var clock = new FixedClock(Now);

            var quoteNumberGenerator = Substitute.For<IQuoteNumberGenerator>();
            quoteNumberGenerator.GenerateAsync(TenantId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("Q-20260607-0001"));

            Sut = new CreateQuotationCommandHandler(
                quotationRepo, customerRepo, quoteNumberGenerator, uow, idempotency, tenant, clock,
                NullLogger<CreateQuotationCommandHandler>.Instance);
        }

        public CreateQuotationCommand BuildCommand(string idemKey = "idem-create-q") => new()
        {
            IdempotencyKey = idemKey,
            CustomerId = Customer.Id,
            ValidUntilDate = DateOnly.FromDateTime(Now.DateTime).AddDays(30),
            ContractType = QuotationContractType.LongTermLease,
            EstimatedDurationMonths = 12,
            Lines =
            [
                new CreateQuotationLineDto(
                    QuotationItemType.VehicleRental,
                    "Toyota Camry 2024",
                    null,
                    1,
                    2000m,
                    0m),
            ],
        };

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
    public async Task Handle_creates_Draft_quotation_and_returns_Success()
    {
        using var h = new Harness();
        var result = await h.Sut.Handle(h.BuildCommand(), CancellationToken.None);

        result.Success.Should().BeTrue(because: $"error: {result.ErrorCode} — {result.ErrorMessage}");
        result.Status.Should().Be(QuotationStatus.Draft);
        result.QuoteNumber.Should().Be("Q-20260607-0001");
        result.QuotationId.Should().NotBeNull().And.NotBe(Guid.Empty);
        result.TotalSar.Should().BeGreaterThan(0m);

        var persisted = await h.Db.Quotations.Include(q => q.Lines).SingleAsync();
        persisted.Status.Should().Be(QuotationStatus.Draft);
        persisted.Lines.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_returns_failure_when_customer_not_found()
    {
        using var h = new Harness();
        var cmd = h.BuildCommand("idem-no-cust") with { CustomerId = Guid.NewGuid() };

        var result = await h.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("quotation.customer_not_found");
    }

    [Fact]
    public async Task Handle_returns_failure_when_ValidUntilDate_is_in_the_past()
    {
        using var h = new Harness();
        var cmd = h.BuildCommand("idem-past-date") with
        {
            ValidUntilDate = DateOnly.FromDateTime(Now.DateTime).AddDays(-1),
        };

        var result = await h.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("quotation.invalid_valid_until");
    }

    [Fact]
    public async Task Handle_idempotency_replay_returns_cached_result_without_double_create()
    {
        using var h = new Harness();
        var cmd = h.BuildCommand("idem-replay-create");

        var first = await h.Sut.Handle(cmd, CancellationToken.None);
        var second = await h.Sut.Handle(cmd, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Should().BeEquivalentTo(first, because: "idempotent replay returns the cached result");
        (await h.Db.Quotations.CountAsync()).Should().Be(1, because: "second call must not write another row");
    }
}
