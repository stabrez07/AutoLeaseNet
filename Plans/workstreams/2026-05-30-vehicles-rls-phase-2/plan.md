# Phase-2 Vehicles RLS extension

**Branch:** `feat/vehicles-rls-phase-2`
**Started:** 2026-05-30
**Status:** Merged 2026-05-30 (PR #30)

---

## Why this exists

Day-9's `Add_RLS_TenancyPolicy` migration applied a `(TenantId, NULL)` predicate to
`dbo.Vehicles`, which makes external customers see zero rows. Three handlers
worked around that with a two-step pattern:

1. RLS-scoped Leases query to derive `vehicleIds` belonging to the caller.
2. `SystemTenancyScope` bypass to read `dbo.Vehicles WHERE Id IN (vehicleIds)`.

That pattern is correct (the lease query is the trust anchor) but it leaks the
RLS bypass into the handler — and three handlers reproducing it is exactly the
duplication the [Day-9 retro](../2026-05-29-day-9-rls-tenant-isolation/retrospective.md)
and PR #29 already flagged. The Phase-2 fix promised in
`GetMyVehiclesQueryHandler.cs:36-38` is to put a customer-derived predicate on
`dbo.Vehicles` so each handler collapses to a single LINQ join.

## Design

### New RLS predicate function — `dbo.fn_VehiclesTenancyPredicate(@TenantId, @Id)`

Mirrors `fn_TenancyPredicate`'s WEBHOOK_BOOTSTRAP / INTERNAL_STAFF / SYSTEM
clauses, then for external users runs:

```sql
EXISTS (
    SELECT 1 FROM dbo.Leases AS l
    WHERE l.VehicleId  = @Id
      AND l.TenantId   = @TenantId
      AND l.CustomerId = CAST(SESSION_CONTEXT(N'CustomerId') AS UNIQUEIDENTIFIER)
)
```

Deliberately **no status filter in the predicate** — RLS answers "is the
customer entitled to know this vehicle exists?" The handler still answers "do
they currently hold it?" (Active/Extended/Suspended). Splitting it this way
lets the lease-detail handler show vehicle info for closed historical leases
without re-bypassing RLS.

### Migration shape

`ALTER SECURITY POLICY dbo.TenancyPolicy DROP FILTER PREDICATE ON dbo.Vehicles`
(plus the two BLOCK predicates), then `ADD FILTER PREDICATE
dbo.fn_VehiclesTenancyPredicate(TenantId, Id) ON dbo.Vehicles` (plus BLOCK pair).
The CREATE FUNCTION lives in the same migration so the policy can reference it
in the same Up().

`Down()` reverses: drop the new predicates from the policy, drop the function,
re-add the old `(TenantId, NULL)` predicates.

### Handler refactor

All three collapse to a single RLS-scoped query with an EXISTS join. The EXISTS
mirrors the new RLS predicate; under InMemory (no RLS) it's the only control,
under real SQL+RLS it's belt-and-braces. Same pattern PR #29 used for the
seeder predicate.

```csharp
// GetMyVehiclesQueryHandler (after)
return await db.Vehicles.AsNoTracking()
    .Where(v => v.TenantId == tenant.TenantId
        && db.Leases.Any(l => l.VehicleId == v.Id
                              && l.CustomerId == customerId
                              && CurrentlyHoldingStatuses.Contains(l.Status)))
    .OrderBy(v => v.Make).ThenBy(v => v.Model).ThenBy(v => v.ModelYear)
    .Select(...)
    .ToListAsync(cancellationToken);
```

`GetMyVehicleDetailQueryHandler` — same shape, `Where(v.Id == request.VehicleId && …)`.

`GetMyLeaseDetailQueryHandler` — the vehicle enrichment loses the
`SystemTenancyScope`. The lease the customer just queried (which is itself
RLS-scoped to them) gives the `vehicleId`. Under Phase-2 RLS the customer can
read that vehicle row because they hold (or held) a lease on it.

## Tests

### RED — new RLS isolation tests for Vehicles
`Integration` category, real SQL. New file
`RlsIsolationTests.Vehicles.cs` (or extend `RlsIsolationTests.cs`):

- External customer with active lease on Vehicle X → can read X.
- External customer with NO lease on Vehicle Y → cannot read Y.
- External customer with only closed lease on Vehicle Z → CAN read Z (history).
- Internal staff → reads all tenant vehicles.
- Cross-tenant query → blocked (same as Day-9).

### GREEN — existing tests stay green
- `GetMyVehiclesQueryHandlerTests` (3 tests, InMemory) — refactor keeps app-side
  EXISTS, so all three (status filter, empty-on-no-leases, throw-on-no-customer)
  still pass.
- `GetMyVehicleDetailQueryHandlerTests` (4 tests, InMemory) — same.
- `MyVehiclesEndpointTests`, `MyVehicleDetailEndpointTests`, `MyLeaseDetailEndpointTests`
  (BFF, InMemory) — same.

## Tasks

- [x] Create branch `feat/vehicles-rls-phase-2`
- [x] Write this plan
- [x] Write `VehiclesRlsIsolationTests.cs` (RED — would fail today because Vehicles RLS hides all rows from external customers)
- [x] Add migration `Add_Vehicles_RLS_PhaseTwo` with `fn_VehiclesTenancyPredicate` + policy ALTER
- [x] Refactor `GetMyVehiclesQueryHandler` — single query, no `SystemTenancyScope`
- [x] Refactor `GetMyVehicleDetailQueryHandler` — single query, no `SystemTenancyScope`
- [x] Refactor `GetMyLeaseDetailQueryHandler` — drop `SystemTenancyScope` around vehicle enrichment
- [x] Update handler-class XML docs (drop the "Phase-2 follow-up" notes; replace with "trust comes from EXISTS join below + DB-side fn_VehiclesTenancyPredicate")
- [x] `dotnet test` — all 358 unit tests still green (RLS test trait is Integration so it's excluded from default run)
- [x] Run integration test locally against SQL to verify GREEN — 7 new tests pass
- [x] Bump `ai_context.md`: decision row #14 + current repo state + migration list
- [x] Write retrospective.md
- [x] Commit, push, open PR, squash-merge, sync main

## Verification (DoD)

- [x] All 358 unit tests pass; +7 new Integration tests pass
- [x] New RLS isolation tests pass against local SQL
- [x] `dotnet build` clean (warnings-as-errors)
- [x] Three handlers no longer reference `SystemTenancyScope`
- [x] Migration applies on top of `Add_ZatcaChainState`
- [x] PR merged to `main`
