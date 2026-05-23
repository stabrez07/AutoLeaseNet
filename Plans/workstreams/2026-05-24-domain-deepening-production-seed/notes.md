# Notes — Domain Deepening + Production-Shaped Seed Data

## Outcome

Closed 2026-05-24 with **136 / 136 tests green** (smoke excluded). All six days landed in
a single workstream: domain entities deepened to full BI granularity, a new
`Adapters.Seed` package populating real-sounding KSA data, the SaveContract path
reshaped to use domain references, and six BFF lookup endpoints over the seeded data.

| Day | Description | Outcome |
|---|---|---|
| A | Lease expansion + 6 new aggregates (Customer / Vehicle / Driver / Branch / RentPolicy / ExtendedCoverage) | 11 Lease-transition tests + 16 aggregate-invariant tests = 27 new tests |
| B | EF Core configs + `Add_Core_Aggregates` migration applied + 6 repository ports + impls | Migration applied to `AutoLeaseNet_Dev`; 8 tables present |
| C | `Adapters.Seed` package, `BogusDataSeeder`, BFF startup hook | Verified live: 3 Branches / 4 RentPolicies / 3 ExtendedCoverages / 20 Customers (6 B2B + 14 B2C) / 60 Vehicles / 80 Drivers / 10 Leases (spans all 9 LeaseStatus values) |
| D | `SaveContractCommand` + handler + `POST /dev/save-contract` reshaped to domain references; handler resolves aggregates, validates, builds the Tajeer V9.7 DTO internally | 8 handler tests (incl. customer-not-found / vehicle-not-available / license-expired / TAMM-not-authorised) + 4 endpoint tests |
| E | Documentation + commit | This file + plan ticks + memory update |
| F | 6 BFF lookup endpoints: `/api/v1/lookups/{branches,rent-policies,extended-coverages,customers,vehicles,drivers}` | 10 endpoint tests (paging shape, search filter, status filter, tenant isolation, pageSize clamp) |

## Design decisions

### 1. Lookup query handlers live in Infrastructure, not Application
The natural place for `GetBranchesQueryHandler` is alongside the other MediatR
handlers in Application, but those handlers would need `AutoLeaseNetDbContext` —
forcing Application → Infrastructure (wrong dependency direction). Two options:

- **(a)** Add an `IReadDb` port that exposes `IQueryable<T>` from Application.
- **(b) ✅ chosen** — keep the query records + DTOs in `Application.Lookups` and put
  the handlers in `Infrastructure.Lookups`. MediatR scans both assemblies. Cleaner
  than option (a) — no abstract leak of `IQueryable`, no extra projection ceremony.

### 2. Pattern B sub-client reference in Application is intentional
`SaveContractCommandHandler` references `ITajeerContractClient` directly (Spec 04 §3.2).
Day D's reshape shifted the handler's INPUT to domain types but kept the OUTPUT
construction (Tajeer V9.7 DTO) inline in the handler. A separate `ITajeerRequestBuilder`
abstraction would be over-engineered for the Phase-1 single-vendor case.

### 3. Vehicle reservation invariant on Save
`Vehicle.Reserve()` is called from the handler immediately after the Tajeer call
succeeds and before `SaveChangesAsync`. This means concurrent Save attempts on the
same vehicle should serialise through optimistic concurrency (RowVersion). A
defensive `vehicle.Status != Available` check earlier in the flow rejects the second
caller with `lease.vehicle.not_available` before Tajeer is ever called.

### 4. Seed catalogue is idempotent + reproducible
`BogusDataSeeder.SeedAsync` short-circuits when `ICustomerRepository.AnyAsync(tenantId)`
returns true. `Randomizer.Seed` is set from `SeedOptions.RandomSeed` (default
`20260524`) so generated IDs are byte-for-byte reproducible across machines and runs.

### 5. Lookup endpoints are tenant-scoped, never use anonymous reads
Every handler calls `LookupGuards.RequireTenant(tenant)`. The 9th lookup endpoint
test asserts `401 Unauthorized` when no auth header is present, proving the
`RequireAuthorization()` decorator on the group is wired correctly.

### 6. Pagination shape: `{ items, page, pageSize, totalCount }`
Default page size = 50, max 200 (`PagedResult.DefaultPageSize` / `MaxPageSize`). The
10th lookup test asserts the max clamp — `?pageSize=10000` is silently clamped to 200,
no `400` (matches MDN's "robustness principle for query params").

## Live row counts after first BFF boot (verified Day C)

```
sqlcmd -E -d AutoLeaseNet_Dev -I -Q "SELECT 'Branches' AS t, COUNT(*) AS n FROM Branches ..."

Branches            3
RentPolicies        4
ExtendedCoverages   3
Customers          20
Vehicles           60
Drivers            80
Leases             10
```

The 10 seeded leases span every status:
`PendingIssuance(×2)`, `Active(×2)`, `Extended(×1)`, `Suspended(×1)`, `Closed(×1)`,
`Cancelled(×1)`, `ExpiredDraft(×1)`, `SaveFailed(×1)`.

## Drift fixed during the workstream

| Item | Severity | Resolution |
|---|---|---|
| Global `dotnet-ef` was net10 — couldn't load our net8 DbContext | Low | (resolved earlier in Day 5) Local `.config/dotnet-tools.json` pinning 8.0.5; migrations always invoked via `dotnet tool run dotnet-ef`. |
| `Microsoft.Extensions.Configuration.Binder` was not centrally pinned | Low | Added to `Directory.Packages.props`; `Adapters.Seed.csproj` references it for `IConfigurationSection.Get<T>()`. |
| CA1305 on `int.ToString()` in plate generation | Low | Wrapped with `CultureInfo.InvariantCulture`. |
| CA1512 on manual `throw new ArgumentOutOfRangeException` in `Lease.CreatePending` | Low | Switched to `ArgumentOutOfRangeException.ThrowIfNegativeOrZero`. |
| Two `appsettings.Development.json` (one tracked baseline, one local-only) — gitignored file held the Tajeer placeholders | Med | The Day-5 `SaveContractEndpointFactory` injects dummy Tajeer config inline so tests are portable; Day-F endpoint tests reuse it via `IClassFixture`. |
| BFF dll lock from earlier seed run | Low | `Get-Process AutoLeaseNet.Bff | Stop-Process -Force` before re-running tests. |

## Verification

```
dotnet build AutoLeaseNet.sln           → 0 warnings, 0 errors
dotnet test  AutoLeaseNet.sln --settings .runsettings → 136 passed / 0 failed (smoke excluded)

  AutoLeaseNet.Adapters.Common.Tests     : 20 (unchanged)
  AutoLeaseNet.Adapters.Tajeer.Tests     : 45 (unchanged from Day 6 part 1)
  AutoLeaseNet.Application.Tests         : 35 (was 5: +11 Lease transitions, +16 aggregate invariants, +3 Day-D handler validations)
  AutoLeaseNet.Infrastructure.Tests      : 4  (unchanged)
  AutoLeaseNet.Bff.Tests                 : 32 (was 21: +1 Day-D endpoint customer-not-found, +10 Day-F lookup endpoints)
```

## What this unblocks

- Day 6 part 2 (webhook receiver) can now dispatch to a deepened `Lease` that already
  carries every state-transition timestamp; `MarkIssued` writes `IssuedAtUtc` +
  `StartKm` + `StartFuelLevelCode` + `IssuanceConditionNotes` directly.
- Any future UI form can populate dropdowns from `/api/v1/lookups/*` without
  scaffolding new endpoints per screen.
- BI / executive reports have every leasing-relevant field captured from row creation —
  no backfilling later.
- The `Seed:Mode` switch (`Empty` | `Demo` | `ImportedFile` (reserved)) is the
  contract a future data-management module will replace.

## Next pickup

Day 6 part 2 — `POST /api/v1/webhooks/tajeer` endpoint (T6.2), dedup translation (T6.5),
`contract.issue → Lease.MarkIssued` dispatch (T6.6). The `WebhookLog` entity + signature
validator + log-only options were checkpointed as commit `a20b5dc` (Day 6 part 1).
