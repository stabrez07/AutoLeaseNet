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
/// Handler-level contract for <see cref="GetMyVehiclesQuery"/>. Pins:
/// (1) the lease-status filter (Active / Extended / Suspended only — Closed,
///     Cancelled, Expired, PendingIssuance excluded), (2) empty list when the
///     caller has no leases, (3) missing CustomerId throws so the BFF can map
///     to 400 like <c>/me/leases</c>.
///
/// EF InMemory has no RLS, so the handler's filtering must be visible in the
/// projection itself (lease-status filter + vehicle-id set), not delegated to a
/// DB predicate. The SystemTenancyScope inside the handler is a no-op here —
/// its real value is the SQL-side RLS bypass which is covered in
/// <c>RlsIsolationTests</c>.
/// </summary>
public sealed class GetMyVehiclesQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-0000-0000-000000000001");
    private static readonly Guid Customer = Guid.Parse("11111111-aaaa-0000-0000-000000000001");
    private static readonly Guid OtherCustomer = Guid.Parse("22222222-bbbb-0000-0000-000000000002");
    private static readonly Guid BranchA = Guid.Parse("33333333-cccc-0000-0000-000000000003");

    [Fact]
    public async Task Handle_returns_vehicles_for_Active_Extended_Suspended_leases_excludes_Closed()
    {
        await using var db = NewDb();
        var now = DateTimeOffset.UtcNow;

        var vActive = AddVehicle(db, plate: "1234", letters: "أ ب ج", make: "Toyota", model: "Camry", year: 2024, now);
        var vExtended = AddVehicle(db, plate: "5678", letters: "د هـ و", make: "Hyundai", model: "Sonata", year: 2023, now);
        var vSuspended = AddVehicle(db, plate: "9012", letters: "ز ح ط", make: "Kia", model: "K5", year: 2024, now);
        var vClosed = AddVehicle(db, plate: "3456", letters: "ي ك ل", make: "Nissan", model: "Sunny", year: 2022, now);
        var vOther = AddVehicle(db, plate: "7890", letters: "م ن س", make: "Lexus", model: "ES", year: 2024, now);
        await db.SaveChangesAsync();

        AddLease(db, contractNumber: 1001, Customer, vActive.Id, LeaseStatus.Active, now);
        AddLease(db, contractNumber: 1002, Customer, vExtended.Id, LeaseStatus.Extended, now);
        AddLease(db, contractNumber: 1003, Customer, vSuspended.Id, LeaseStatus.Suspended, now);
        AddLease(db, contractNumber: 1004, Customer, vClosed.Id, LeaseStatus.Closed, now);
        AddLease(db, contractNumber: 1005, OtherCustomer, vOther.Id, LeaseStatus.Active, now);
        await db.SaveChangesAsync();

        var handler = new GetMyVehiclesQueryHandler(db, new StubTenantContext(TenantId, Customer));

        var result = await handler.Handle(new GetMyVehiclesQuery(), CancellationToken.None);

        result.Select(v => v.Id).Should().BeEquivalentTo(new[] { vActive.Id, vExtended.Id, vSuspended.Id },
            because: "only Active/Extended/Suspended leases of the calling customer count");
        result.Should().NotContain(v => v.Id == vClosed.Id, because: "Closed leases released the vehicle");
        // OtherCustomer's vehicle is filtered out by the lease-side CustomerId filter (not RLS here).
        result.Should().NotContain(v => v.Id == vOther.Id, because: "other customer's vehicle must not leak");

        var camry = result.Single(v => v.Id == vActive.Id);
        camry.PlateNumber.Should().Be("1234");
        camry.PlateLetters.Should().Be("أ ب ج");
        camry.Make.Should().Be("Toyota");
        camry.Model.Should().Be("Camry");
        camry.ModelYear.Should().Be(2024);
    }

    [Fact]
    public async Task Handle_returns_empty_when_caller_has_no_leases()
    {
        await using var db = NewDb();
        var handler = new GetMyVehiclesQueryHandler(db, new StubTenantContext(TenantId, Customer));

        var result = await handler.Handle(new GetMyVehiclesQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_throws_when_CustomerId_missing()
    {
        await using var db = NewDb();
        var handler = new GetMyVehiclesQueryHandler(db, new StubTenantContext(TenantId, customerId: null));

        Func<Task> act = () => handler.Handle(new GetMyVehiclesQuery(), CancellationToken.None);

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
        AutoLeaseNetDbContext db,
        long contractNumber,
        Guid customerId,
        Guid vehicleId,
        LeaseStatus targetStatus,
        DateTimeOffset now)
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
            case LeaseStatus.Extended:
                lease.MarkIssued(startKm: 9500, startFuelLevelCode: 4, conditionNotes: null, nowUtc: now);
                lease.IncrementExtension(newEndUtc: now.AddDays(20), nowUtc: now);
                break;
            case LeaseStatus.Suspended:
                lease.MarkIssued(startKm: 9500, startFuelLevelCode: 4, conditionNotes: null, nowUtc: now);
                lease.MarkSuspended(suspensionReasonCode: 1, nowUtc: now);
                break;
            case LeaseStatus.Closed:
                lease.MarkIssued(startKm: 9500, startFuelLevelCode: 4, conditionNotes: null, nowUtc: now);
                lease.MarkClosed(
                    closureMainReasonCode: 1,
                    closureSubReasonCode: null,
                    endKm: 10500,
                    returnFuelLevelCode: 4,
                    returnConditionNotes: null,
                    damagesObserved: null,
                    nowUtc: now);
                break;
            case LeaseStatus.PendingIssuance:
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
