# Retrospective — Phase-2 Vehicles RLS extension

**PR**: #30 (TBA)
**Branch**: `feat/vehicles-rls-phase-2`
**Merged**: 2026-05-30

---

## What landed

- **New predicate function `dbo.fn_VehiclesTenancyPredicate(@TenantId, @Id)`** —
  mirrors `fn_TenancyPredicate`'s WEBHOOK_BOOTSTRAP / INTERNAL_STAFF / SYSTEM
  cascade, then for external users runs `EXISTS (SELECT 1 FROM dbo.Leases WHERE
  VehicleId = @Id AND TenantId = @TenantId AND CustomerId =
  SESSION_CONTEXT('CustomerId'))`. SCHEMABINDING couples the function to
  `Leases.(VehicleId, TenantId, CustomerId)` — those columns can't drop while
  the function exists. Intended.
- **Migration `20260529235911_Add_Vehicles_RLS_PhaseTwo`** — rewires
  `dbo.TenancyPolicy`'s three Vehicles predicates via `ALTER FILTER PREDICATE`
  / `ALTER BLOCK PREDICATE`. Down() swaps back to `fn_TenancyPredicate(TenantId,
  NULL)` and drops the new function.
- **Three handlers collapsed**:
  - `GetMyVehiclesQueryHandler` — was two-step (RLS-scoped Leases → derive id
    set → SystemTenancyScope wrap for Vehicles). Now one query with an
    application-side EXISTS join that mirrors the new RLS predicate.
  - `GetMyVehicleDetailQueryHandler` — same shape collapse.
  - `GetMyLeaseDetailQueryHandler` — vehicle enrichment shed its
    `SystemTenancyScope`; the new RLS predicate accepts the read because the
    customer has (or had) a lease on the vehicle.
- **7 new `VehiclesRlsIsolationTests` (Integration category)** — external
  customer sees vehicle with active lease, sees vehicle with only closed lease
  (history), does NOT see orphan vehicle, does NOT see cross-tenant vehicle,
  customer with no leases sees zero vehicles in tenant; internal staff sees
  all vehicles in tenant; SYSTEM context sees all in tenant. Run against local
  SQL Server, all green.
- **Net code delta**: ~60 lines deleted (the two-step pattern in three handlers).
  Each handler now reads like the lease-list handler — single LINQ statement,
  no bypass.

## Why this PR mattered

The Day-9 RLS migration parked Vehicles on `(TenantId, NULL)` and the
[`GetMyVehiclesQueryHandler` XML comment](../../packages/application/AutoLeaseNet.Infrastructure/Me/GetMyVehiclesQueryHandler.cs)
flagged the cleanup explicitly: *"Phase-2 follow-up: extend the RLS predicate
on Vehicles with a customer-derived clause … the handler collapses to a single
LINQ join and the SystemTenancyScope goes away."* Three handlers reproducing
the bypass + the Phase-2 retro note + PR #29's matching dedup pattern made this
the next obvious cleanup.

The design-time question — whether RLS should filter by lease *status* — split
the work neatly. RLS answers "is the customer entitled to know this vehicle
exists?" (any lease relationship). The handler answers "are they currently
holding it?" (Active/Extended/Suspended). That split lets the lease-detail
view show vehicle data on a Closed lease without re-bypassing — exactly the
historical-data case the customer portal needs.

## What worked

- **TDD shape**: writing `VehiclesRlsIsolationTests` first locked the predicate
  semantics before any SQL was written. The "external customer with only
  closed lease sees vehicle" test forced the no-status-in-RLS decision into
  the test file before the migration.
- **`dotnet ef migrations add` for a SQL-only migration** still gives clean
  scaffolding (empty `Up`/`Down` + Designer.cs snapshot) that matches the
  Day-9 + ZATCA-chain-state pattern.
- **ALTER PREDICATE in-place swap** worked first try after the DROP+ADD
  experiment failed (see below). Cleaner than re-creating the predicate too.

## What surprised

- **`ALTER SECURITY POLICY` rejects DROP + ADD on the same table in one
  statement.** The planner validates the post-state and reports
  *"A FILTER predicate for the same operation has already been defined on
  table 'dbo.Vehicles'"* even though the DROP would clear the slot before
  the ADD. The fix is `ALTER FILTER PREDICATE … ON dbo.Vehicles` (in-place
  swap). The atomic-rollback behaviour saved me — the failed first attempt
  left no partial state; `__EFMigrationsHistory` was untouched and the
  function wasn't created. Net cost: one wasted migration run.
- **EF Core's design-time snapshot is fine with SQL-only migrations** — no
  model changes means no shadow-snapshot drift. The Designer.cs file
  generated alongside the migration is a no-op snapshot that matches the
  previous one. (Confirmed by diffing against
  `20260529232659_Add_ZatcaChainState.Designer.cs`.)
- **The Day-9 retro's `[Trait("Category", "Integration")]` pattern composes
  cleanly** — the new tests slot in next to `RlsIsolationTests.cs` and CI
  still excludes them by default via the repo-root `.runsettings`.

## Carry-forward (documented for the next pickup)

- **RLS on Inspection child tables** (`InspectionPhotos`, `InspectionDamageMarkers`)
  — both currently lack a `TenantId` column and load only via aggregate root.
  Phase-2 backfill task; not on the demo critical path.
- **Webhook URLs encode tenant** — when this lands, both
  `SystemTenancyScope.ForWebhookBootstrap()` AND the predicate functions'
  `UserType = 'WEBHOOK_BOOTSTRAP'` clauses retire together.
- **Vehicle Replacement Saga** — subscribes to `IncidentReportedDomainEvent`,
  handles vehicle swap mid-lease. Touches the Vehicle aggregate's write path
  (BLOCK predicates now require the customer's EXISTS check, so internal-only
  paths must scope themselves).
- **Quotation aggregate + 3-tier approval (Week-4 entry point)** — biggest
  scope of the carry-forward list.
- **ZATCA Week-4 actual** — UBL 2.1 + ECDSA P-256 + TLV QR + `ZatcaSubmission`
  saga that drives `ZatcaChainState.AdvanceTo`.

## Tests

| Suite | Before | After | Notes |
|---|---|---|---|
| Adapters.Common | 20 | 20 | unchanged |
| Adapters.Tajeer | 82 | 82 | unchanged |
| Adapters.Zatca  | 12 | 12 | unchanged |
| Infrastructure  | 59 | 59 | unit count same; +7 Integration `VehiclesRlsIsolationTests` |
| Application     | 113 | 113 | unchanged |
| Bff             | 72 | 72 | unchanged |
| **Total (unit)** | 358 | 358 | |
| **Total (incl. Integration)** | 14 integration | 21 integration | +7 new Vehicles RLS |

CI run-time unchanged (~2 min). Local integration run including new tests:
< 1 second.
