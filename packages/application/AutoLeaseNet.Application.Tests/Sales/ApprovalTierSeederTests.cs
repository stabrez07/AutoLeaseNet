using AutoLeaseNet.Adapters.Seed;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Sales;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Sales;

public sealed class ApprovalTierSeederTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Running_seed_twice_does_not_duplicate_approval_tiers()
    {
        await using var db = NewDb();
        var seeder = new BogusDataSeeder(
            new SeedOptions { Mode = SeedMode.Demo, TenantId = TenantId },
            new EfCustomerRepository(db),
            new EfVehicleRepository(db),
            new EfDriverRepository(db),
            new EfBranchRepository(db),
            new EfRentPolicyRepository(db),
            new EfExtendedCoverageRepository(db),
            new EfLeaseRepository(db),
            new EfInspectionRepository(db),
            new EfIncidentRepository(db),
            new EfApprovalTierRepository(db),
            new InMemoryUow(db),
            new FixedClock(Now),
            NullLogger<BogusDataSeeder>.Instance,
            new EfQuotationRepository(db));

        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);

        var tiers = await db.Set<ApprovalTier>()
            .Where(t => t.TenantId == TenantId)
            .OrderBy(t => t.TierLevel)
            .ToListAsync();

        tiers.Should().HaveCount(3);
        tiers.Select(t => t.TierLevel).Should().Equal((byte)1, (byte)2, (byte)3);
    }

    private static AutoLeaseNetDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AutoLeaseNetDbContext(options);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class InMemoryUow(AutoLeaseNetDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }
}
