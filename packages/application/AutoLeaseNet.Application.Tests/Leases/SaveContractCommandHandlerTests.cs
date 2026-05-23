using AutoLeaseNet.Adapters.Cache.InMemory;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using AutoLeaseNet.Application.Leases;
using AutoLeaseNet.Application.Ports.Idempotency;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Tenancy;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Branches;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Drivers;
using AutoLeaseNet.Domain.ExtendedCoverages;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.RentPolicies;
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
/// Day D — SaveContractCommandHandler against the reshaped domain-driven command.
/// Harness seeds Customer / Vehicle / Driver / Branch / RentPolicy / ExtendedCoverage so
/// the handler's aggregate lookups resolve. Negative tests target each business-rule gate.
/// </summary>
public sealed class SaveContractCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Now = new(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

    private sealed class Harness : IDisposable
    {
        public AutoLeaseNetDbContext Db { get; }
        public InMemoryTajeerContractClient Tajeer { get; }
        public SaveContractCommandHandler Sut { get; }
        public Customer Customer { get; }
        public Vehicle Vehicle { get; }
        public Driver Driver { get; }
        public Branch Branch { get; }
        public RentPolicy RentPolicy { get; }
        public ExtendedCoverage Coverage { get; }

        // Aggregate seeding always uses a real tenant (a domain invariant). The handler's
        // tenant-context guard can be exercised independently via overrideContextTenant.
        public Harness(InMemoryTajeerContractClient? tajeer = null, Guid? overrideContextTenant = null)
        {
            var effectiveTenant = TenantId;
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options);
            Tajeer = tajeer ?? new InMemoryTajeerContractClient();

            // Seed referenceable aggregates so lookups succeed.
            Branch = Branch.Create(new BranchCreateInput
            {
                TenantId = effectiveTenant,
                Code = "RUH-01", NameEn = "Olaya HQ", NameAr = "العليا",
                TajeerBranchId = 1, TajeerOperatorId = 1001, NowUtc = Now,
            });
            RentPolicy = RentPolicy.Create(new RentPolicyCreateInput
            {
                TenantId = effectiveTenant,
                Code = "STD-DAILY", NameEn = "Standard Daily", NameAr = "يومي قياسي",
                BaseDailyRate = 150m, AllowedKmPerDay = 300, ExtraKmFee = 0.5m,
                TajeerRentPolicyId = 1, NowUtc = Now,
            });
            Coverage = ExtendedCoverage.Create(new ExtendedCoverageCreateInput
            {
                TenantId = effectiveTenant,
                Code = "CDW-FULL", NameEn = "Full CDW", NameAr = "شامل",
                CoverageType = CoverageType.FullCdw, DailyRate = 25m, DeductibleAmount = 500m,
                TajeerExtendedCoverageId = 2, NowUtc = Now,
            });
            Customer = Customer.CreateB2C(new B2CCreateInput
            {
                TenantId = effectiveTenant,
                PersonNameEn = "Ahmed Test", PersonNameAr = "أحمد",
                IdTypeCode = 1, PersonIdNumber = "1234567890",
                Mobile = "+966500000001", Email = "ahmed@example.sa",
                NationalAddress = "RIYD1234-12345", NationalityCode = "SA",
                NowUtc = Now,
            });
            Vehicle = Vehicle.Create(new VehicleCreateInput
            {
                TenantId = effectiveTenant,
                PlateNumber = "1234", PlateLetters = "أ ب ج", PlateTypeCode = 1,
                Vin = "VIN12345678", Make = "Toyota", Model = "Camry", ModelYear = 2024,
                OwnerBranchId = Branch.Id, CurrentBranchId = Branch.Id, CurrentKm = 5000,
                NowUtc = Now,
            });
            Driver = Driver.Create(new DriverCreateInput
            {
                TenantId = effectiveTenant,
                PersonNameEn = "Ahmed Test", PersonNameAr = "أحمد",
                IdTypeCode = 1, PersonIdNumber = "1234567890",
                DriverLicenseNumber = "DL-1234567890",
                LicenseExpiryDate = new DateOnly(2028, 6, 1),
                NationalityCode = "SA", Mobile = "+966500000001", NationalAddress = "RIYD1234-12345",
                NowUtc = Now,
            });

            Db.Branches.Add(Branch); Db.RentPolicies.Add(RentPolicy); Db.ExtendedCoverages.Add(Coverage);
            Db.Customers.Add(Customer); Db.Vehicles.Add(Vehicle); Db.Drivers.Add(Driver);
            Db.SaveChanges();

            var leases = new EfLeaseRepository(Db);
            var customers = new EfCustomerRepository(Db);
            var vehicles = new EfVehicleRepository(Db);
            var drivers = new EfDriverRepository(Db);
            var rentPolicies = new EfRentPolicyRepository(Db);
            var coverages = new EfExtendedCoverageRepository(Db);
            var branches = new EfBranchRepository(Db);
            var uow = new InMemoryUow(Db);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            IIdempotencyStore idempotency = new InMemoryIdempotencyStore(memoryCache);
            var tenant = new StubTenantContext(overrideContextTenant ?? effectiveTenant);
            var clock = new FixedClock(Now);

            Sut = new SaveContractCommandHandler(
                Tajeer, leases, customers, vehicles, drivers, rentPolicies, coverages, branches,
                uow, idempotency, tenant, clock,
                NullLogger<SaveContractCommandHandler>.Instance);
        }

        public SaveContractCommand BuildCommand(string idempotencyKey = "idem-001") => new()
        {
            IdempotencyKey = idempotencyKey,
            CustomerId = Customer.Id,
            VehicleId = Vehicle.Id,
            PrimaryDriverId = Driver.Id,
            RentPolicyId = RentPolicy.Id,
            ExtendedCoverageId = Coverage.Id,
            WorkingBranchId = Branch.Id,
            ReceiveBranchId = Branch.Id,
            ReturnBranchId = Branch.Id,
            ContractStartUtc = Now.AddHours(1),
            ContractEndUtc = Now.AddDays(2),
            ContractTypeCode = 1,
            AllowedKmPerDay = 300,
            RentAmount = 200m,
            PaidAmount = 50m,
            PaymentMethodCode = 1,
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
            public Guid? UserId => null;
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
    public async Task Handle_persists_Lease_with_full_FK_refs_on_Tajeer_success()
    {
        using var harness = new Harness();

        var result = await harness.Sut.Handle(harness.BuildCommand("idem-success"), CancellationToken.None);

        result.Success.Should().BeTrue(because: $"error was {result.ErrorCode} — {result.ErrorMessage}");
        result.LeaseId.Should().NotBeNull();
        result.TajeerContractNumber.Should().BeGreaterThan(0);

        var lease = await harness.Db.Leases.SingleAsync();
        lease.Status.Should().Be(LeaseStatus.PendingIssuance);
        lease.CustomerId.Should().Be(harness.Customer.Id);
        lease.VehicleId.Should().Be(harness.Vehicle.Id);
        lease.PrimaryDriverId.Should().Be(harness.Driver.Id);
        lease.RentPolicyId.Should().Be(harness.RentPolicy.Id);
        lease.ExtendedCoverageId.Should().Be(harness.Coverage.Id);
        lease.WorkingBranchId.Should().Be(harness.Branch.Id);
        lease.TajeerWorkingBranchId.Should().Be(harness.Branch.TajeerBranchId);
        lease.TajeerOperatorId.Should().Be(harness.Branch.TajeerOperatorId);
        lease.RentAmount.Should().Be(200m);
        harness.Tajeer.SaveCalls.Should().HaveCount(1);

        // Vehicle reservation invariant.
        var vehicle = await harness.Db.Vehicles.SingleAsync();
        vehicle.Status.Should().Be(VehicleStatus.Reserved);
    }

    [Fact]
    public async Task Handle_returns_422_when_customer_not_found()
    {
        using var harness = new Harness();
        var cmd = harness.BuildCommand("idem-cust") with { CustomerId = Guid.NewGuid() };

        var result = await harness.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("lease.customer.not_found");
        result.IsTransient.Should().BeFalse();
        harness.Tajeer.SaveCalls.Should().BeEmpty(because: "Tajeer must not be called when validation fails locally");
        (await harness.Db.Leases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_returns_422_when_vehicle_not_available()
    {
        using var harness = new Harness();
        harness.Vehicle.MarkDamaged("Front damage observed", Now);
        await harness.Db.SaveChangesAsync();

        var result = await harness.Sut.Handle(harness.BuildCommand("idem-veh"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("lease.vehicle.not_available");
        harness.Tajeer.SaveCalls.Should().BeEmpty();
        (await harness.Db.Leases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_returns_422_when_driver_license_expired()
    {
        using var harness = new Harness();
        // Replace the driver with one whose license has already expired.
        var expired = Driver.Create(new DriverCreateInput
        {
            TenantId = TenantId,
            PersonNameEn = "Expired Driver",
            IdTypeCode = 1, PersonIdNumber = "9999999999",
            DriverLicenseNumber = "DL-EXPIRED",
            LicenseExpiryDate = new DateOnly(2025, 1, 1),
            NowUtc = Now,
        });
        harness.Db.Drivers.Add(expired);
        await harness.Db.SaveChangesAsync();

        var cmd = harness.BuildCommand("idem-lic") with { PrimaryDriverId = expired.Id };

        var result = await harness.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("lease.driver.license_expired");
        harness.Tajeer.SaveCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_returns_422_when_authorized_driver_not_TAMM_authorized()
    {
        using var harness = new Harness();
        var unauthorized = Driver.Create(new DriverCreateInput
        {
            TenantId = TenantId,
            PersonNameEn = "Authorised Driver",
            IdTypeCode = 1, PersonIdNumber = "1111111111",
            DriverLicenseNumber = "DL-NOAUTH",
            LicenseExpiryDate = new DateOnly(2028, 1, 1),
            NowUtc = Now,
        });
        harness.Db.Drivers.Add(unauthorized);
        await harness.Db.SaveChangesAsync();

        var cmd = harness.BuildCommand("idem-tamm") with { AuthorizedDriverId = unauthorized.Id };

        var result = await harness.Sut.Handle(cmd, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("lease.driver.tamm_not_authorized");
    }

    [Fact]
    public async Task Handle_does_not_persist_Lease_when_Tajeer_returns_business_error()
    {
        var failing = new InMemoryTajeerContractClient(
            _ => AutoLeaseNet.Adapters.Common.Result.IntegrationResult<SaveContractResponse>.Failure(
                errorCode: "tajeer.vendor.server.error.renter.mobile.invalid",
                errorMessage: "Mobile invalid",
                isTransient: false));
        using var harness = new Harness(failing);

        var result = await harness.Sut.Handle(harness.BuildCommand("idem-tajeer-fail"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.LeaseId.Should().BeNull();
        result.ErrorCode.Should().Be("tajeer.vendor.server.error.renter.mobile.invalid");
        (await harness.Db.Leases.CountAsync()).Should().Be(0);

        // Vehicle must NOT be reserved on Tajeer failure.
        var vehicle = await harness.Db.Vehicles.SingleAsync();
        vehicle.Status.Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public async Task Handle_replays_cached_result_for_same_idempotency_key_without_calling_Tajeer()
    {
        using var harness = new Harness();

        var first = await harness.Sut.Handle(harness.BuildCommand("idem-replay"), CancellationToken.None);
        var second = await harness.Sut.Handle(harness.BuildCommand("idem-replay"), CancellationToken.None);

        second.Should().BeEquivalentTo(first);
        harness.Tajeer.SaveCalls.Should().HaveCount(1, because: "the second call is served from the idempotency cache");
        (await harness.Db.Leases.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_throws_when_tenant_context_is_empty()
    {
        using var harness = new Harness(overrideContextTenant: Guid.Empty);

        var act = () => harness.Sut.Handle(harness.BuildCommand("idem-empty-tenant"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*authenticated tenant context*");
    }
}
