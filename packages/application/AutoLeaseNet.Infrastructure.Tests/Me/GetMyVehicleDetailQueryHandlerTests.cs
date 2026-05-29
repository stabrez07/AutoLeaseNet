using AutoLeaseNet.Application.Me;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Shared;
using AutoLeaseNet.Domain.Vehicles;
using AutoLeaseNet.Infrastructure.Me;
using AutoLeaseNet.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoLeaseNet.Infrastructure.Tests.Me;

/// <summary>
/// Handler-level contract for <see cref="GetMyVehicleDetailQuery"/>. Pins the
/// lease-side EXISTS check (Active/Extended/Suspended only — Closed lease's
/// vehicle does NOT grant access), the empty result when no lease at all, and
/// the missing-CustomerId throw.
/// </summary>
public sealed class GetMyVehicleDetailQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-0000-0000-000000000001");
    private static readonly Guid Customer = Guid.Parse("11111111-aaaa-0000-0000-000000000001");
    private static readonly Guid OtherCustomer = Guid.Parse("22222222-bbbb-0000-0000-000000000002");
    private static readonly Guid BranchA = Guid.Parse("33333333-cccc-0000-0000-000000000003");

    [Fact]
    public async Task Handle_returns_detail_when_caller_has_current_lease_on_vehicle()
    {
        await using var db = NewDb();
        var now = DateTimeOffset.UtcNow;
        var vehicle = AddVehicle(db, plate: "1234", letters: "أ ب ج", make: "Toyota", model: "Camry", year: 2024, now);
        await db.SaveChangesAsync();
        AddLease(db, contractNumber: 1001, Customer, vehicle.Id, LeaseStatus.Active, now);
        await db.SaveChangesAsync();

        var handler = new GetMyVehicleDetailQueryHandler(db, new StubTenantContext(TenantId, Customer));

        var result = await handler.Handle(new GetMyVehicleDetailQuery(vehicle.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(vehicle.Id);
        result.PlateNumber.Should().Be("1234");
        result.PlateLetters.Should().Be("أ ب ج");
        result.Make.Should().Be("Toyota");
        result.Model.Should().Be("Camry");
        result.ModelYear.Should().Be(2024);
        result.Seats.Should().Be(5);
    }

    [Fact]
    public async Task Handle_returns_null_when_only_closed_lease_exists_on_vehicle()
    {
        await using var db = NewDb();
        var now = DateTimeOffset.UtcNow;
        var vehicle = AddVehicle(db, plate: "5678", letters: "د هـ و", make: "Hyundai", model: "Sonata", year: 2023, now);
        await db.SaveChangesAsync();
        // Closed lease — the customer returned the car. Detail must be hidden,
        // matching the My Vehicles list's "currently holding" semantics.
        AddLease(db, contractNumber: 2002, Customer, vehicle.Id, LeaseStatus.Closed, now);
        await db.SaveChangesAsync();

        var handler = new GetMyVehicleDetailQueryHandler(db, new StubTenantContext(TenantId, Customer));

        var result = await handler.Handle(new GetMyVehicleDetailQuery(vehicle.Id), CancellationToken.None);

        result.Should().BeNull(because: "Closed lease releases the vehicle — symmetry with My Vehicles list");
    }

    [Fact]
    public async Task Handle_returns_null_when_caller_has_no_lease_on_vehicle()
    {
        await using var db = NewDb();
        var now = DateTimeOffset.UtcNow;
        var vehicle = AddVehicle(db, plate: "9012", letters: "ز ح ط", make: "Kia", model: "K5", year: 2024, now);
        await db.SaveChangesAsync();
        // Someone else's lease — caller has none.
        AddLease(db, contractNumber: 3003, OtherCustomer, vehicle.Id, LeaseStatus.Active, now);
        await db.SaveChangesAsync();

        var handler = new GetMyVehicleDetailQueryHandler(db, new StubTenantContext(TenantId, Customer));

        var result = await handler.Handle(new GetMyVehicleDetailQuery(vehicle.Id), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_throws_when_CustomerId_missing()
    {
        await using var db = NewDb();
        var handler = new GetMyVehicleDetailQueryHandler(db, new StubTenantContext(TenantId, customerId: null));

        Func<Task> act = () => handler.Handle(new GetMyVehicleDetailQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*customer context*");
    }

    // ---------- helpers ----------

    private static AutoLeaseNetDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AutoLeaseNetDbContext(options);
    }

    private static Vehicle AddVehicle(
        AutoLeaseNetDbContext db, string plate, string letters, string make, string model, int year, DateTimeOffset now)
    {
        var v = Vehicle.Create(new VehicleCreateInput
        {
            TenantId = TenantId,
            PlateNumber = plate,
            PlateLetters = letters,
            PlateTypeCode = 1,
            Vin = $"VIN-{plate}-{Guid.NewGuid():N}",
            Make = make,
            Model = model,
            ModelYear = year,
            OwnerBranchId = BranchA,
            FuelType = FuelType.Petrol91,
            TransmissionType = TransmissionType.Automatic,
            BodyType = BodyType.Sedan,
            Seats = 5,
            CurrentKm = 10000,
            NowUtc = now,
        });
        db.Vehicles.Add(v);
        return v;
    }

    private static void AddLease(
        AutoLeaseNetDbContext db, long contractNumber, Guid customerId, Guid vehicleId,
        LeaseStatus targetStatus, DateTimeOffset now)
    {
        var lease = Lease.CreatePending(new CreatePendingInput
        {
            TenantId = TenantId,
            CustomerId = customerId,
            VehicleId = vehicleId,
            TajeerContractNumber = contractNumber,
            IssuanceUrl = $"https://example/{contractNumber}/tok",
            ContractTypeCode = 1,
            ContractStartUtc = now.AddDays(-1),
            ContractEndUtc = now.AddDays(10),
            RentAmount = 200m,
            PaymentMethodCode = 1,
            NowUtc = now.AddDays(-1),
        });
        switch (targetStatus)
        {
            case LeaseStatus.Active:
                lease.MarkIssued(startKm: 9500, startFuelLevelCode: 4, conditionNotes: null, nowUtc: now);
                break;
            case LeaseStatus.Closed:
                lease.MarkIssued(startKm: 9500, startFuelLevelCode: 4, conditionNotes: null, nowUtc: now);
                lease.MarkClosed(
                    closureMainReasonCode: 1, closureSubReasonCode: null,
                    endKm: 10500, returnFuelLevelCode: 4,
                    returnConditionNotes: null, damagesObserved: null, nowUtc: now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetStatus), targetStatus, "Unsupported test status.");
        }
        db.Leases.Add(lease);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public StubTenantContext(Guid tenantId, Guid? customerId)
        {
            TenantId = tenantId;
            CustomerId = customerId;
        }
        public Guid TenantId { get; }
        public Guid? CustomerId { get; }
        public Guid? UserId => null;
        public string UserType => CustomerId is null ? "INTERNAL_STAFF" : "EXTERNAL_INDIVIDUAL";
        public IReadOnlyList<Guid> BranchIds => Array.Empty<Guid>();
        public bool IsInternalStaff => CustomerId is null;
        public bool IsSystem => false;
    }
}
