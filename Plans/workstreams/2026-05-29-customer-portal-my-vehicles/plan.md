# Workstream — Customer Portal: My Vehicles

**Date opened**: 2026-05-29
**Predecessor**: PR #23 (Tajeer GetAsync + drift detector) and PR #22 (Customer Portal scaffold)
**Goal**: Ship `GET /api/v1/me/vehicles` end-to-end — Application query → Infrastructure handler → BFF endpoint → typed customer-portal client → `/vehicles` page with table — so a signed-in customer can see the vehicles currently tied to their active leases.

## Why now

Carry-forward item #4 from the customer-portal-scaffold retro: *"Customer Portal — My Vehicles — needs an `/api/v1/me/vehicles` endpoint that scopes via the customer's leases (since RLS on Vehicles is internal-only by Day-9 design)."*

Demo path: dashboard already shows lease counts; the natural next click is "what am I driving?". My Leases shows contract numbers; My Vehicles shows the cars those contracts cover. Together, dashboard + leases + vehicles is the first three-page user journey on the Customer Portal.

## Scope (PR #24)

**In**:
- `GetMyVehiclesQuery` + `MyVehicleDto` (Application layer).
- `GetMyVehiclesQueryHandler` (Infrastructure layer) — joins `Leases` (RLS-scoped to my customer) to `Vehicles` (which needs `SystemTenancyScope` because the Day-9 Vehicles RLS predicate is internal-staff-only). App-side enforcement: result set is restricted to vehicles attached to leases whose `CustomerId` matches the caller, where the lease is in a status that means the customer currently has the vehicle (Active / Extended / Suspended). Closed / Cancelled / Expired / Pending leases excluded.
- `GET /api/v1/me/vehicles` endpoint in `MeEndpoints.cs`. Same auth + error shape as `/me/leases`: 401 anon, 400 with `me.requires_customer_context` for INTERNAL_STAFF, 200 with vehicle list for EXTERNAL_INDIVIDUAL.
- 3 BFF endpoint tests (clone the MeEndpoints `/leases` shape).
- 2 handler-level tests against EF InMemory pinning the join + status filter.
- `apps/customer-portal/lib/bff-client.ts` — add `MyVehicle` interface + `getMyVehicles()` method.
- `apps/customer-portal/app/vehicles/page.tsx` — table: plate (Tajeer Arabic-letter format), make/model/year, color, current KM, license expiry. Loading / empty / error states like the leases page.
- Add "My Vehicles" to nav + dashboard "Currently driving" stat tile.
- i18n: EN + AR strings for the new page.

**Out**:
- RLS on Vehicles with a customer-derived predicate (Phase 2 — migration comment already flags it).
- Lease detail page (separate workstream).
- Photo / inspection attachments.
- Per-vehicle damage / incident history.
- Vehicle availability for B2B fleet admins (different shape — they want every car they own, not just leased-out ones; that's web-portal territory anyway).

## Design notes

### Why `SystemTenancyScope` in the handler

The Day-9 RLS migration applies `dbo.fn_TenancyPredicate(TenantId, NULL)` to `Vehicles`, deliberately blocking external-user reads. The migration comment plans a Phase-2 follow-up to add a CustomerId-derived predicate. Phase 1 unblocks the demo with:

1. Query `Leases` under the natural request scope (RLS scopes to my customer's leases — that's the security control).
2. Distinct-project to `VehicleId` for those leases in `Active | Extended | Suspended`.
3. Open a `SystemTenancyScope.For(tenantId)` block and re-query `Vehicles` filtered by that id set.

The trust boundary is the handler: the SystemTenancyScope is bounded to the read, the id set is derived from RLS-scoped lease rows, so it's algebraically impossible for the handler to return a vehicle the caller doesn't have a lease on. Auditable in one screenful.

When Phase 2 adds a customer-derived Vehicles predicate, this handler simplifies to a single LINQ join — but Phase 2 needs the join column populated on Vehicles (it isn't today) and is a Vehicles-schema change, not a quick demo unblock.

### Why filter on status ∈ Active / Extended / Suspended

"My vehicles" colloquially means *the cars I currently have*. A Closed or Cancelled lease released the vehicle — listing the car would confuse the customer ("why is the Camry I returned last month on this page?"). PendingIssuance hasn't yet handed the keys over. Suspended is included because the contract is still live, the customer just can't drive it until reactivated.

### Why no per-vehicle drill-in this PR

The leases page also has no drill-in yet. Adding one to vehicles would create a UI asymmetry; better to land both drill-ins together in a single "detail pages" PR after both list pages exist.

### Plate display

`PlateNumber` + `PlateLetters` + `PlateTypeCode` is Tajeer's KSA format (Spec 03 §11.1). The Domain comment says presentation-layer conversion to legacy ENG-letter format is a separate helper. Phase 1: render as `{PlateLetters} {PlateNumber}` (Arabic letters render correctly with the `dir="rtl"` page wrap on AR locale; LTR locale will just show them in input order). Plate type code is shown as a small badge — code-only for now; the lookup-to-label join is a small future enhancement.

## Plan (RED → GREEN, 2–5 min tasks)

1. **RED** — `services/bff.tests/Endpoints/MyVehiclesEndpointTests.cs` with 3 tests cloned from `MeEndpointTests`: anonymous → 401, INTERNAL_STAFF → 400 (`me.requires_customer_context`), EXTERNAL_INDIVIDUAL → 200 with a JSON array shape. Use a fresh `MyVehiclesFactory` mirroring `MeFactory`. Build fails: `GetMyVehiclesQuery` / `MyVehicleDto` / endpoint don't exist.
2. **RED** — `packages/application/AutoLeaseNet.Infrastructure.Tests/Me/GetMyVehiclesQueryHandlerTests.cs` with 2 tests: (a) returns vehicles for Active / Extended / Suspended leases of the calling customer only — Closed lease's vehicle excluded; (b) returns empty list when the caller has no leases. EF InMemory + a hand-rolled `ITenantContext`. Build fails: handler doesn't exist.
3. **GREEN** — Create `packages/application/AutoLeaseNet.Application/Me/MyVehiclesQuery.cs` with `GetMyVehiclesQuery` + `MyVehicleDto` (Id, PlateNumber, PlateLetters, PlateTypeCode, Make, Model, ModelYear, Color, CurrentKm, LicenseExpiryDate, InsuranceExpiryDate).
4. **GREEN** — Create `packages/application/AutoLeaseNet.Infrastructure/Me/GetMyVehiclesQueryHandler.cs`. Step 1 query Leases for VehicleIds. Step 2 `SystemTenancyScope.For(tenant.TenantId)` block + Vehicles query. Project to `MyVehicleDto`. Throw `InvalidOperationException("/me/vehicles requires a customer context …")` when CustomerId is missing.
5. **GREEN** — Add `MapGet("/vehicles", …)` to `MeEndpoints.cs`. Same `try { Ok } catch (InvalidOperationException) { 400 }` pattern as `/leases`.
6. **Verify** — `dotnet test` → all 320+5 tests pass.
7. **GREEN** — Customer-portal: add `MyVehicle` interface + `getMyVehicles()` in `lib/bff-client.ts`.
8. **GREEN** — Add `app/vehicles/page.tsx` mirroring `app/leases/page.tsx`. Loading / empty / error states.
9. **GREEN** — Add "My Vehicles" nav link in `components/app-shell.tsx`.
10. **GREEN** — Add "Currently driving" stat tile to `app/page.tsx` dashboard (count from `/me/vehicles`).
11. **GREEN** — Add EN + AR i18n strings: `nav.myVehicles`, `dashboard.cards.currentlyDriving`, `vehicles.*` (title, subtitle, empty, columns: plate, makeModel, year, color, km, licenseExpiry, insuranceExpiry).
12. **Verify** — `pnpm build` (both portals) green.
13. Retrospective, ai_context bump, commit, PR, squash-merge.

## Risks

- **Vehicles RLS bypass via `SystemTenancyScope` is a real escape hatch** — the handler must be small and auditable. Code review (self) checklist: (1) the SystemTenancyScope is bounded to the Vehicles query only, (2) the vehicle id set comes from a Leases query that is NOT under SystemTenancyScope (so it's customer-RLS-filtered), (3) the Vehicles query has a `WHERE Id IN (…)` so it can't return a vehicle outside that id set. If any of those three is wrong, an external user could read every vehicle in the tenant.
- **InMemory tests don't prove RLS** (same caveat as `/me/leases`). Real-SQL proof lives in `RlsIsolationTests` — adding a `/me/vehicles` row there is a follow-up, not blocking.
- **Plate letter rendering on LTR locale** — Arabic-letter plate strings in an EN UI may render visually odd. Acceptable for Phase 1; design.md will land the right typography.

## Definition of Done

- [ ] 5 new tests green (3 endpoint + 2 handler)
- [ ] `dotnet build` + `dotnet test` clean across the solution
- [ ] `pnpm build` clean for both portals
- [ ] Manually verified: signed-in demo customer sees their vehicles on `/vehicles` and the dashboard tile populates
- [ ] retrospective.md filed
- [ ] ai_context.md bumped
- [ ] PR opened, reviewed, squash-merged, branch deleted
