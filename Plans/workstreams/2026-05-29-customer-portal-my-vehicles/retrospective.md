# Retrospective — Customer Portal: My Vehicles

**Date closed**: 2026-05-29
**PR**: TBD
**Commit**: TBD

## What shipped

End-to-end `/me/vehicles` slice — the second demo-unblocking page on the
Customer Portal after the dashboard + leases scaffold.

**Backend**:
- `GetMyVehiclesQuery` + `MyVehicleDto` in `AutoLeaseNet.Application.Me`.
- `GetMyVehiclesQueryHandler` in `AutoLeaseNet.Infrastructure.Me`. Two-step
  query — lease-side filter under natural RLS to derive vehicle ids, then a
  bounded `SystemTenancyScope.For(tenantId)` block for the Vehicles read.
  Lease-side status filter: `Active | Extended | Suspended` (colloquial
  "the vehicle I currently have" — Closed / Cancelled / Expired / Pending
  excluded).
- `GET /api/v1/me/vehicles` endpoint added to `MeEndpoints.cs` with the same
  401 / 400 / 200 shape as `/me/leases`.
- 3 BFF endpoint tests (anonymous, internal-staff, external-customer).
- 3 handler-level tests (status filter + customer scoping, empty-when-no-leases,
  throws when CustomerId missing). One more than the plan's "2 tests"; the
  missing-CustomerId path felt worth pinning explicitly given the trust-boundary
  discussion.

**Frontend (customer-portal)**:
- `lib/bff-client.ts` — `MyVehicle` interface + `getMyVehicles()` method.
- `app/vehicles/page.tsx` — table mirroring the leases page: plate (Tajeer
  Arabic-letter format inside a `dir="rtl"` span so the letters render in
  intended order on the EN locale too), make/model, year, color, current KM,
  license expiry, insurance expiry. Loading / empty / error states.
- `components/app-shell.tsx` — "My Vehicles" added to nav.
- `app/page.tsx` — dashboard expanded to **4** stat cards; new "Currently
  driving" tile counts `/me/vehicles`; second CTA link.
- `lib/i18n.ts` — EN + AR strings for `nav.myVehicles`,
  `dashboard.cards.currentlyDriving`, `dashboard.ctaVehicles`, `vehicles.*`.

## Key design decision: SystemTenancyScope in the handler

The interesting call was how to read Vehicles for an external user when the
Day-9 RLS policy passes `NULL` for CustomerId on the Vehicles table
(internal-staff-only by design). Three options were on the table:

1. **Extend Vehicles RLS now** — add a customer-derived clause to the
   predicate. The cleanest answer, but it's a Vehicles-schema change (Vehicles
   has no CustomerId; the relationship is via Lease). Not a quick demo unblock.
2. **App-side join via Lease navigation** — would still hit the Vehicles RLS
   block since the predicate filters before the join.
3. **Two-step with bounded `SystemTenancyScope`** — what shipped. The
   security control is that the vehicle id set comes from a NOT-bypassed
   Leases query, and the Vehicles read has a `WHERE Id IN (idSet)` clause,
   so it's algebraically impossible to return a vehicle the caller doesn't
   have a lease on.

Wrote the handler XML doc as if it were a security review — three invariants
called out explicitly that must hold under future edits. The handler is 90
lines of code total; it should fit on one screen of an audit review.

The retro for the Day-9 RLS workstream had already flagged the Phase-2
follow-up of "extend Vehicles RLS with a customer-derived predicate". That
remains the right end-state. When it lands, this handler collapses to a
single LINQ join and the SystemTenancyScope goes away.

## What went well

- The customer-portal-scaffold PR #22 was excellent template material —
  bff-client + locale + i18n + UI primitives + table page pattern all
  cloneable. The leases page → vehicles page mapping was almost mechanical,
  which is exactly what a good scaffold should produce.
- Following the same "EF InMemory tests pin shape; RLS proof is separate"
  honesty as the previous Me-endpoint tests kept this PR small. No
  temptation to over-claim what an InMemory test can prove.
- The two-step handler is the kind of decision that benefits from explicit
  inline documentation — wrote the trust-boundary discussion as XML doc on
  the class, not in a separate ADR. A reviewer hitting this handler in 6
  months will immediately see WHY it's shaped this way.
- The handler tests use a `StubTenantContext` — a one-liner that didn't
  require any test-only DI plumbing. Considered putting it in a shared test
  helpers folder; deferred since no other Infrastructure.Tests file needs
  it yet.

## What surprised me

- **Captured-parameter analyzer (CS9124)** fired on the primary-ctor form of
  `StubTenantContext` because I both captured the parameter into a property
  and used it in computed-property expressions for `UserType` / `IsInternalStaff`.
  Switched to an explicit ctor that assigns to readonly properties. Worth
  remembering: primary ctors are great until you want to do *anything* with
  the parameter beyond a single property capture.
- **`dotnet test`'s "Passed!" summary line is per-project, not solution-wide**
  — when filtered to the test output, only the last project's totals appear.
  Had to run `--logger "console;verbosity=minimal"` then grep to confirm
  337 = 20+82+55+113+67. Easy to be misled by the BFF-only "67 passed" line
  into thinking that's the total.
- **OutboxDrainService verbose SQL errors in test output** are pure noise
  during test-host shutdown — the in-memory tests don't have SQL configured,
  the drain loop tries to open a connection, logs the failure, the loop
  continues. Should add a "test runs short-circuit drain" config someday
  (`Outbox:Enabled=false` already exists in the factories; the noise comes
  from the few that don't set it).

## What I'd do differently

- **`BffTestHostDefaults` shared helper is now overdue from FIVE retros**.
  `MyVehiclesFactory` is a near-exact copy of `MeFactory` which is a
  near-exact copy of `IncidentFactory` etc. Every new `/me/*` endpoint
  workstream is going to need this. The cost of the copy is small per
  workstream; the cost of NOT having a single source of truth for "what
  config does a test host need" is that next year's onboarding dev will
  spend a day untangling 8 similar-but-not-identical factories. Should
  cut its own PR (~30 min: extract the config dictionary + the
  ConfigureTestServices dance into a static helper, retrofit the 8
  factories).
- **Skipped a server-side ordering test**. Plan said to order by
  Make/Model/ModelYear and the handler does, but I didn't write an explicit
  test for it. Acceptable for Phase 1 — the BI value is low and the test
  would be brittle to seed changes — but worth knowing.

## Numbers

- Files added (backend): 4 (`MyVehiclesQuery.cs`, `GetMyVehiclesQueryHandler.cs`,
  `MyVehiclesEndpointTests.cs`, `GetMyVehiclesQueryHandlerTests.cs`).
- Files added (frontend): 1 (`app/vehicles/page.tsx`).
- Files modified: 6 (`MeEndpoints.cs`, `bff-client.ts`, `i18n.ts`,
  `app-shell.tsx`, `app/page.tsx`, `ai_context.md`).
- Plus: plan + retro.
- Tests: 331 → **337** default (+6: 3 endpoint + 3 handler).
- Both portals build green: customer-portal (**4** routes incl. new
  `/vehicles`), web-portal (7 routes — unchanged).
- Total elapsed: ~75 min.

## Hand-off

Two pages on the Customer Portal now (leases + vehicles) plus a 4-tile
dashboard. The natural next steps:

1. **Lease detail page** — drill-in from the leases table. Same shape as
   vehicles: new BFF endpoint `GET /api/v1/me/leases/{id}` + page.
2. **Vehicle detail page** — drill-in from the vehicles table. Show service
   history, regulatory expiries, any open incidents.
3. **`BffTestHostDefaults` shared helper** — FIVE retros now. Should land
   before the next BFF endpoint workstream.
4. **ZATCA adapter (Week-4 critical path)** — still zero code, the longest
   pole left for Week-4 demo.
5. **Vehicle Replacement Saga** — `IncidentReportedDomainEvent` subscriber
   per Spec 02 §6.5.
6. **Close-saga refactor → TajeerStatusMapper** — 5-line cleanup, ideally
   bundled with another small PR.
7. **Drop `continue-on-error: true` from JS CI** — both portals build
   cleanly now (this PR keeps that property true).
8. **Phase-2 follow-up**: extend `Vehicles` RLS with a customer-derived
   predicate, then simplify `GetMyVehiclesQueryHandler` to a single join.

Each its own PR per the cadence.
