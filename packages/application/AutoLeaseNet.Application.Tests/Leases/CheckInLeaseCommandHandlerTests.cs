using AutoLeaseNet.Adapters.Cache.InMemory;
using AutoLeaseNet.Application.Leases;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Branches;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Drivers;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Operations;
using AutoLeaseNet.Domain.Vehicles;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Leases;

/// <summary>
/// Day-19 check-in saga handler tests. Each test uses an EF Core InMemory DbContext
/// with a hand-rolled fixture (Customer + Vehicle + Driver + Branch + Lease in the
/// right starting state).
/// </summary>
public sealed class CheckInLeaseCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("a1a1a1a1-0001-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 5, 25, 14, 0, 0, TimeSpan.Zero);

    private sealed class Harness : IDisposable
    {
        public AutoLeaseNetDbContext Db { get; }
        public CheckInLeaseCommandHandler Sut { get; }
        public Lease Lease { get; }
        public Vehicle Vehicle { get; }

        public Harness(LeaseStatus startingStatus = LeaseStatus.Active, int currentKm = 50_000)
        {
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

            var branch = Branch.Create(new BranchCreateInput
            {
                TenantId = TenantId, Code = "T-1", NameEn = "Test", NameAr = "اختبار",
                CityEn = "Riyadh", CityAr = "الرياض", RegionEn = "Riyadh", RegionAr = "منطقة الرياض",
                LicenseNumber = "L-1", Address = "Test", PhoneNumber = "+966112345678",
                Latitude = 24m, Longitude = 46m,
                TajeerBranchId = 1, TajeerOperatorId = 1, NowUtc = Now,
            });
            Vehicle = Vehicle.Create(new VehicleCreateInput
            {
                TenantId = TenantId, PlateNumber = "1234", PlateLetters = "ا ب ج", PlateTypeCode = 1,
                Vin = "TESTVIN1234567890", Make = "Toyota", Model = "Camry", ModelYear = 2024,
                OwnerBranchId = branch.Id, CurrentKm = currentKm, NowUtc = Now,
            });
            Vehicle.Reserve(Now); Vehicle.StartRental(Now); // OnRent

            Lease = Lease.CreatePending(new CreatePendingInput
            {
                TenantId = TenantId, VehicleId = Vehicle.Id,
                TajeerContractNumber = 5000, IssuanceUrl = "https://x/y",
                ContractTypeCode = 1,
                ContractStartUtc = Now.AddDays(-3), ContractEndUtc = Now.AddDays(-1),
                RentAmount = 500m, PaymentMethodCode = 1,
                NowUtc = Now.AddDays(-3),
            });
            if (startingStatus is LeaseStatus.Active or LeaseStatus.Extended or LeaseStatus.Suspended)
                Lease.MarkIssued(currentKm, 4, null, Now.AddDays(-3).AddMinutes(30));
            if (startingStatus is LeaseStatus.Suspended)
                Lease.MarkSuspended(2, Now.AddHours(-1));
            if (startingStatus is LeaseStatus.Extended)
                Lease.IncrementExtension(Now.AddDays(1), Now.AddDays(-1));

            Db.Branches.Add(branch); Db.Vehicles.Add(Vehicle); Db.Leases.Add(Lease);
            Db.SaveChanges();

            var leaseRepo = new EfLeaseRepository(Db);
            var vehicleRepo = new EfVehicleRepository(Db);
            var inspectionRepo = new EfInspectionRepository(Db);
            var uow = new InMemoryUow(Db);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            IIdempotencyStore idempotency = new InMemoryIdempotencyStore(memoryCache);
            var tenant = new StubTenantContext(TenantId);
            var clock = new FixedClock(Now);

            Sut = new CheckInLeaseCommandHandler(leaseRepo, vehicleRepo, inspectionRepo,
                uow, idempotency, tenant, clock,
                NullLogger<CheckInLeaseCommandHandler>.Instance);
        }

        public CheckInLeaseCommand BuildCommand(string idempotencyKey = "idem-checkin", int? endKm = null) => new()
        {
            IdempotencyKey = idempotencyKey,
            LeaseId = Lease.Id,
            OdometerKm = endKm ?? (Vehicle.CurrentKm + 320),
            FuelLevel = FuelLevel.Half,
            ClosureMainReasonCode = 1,
            ReturnConditionNotes = "Returned clean",
        };

        public void Dispose() => Db.Dispose();

        private sealed class InMemoryUow(AutoLeaseNetDbContext db) : IUnitOfWork
        {
            public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        }

        private sealed class StubTenantContext(Guid tenantId) : ITenantContext
        {
            public Guid TenantId { get; } = tenantId;
            public Guid? CustomerId => null;
            public Guid? UserId { get; } = Guid.Parse("d4d4d4d4-0000-0000-0000-000000000030");
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
    public async Task Handle_closes_Active_lease_and_returns_vehicle()
    {
        using var h = new Harness(startingStatus: LeaseStatus.Active);
        var startKm = h.Vehicle.CurrentKm;
        var expectedEndKm = startKm + 320;

        var result = await h.Sut.Handle(h.BuildCommand(), CancellationToken.None);

        result.Success.Should().BeTrue(because: $"error was {result.ErrorCode} — {result.ErrorMessage}");
        result.LeaseStatus.Should().Be(nameof(LeaseStatus.Closed));

        var lease = await h.Db.Leases.SingleAsync();
        lease.Status.Should().Be(LeaseStatus.Closed);
        lease.EndKm.Should().Be(expectedEndKm);
        lease.ClosedAtUtc.Should().NotBeNull();

        var vehicle = await h.Db.Vehicles.SingleAsync();
        vehicle.Status.Should().Be(VehicleStatus.Available);
        vehicle.CurrentKm.Should().Be(expectedEndKm);

        var inspection = await h.Db.Inspections.SingleAsync();
        inspection.Type.Should().Be(InspectionType.CheckIn);
        inspection.Status.Should().Be(InspectionStatus.Completed);
        inspection.LeaseId.Should().Be(lease.Id);
    }

    [Fact]
    public async Task Handle_closes_Extended_and_Suspended_leases_too()
    {
        using var hExt = new Harness(startingStatus: LeaseStatus.Extended);
        var rExt = await hExt.Sut.Handle(hExt.BuildCommand("k-ext"), CancellationToken.None);
        rExt.Success.Should().BeTrue();

        using var hSus = new Harness(startingStatus: LeaseStatus.Suspended);
        var rSus = await hSus.Sut.Handle(hSus.BuildCommand("k-sus"), CancellationToken.None);
        rSus.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_returns_422_for_unknown_lease()
    {
        using var h = new Harness();
        var cmd = h.BuildCommand("k-unknown") with { LeaseId = Guid.NewGuid() };

        var result = await h.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("lease.not_found");
    }

    [Fact]
    public async Task Handle_returns_422_when_lease_in_PendingIssuance()
    {
        using var h = new Harness(startingStatus: LeaseStatus.PendingIssuance);

        var result = await h.Sut.Handle(h.BuildCommand("k-pending"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("lease.invalid_state_for_check_in");
    }

    [Fact]
    public async Task Handle_returns_422_on_odometer_regression()
    {
        using var h = new Harness(startingStatus: LeaseStatus.Active, currentKm: 60_000);
        var cmd = h.BuildCommand("k-regress", endKm: 59_999);

        var result = await h.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("inspection.odometer_regression");
    }

    [Fact]
    public async Task Handle_idempotency_replay_returns_cached_result_without_double_close()
    {
        using var h = new Harness(startingStatus: LeaseStatus.Active);
        var cmd = h.BuildCommand("k-replay");

        var first = await h.Sut.Handle(cmd, CancellationToken.None);
        var second = await h.Sut.Handle(cmd, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Should().BeEquivalentTo(first, because: "idempotent replay returns the cached envelope");
        (await h.Db.Inspections.CountAsync()).Should().Be(1, because: "second call must not write another CHECK_IN row");
    }
}
