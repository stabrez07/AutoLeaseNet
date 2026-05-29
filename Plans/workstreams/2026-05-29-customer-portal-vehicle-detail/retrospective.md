# Retrospective — Customer Portal: Vehicle detail page

**Date closed**: 2026-05-29
**PR**: TBD
**Commit**: TBD

## What shipped

The customer portal demo path is now symmetric: both list pages have drill-ins.

**Backend**:
- `GetMyVehicleDetailQuery(Guid VehicleId)` + `MyVehicleDetailDto` in
  `AutoLeaseNet.Application.Me`. DTO surfaces customer-visible fields only —
  plate triple, make/model/year, color, fuel/transmission/body codes, seats,
  regulatory expiries, current KM + next service due, insurance company +
  policy number. VIN / engine number / branch / financial / telematics /
  notes are operator-only and excluded.
- `GetMyVehicleDetailQueryHandler` in `AutoLeaseNet.Infrastructure.Me`. Same
  trust shape as `GetMyVehicles`: lease-side `EXISTS` (caller's customer has
  a lease in Active/Extended/Suspended on this vehicle) gates the Vehicle
  read under `SystemTenancyScope`. Returns `null` for "not visible" → 404.
- `GET /api/v1/me/vehicles/{id:guid}` in `MeEndpoints.cs`.
- 3 BFF endpoint tests + 4 handler tests.

**Frontend (customer-portal)**:
- `app/vehicles/[id]/page.tsx` — three card sections (identification /
  regulatory / service) + friendly not-found + back link.
- `app/vehicles/page.tsx` — plate cell wrapped in `<Link>` to drill in.
- `lib/bff-client.ts` — `MyVehicleDetail` interface + `getMyVehicleDetail(id)`.
- AR + EN i18n for `vehicleDetail.*`.

## What went well

- **Third trust-boundary handler in three PRs** (#24, #26, this) — the
  pattern is fully internalized now. Wrote the new handler from memory in
  ~5 minutes including the XML doc cross-reference to the original.
- **Same factory shape from PR #26** — `MyVehicleDetailFactory` is the
  fourth Demo-mode factory using `BffTestHostDefaults.DemoSeedDefaults`.
  Counts as evidence that PR #25's cleanup is durable; nobody will go back
  to inline-30-line config dictionaries now that the helper is the
  obvious choice.
- **"Closed lease does NOT grant access" test was worth writing** —
  pins the symmetry with `GetMyVehicles`'s currently-holding filter
  explicitly. A future PR that wants to loosen this (e.g. show "past
  vehicles") will see this test fail and have to make a deliberate choice.
- **No i18n surprises this time** — PR #26's status-code fix proved
  there's no other latent off-by-one in the customer portal. The new
  `vehicleDetail.*` keys are integers-as-codes only where the Domain
  uses an int (plate type code, fuel/transmission/body codes); when
  design.md lands, those can become lookup-driven labels.

## What surprised me

- **Insurance policy number visibility** was the one debatable field.
  Decided to include it — the customer already received the policy at
  issuance, the portal doesn't reveal anything new, and it's useful for
  a "claim or roadside assistance" workflow Phase 2 will likely build.
  Worth a future conversation if the user has stricter PII rules in
  mind.
- **The handler doesn't need the lease's `VehicleId` in a WHERE-IN** the
  way `GetMyLeaseDetail` does — the URL parameter IS the vehicle id, and
  the lease-side check is a boolean `AnyAsync`. Simpler than the lease
  detail handler, even though it does the same logical thing.

## What I'd do differently

- **`BffTestSeedWaiter` is now FOUR retros asking** for the extract:
  customer-portal scaffold, My Vehicles, Lease detail, this one. The
  factory's `EnsureSeededAsync` is 15 lines of the same polling loop.
  The next workstream-of-opportunity should take a 20-min detour for
  this.
- **Skipped a `PickAnyCustomerWithLeaseOnVehicleAsync` factory helper**
  that would have made the "happy path" test fully data-driven. The
  current `PickCustomerWithActiveLeaseVehicleAsync` is purpose-built
  here; if the pattern keeps recurring, it can graduate to a shared
  factory helper too.

## Numbers

- Files added (backend): 4 (`MyVehicleDetailQuery.cs`,
  `GetMyVehicleDetailQueryHandler.cs`, `MyVehicleDetailEndpointTests.cs`,
  `GetMyVehicleDetailQueryHandlerTests.cs`).
- Files added (frontend): 1 (`app/vehicles/[id]/page.tsx`).
- Files modified: 4 (`MeEndpoints.cs`, `bff-client.ts`, `i18n.ts`,
  `app/vehicles/page.tsx`).
- Plus: plan + retro.
- Tests: 344 → **351** default (+7: 3 endpoint + 4 handler).
- customer-portal build: 6 routes (`/`, `/leases`, `/leases/[id]`,
  `/vehicles`, `/vehicles/[id]`, `/_not-found`).
- Total elapsed: ~55 min — fastest "third in a series" run yet.

## Hand-off

Demo path symmetric and complete. Updated carry-forward:

1. **`BffTestSeedWaiter` extract** — fourth retro asking. Should land
   before the next BFF endpoint workstream.
2. **ZATCA adapter (Week-4 critical path)** — still zero code, the
   biggest remaining gap on the demo schedule.
3. **Vehicle Replacement Saga** — `IncidentReportedDomainEvent` subscriber.
4. **Close-saga refactor → TajeerStatusMapper** — 5-line cleanup; bundle.
5. **Phase-2 Vehicles RLS extension** — collapses the bypass scope in
   three handlers (GetMyVehicles, GetMyLeaseDetail, GetMyVehicleDetail)
   to single LINQ joins. Increasing payoff each new endpoint.
6. **Always Encrypted on PII** — gated on AKV.
7. **Customer Portal i18n migration to next-intl + `[locale]` segments** —
   both portals' scaffolds promised this; the more handcrafted
   `useLocale()` cookie pattern is fine until design.md lands.
