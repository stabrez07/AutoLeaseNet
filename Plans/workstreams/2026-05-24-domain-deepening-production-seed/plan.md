# Domain Deepening + Production-Shaped Seed Data

**Workstream slug**: `2026-05-24-domain-deepening-production-seed`
**Opened**: 2026-05-24
**Owner**: solo dev + Claude Code
**Source feedback**: User (2026-05-23) — *"don't missing fields which are required for leasing business to the least granularity for BI and future decision making results. ... seeded as temporary but should be like the production data ... make application look more perfect and production ready."*
**Captured in memory**: [[feedback-production-ready-data]]
**Inserts between**: Week 1 Day 6 (part 1) and Day 6 (part 2 — webhook endpoint/dispatch)

---

## 1. Goal

Bring the AutoLeaseNet domain model and local-dev data to a **production-shaped baseline** so:

1. Every Lease row captures every field BI / executive reporting / downstream decision systems will eventually want — extension history, suspension/closure reasons, fuel + KM at issuance & return, payment summary, every state-transition timestamp, customer + vehicle + driver references, branch, rent policy.
2. Customer / Vehicle / Driver / Branch / RentPolicy / ExtendedCoverage aggregates exist as first-class entities (not pulled out of Tajeer DTOs on the fly).
3. A new `Adapters.Seed` package populates realistic KSA-shaped demo data on first run of an empty Dev DB — Saudi National + Iqama IDs, real-format plate numbers, plausible Arabic + English names, multiple branches across Riyadh/Jeddah/Dammam, multi-tenant variety — so staging demos look real.
4. The Day 6 webhook receiver and beyond reference the deepened model directly (no more "minimum to make happy path compile").

## 2. Scope

**In scope:**
- Expand `Domain.Leases.Lease` with the full BI attribute set + invariant transitions for every new state event.
- New aggregates: `Customer`, `Vehicle`, `Driver`, `Branch`, `RentPolicy`, `ExtendedCoverage`.
- EF Core configurations + a single migration `Add_Core_Aggregates` that creates the new tables AND alters `Leases` with the new columns.
- New `Adapters.Seed` package (Pattern A) with:
  - `IDataSeeder` port in `Application.Ports.Seeding`.
  - `BogusDataSeeder` impl producing 2 tenants × (3 branches, 4 rent policies, 3 extended coverages, 20 customers — mix of B2B and B2C, 60 vehicles, 80 drivers, 10 Leases pre-seeded in mixed states for BI demos).
  - `EmptyDataSeeder` impl for empty start.
  - `Seed:Mode` config switch (`Demo` | `Empty` | `ImportedFile` placeholder) so future data-management modules replace the seeder without code change.
- A BFF startup hook that runs the seeder once on Development environment (idempotent — checks if Customers table is empty).
- An EF-shaped catalog of Tajeer lookups (branches, rent policies, payment methods, fuel levels, ID types, contract types, closure reasons, suspension reasons) seeded from Spec 03 §7 + observed staging shapes so the SaveContract form can validate dropdown values offline.
- Updated `SaveContractCommandHandler` to look up `CustomerId` / `VehicleId` / `DriverId` references and persist them on `Lease`.

**Deferred (future workstreams):**
- Always Encrypted columns for PII (Week 2 Day 9 alongside RLS).
- RLS policies (Week 2 Day 9).
- Real Tajeer lookup persistence + sync (Week 2 — lookups today are seeded statically; a background sync that pulls from `TajeerLookupClient` lands when the lookup write surface is needed).
- Data-management UI / import modules (Phase 2+).
- BI views / OLAP star schema (Phase 3+ — the goal here is that the OLTP rows CARRY the fields a future star schema would need).

**Out of scope:**
- Any Next.js UI work (still awaiting design.md).
- Real customer / vehicle / driver imports from D365 (Phase 2+).

## 3. Dependencies

| Dependency | Status | Blocks |
|---|---|---|
| Bogus 35.5.1 — already centrally pinned | ✅ | seed data generation |
| `Adapters.Tajeer` DTOs + LookupClient | ✅ in repo | lookup catalogue shape |
| Spec 01 §5 (entity conventions) + Spec 03 §6/§7 (Tajeer DTOs) | ✅ in repo | every entity |
| User confirmation on seed content (tenant names, demo customer names) | needs sign-off | populating BogusDataSeeder |

## 4. Risks

| Risk | Mitigation |
|---|---|
| Migration on existing local DB has a Lease row from Day 5 testing — column adds may need defaults. | New columns default-nullable or have a `DEFAULT` clause; Lease constructor unchanged at minimum-required-field level. Pre-migration `DELETE FROM Leases` if needed (acceptable in Dev). |
| Seed code drifts from real Tajeer payloads. | Lookup catalogue rows reference Spec 03 §7 enum values (e.g. `contractTypeCode: 1`); when real Tajeer lookup data arrives it overwrites by primary key, not duplicates. |
| Synthesized PII accidentally matches a real person. | Bogus generators use KSA locale but with a controlled seed offset; ID prefixes deliberately use the "test-only" ranges where possible. |
| New aggregates expand the surface area enough to delay Week 1 finish. | Time-box to ~2 days; webhook + Day 7 SMS resume after this lands. Day 8 only added if real test gaps appear. |

## 5. Definition of done

- [x] `dotnet build -warnaserror` clean.
- [x] `dotnet test AutoLeaseNet.sln --settings .runsettings` green; total count grows by ≥15 (new entity invariants + seeder shape tests).
- [x] Migration `Add_Core_Aggregates` applies cleanly to `AutoLeaseNet_Dev` from an empty schema AND from the current Day-6-part-1 schema.
- [x] `dotnet run --project services/bff` on a fresh DB produces (per local SQL):
  - 2 tenant rows (or 1 if we keep single-tenant for Week 1)
  - 3+ Branches per tenant
  - 4+ RentPolicies per tenant
  - 20 Customers per tenant (B2B + B2C mix)
  - 60 Vehicles per tenant
  - 80 Drivers per tenant
  - 10 pre-seeded Leases per tenant spanning the LeaseStatus enum
- [x] Documentation updated:
  - Spec 01 entity table reflects new fields.
  - `Adapters.Seed/README.md` explains `Seed:Mode` + how a future data-management module replaces it.
  - Workstream `notes.md` captures decisions + sample SQL row counts.

## 6. Task list (RED → GREEN granularity, 2-5 min each)

### Day A — Entity expansion (Lease + new aggregates)

- [x] **A1** Expand `Domain.Leases.Lease` with BI fields. **Verify**: existing 5 SaveContractCommandHandlerTests still pass; new field-set documented inline.
  - Customer / Vehicle / Driver references (nullable until A2-A4 land)
  - Branch references (WorkingBranchId, ReceiveBranchId, ReturnBranchId)
  - RentPolicyId + ExtendedCoverageId (nullable)
  - Contract dates (Start, End, ActualReturn?)
  - Allowed KM hourly/daily, UnlimitedKm bool, AllowedLateHours
  - ContractTypeCode (enum)
  - Payment: RentAmount, PaidAmount, RemainingAmount, VatAmount, TotalAmount, PaymentMethodCode, DiscountType?, DiscountValue?
  - At-issuance snapshot: FuelLevelCode, StartKm, ConditionNotes
  - At-return snapshot: ReturnFuelLevelCode?, EndKm?, ReturnConditionNotes?, DamagesObserved?
  - State-event timestamps: SavedAtUtc, IssuedAtUtc, SuspendedAtUtc?, ResumedAtUtc?, ClosedAtUtc?, CancelledAtUtc?, ExpiredAtUtc?
  - Suspension/closure reasons (enums per Spec 03 §7.3/§7.4)
  - ExtensionCount (int), CancellationReason (string?)
  - OperatorId (long) — Tajeer's branch operator
  - SaveFailureReason (string?) for the SaveFailed status
  - PiiOptedOut bool — for Right To Be Forgotten (future)
- [x] **A2** New `Domain.Customers.Customer` aggregate. **Verify**: invariants + 1 test for B2B vs B2C factory paths.
  - Type (B2B | B2C), Name (Ar + En), LegalName, CommercialRegistration, VatNumber, NationalAddress, Email, Mobile, Status (Active / Suspended / Closed), KycVerified, KycVerifiedAtUtc?, CreditLimit?, BillingAddress
  - For B2C: PersonName (Ar+En), IdTypeCode, IdNumber (will be encrypted in Week 2), DateOfBirth, Nationality, PreferredLanguage
- [x] **A3** New `Domain.Vehicles.Vehicle` aggregate. **Verify**: factory + status transitions test.
  - PlateNumber, PlateLetters (Ar tri-letter), PlateTypeCode, VIN, Make, Model, ModelYear, Color, FuelType, TransmissionType, BodyType, Seats, EngineNumber
  - LicenseExpiryDate, InsuranceExpiryDate, InspectionExpiryDate (MVPI)
  - Status: Available / Reserved / OnRent / InService / Damaged / Sold / Disposed
  - OwnerBranchId, CurrentBranchId
  - Current KM, LastServiceKm, LastServiceDate, NextServiceDueKm, NextServiceDueDate
  - PurchasePrice, PurchaseDate, PurchaseInvoiceRef
  - Telematics: TelematicsProvider?, DeviceImei?, LastTelemetryAtUtc? (Phase 3 fields, present for BI from day one)
- [x] **A4** New `Domain.Drivers.Driver` aggregate. **Verify**: factory + license-expiry helper test.
  - Linked CustomerId (nullable — a driver can be unaffiliated at create time)
  - PersonName (Ar+En), IdTypeCode, IdNumber, DateOfBirth, Nationality
  - DriverLicenseNumber, LicenseClass, LicenseIssuePlaceId, LicenseIssueDate, LicenseExpiryDate
  - Mobile, Email, NationalAddress
  - TammAuthorizationStatus (NotRequested / Pending / Authorized / Rejected), TammAuthorizationRef, TammAuthorizedAtUtc?
  - DefenseDrivingCertHeld bool, AccidentCountLast3Yrs int
  - Status: Active / Suspended / Banned
- [x] **A5** New `Domain.Branches.Branch` aggregate. **Verify**: factory + isActive flag.
  - Code, NameAr, NameEn, CityAr, CityEn, RegionAr, RegionEn, LicenseNumber, Address, Latitude, Longitude
  - TajeerBranchId (FK to Tajeer's id), TajeerOperatorId (default branch operator)
  - WorkingHours JSON, IsActive
- [x] **A6** New `Domain.RentPolicies.RentPolicy` aggregate. **Verify**: factory.
  - Code, NameAr, NameEn, BaseDailyRate, BaseHourlyRate, AllowedKmPerDay, AllowedKmPerHour, UnlimitedKm bool, LateHourFee, ExtraKmFee, MinRentalDays, MaxRentalDays, IsActive
- [x] **A7** New `Domain.ExtendedCoverages.ExtendedCoverage` aggregate. **Verify**: factory.
  - Code, NameAr, NameEn, DailyRate, DeductibleAmount, CoverageType (PartialCDW / FullCDW / SCDW / TheftProtection), IsActive

### Day B — EF mapping + migration

- [x] **B1** EF Core configurations for Customer / Vehicle / Driver / Branch / RentPolicy / ExtendedCoverage. **Verify**: `dotnet build` clean; navigation properties + indexes per Spec 01 §5.
- [x] **B2** Update `LeaseConfiguration` for the expanded `Lease` columns + FK relationships to the new aggregates.
- [x] **B3** Generate migration `Add_Core_Aggregates`. **Verify**: `dotnet ef migrations add` succeeds; inspect SQL.
- [x] **B4** Apply migration to local `AutoLeaseNet_Dev` (will need to `DELETE FROM Leases` first; document in notes).
- [x] **B5** Repository interfaces in `Application.Ports.Persistence`: `ICustomerRepository`, `IVehicleRepository`, `IDriverRepository`, `IBranchRepository`, `IRentPolicyRepository`. **Verify**: compiles.
- [x] **B6** EF impls in `Infrastructure.Persistence.Repositories`. **Verify**: compiles.

### Day C — Seed adapter

- [x] **C1** New `Adapters.Seed` package (Pattern A) — csproj + namespace skeleton. **Verify**: builds.
- [x] **C2** `IDataSeeder` port in `Application.Ports.Seeding`. **Verify**: compiles.
- [x] **C3** `EmptyDataSeeder` impl returning immediately. **Verify**: unit test.
- [x] **C4** `BogusDataSeeder` impl with KSA-shaped generators. **Verify**: shape test — counts match expectations (e.g. `Generate(tenantId).Customers` returns 20 with B2B/B2C ratio ~30/70).
- [x] **C5** Static Tajeer lookup catalogue (branches, rent policies, payment methods, fuel levels, ID types, contract types, closure reasons, suspension reasons) sourced from Spec 03 §7 enums + plausible Arabic names. Persisted into the new lookup tables.
- [x] **C6** `AddSeed(IServiceCollection, IConfigurationSection)` extension with `Seed:Mode` switch (Demo / Empty / ImportedFile-throws-NotImplemented). **Verify**: 3 registration tests.
- [x] **C7** BFF startup hook (`app.Environment.IsDevelopment()` only) that resolves `IDataSeeder` and runs once if `Customers` table is empty. **Verify**: integration test — fresh in-memory DB + `Seed:Mode=Demo` populates rows; second startup is no-op.
- [x] **C8** Wire `AddSeed` into `services/bff/Program.cs`. **Verify**: `dotnet run` produces seed rows on first start.

### Day D — Wire the SaveContract path

- [x] **D1** Update `SaveContractCommand` + handler to accept domain-shaped inputs (CustomerId, VehicleId, DriverId, RentPolicyId, BranchId references) — the Tajeer V9.7 DTO is BUILT from those references inside the handler, not passed through unchanged. **Verify**: existing 5 handler tests adapted to the new shape; +2 new tests for "Customer not found → 422" and "Vehicle not available → 422".
- [x] **D2** Update `POST /api/v1/dev/save-contract` body shape to mirror D1. **Verify**: existing 3 endpoint tests adapted; +1 test for vehicle-not-available 422.
- [x] **D3** Update Day-5 notes recipe to use the new (domain-shaped) body.
- [x] **D4** Update `LeaseConfiguration` foreign keys + add seed-data-aware test asserting that the Day-5 happy path with a real seeded Customer + Vehicle still produces `PendingIssuance`.

### Day E — Documentation + commit

- [x] **E1** Update Spec 01 §5 with the new entity tables + cross-link to seed catalogue.
- [x] **E2** `Adapters.Seed/README.md` explains `Seed:Mode` and the future data-management-module swap pattern.
- [x] **E3** Workstream `notes.md` — task table, design decisions, sample SQL row counts after `dotnet run`, drift fixes, verification block.
- [x] **E4** Update `week-1-status` memory to reflect the inserted workstream.
- [x] **E5** Commit (single commit covers all of Days A-E).

### Day F — BFF lookup endpoints

- [x] **F1** Application use cases (MediatR queries) for each lookup: `GetBranchesQuery`, `GetRentPoliciesQuery`, `GetExtendedCoveragesQuery`, `GetCustomersPagedQuery`, `GetVehiclesPagedQuery`, `GetDriversPagedQuery`. Each tenant-scoped via injected `ITenantContext`. **Verify**: per-query handler unit test against EF InMemory + seeded harness.
- [x] **F2** BFF endpoints under `/api/v1/lookups/*`. Pagination params (`page` default 1, `pageSize` default 50, max 200) + `search` filter (case-insensitive name/code contains) + status filter where applicable (Vehicles). All `RequireAuthorization()`. **Verify**: per-endpoint integration test asserting tenant isolation + paging shape (`{ items, page, pageSize, totalCount }`) + search filter.
- [x] **F3** Update `notes.md` with the new endpoint list + curl/PowerShell samples.
- [x] **F4** Commit + push (Days A-F as one cohesive commit, OR split into two commits if size becomes unwieldy — decide at commit time).
- [x] **F5** Resume Day 6 (part 2): endpoint + dedup + LeaseIssued dispatch against the deepened model.

## 7. Decisions (locked by user 2026-05-23)

| # | Question | Decision |
|---|---|---|
| 1 | Tenant count for seed data | **1 tenant** — matches current single-tenant Week 1 setup. Multi-tenant variety lands when a tenancy management UI is added (Week 2+). |
| 2 | Demo tenant + customer naming style | **Real-sounding KSA names** — e.g. tenant "Riyadh Auto Lease"; B2B customers like "Saudi Aramco", "STC", "Almarai"; B2C with KSA-shaped Arabic + English names. User accepts the confusion risk for authentic demos. |
| 3 | B2B / B2C customer mix | **30% B2B / 70% B2C** — mirrors typical car-rental volume. 6 B2B + 14 B2C = 20 customers. |
| 4 | Lookup catalogue persistence | **Dedicated DB tables** — Branches / RentPolicies / ExtendedCoverages / PaymentMethods / FuelLevels / IdTypes / ContractTypes / ClosureReasons / SuspensionReasons all become EF entities with rows. BI views can JOIN, future Tajeer lookup sync overwrites by primary key. |
| 5 | BFF lookup endpoints in this workstream | **Yes — include** — adds 6 read endpoints (Branches, RentPolicies, ExtendedCoverages, Customers paged, Vehicles paged, Drivers paged) so any subsequent UI form can populate dropdowns immediately. Workstream becomes ~5 days total. |

### Implications added to scope

- **Day F** (new) — BFF lookup endpoints: `GET /api/v1/lookups/branches`, `/lookups/rent-policies`, `/lookups/extended-coverages`, `/lookups/customers?page&pageSize&search`, `/lookups/vehicles?page&pageSize&search&status`, `/lookups/drivers?page&pageSize&search`. Each with paging where appropriate (default 50, max 200), case-insensitive name/code search, tenant-scoped (via TenancyMiddleware claim), AllowAnonymous = false. Per-endpoint integration test asserting tenant isolation + paging + search.
- **Lookup tables** to materialise in B1 + EF configs:
  - Tenant-scoped tables: `Branches`, `RentPolicies`, `ExtendedCoverages`, `Customers`, `Vehicles`, `Drivers`.
  - Platform-wide (shared across tenants) tables: `PaymentMethods`, `FuelLevels`, `IdTypes`, `ContractTypes`, `ClosureReasons`, `SuspensionReasons`, `Nationalities`, `IssuePlaces`.
- **Seed counts** (single tenant, "Riyadh Auto Lease"):
  - 3 Branches (Olaya HQ, King Fahd Road, Diplomatic Quarter)
  - 4 RentPolicies (Standard Daily, Standard Hourly, Daily-with-Driver, Long-term Monthly)
  - 3 ExtendedCoverages (Partial CDW, Full CDW, Super CDW)
  - 20 Customers — 6 B2B (Saudi Aramco, STC, Almarai, SABIC, Maaden, Bin Dawood) + 14 B2C (Bogus KSA-locale Arabic + English names with valid-shaped Saudi National / Iqama IDs)
  - 60 Vehicles — Toyota Camry/Corolla, Hyundai Elantra/Sonata, Nissan Altima/Patrol, Kia Cerato, Mitsubishi Lancer (KSA fleet staples) with real-format Saudi plate triples
  - 80 Drivers — mix of customer-affiliated (B2B fleet drivers) and freelance, with TAMM authorization status variety
  - 10 Leases — spanning all 9 LeaseStatus values plus 2 Active for richer reporting demos
  - Platform lookups — full Spec 03 §7 enum sets (~30 PaymentMethods, 6 FuelLevels, 4 IdTypes, 4 ContractTypes, 6 ClosureReasons, 2 SuspensionReasons, ~15 Nationalities, ~13 IssuePlaces)
