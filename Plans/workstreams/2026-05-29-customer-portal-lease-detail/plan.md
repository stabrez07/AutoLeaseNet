# Workstream — Customer Portal: Lease detail page

**Date opened**: 2026-05-29
**Predecessors**: PR #22 (Customer Portal scaffold), PR #24 (My Vehicles), PR #25 (BffTestHostDefaults).
**Goal**: Drill-in from `/leases` → `/leases/{id}` showing contract terms, the assigned vehicle (when set), payment breakdown, and a lifecycle timeline. Adds `GET /api/v1/me/leases/{id}` end-to-end. Cheaper to ship now thanks to PR #25's helper.

## Why now

Customer-portal-scaffold retro carry-forward #5: *"Customer Portal — Lease detail page — drill-in from the leases table."* The leases list is the most-clicked page in the demo path; without a drill-in it's a dead-end. Vehicles list has the same gap but is less central; tackle vehicle detail in a follow-up bundle.

## Scope

**In**:
- `GetMyLeaseDetailQuery(Guid LeaseId)` + `MyLeaseDetailDto` in `AutoLeaseNet.Application.Me`. DTO carries: contract terms (start/end/typeCode, allowed-km, late-hours), full payment block (rent, paid, remaining, vat, total, paymentMethodCode, discount), the assigned vehicle's plate triple + make/model/year when set, status, all the lifecycle timestamps the customer can usefully see (Saved, Issued, Suspended, Resumed, Closed, Cancelled, Expired), and extension count.
- `GetMyLeaseDetailQueryHandler` in `AutoLeaseNet.Infrastructure.Me`. Lease query is RLS-scoped to the caller; bounded `SystemTenancyScope` block for the vehicle read (same trust boundary discussion as PR #24 — the lease's VehicleId acts as the WHERE-IN clause). Returns null when the lease id isn't visible to the caller (RLS hid it → not-found from the caller's POV).
- `GET /api/v1/me/leases/{id:guid}` in `MeEndpoints.cs`: 401 anon, 400 missing CustomerId, 404 unknown / not mine, 200 detail.
- 3 BFF endpoint tests + 3 handler tests using the new `BffTestHostDefaults` helper.
- `app/leases/[id]/page.tsx` — three sections: contract terms, vehicle (if assigned), payment + timeline. Loading/error/not-found states.
- Make `app/leases/page.tsx` rows clickable — `<Link href={`/leases/${l.id}`}>`.
- `bff-client.ts` — `MyLeaseDetail` interface + `getMyLeaseDetail(id)` method.
- AR + EN i18n strings for the new page sections.

**Adjacent fix (in same PR)**: PR #22's customer-portal status code map is off-by-one from the `LeaseStatus` enum. i18n `statuses` dictionary uses keys `1..7 + 99` but the actual enum is `Draft=0, SaveFailed=1, PendingIssuance=2, Active=3, Extended=4, Suspended=5, Closed=6, Cancelled=7, ExpiredDraft=8`. Dashboard's `l.status === 2 || l.status === 3` (intended as "Active or Extended") actually counts "PendingIssuance or Active". `statusTone` has the same shift. Fixing as part of this workstream because:
1. The detail page reuses the same i18n + helper.
2. It's a real demo-blocker — wrong labels would survive into the customer's screen.
3. The fix is mechanical (~12 lines across 3 files).

**Out**:
- Vehicle detail page (separate workstream — pairs nicely with this one).
- Payment receipt download / invoice PDF.
- Edit / cancel actions (Phase 2 — these are mutating endpoints).
- `BffTestSeedWaiter` extract (the next-biggest factory copy-paste; separate cleanup PR).

## Design notes

### Status code fix scope

What it should be after this PR (matching the enum):

```
0:  Draft
1:  Save failed
2:  Pending issuance
3:  Active
4:  Extended
5:  Suspended
6:  Closed
7:  Cancelled
8:  Expired draft
```

Dashboard filter: `active = leases.filter(l => l.status === 3 || l.status === 4).length` (Active or Extended). `closed = leases.filter(l => l.status === 6).length`. `statusTone` cases shift accordingly.

### Vehicle on detail page when lease has no VehicleId

`Lease.VehicleId` is nullable today — Day-5 callers construct from Tajeer DTOs before the Day-D vehicle-lookup reshape. The detail page must tolerate `null` and render "Not yet assigned" rather than crashing.

### 404 vs 403

The handler returns `null` for "not visible to this customer" (RLS hides the row). The endpoint returns 404 — deliberately indistinguishable from "doesn't exist" so the customer portal doesn't leak the existence of leases the caller doesn't own. Same pattern HTTP-status-wise as a typical REST API.

## Plan (RED → GREEN)

1. **Fix the i18n / dashboard / statusTone status code map** — first, before any new code; running existing leases-list tests after each edit to confirm no regressions.
2. **RED** — `services/bff.tests/Endpoints/MyLeaseDetailEndpointTests.cs` with 3 tests: anon → 401, external-customer + bad id → 404, external-customer + good id → 200 with shape.
3. **RED** — `packages/application/AutoLeaseNet.Infrastructure.Tests/Me/GetMyLeaseDetailQueryHandlerTests.cs` with 3 tests: returns full DTO when caller owns the lease, returns null when not visible, throws when CustomerId missing.
4. **GREEN** — `MyLeaseDetailQuery.cs` (Application.Me) with `GetMyLeaseDetailQuery(Guid LeaseId)` + `MyLeaseDetailDto` + nested `LeaseVehicleSummaryDto`.
5. **GREEN** — `GetMyLeaseDetailQueryHandler.cs` (Infrastructure.Me). Step 1: lease lookup under natural scope. Step 2: bounded `SystemTenancyScope` for the vehicle read iff `VehicleId != null`. Project to DTO.
6. **GREEN** — `MapGet("/leases/{id:guid}", …)` in `MeEndpoints.cs`. 401 anon, 400 missing CustomerId, 404 when handler returns null, 200 otherwise.
7. **GREEN** — `bff-client.ts`: `MyLeaseDetail` interface + `getMyLeaseDetail(id)`.
8. **GREEN** — `app/leases/[id]/page.tsx`. Three card sections; "Back to all leases" link; 404 friendly redirect to list.
9. **GREEN** — Make `app/leases/page.tsx` rows clickable via Link wrap.
10. **GREEN** — AR + EN i18n for `leaseDetail.*`.
11. **Verify** — `dotnet test AutoLeaseNet.sln` clean (337 + 6 = 343); `pnpm --recursive build` clean.
12. Retrospective, ai_context bump, commit, PR, squash-merge.

## Risks

- **Status code fix could ripple** — but the existing leases page has only the dashboard count + the badge label, both purely cosmetic. Worst case: a test in `MeEndpointTests` that asserted on `first.Status.Should().BeGreaterThan(0)` continues to pass (since all statuses are ≥ 0).
- **`Lease.VehicleId` is nullable** in the domain but real leases for a Day-D-or-later customer SHOULD have it. The detail page must still tolerate null gracefully — confirmed in the design.
- **`SystemTenancyScope` bypass is back** for the vehicle read. Same trust-boundary discussion as PR #24 applies; the lease's `VehicleId` is the WHERE-IN. Documented inline.

## Definition of Done

- [ ] Status code map matches the enum across `i18n.ts`, `app/page.tsx` filters, and `ui.tsx#statusTone`.
- [ ] 6 new tests pass (3 endpoint + 3 handler); existing tests still green.
- [ ] `dotnet build` + `pnpm build` both clean.
- [ ] Leases list rows clickable; detail page renders contract/vehicle/payment/timeline; 404 path doesn't crash.
- [ ] retrospective.md filed.
- [ ] ai_context.md bumped.
- [ ] PR opened, CI green, squash-merged, branch deleted.
