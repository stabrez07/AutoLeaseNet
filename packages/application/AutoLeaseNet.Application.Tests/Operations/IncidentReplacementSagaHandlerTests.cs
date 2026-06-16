using AutoLeaseNet.Adapters.Common.Result;
using AutoLeaseNet.Adapters.Tajeer.Contracts.Dtos;
using AutoLeaseNet.Adapters.Tajeer.InMemory.Contracts;
using AutoLeaseNet.Application.Leases;
using AutoLeaseNet.Application.Notifications;
using AutoLeaseNet.Application.Operations.Notifications;
using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Branches;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Drivers;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Operations;
using AutoLeaseNet.Domain.RentPolicies;
using AutoLeaseNet.Domain.Vehicles;
using AutoLeaseNet.Infrastructure.Persistence;
using AutoLeaseNet.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Operations;

public sealed class IncidentReplacementSagaHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_when_no_replacement_vehicle_available_does_not_create_new_lease()
    {
        using var h = new Harness();
        var seeded = h.Seed(totalLoss: true, withReplacementVehicle: false, reserveReplacementVehicle: false);

        await h.Sut.Handle(new DomainEventNotification<IncidentReportedDomainEvent>(seeded.Event), CancellationToken.None);

        await h.Mediator.DidNotReceive().Send(Arg.Any<SaveContractCommand>(), Arg.Any<CancellationToken>());
        var incident = await h.Db.Incidents.SingleAsync(i => i.Id == seeded.Incident.Id);
        incident.ReplacementLeaseId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_when_old_close_fails_compensates_by_cancelling_new_lease_and_releasing_vehicle()
    {
        var tajeer = new InMemoryTajeerContractClient(
            closeFactory: _ => IntegrationResult<CloseContractResponse>.Failure(
                errorCode: "tajeer.http.503", errorMessage: "down", isTransient: true));
        using var h = new Harness(tajeer);
        var seeded = h.Seed(totalLoss: true, withReplacementVehicle: true, reserveReplacementVehicle: false);

        h.Mediator
            .Send(Arg.Any<SaveContractCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SaveContractCommandResult(
                Success: true,
                LeaseId: seeded.NewLease!.Id,
                TajeerContractNumber: seeded.NewLease.TajeerContractNumber,
                IssuanceUrl: seeded.NewLease.IssuanceUrl,
                ErrorCode: null,
                ErrorMessage: null,
                IsTransient: false));

        await h.Sut.Handle(new DomainEventNotification<IncidentReportedDomainEvent>(seeded.Event), CancellationToken.None);

        var incident = await h.Db.Incidents.SingleAsync(i => i.Id == seeded.Incident.Id);
        var newLease = await h.Db.Leases.SingleAsync(l => l.Id == seeded.NewLease!.Id);
        var replacement = await h.Db.Vehicles.SingleAsync(v => v.Id == seeded.ReplacementVehicle!.Id);
        var oldLease = await h.Db.Leases.SingleAsync(l => l.Id == seeded.OldLease.Id);

        incident.ReplacementLeaseId.Should().Be(seeded.NewLease!.Id);
        newLease.Status.Should().Be(LeaseStatus.Cancelled);
        replacement.Status.Should().Be(VehicleStatus.Available);
        oldLease.Status.Should().Be(LeaseStatus.Active);
        tajeer.CloseCalls.Should().ContainSingle();
        tajeer.CancelCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_when_event_not_requiring_replacement_is_noop()
    {
        using var h = new Harness();
        var seeded = h.Seed(totalLoss: false, withReplacementVehicle: true, reserveReplacementVehicle: false);

        await h.Sut.Handle(new DomainEventNotification<IncidentReportedDomainEvent>(seeded.Event), CancellationToken.None);

        await h.Mediator.DidNotReceive().Send(Arg.Any<SaveContractCommand>(), Arg.Any<CancellationToken>());
    }

    private sealed class Harness : IDisposable
    {
        public AutoLeaseNetDbContext Db { get; }
        public IMediator Mediator { get; }
        public IncidentReplacementSagaHandler Sut { get; }

        public Harness(InMemoryTajeerContractClient? tajeer = null)
        {
            Db = new AutoLeaseNetDbContext(new DbContextOptionsBuilder<AutoLeaseNetDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            Mediator = Substitute.For<IMediator>();
            var incidents = new EfIncidentRepository(Db);
            var leases = new EfLeaseRepository(Db);
            var vehicles = new EfVehicleRepository(Db);
            var clock = new FixedClock(Now);
            var tajeerClient = tajeer ?? new InMemoryTajeerContractClient();
            Sut = new IncidentReplacementSagaHandler(
                incidents,
                leases,
                vehicles,
                new InMemoryUow(Db),
                tajeerClient,
                Mediator,
                clock,
                NullLogger<IncidentReplacementSagaHandler>.Instance);
        }

        public SeedResult Seed(bool totalLoss, bool withReplacementVehicle, bool reserveReplacementVehicle)
        {
            var branch = Branch.Create(new BranchCreateInput
            {
                TenantId = TenantId,
                Code = "RUH-01",
                NameEn = "HQ",
                NameAr = "المقر",
                TajeerBranchId = 1,
                TajeerOperatorId = 1001,
                NowUtc = Now,
            });
            var customer = Customer.CreateB2C(new B2CCreateInput
            {
                TenantId = TenantId,
                PersonNameEn = "Ahmed",
                IdTypeCode = 1,
                PersonIdNumber = "1234567890",
                Mobile = "+966500000001",
                NowUtc = Now,
            });
            var driver = Driver.Create(new DriverCreateInput
            {
                TenantId = TenantId,
                PersonNameEn = "Ahmed",
                IdTypeCode = 1,
                PersonIdNumber = "1234567890",
                DriverLicenseNumber = "DL-123",
                LicenseExpiryDate = new DateOnly(2028, 1, 1),
                NowUtc = Now,
            });
            var rentPolicy = RentPolicy.Create(new RentPolicyCreateInput
            {
                TenantId = TenantId,
                Code = "STD",
                NameEn = "Standard",
                NameAr = "قياسي",
                BaseDailyRate = 100m,
                AllowedKmPerDay = 300,
                ExtraKmFee = 0.5m,
                TajeerRentPolicyId = 1,
                NowUtc = Now,
            });

            var oldVehicle = Vehicle.Create(new VehicleCreateInput
            {
                TenantId = TenantId,
                PlateNumber = "1111",
                PlateLetters = "أ ب ج",
                PlateTypeCode = 1,
                Vin = "VIN-OLD",
                Make = "Toyota",
                Model = "Camry",
                ModelYear = 2024,
                BodyType = BodyType.Sedan,
                Seats = 5,
                OwnerBranchId = branch.Id,
                CurrentBranchId = branch.Id,
                CurrentKm = 20000,
                NowUtc = Now,
            });
            oldVehicle.Reserve(Now);
            oldVehicle.StartRental(Now);

            Vehicle? replacement = null;
            if (withReplacementVehicle)
            {
                replacement = Vehicle.Create(new VehicleCreateInput
                {
                    TenantId = TenantId,
                    PlateNumber = "2222",
                    PlateLetters = "د هـ و",
                    PlateTypeCode = 1,
                    Vin = "VIN-NEW",
                    Make = "Toyota",
                    Model = "Camry",
                    ModelYear = 2024,
                    BodyType = BodyType.Sedan,
                    Seats = 5,
                    OwnerBranchId = branch.Id,
                    CurrentBranchId = branch.Id,
                    CurrentKm = 10000,
                    NowUtc = Now,
                });
                if (reserveReplacementVehicle)
                {
                    replacement.Reserve(Now);
                }
                Db.Vehicles.Add(replacement);
            }

            var oldLease = Lease.CreatePending(new CreatePendingInput
            {
                TenantId = TenantId,
                CustomerId = customer.Id,
                VehicleId = oldVehicle.Id,
                PrimaryDriverId = driver.Id,
                RentPolicyId = rentPolicy.Id,
                WorkingBranchId = branch.Id,
                ReceiveBranchId = branch.Id,
                ReturnBranchId = branch.Id,
                TajeerContractNumber = 1111,
                TajeerIssuanceToken = "tok-old",
                IssuanceUrl = "https://example/old",
                TajeerWorkingBranchId = 1,
                TajeerReceiveBranchId = 1,
                TajeerReturnBranchId = 1,
                TajeerRentPolicyId = 1,
                TajeerOperatorId = 1001,
                ContractTypeCode = 1,
                ContractStartUtc = Now.AddDays(-2),
                ContractEndUtc = Now.AddDays(5),
                AllowedKmPerDay = 300,
                AllowedKmPerHour = 10,
                UnlimitedKm = false,
                AllowedLateHours = 2,
                RentAmount = 300m,
                PaidAmount = 100m,
                RemainingAmount = 200m,
                VatAmount = 45m,
                TotalAmount = 345m,
                PaymentMethodCode = 1,
                NowUtc = Now.AddDays(-2),
            });
            oldLease.MarkIssued(20000, 3, "ok", Now.AddDays(-2));

            Lease? newLease = null;
            if (replacement is not null)
            {
                newLease = Lease.CreatePending(new CreatePendingInput
                {
                    TenantId = TenantId,
                    CustomerId = customer.Id,
                    VehicleId = replacement.Id,
                    PrimaryDriverId = driver.Id,
                    RentPolicyId = rentPolicy.Id,
                    WorkingBranchId = branch.Id,
                    ReceiveBranchId = branch.Id,
                    ReturnBranchId = branch.Id,
                    TajeerContractNumber = 2222,
                    TajeerIssuanceToken = "tok-new",
                    IssuanceUrl = "https://example/new",
                    TajeerWorkingBranchId = 1,
                    TajeerReceiveBranchId = 1,
                    TajeerReturnBranchId = 1,
                    TajeerRentPolicyId = 1,
                    TajeerOperatorId = 1001,
                    ContractTypeCode = 1,
                    ContractStartUtc = Now,
                    ContractEndUtc = Now.AddDays(7),
                    AllowedKmPerDay = 300,
                    AllowedKmPerHour = 10,
                    UnlimitedKm = false,
                    AllowedLateHours = 2,
                    RentAmount = 350m,
                    PaidAmount = 0m,
                    RemainingAmount = 350m,
                    VatAmount = 52.5m,
                    TotalAmount = 402.5m,
                    PaymentMethodCode = 1,
                    NowUtc = Now,
                });
                Db.Leases.Add(newLease);
            }

            var incident = Incident.Report(new ReportIncidentInput
            {
                TenantId = TenantId,
                VehicleId = oldVehicle.Id,
                LeaseId = oldLease.Id,
                ReportedByPersonId = driver.Id,
                Type = IncidentType.TrafficAccident,
                Severity = totalLoss ? IncidentSeverity.TotalLoss : IncidentSeverity.Minor,
                IncidentTimeUtc = Now.AddHours(-1),
                Description = "incident",
                NowUtc = Now,
            });
            var evt = (IncidentReportedDomainEvent)incident.DomainEvents.Single();

            Db.Branches.Add(branch);
            Db.Customers.Add(customer);
            Db.Drivers.Add(driver);
            Db.RentPolicies.Add(rentPolicy);
            Db.Vehicles.Add(oldVehicle);
            Db.Leases.Add(oldLease);
            Db.Incidents.Add(incident);
            Db.SaveChanges();

            return new SeedResult(incident, evt, oldLease, newLease, replacement);
        }

        public void Dispose() => Db.Dispose();
    }

    private sealed class InMemoryUow(AutoLeaseNetDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed record SeedResult(
        Incident Incident,
        IncidentReportedDomainEvent Event,
        Lease OldLease,
        Lease? NewLease,
        Vehicle? ReplacementVehicle);
}
