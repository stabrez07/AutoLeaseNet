# Retrospective — Customer Portal: Lease detail page

**Date closed**: 2026-05-29
**PR**: TBD
**Commit**: TBD

## What shipped

The leases list is no longer a dead-end: click a contract number to drill in.

**Backend**:
- `GetMyLeaseDetailQuery(Guid LeaseId)` + `MyLeaseDetailDto` + nested
  `LeaseVehicleSummaryDto` in `AutoLeaseNet.Application.Me`. DTO carries
  contract terms, the full payment block, lifecycle timestamps, reason codes,
  and the assigned vehicle's plate triple + make/model/year when set.
- `GetMyLeaseDetailQueryHandler` in `AutoLeaseNet.Infrastructure.Me`. Same
  two-step trust shape as `GetMyVehiclesQueryHandler`: lease read with explicit
  CustomerId predicate (under natural scope, RLS-redundant in prod, InMemory-honest
  here), then a bounded `SystemTenancyScope.For(tenantId)` block iff the lease
  has a `VehicleId`. Returns `null` for "not visible" so the endpoint emits 404
  without distinguishing "doesn't exist" from "not yours".
- `GET /api/v1/me/leases/{id:guid}` in `MeEndpoints.cs`. 401 anon, 400 missing
  customer context, 404 unknown / not mine, 200 detail.
- 3 BFF endpoint tests + 4 handler tests.

**Frontend (customer-portal)**:
- `app/leases/[id]/page.tsx` — four card sections (contract / vehicle /
  payment / timeline) with a "Back to all leases" link + a friendly
  not-found state.
- `app/leases/page.tsx` — contract number cell wrapped in `<Link>` to drill in.
- `lib/bff-client.ts` — `LeaseVehicleSummary` + `MyLeaseDetail` interfaces +
  `getMyLeaseDetail(id)`.
- AR + EN i18n for `leaseDetail.*` — sections, contract labels, payment
  labels, timeline labels.

**Adjacent fix included**: PR #22's customer-portal `LeaseStatus` code map was
off-by-one against the `Domain/Leases/LeaseStatus.cs` enum. The i18n dictionary
used keys `1..7 + 99`; the actual enum is `Draft=0, SaveFailed=1,
PendingIssuance=2, Active=3, Extended=4, Suspended=5, Closed=6, Cancelled=7,
ExpiredDraft=8`. The dashboard "Active" filter was actually counting
"PendingIssuance or Active". `statusTone` had the same shift. Fixed in this PR
because the detail page reused the same i18n + helper and would have inherited
the wrong labels. Comment added to both helpers anchoring them to the enum
file so the next reader doesn't have to dig.

## What went well

- **`BffTestHostDefaults` paid off immediately** — the new endpoint test +
  factory was ~140 lines (test logic) instead of ~190 (with the inline
  config dance PR #25 deleted). First post-helper BFF workstream confirms
  the cleanup was worth doing.
- **Reusing the trust-boundary doc from PR #24** — the handler's XML doc
  references `GetMyVehiclesQueryHandler` so readers find the longer
  explanation in one place. Saved a screen of repeat prose.
- **404 contract is explicit**: the handler returns nullable + the endpoint
  maps to NotFound, with the comment "deliberately indistinguishable from
  'doesn't exist'". A future PR that wants to leak existence (rare) has to
  consciously change both layers.
- **Found the status-code-map bug before the customer would have** —
  surveying the existing leases page to clone its pattern surfaced the
  off-by-one. If I'd jumped straight into the detail page without re-reading
  the list page, the bug would have shipped twice and the demo would have
  shown "Active" for PendingIssuance contracts.

## What surprised me

- **The off-by-one bug was easy to miss because the labels still looked
  plausible**. `2: 'Active'` reads fine on its own; only by cross-referencing
  against the .cs enum is the misalignment obvious. The fix added a comment
  pinning the keys to the enum file (`Domain/Leases/LeaseStatus.cs`). Lesson:
  any client-side enum-int map needs to cite the source.
- **`SaveContractEndpointFactory`'s seeder-wait loop and the new
  `MyLeaseDetailFactory`'s are identical** — 5 lines of `while
  (DateTime.UtcNow < deadline)` polling for a row to appear. That's the
  `BffTestSeedWaiter` extract the PR #25 retro flagged. Did not bundle here
  to keep this PR's intent focused; remains the top tech-debt carry-forward.
- **`useParams<{ id: string }>()` returns possibly-undefined** in Next 14
  App Router typings. Had to guard `if (!id) return` early or the build
  would have complained. Small surprise relative to the docs.

## What I'd do differently

- **Could have added an `IClassFixture` for the new factory** to avoid the
  per-test seeder spin-up cost. Defaulted to per-test factories for symmetry
  with the existing pattern; the BffTestSeedWaiter extract can introduce
  shared-fixture cadence as a separate cleanup.
- **Skipped the `MeFactory.PickAnyCustomerIdAsync` arbitrary-customer
  pattern**'s drawback: it picks the FIRST customer in the seeded set, not
  necessarily one with leases. For the "200 with shape" test I added
  `PickAnyLeaseIdAsync` (lease-first) so the assertion is non-vacuous. Worth
  pinning as a future test-helper convention: when the test needs a row,
  pick by the entity it actually needs, not by an upstream FK.

## Numbers

- Files added (backend): 4 (`MyLeaseDetailQuery.cs`,
  `GetMyLeaseDetailQueryHandler.cs`, `MyLeaseDetailEndpointTests.cs`,
  `GetMyLeaseDetailQueryHandlerTests.cs`).
- Files added (frontend): 1 (`app/leases/[id]/page.tsx`).
- Files modified: 6 (`MeEndpoints.cs`, `bff-client.ts`, `i18n.ts`,
  `components/ui.tsx` — statusTone fix, `app/page.tsx` — dashboard filter
  fix, `app/leases/page.tsx` — link wrap).
- Plus: plan + retro.
- Tests: 337 → **344** default (+7: 3 endpoint + 4 handler).
- customer-portal build: 5 routes (`/`, `/leases`, `/leases/[id]`,
  `/vehicles`, `/_not-found`).
- web-portal build: unchanged (7 routes).
- Total elapsed: ~80 min.

## Hand-off

Three customer-facing pages now: dashboard + leases list + lease detail +
vehicles list. Carry-forward picklist (updated):

1. **`BffTestSeedWaiter` extract** — next-biggest factory copy-paste, third
   retro asking now.
2. **Vehicle detail page** — drill-in from the vehicles table. Same shape
   as this PR; could clone in ~60 min.
3. **ZATCA adapter (Week-4 critical path)** — still zero code.
4. **Close-saga refactor → TajeerStatusMapper** — 5-line cleanup; bundle.
5. **Vehicle Replacement Saga** — `IncidentReportedDomainEvent` subscriber.
6. **Phase-2 RLS extension on Vehicles** — collapses the bypass scope in
   GetMyVehicles + GetMyLeaseDetail to a single LINQ join.
7. **Always Encrypted on PII** — gated on AKV.
