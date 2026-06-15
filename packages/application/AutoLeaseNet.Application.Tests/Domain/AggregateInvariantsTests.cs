using AutoLeaseNet.Domain.Branches;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Drivers;
using AutoLeaseNet.Domain.ExtendedCoverages;
using AutoLeaseNet.Domain.RentPolicies;
using AutoLeaseNet.Domain.Vehicles;
using FluentAssertions;
using Xunit;

namespace AutoLeaseNet.Application.Tests.Domain;

/// <summary>
/// A2–A7 — minimum-viable invariants for the 6 new aggregates. Per-aggregate behaviour
/// (KYC verification, TAMM transitions, vehicle status machine, etc.) is exercised by
/// focused tests as the relevant business flows land in later workstreams; this suite
/// proves factories enforce their required fields.
/// </summary>
public sealed class AggregateInvariantsTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 9, 0, 0, TimeSpan.Zero);

    // ─── Customer ───────────────────────────────────────────────────────────
    [Fact]
    public void Customer_B2B_factory_requires_legal_name_and_commercial_registration()
    {
        var act = () => Customer.CreateB2B(new B2BCreateInput
        {
            TenantId = Tenant,
            LegalName = "",
            CommercialRegistration = "1010101010",
            NowUtc = Now,
        });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Customer_B2B_factory_succeeds_with_required_fields()
    {
        var c = Customer.CreateB2B(new B2BCreateInput
        {
            TenantId = Tenant,
            LegalName = "Saudi Aramco",
            LegalNameAr = "أرامكو السعودية",
            CommercialRegistration = "2050000000",
            VatNumber = "300000000000003",
            CreditLimit = 1_000_000m,
            NowUtc = Now,
        });
        c.Type.Should().Be(CustomerType.B2B);
        c.Status.Should().Be(CustomerStatus.Active);
        c.DisplayName.Should().Be("Saudi Aramco");
    }

    [Fact]
    public void Customer_B2C_factory_validates_id_type_range()
    {
        var act = () => Customer.CreateB2C(new B2CCreateInput
        {
            TenantId = Tenant,
            PersonNameEn = "Ahmed Ali",
            IdTypeCode = 99,
            PersonIdNumber = "1234567890",
            NowUtc = Now,
        });
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Customer_Suspend_then_Reactivate_round_trips()
    {
        var c = Customer.CreateB2C(new B2CCreateInput
        {
            TenantId = Tenant,
            PersonNameEn = "Sara Mohammed",
            IdTypeCode = 1,
            PersonIdNumber = "1098765432",
            NowUtc = Now,
        });
        c.Suspend(Now.AddDays(1));
        c.Status.Should().Be(CustomerStatus.Suspended);
        c.Reactivate(Now.AddDays(2));
        c.Status.Should().Be(CustomerStatus.Active);
    }

    // ─── Vehicle ────────────────────────────────────────────────────────────
    [Fact]
    public void Vehicle_factory_creates_Available_status_with_full_fleet_attributes()
    {
        var v = Vehicle.Create(new VehicleCreateInput
        {
            TenantId = Tenant,
            PlateNumber = "1234",
            PlateLetters = "أ ب ج",
            PlateTypeCode = 1,
            Vin = "JTDBT923871234567",
            Make = "Toyota",
            Model = "Camry",
            ModelYear = 2024,
            FuelType = FuelType.Petrol91,
            BodyType = BodyType.Sedan,
            OwnerBranchId = Guid.NewGuid(),
            CurrentKm = 5_400,
            NowUtc = Now,
        });
        v.Status.Should().Be(VehicleStatus.Available);
        v.PlateNumber.Should().Be("1234");
        v.PlateLetters.Should().Be("أ ب ج");
    }

    [Fact]
    public void Vehicle_Return_advances_km_and_returns_to_Available()
    {
        var v = Vehicle.Create(new VehicleCreateInput
        {
            TenantId = Tenant,
            PlateNumber = "5678", PlateLetters = "د هـ و", PlateTypeCode = 1,
            Vin = "X", Make = "Hyundai", Model = "Elantra", ModelYear = 2023,
            OwnerBranchId = Guid.NewGuid(), CurrentKm = 10_000, NowUtc = Now,
        });
        v.Reserve(Now);
        v.StartRental(Now);
        v.Status.Should().Be(VehicleStatus.OnRent);

        v.Return(endKm: 10_350, nowUtc: Now.AddDays(2));

        v.Status.Should().Be(VehicleStatus.Available);
        v.CurrentKm.Should().Be(10_350);
    }

    [Fact]
    public void Vehicle_Return_with_lower_km_throws()
    {
        var v = Vehicle.Create(new VehicleCreateInput
        {
            TenantId = Tenant,
            PlateNumber = "9999", PlateLetters = "ز ح ط", PlateTypeCode = 1,
            Vin = "Y", Make = "Kia", Model = "Cerato", ModelYear = 2024,
            OwnerBranchId = Guid.NewGuid(), CurrentKm = 50_000, NowUtc = Now,
        });
        v.Reserve(Now); v.StartRental(Now);
        var act = () => v.Return(endKm: 49_999, nowUtc: Now);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Vehicle_ReleaseReservation_returns_to_Available()
    {
        var v = Vehicle.Create(new VehicleCreateInput
        {
            TenantId = Tenant,
            PlateNumber = "7777", PlateLetters = "ر س ش", PlateTypeCode = 1,
            Vin = "Z", Make = "Nissan", Model = "Altima", ModelYear = 2024,
            OwnerBranchId = Guid.NewGuid(), CurrentKm = 12_000, NowUtc = Now,
        });

        v.Reserve(Now);
        v.ReleaseReservation(Now.AddMinutes(1));

        v.Status.Should().Be(VehicleStatus.Available);
    }

    // ─── Driver ─────────────────────────────────────────────────────────────
    [Fact]
    public void Driver_factory_initialises_with_TammNotRequested()
    {
        var d = Driver.Create(new DriverCreateInput
        {
            TenantId = Tenant,
            PersonNameEn = "Khalid Saleh",
            IdTypeCode = 1,
            PersonIdNumber = "1056789012",
            DriverLicenseNumber = "L4567890123",
            LicenseExpiryDate = new DateOnly(2028, 6, 1),
            NowUtc = Now,
        });
        d.Status.Should().Be(DriverStatus.Active);
        d.TammAuthorizationStatus.Should().Be(TammAuthorizationStatus.NotRequested);
    }

    [Fact]
    public void Driver_TAMM_authorization_flow_pending_then_authorized()
    {
        var d = Driver.Create(new DriverCreateInput
        {
            TenantId = Tenant, PersonNameEn = "X", IdTypeCode = 2, PersonIdNumber = "2123456789",
            DriverLicenseNumber = "Y", LicenseExpiryDate = new DateOnly(2027, 1, 1), NowUtc = Now,
        });
        d.MarkTammAuthorizationPending("tamm-ref-123", Now.AddMinutes(1));
        d.TammAuthorizationStatus.Should().Be(TammAuthorizationStatus.Pending);
        d.MarkTammAuthorized(Now.AddMinutes(5));
        d.TammAuthorizationStatus.Should().Be(TammAuthorizationStatus.Authorized);
        d.TammAuthorizedAtUtc.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void Driver_IsLicenseExpiringSoon_flags_within_window()
    {
        var d = Driver.Create(new DriverCreateInput
        {
            TenantId = Tenant, PersonNameEn = "X", IdTypeCode = 1, PersonIdNumber = "1",
            DriverLicenseNumber = "Y", LicenseExpiryDate = new DateOnly(2026, 6, 1), NowUtc = Now,
        });
        d.IsLicenseExpiringSoon(new DateOnly(2026, 5, 20), days: 30).Should().BeTrue();
        d.IsLicenseExpiringSoon(new DateOnly(2026, 1, 1), days: 30).Should().BeFalse();
    }

    // ─── Branch ─────────────────────────────────────────────────────────────
    [Fact]
    public void Branch_factory_requires_Tajeer_ids_positive()
    {
        var act = () => Branch.Create(new BranchCreateInput
        {
            TenantId = Tenant,
            Code = "RUH-01", NameEn = "Olaya HQ", NameAr = "العليا",
            TajeerBranchId = 0, TajeerOperatorId = 1, NowUtc = Now,
        });
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Branch_factory_starts_active_and_can_deactivate()
    {
        var b = Branch.Create(new BranchCreateInput
        {
            TenantId = Tenant,
            Code = "RUH-01", NameEn = "Olaya HQ", NameAr = "العليا",
            TajeerBranchId = 1, TajeerOperatorId = 999, NowUtc = Now,
        });
        b.IsActive.Should().BeTrue();
        b.Deactivate(Now.AddDays(1));
        b.IsActive.Should().BeFalse();
    }

    // ─── RentPolicy ─────────────────────────────────────────────────────────
    [Fact]
    public void RentPolicy_factory_requires_positive_daily_rate()
    {
        var act = () => RentPolicy.Create(new RentPolicyCreateInput
        {
            TenantId = Tenant,
            Code = "STD-DAILY", NameEn = "Standard Daily", NameAr = "يومي قياسي",
            BaseDailyRate = 0m, TajeerRentPolicyId = 1, NowUtc = Now,
        });
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RentPolicy_factory_succeeds()
    {
        var p = RentPolicy.Create(new RentPolicyCreateInput
        {
            TenantId = Tenant,
            Code = "STD-DAILY", NameEn = "Standard Daily", NameAr = "يومي قياسي",
            BaseDailyRate = 150m, AllowedKmPerDay = 300, ExtraKmFee = 0.5m,
            TajeerRentPolicyId = 1, NowUtc = Now,
        });
        p.IsActive.Should().BeTrue();
        p.BaseDailyRate.Should().Be(150m);
    }

    // ─── ExtendedCoverage ───────────────────────────────────────────────────
    [Fact]
    public void ExtendedCoverage_factory_validates_daily_rate_positive()
    {
        var act = () => ExtendedCoverage.Create(new ExtendedCoverageCreateInput
        {
            TenantId = Tenant,
            Code = "CDW-FULL", NameEn = "Full CDW", NameAr = "تأمين شامل",
            CoverageType = CoverageType.FullCdw,
            DailyRate = 0m, TajeerExtendedCoverageId = 1, NowUtc = Now,
        });
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExtendedCoverage_factory_succeeds()
    {
        var c = ExtendedCoverage.Create(new ExtendedCoverageCreateInput
        {
            TenantId = Tenant,
            Code = "CDW-FULL", NameEn = "Full CDW", NameAr = "تأمين شامل",
            CoverageType = CoverageType.FullCdw,
            DailyRate = 25m, DeductibleAmount = 500m,
            TajeerExtendedCoverageId = 2, NowUtc = Now,
        });
        c.CoverageType.Should().Be(CoverageType.FullCdw);
        c.IsActive.Should().BeTrue();
    }
}
