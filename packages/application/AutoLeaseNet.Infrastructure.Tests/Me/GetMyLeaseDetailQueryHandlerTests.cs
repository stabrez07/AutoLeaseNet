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
/// Handler-level contract for <see cref="GetMyLeaseDetailQuery"/>. Pins:
/// (1) returns the full DTO when the caller owns the lease — vehicle nested
///     when assigned, payment + timeline populated;
/// (2) returns null when the lease isn't visible to the caller (so the
///     endpoint can map to 404);
/// (3) throws when CustomerId is missing (BFF maps to 400).
/// </summary>
public sealed class GetMyLeaseDetailQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-0000-0000-000000000001");
    private static readonly Guid Customer = Guid.Parse("11111111-aaaa-0000-0000-000000000001");
    private static readonly Guid OtherCustomer = Guid.Parse("22222222-bbbb-0000-0000-000000000002");
    private static readonly Guid BranchA = Guid.Parse("33333333-cccc-0000-0000-000000000003");

    [Fact]
    public async Task Handle_returns_full_detail_with_vehicle_for_caller_owned_lease()
    {
        await using var db = NewDb();
        var now = DateTimeOffset.UtcNow;
        var vehicle = AddVehicle(db, plate: "1234", letters: "أ ب ج", make: "Toyota", model: "Camry", year: 2024, now);
        await db.SaveChangesAsync();

        var leaseId = AddActiveLease(db, contractNumber: 1001, Customer, vehicleId: vehicle.Id, now);
        await db.SaveChangesAsync();

        var handler = new GetMyLeaseDetailQueryHandler(db, new StubTenantContext(TenantId, Customer));

        var result = await handler.Handle(new GetMyLeaseDetailQuery(leaseId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(leaseId);
        result.TajeerContractNumber.Should().Be(1001);
        result.Status.Should().Be((int)LeaseStatus.Active);
        result.RentAmount.Should().Be(200m);
        result.Vehicle.Should().NotBeNull();
        result.Vehicle!.Id.Should().Be(vehicle.Id);
        result.Vehicle.PlateNumber.Should().Be("1234");
        result.Vehicle.PlateLetters.Should().Be("أ ب ج");
        result.Vehicle.Make.Should().Be("Toyota");
        result.IssuedAtUtc.Should().NotBeNull(because: "lease was MarkIssued in setup");
        result.SavedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_returns_null_when_lease_belongs_to_other_customer()
    {
        await using var db = NewDb();
        var now = DateTimeOffset.UtcNow;
        // No real RLS under InMemory, so the handler must enforce the
        // CustomerId match in code to keep the InMemory path honest.
        var foreignLeaseId = AddActiveLease(db, contractNumber: 9999, OtherCustomer, vehicleId: null, now);
        await db.SaveChangesAsync();

        var handler = new GetMyLeaseDetailQueryHandler(db, new StubTenantContext(TenantId, Customer));

        var result = await handler.Handle(new GetMyLeaseDetailQuery(foreignLeaseId), CancellationToken.None);

        result.Should().BeNull(because: "lease belongs to a different customer — handler must hide it for InMemory parity with RLS");
    }

    [Fact]
    public async Task Handle_throws_when_CustomerId_missing()
    {
        await using var db = NewDb();
        var handler = new GetMyLeaseDetailQueryHandler(db, new StubTenantContext(TenantId, customerId: null));

        Func<Task> act = () => handler.Handle(new GetMyLeaseDetailQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*customer context*");
    }

    [Fact]
    public async Task Handle_returns_detail_without_vehicle_when_lease_has_no_VehicleId()
    {
        await using var db = NewDb();
        var now = DateTimeOffset.UtcNow;
        var leaseId = AddActiveLease(db, contractNumber: 1002, Customer, vehicleId: null, now);
        await db.SaveChangesAsync();

        var handler = new GetMyLeaseDetailQueryHandler(db, new StubTenantContext(TenantId, Customer));

        var result = await handler.Handle(new GetMyLeaseDetailQuery(leaseId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Vehicle.Should().BeNull(because: "Day-5 leases can have null VehicleId until Day-D reshape");
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

    private static Guid AddActiveLease(
        AutoLeaseNetDbContext db, long contractNumber, Guid customerId, Guid? vehicleId, DateTimeOffset now)
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
        lease.MarkIssued(startKm: 9500, startFuelLevelCode: 4, conditionNotes: null, nowUtc: now);
        db.Leases.Add(lease);
        return lease.Id;
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
