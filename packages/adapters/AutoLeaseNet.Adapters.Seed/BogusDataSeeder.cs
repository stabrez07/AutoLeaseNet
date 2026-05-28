using AutoLeaseNet.Application.Ports.Persistence;
using AutoLeaseNet.Application.Ports.Seeding;
using AutoLeaseNet.Application.Ports.Time;
using AutoLeaseNet.Domain.Branches;
using AutoLeaseNet.Domain.Customers;
using AutoLeaseNet.Domain.Drivers;
using AutoLeaseNet.Domain.ExtendedCoverages;
using AutoLeaseNet.Domain.Leases;
using AutoLeaseNet.Domain.Operations;
using AutoLeaseNet.Domain.RentPolicies;
using AutoLeaseNet.Domain.Vehicles;
using Bogus;
using Microsoft.Extensions.Logging;

namespace AutoLeaseNet.Adapters.Seed;

/// <summary>
/// Production-shaped seed for the single demo tenant. Generates:
/// <list type="bullet">
///   <item>3 Branches across Riyadh / Jeddah / Dammam.</item>
///   <item>4 RentPolicies (Standard Daily, Standard Hourly, Daily-with-Driver, Long-term Monthly).</item>
///   <item>3 ExtendedCoverages (Partial CDW, Full CDW, Super CDW).</item>
///   <item>20 Customers — 6 B2B (Saudi Aramco, STC, Almarai, SABIC, Maaden, Bin Dawood) + 14 B2C with KSA-locale names + valid-shaped Saudi National / Iqama IDs.</item>
///   <item>60 Vehicles — Toyota / Hyundai / Nissan / Kia / Mitsubishi fleet staples with real-format Saudi plate triples.</item>
///   <item>80 Drivers — mix of customer-affiliated + freelance, with TAMM authorization status variety.</item>
///   <item>10 Leases — spanning every LeaseStatus for richer reporting demos.</item>
/// </list>
/// Idempotent: short-circuits when <see cref="ICustomerRepository.AnyAsync"/> returns true.
/// All Bogus generators seeded from <see cref="SeedOptions.RandomSeed"/> for reproducibility.
/// </summary>
public sealed partial class BogusDataSeeder(
    SeedOptions options,
    ICustomerRepository customers,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    IBranchRepository branches,
    IRentPolicyRepository rentPolicies,
    IExtendedCoverageRepository coverages,
    ILeaseRepository leases,
    IInspectionRepository inspections,
    IIncidentRepository incidents,
    IUnitOfWork uow,
    IClock clock,
    ILogger<BogusDataSeeder> logger) : IDataSeeder
{
    public Guid TenantId { get; } = options.TenantId;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await customers.AnyAsync(TenantId, ct).ConfigureAwait(false))
        {
            LogAlreadySeeded(TenantId);
            return;
        }

        Randomizer.Seed = new Random(options.RandomSeed);
        var nowUtc = clock.UtcNow;

        var seededBranches = SeedBranches(nowUtc);
        var seededPolicies = SeedRentPolicies(nowUtc);
        var seededCoverages = SeedExtendedCoverages(nowUtc);
        var seededCustomers = SeedCustomers(nowUtc);
        var seededVehicles = SeedVehicles(seededBranches, nowUtc);
        var seededDrivers = SeedDrivers(seededCustomers, nowUtc);

        var seededLeases = SeedLeases(seededCustomers, seededVehicles, seededDrivers, seededBranches,
            seededPolicies, seededCoverages, nowUtc);

        SeedInspections(seededLeases, seededDrivers, nowUtc);
        SeedIncidents(seededLeases, seededDrivers, nowUtc);

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
        LogSeedComplete(TenantId, seededCustomers.Count, seededVehicles.Count, seededDrivers.Count);
    }

    // ─── Branches (3) ───────────────────────────────────────────────────────
    private List<Branch> SeedBranches(DateTimeOffset now)
    {
        var data = new[]
        {
            new BranchCreateInput { TenantId = TenantId, Code = "RUH-OLY", NameEn = "Riyadh - Olaya HQ", NameAr = "الرياض - العليا",
                CityEn = "Riyadh", CityAr = "الرياض", RegionEn = "Riyadh", RegionAr = "منطقة الرياض",
                LicenseNumber = "LIC-001", Address = "Olaya Street, Riyadh 12244", PhoneNumber = "+966112345678",
                Latitude = 24.6892m, Longitude = 46.6890m,
                TajeerBranchId = 1, TajeerOperatorId = 1001,
                WorkingHoursJson = """{"sun-thu":"08:00-22:00","fri":"14:00-22:00","sat":"08:00-22:00"}""",
                NowUtc = now },
            new BranchCreateInput { TenantId = TenantId, Code = "JED-KFR", NameEn = "Jeddah - King Fahd Road", NameAr = "جدة - طريق الملك فهد",
                CityEn = "Jeddah", CityAr = "جدة", RegionEn = "Makkah", RegionAr = "منطقة مكة",
                LicenseNumber = "LIC-002", Address = "King Fahd Road, Jeddah 21462", PhoneNumber = "+966126543210",
                Latitude = 21.5433m, Longitude = 39.1728m,
                TajeerBranchId = 2, TajeerOperatorId = 1002,
                WorkingHoursJson = """{"sun-thu":"08:00-23:00","fri":"14:00-23:00","sat":"08:00-23:00"}""",
                NowUtc = now },
            new BranchCreateInput { TenantId = TenantId, Code = "DMM-CRD", NameEn = "Dammam - Corniche Road", NameAr = "الدمام - طريق الكورنيش",
                CityEn = "Dammam", CityAr = "الدمام", RegionEn = "Eastern", RegionAr = "المنطقة الشرقية",
                LicenseNumber = "LIC-003", Address = "Corniche Road, Dammam 32413", PhoneNumber = "+966138888899",
                Latitude = 26.4207m, Longitude = 50.0888m,
                TajeerBranchId = 3, TajeerOperatorId = 1003,
                NowUtc = now },
        };
        var created = data.Select(Branch.Create).ToList();
        created.ForEach(branches.Add);
        return created;
    }

    // ─── RentPolicies (4) ───────────────────────────────────────────────────
    private List<RentPolicy> SeedRentPolicies(DateTimeOffset now)
    {
        var data = new[]
        {
            new RentPolicyCreateInput { TenantId = TenantId, Code = "STD-DAILY", NameEn = "Standard Daily", NameAr = "يومي قياسي",
                DescriptionEn = "Standard daily rental, 300km/day allowance.",
                DescriptionAr = "إيجار يومي قياسي، 300 كم في اليوم.",
                BaseDailyRate = 150m, AllowedKmPerDay = 300, ExtraKmFee = 0.5m, MinRentalDays = 1, MaxRentalDays = 30,
                SecurityDeposit = 1000m, LateHourFee = 50m, TajeerRentPolicyId = 1, NowUtc = now },
            new RentPolicyCreateInput { TenantId = TenantId, Code = "STD-HOURLY", NameEn = "Standard Hourly", NameAr = "ساعي قياسي",
                BaseDailyRate = 200m, BaseHourlyRate = 25m, AllowedKmPerHour = 30, ExtraKmFee = 0.75m,
                LateHourFee = 30m, TajeerRentPolicyId = 2, NowUtc = now },
            new RentPolicyCreateInput { TenantId = TenantId, Code = "DAILY-DRV", NameEn = "Daily with Driver", NameAr = "يومي مع سائق",
                DescriptionEn = "Daily rental including a professional driver.",
                BaseDailyRate = 350m, AllowedKmPerDay = 250, ExtraKmFee = 0.6m, MinRentalDays = 1,
                SecurityDeposit = 1500m, LateHourFee = 75m, TajeerRentPolicyId = 3, NowUtc = now },
            new RentPolicyCreateInput { TenantId = TenantId, Code = "LT-MONTHLY", NameEn = "Long-term Monthly", NameAr = "طويل الأمد شهري",
                BaseDailyRate = 90m, AllowedKmPerDay = 200, ExtraKmFee = 0.4m, MinRentalDays = 30, MaxRentalDays = 365,
                SecurityDeposit = 500m, TajeerRentPolicyId = 4, NowUtc = now },
        };
        var created = data.Select(RentPolicy.Create).ToList();
        created.ForEach(rentPolicies.Add);
        return created;
    }

    // ─── ExtendedCoverages (3) ──────────────────────────────────────────────
    private List<ExtendedCoverage> SeedExtendedCoverages(DateTimeOffset now)
    {
        var data = new[]
        {
            new ExtendedCoverageCreateInput { TenantId = TenantId, Code = "CDW-PART", NameEn = "Partial CDW", NameAr = "تأمين جزئي",
                DescriptionEn = "Covers collision damage with SAR 1,000 deductible.",
                CoverageType = CoverageType.PartialCdw, DailyRate = 15m, DeductibleAmount = 1000m,
                TajeerExtendedCoverageId = 1, NowUtc = now },
            new ExtendedCoverageCreateInput { TenantId = TenantId, Code = "CDW-FULL", NameEn = "Full CDW", NameAr = "تأمين شامل",
                DescriptionEn = "Full collision damage waiver with SAR 500 deductible.",
                CoverageType = CoverageType.FullCdw, DailyRate = 25m, DeductibleAmount = 500m,
                TajeerExtendedCoverageId = 2, NowUtc = now },
            new ExtendedCoverageCreateInput { TenantId = TenantId, Code = "CDW-SUPER", NameEn = "Super CDW", NameAr = "تأمين سوبر شامل",
                DescriptionEn = "Zero-deductible super CDW including theft protection.",
                CoverageType = CoverageType.SuperCdw, DailyRate = 45m, DeductibleAmount = 0m,
                TajeerExtendedCoverageId = 3, NowUtc = now },
        };
        var created = data.Select(ExtendedCoverage.Create).ToList();
        created.ForEach(coverages.Add);
        return created;
    }

    // ─── Customers (20: 6 B2B + 14 B2C) ─────────────────────────────────────
    private List<Customer> SeedCustomers(DateTimeOffset now)
    {
        var b2b = new[]
        {
            ("Saudi Aramco", "أرامكو السعودية", "2050000000", "300000000000003", 100_000_000m),
            ("Saudi Telecom Company", "شركة الاتصالات السعودية", "1010030000", "300000000000004", 20_000_000m),
            ("Almarai Company", "شركة المراعي", "1010030001", "300000000000005", 15_000_000m),
            ("SABIC", "سابك", "1010002000", "300000000000006", 50_000_000m),
            ("Maaden", "معادن", "1010164000", "300000000000007", 25_000_000m),
            ("Bin Dawood Holding", "بن داود القابضة", "4030000000", "300000000000008", 10_000_000m),
        };
        var created = new List<Customer>();
        foreach (var (legal, legalAr, cr, vat, credit) in b2b)
        {
            var c = Customer.CreateB2B(new B2BCreateInput
            {
                TenantId = TenantId,
                LegalName = legal, LegalNameAr = legalAr,
                CommercialRegistration = cr, VatNumber = vat,
                Email = $"fleet@{legal.Split(' ')[0].ToLowerInvariant()}.com.sa",
                Mobile = $"+9665{Random.Shared.Next(10000000, 99999999)}",
                NationalAddress = "RIYD2345-12345", BillingAddress = "Riyadh, Saudi Arabia",
                CreditLimit = credit, CreditCurrency = "SAR",
                PreferredLanguage = PreferredLanguage.Ar,
                NowUtc = now,
            });
            customers.Add(c);
            created.Add(c);
        }

        var personFaker = new Faker("ar"); // Arabic locale
        for (var i = 0; i < 14; i++)
        {
            var nameAr = personFaker.Name.FullName();
            var nameEn = TransliterateRough(nameAr, i);
            var idType = i % 3 == 0 ? 2 : 1; // every 3rd is Iqama (2), rest Saudi National (1)
            var idPrefix = idType == 1 ? '1' : '2';
            var idNumber = $"{idPrefix}{Random.Shared.Next(100_000_000, 999_999_999)}";

            var c = Customer.CreateB2C(new B2CCreateInput
            {
                TenantId = TenantId,
                PersonNameEn = nameEn, PersonNameAr = nameAr,
                IdTypeCode = idType,
                PersonIdNumber = idNumber,
                DateOfBirth = new DateOnly(1970 + (i % 30), 1 + (i % 12), 1 + (i % 28)),
                NationalityCode = idType == 1 ? "SA" : "EG",
                Email = $"customer{i + 1:00}@example.sa",
                Mobile = $"+9665{Random.Shared.Next(10000000, 99999999)}",
                NationalAddress = "RIYD1234-12345",
                PreferredLanguage = i % 2 == 0 ? PreferredLanguage.Ar : PreferredLanguage.En,
                NowUtc = now,
            });
            customers.Add(c);
            created.Add(c);
        }
        return created;
    }

    // ─── Vehicles (60) ──────────────────────────────────────────────────────
    private List<Vehicle> SeedVehicles(List<Branch> branchList, DateTimeOffset now)
    {
        var fleetTemplates = new (string Make, string Model, BodyType Body, FuelType Fuel, int Seats, decimal Price)[]
        {
            ("Toyota", "Camry", BodyType.Sedan, FuelType.Petrol91, 5, 110_000m),
            ("Toyota", "Corolla", BodyType.Sedan, FuelType.Petrol91, 5, 85_000m),
            ("Hyundai", "Elantra", BodyType.Sedan, FuelType.Petrol91, 5, 78_000m),
            ("Hyundai", "Sonata", BodyType.Sedan, FuelType.Petrol91, 5, 105_000m),
            ("Nissan", "Altima", BodyType.Sedan, FuelType.Petrol91, 5, 102_000m),
            ("Nissan", "Patrol", BodyType.Suv, FuelType.Petrol95, 7, 280_000m),
            ("Kia", "Cerato", BodyType.Sedan, FuelType.Petrol91, 5, 82_000m),
            ("Mitsubishi", "Lancer", BodyType.Sedan, FuelType.Petrol91, 5, 75_000m),
            ("Toyota", "Hilux", BodyType.Pickup, FuelType.Diesel, 5, 145_000m),
            ("Hyundai", "Tucson", BodyType.Suv, FuelType.Petrol91, 5, 130_000m),
        };
        var colors = new[] { "White", "Silver", "Black", "Grey", "Beige" };
        var arabicLetterTriples = new[] { "أ ب ج", "د هـ و", "ز ح ط", "ي ك ل", "م ن س", "ع ف ص", "ق ر ش", "ت ث خ", "ذ ض ظ", "غ" };

        var created = new List<Vehicle>();
        for (var i = 0; i < 60; i++)
        {
            var t = fleetTemplates[i % fleetTemplates.Length];
            var branch = branchList[i % branchList.Count];
            var plateNumber = (1000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var plateLetters = arabicLetterTriples[i % arabicLetterTriples.Length];
            var modelYear = 2022 + (i % 3); // 2022/2023/2024 spread

            var v = Vehicle.Create(new VehicleCreateInput
            {
                TenantId = TenantId,
                PlateNumber = plateNumber, PlateLetters = plateLetters, PlateTypeCode = 1,
                Vin = $"VINSA{i:D12}",
                EngineNumber = $"ENG-{i:D8}",
                Make = t.Make, Model = t.Model, ModelYear = modelYear,
                Color = colors[i % colors.Length],
                FuelType = t.Fuel, TransmissionType = TransmissionType.Automatic, BodyType = t.Body, Seats = t.Seats,
                LicenseExpiryDate = new DateOnly(2027 + (i % 3), 1 + (i % 12), 1 + (i % 28)),
                InsuranceExpiryDate = new DateOnly(2027, 1 + (i % 12), 1 + (i % 28)),
                InspectionExpiryDate = new DateOnly(2027, 1 + (i % 12), 1 + (i % 28)),
                InsuranceCompany = "Tawuniya",
                InsurancePolicyNumber = $"POL-{i:D9}",
                OwnerBranchId = branch.Id,
                CurrentBranchId = branch.Id,
                CurrentKm = Random.Shared.Next(5_000, 80_000),
                PurchasePrice = t.Price,
                PurchaseDate = new DateOnly(modelYear, 1 + (i % 12), 1 + (i % 28)),
                NowUtc = now,
            });
            vehicles.Add(v);
            created.Add(v);
        }
        return created;
    }

    // ─── Drivers (80) ───────────────────────────────────────────────────────
    private List<Driver> SeedDrivers(List<Customer> customerList, DateTimeOffset now)
    {
        var b2bCustomers = customerList.Where(c => c.Type == CustomerType.B2B).ToList();
        var personFaker = new Faker("ar");
        var created = new List<Driver>();

        for (var i = 0; i < 80; i++)
        {
            // First ~30 drivers belong to B2B fleet pools; rest are freelance.
            Customer? affiliation = i < 30 ? b2bCustomers[i % b2bCustomers.Count] : null;
            var nameAr = personFaker.Name.FullName();
            var nameEn = TransliterateRough(nameAr, i);
            var idType = i % 4 == 0 ? 2 : 1; // 25% Iqama, 75% Saudi National
            var idPrefix = idType == 1 ? '1' : '2';
            var idNumber = $"{idPrefix}{Random.Shared.Next(100_000_000, 999_999_999)}";

            var d = Driver.Create(new DriverCreateInput
            {
                TenantId = TenantId,
                CustomerId = affiliation?.Id,
                PersonNameEn = nameEn, PersonNameAr = nameAr,
                IdTypeCode = idType,
                PersonIdNumber = idNumber,
                DateOfBirth = new DateOnly(1980 + (i % 25), 1 + (i % 12), 1 + (i % 28)),
                NationalityCode = idType == 1 ? "SA" : "EG",
                DriverLicenseNumber = $"DL-{idNumber}",
                LicenseClass = 1,
                LicenseIssueDate = new DateOnly(2020, 1, 1),
                LicenseExpiryDate = new DateOnly(2027 + (i % 3), 1 + (i % 12), 1 + (i % 28)),
                Mobile = $"+9665{Random.Shared.Next(10000000, 99999999)}",
                Email = $"driver{i + 1:000}@example.sa",
                NationalAddress = "RIYD1234-12345",
                NowUtc = now,
            });

            // Spread TAMM authorization states across the seeded drivers.
            switch (i % 5)
            {
                case 1: d.MarkTammAuthorizationPending($"tamm-{i:D6}", now.AddDays(-1)); break;
                case 2: d.MarkTammAuthorizationPending($"tamm-{i:D6}", now.AddDays(-3));
                         d.MarkTammAuthorized(now.AddDays(-1)); break;
                case 3: d.MarkTammAuthorizationPending($"tamm-{i:D6}", now.AddDays(-3));
                         d.MarkTammRejected(now.AddDays(-1)); break;
            }
            if (i % 7 == 0) d.MarkDefensiveDrivingCertHeld(now.AddDays(-30));

            drivers.Add(d);
            created.Add(d);
        }
        return created;
    }

    // ─── Leases (10: spans every status) ────────────────────────────────────
    private List<SeededLease> SeedLeases(
        List<Customer> custs, List<Vehicle> vehs, List<Driver> drvs,
        List<Branch> brs, List<RentPolicy> pols, List<ExtendedCoverage> covs, DateTimeOffset now)
    {
        var result = new List<SeededLease>();
        // Pair each lease with a customer/vehicle/driver to give realistic referential rows.
        var seedTemplates = new (LeaseStatus FinalStatus, int DaysAgo, decimal Rent, decimal Paid)[]
        {
            (LeaseStatus.PendingIssuance, 0,  600m, 200m),
            (LeaseStatus.PendingIssuance, 1,  450m, 150m),
            (LeaseStatus.Active,          5,  1200m, 1200m),
            (LeaseStatus.Active,          12, 800m, 800m),
            (LeaseStatus.Extended,        25, 1500m, 1500m),
            (LeaseStatus.Suspended,       8,  900m, 900m),
            (LeaseStatus.Closed,          45, 1800m, 1800m),
            (LeaseStatus.Cancelled,       3,  300m, 0m),
            (LeaseStatus.ExpiredDraft,    2,  500m, 0m),
            (LeaseStatus.SaveFailed,      0,  400m, 0m),
        };

        long contractCounter = 1_000_000_000L;
        for (var i = 0; i < seedTemplates.Length; i++)
        {
            var (finalStatus, daysAgo, rent, paid) = seedTemplates[i];
            var savedAt = now.AddDays(-daysAgo);
            var startUtc = savedAt.AddHours(1);
            var endUtc = startUtc.AddDays(2);
            var contractNumber = ++contractCounter;
            var cust = custs[i % custs.Count];
            var veh = vehs[i % vehs.Count];
            var drv = drvs[i % drvs.Count];
            var br = brs[i % brs.Count];
            var pol = pols[i % pols.Count];

            var lease = Lease.CreatePending(new CreatePendingInput
            {
                TenantId = TenantId,
                CustomerId = cust.Id,
                VehicleId = veh.Id,
                PrimaryDriverId = drv.Id,
                RentPolicyId = pol.Id,
                ExtendedCoverageId = covs[i % covs.Count].Id,
                WorkingBranchId = br.Id,
                ReceiveBranchId = br.Id,
                ReturnBranchId = br.Id,

                TajeerContractNumber = contractNumber,
                TajeerIssuanceToken = $"tok-{contractNumber:N0}",
                IssuanceUrl = $"https://tajeerstg.logisti.sa/#/public-contract/{contractNumber}/tok",
                TajeerWorkingBranchId = br.TajeerBranchId,
                TajeerReceiveBranchId = br.TajeerBranchId,
                TajeerReturnBranchId = br.TajeerBranchId,
                TajeerRentPolicyId = pol.TajeerRentPolicyId,
                TajeerExtendedCoverageId = covs[i % covs.Count].TajeerExtendedCoverageId,
                TajeerOperatorId = br.TajeerOperatorId,

                ContractTypeCode = 1,
                ContractStartUtc = startUtc,
                ContractEndUtc = endUtc,
                AllowedKmPerDay = pol.AllowedKmPerDay,
                AllowedLateHours = 2,

                RentAmount = rent,
                PaidAmount = paid,
                RemainingAmount = rent - paid,
                VatAmount = Math.Round(rent * 0.15m, 2),
                TotalAmount = rent + Math.Round(rent * 0.15m, 2),
                PaymentMethodCode = 1,

                NowUtc = savedAt,
            });

            switch (finalStatus)
            {
                case LeaseStatus.Active:
                    lease.MarkIssued(veh.CurrentKm, 4, "Clean handover", savedAt.AddMinutes(30));
                    EnsureVehicleOnRent(veh, savedAt.AddMinutes(30));
                    break;
                case LeaseStatus.Extended:
                    lease.MarkIssued(veh.CurrentKm, 4, null, savedAt.AddMinutes(30));
                    EnsureVehicleOnRent(veh, savedAt.AddMinutes(30));
                    lease.IncrementExtension(endUtc.AddDays(7), savedAt.AddDays(1));
                    break;
                case LeaseStatus.Suspended:
                    lease.MarkIssued(veh.CurrentKm, 4, null, savedAt.AddMinutes(30));
                    EnsureVehicleOnRent(veh, savedAt.AddMinutes(30));
                    lease.MarkSuspended(2, savedAt.AddHours(3));
                    break;
                case LeaseStatus.Closed:
                    lease.MarkIssued(veh.CurrentKm, 4, null, savedAt.AddMinutes(30));
                    EnsureVehicleOnRent(veh, savedAt.AddMinutes(30));
                    lease.MarkClosed(1, null, veh.CurrentKm + 320, 3,
                        "Returned clean", null, savedAt.AddDays(2));
                    veh.Return(veh.CurrentKm + 320, savedAt.AddDays(2));
                    break;
                case LeaseStatus.Cancelled:
                    lease.MarkCancelled("Renter cancelled before issuance", savedAt.AddHours(2));
                    break;
                case LeaseStatus.ExpiredDraft:
                    lease.MarkExpired(savedAt.AddHours(13));
                    break;
                case LeaseStatus.SaveFailed:
                    lease.RecordSaveFailure("server.error.renter.mobile.invalid", savedAt.AddSeconds(2));
                    break;
                case LeaseStatus.PendingIssuance:
                default:
                    break; // already in PendingIssuance from CreatePending
            }

            leases.Add(lease);
            result.Add(new SeededLease(lease, veh, drv, savedAt));
        }
        return result;
    }

    // ─── Incidents (Spec 01 §5.6 / Spec 02 §4.7). One per Closed lease — half resolved
    //     traffic accidents (minor), half closed breakdowns. Reporter alternates between
    //     the first two seeded drivers for determinism.
    private void SeedIncidents(List<SeededLease> seededLeases, List<Driver> drvs, DateTimeOffset now)
    {
        if (drvs.Count == 0) return;
        var idx = 0;
        foreach (var sl in seededLeases.Where(l => l.Lease.Status == LeaseStatus.Closed))
        {
            var isAccident = idx % 2 == 0;
            var reportedAt = sl.SavedAt.AddDays(1);
            var incident = Incident.Report(new ReportIncidentInput
            {
                TenantId = TenantId,
                VehicleId = sl.Vehicle.Id,
                LeaseId = sl.Lease.Id,
                ReportedByPersonId = drvs[idx % drvs.Count].Id,
                Type = isAccident ? IncidentType.TrafficAccident : IncidentType.Breakdown,
                Severity = IncidentSeverity.Minor,
                IncidentTimeUtc = reportedAt.AddMinutes(-30),
                Description = isAccident
                    ? "Front bumper grazed parking pillar — minor cosmetic."
                    : "Battery flat after 4-hour airport wait — jump-started, returned.",
                LocationDescription = isAccident ? "Riyadh — Olaya parking" : "Jeddah — airport pickup zone",
                PoliceReportNumber = isAccident ? $"RP-2026-{1000 + idx:D4}" : null,
                NowUtc = reportedAt,
            });
            incident.MarkResolved(
                resolutionNotes: isAccident
                    ? "Detail-shop polish — SAR 120 charged to renter."
                    : "Battery replaced under fleet warranty.",
                nowUtc: reportedAt.AddHours(6));
            incident.MarkClosed(reportedAt.AddDays(1));
            incidents.Add(incident);
            idx++;
        }
    }

    // ─── Inspections (per Spec 01 §invariants 2/3 — ACTIVE/EXTENDED/SUSPENDED leases get
    //     a CHECK_OUT; CLOSED leases get both CHECK_OUT + CHECK_IN. Each carries 0–3
    //     deterministic damage markers so the sketch endpoint has something to render).
    private void SeedInspections(List<SeededLease> seededLeases, List<Driver> drvs, DateTimeOffset now)
    {
        var rng = new Random(options.RandomSeed ^ 0x1517);
        var markerTypes = new[]
        {
            DamageMarkerType.SmallScratch,
            DamageMarkerType.DeepScratch,
            DamageMarkerType.VeryDeepScratch,
            DamageMarkerType.BendInBody,
        };

        foreach (var sl in seededLeases)
        {
            switch (sl.Lease.Status)
            {
                case LeaseStatus.Active:
                case LeaseStatus.Extended:
                case LeaseStatus.Suspended:
                    inspections.Add(BuildCheckOut(sl, drvs, now, rng, markerTypes));
                    break;
                case LeaseStatus.Closed:
                    inspections.Add(BuildCheckOut(sl, drvs, now, rng, markerTypes));
                    inspections.Add(BuildCheckIn(sl, drvs, now, rng, markerTypes));
                    break;
                default:
                    break; // PendingIssuance / Cancelled / ExpiredDraft / SaveFailed: no inspection rows.
            }
        }
    }

    private Inspection BuildCheckOut(SeededLease sl, List<Driver> drvs, DateTimeOffset now, Random rng, DamageMarkerType[] markerTypes)
    {
        var checkOutAt = sl.SavedAt.AddMinutes(20);
        // Start the inspection un-linked, then drive the link through the same domain
        // method the SaveContract saga uses — so the seeded data has a non-null
        // LeaseLinkedAtUtc audit timestamp matching real flows.
        var i = Inspection.Start(new StartInspectionInput
        {
            TenantId = TenantId,
            VehicleId = sl.Vehicle.Id,
            LeaseId = null,
            Type = InspectionType.CheckOut,
            PerformedByUserId = drvs[0].Id,
            OdometerKm = sl.Vehicle.CurrentKm,
            FuelLevel = FuelLevel.Full,
            AcCondition = 1, RadioStereoCondition = 1, ScreenCondition = 1,
            SpeedometerCondition = 1, KeysCondition = 1, CarSeatsCondition = 1,
            SafetyTriangleCondition = 1, FireExtinguisherCondition = 1,
            FirstAidKitCondition = 1, SpareTireToolsCondition = 1,
            TiresCondition = 1, SpareTireCondition = 1,
            Notes = "Pre-delivery condition: clean.",
            NowUtc = checkOutAt,
        });
        AddDeterministicMarkers(i, rng, markerTypes, checkOutAt);
        i.Complete(checkOutAt.AddMinutes(5));
        i.LinkToLease(sl.Lease.Id, checkOutAt.AddMinutes(6));
        return i;
    }

    private Inspection BuildCheckIn(SeededLease sl, List<Driver> drvs, DateTimeOffset now, Random rng, DamageMarkerType[] markerTypes)
    {
        var returnedAt = (sl.Lease.ActualReturnUtc ?? sl.SavedAt.AddDays(2)).AddMinutes(15);
        var endKm = sl.Lease.EndKm ?? (sl.Vehicle.CurrentKm + 320);
        var i = Inspection.Start(new StartInspectionInput
        {
            TenantId = TenantId,
            VehicleId = sl.Vehicle.Id,
            LeaseId = sl.Lease.Id,
            Type = InspectionType.CheckIn,
            PerformedByUserId = drvs[0].Id,
            OdometerKm = endKm,
            FuelLevel = FuelLevel.Half,
            Notes = "Returned with minor scuffs.",
            NowUtc = returnedAt,
        });
        AddDeterministicMarkers(i, rng, markerTypes, returnedAt);
        i.Complete(returnedAt.AddMinutes(8));
        return i;
    }

    private static void AddDeterministicMarkers(Inspection i, Random rng, DamageMarkerType[] markerTypes, DateTimeOffset at)
    {
        var count = rng.Next(0, 4);
        for (var n = 0; n < count; n++)
        {
            var type = markerTypes[rng.Next(markerTypes.Length)];
            var x = (decimal)(rng.NextDouble() * (double)InspectionDamageMarker.CanvasWidth);
            var y = (decimal)(rng.NextDouble() * (double)InspectionDamageMarker.CanvasHeight);
            i.AddDamageMarker(type, Math.Round(x, 4), Math.Round(y, 4), at);
        }
    }

    private sealed record SeededLease(Lease Lease, Vehicle Vehicle, Driver Driver, DateTimeOffset SavedAt);

    // Lifts a Vehicle from Available → Reserved → OnRent so seeded Active/Extended/
    // Suspended/Closed leases reflect realistic vehicle state for downstream check-in
    // tests. Idempotent against being called when the vehicle is already past Available.
    private static void EnsureVehicleOnRent(Vehicle veh, DateTimeOffset at)
    {
        if (veh.Status == VehicleStatus.Available) { veh.Reserve(at); veh.StartRental(at); }
        else if (veh.Status == VehicleStatus.Reserved) { veh.StartRental(at); }
    }

    // Lightweight transliteration helper — for demo seed only, not production-grade.
    private static string TransliterateRough(string arabic, int salt)
    {
        var first = arabic.Split(' ').FirstOrDefault() ?? "Person";
        return $"Driver-{salt + 1:000} ({first})";
    }

    [LoggerMessage(EventId = 9001, Level = LogLevel.Information,
        Message = "Tenant {TenantId} already seeded — skipping BogusDataSeeder.")]
    partial void LogAlreadySeeded(Guid tenantId);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Information,
        Message = "Seeded tenant {TenantId}: {CustomerCount} customers, {VehicleCount} vehicles, {DriverCount} drivers.")]
    partial void LogSeedComplete(Guid tenantId, int customerCount, int vehicleCount, int driverCount);
}
