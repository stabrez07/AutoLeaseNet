# Workstream — Customer Portal: Vehicle detail page

**Date opened**: 2026-05-29
**Predecessors**: PR #24 (My Vehicles), PR #25 (`BffTestHostDefaults`), PR #26 (Lease detail).
**Goal**: Drill-in from `/vehicles` → `/vehicles/{id}` showing identification, regulatory expiries (license / insurance / MVPI inspection), service schedule, and insurance contact. Completes the symmetric demo path — both customer list pages now have drill-ins.

## Why now

PR #26 retro carry-forward #2: *"Vehicle detail page — drill-in from the vehicles table. Same shape as this PR; could clone in ~60 min."* The trust-boundary handler pattern is now firmly established (PR #24 + PR #26 both use it), so a third implementation is largely mechanical and pays off the demo-path symmetry.

## Scope

**In**:
- `GetMyVehicleDetailQuery(Guid VehicleId)` + `MyVehicleDetailDto` in `AutoLeaseNet.Application.Me`. DTO carries customer-visible fields only: plate triple, make/model/year, color, fuel/transmission/body codes, seats, regulatory expiries, current KM + next service due, insurance company + policy number.
- `GetMyVehicleDetailQueryHandler` in `AutoLeaseNet.Infrastructure.Me`. Same trust shape as `GetMyVehicles`: lease-side `EXISTS` check (caller's customer has a current Active/Extended/Suspended lease on this vehicle) gates the Vehicle read under `SystemTenancyScope`. Returns `null` for "not visible" so endpoint emits 404 without distinguishing "doesn't exist" from "not yours".
- `GET /api/v1/me/vehicles/{id:guid}` in `MeEndpoints.cs`. Same shape as `/leases/{id}`.
- 3 BFF endpoint tests + 3 handler tests.
- `app/vehicles/[id]/page.tsx` — three card sections: identification, regulatory expiries, service. Loading / not-found / error states.
- `app/vehicles/page.tsx` — wrap the plate cell in `<Link>` to drill in.
- `lib/bff-client.ts` — `MyVehicleDetail` interface + `getMyVehicleDetail(id)`.
- AR + EN i18n strings for `vehicleDetail.*`.

**Out**:
- VIN / engine number — mildly PII; not in customer-facing DTO.
- Branch / financial / telematics / notes — operator-only fields.
- Service-due alerts or push notifications.
- `BffTestSeedWaiter` extract — separate cleanup PR (now four retros asking).

## Design notes

### Why "current lease" (Active/Extended/Suspended), not historical

Symmetry with `GetMyVehicles` which scopes to the same set. A customer who returned a car six months ago wouldn't expect to see that car under "my vehicles" — the list page already filters them out. Allowing drill-in to a vehicle from a closed lease would be a different feature ("lease history with vehicle reference") and would need a different mental model.

### Trust boundary

Identical to `GetMyVehicles`:
1. Query Leases under the natural scope, filtered to caller's customer + this vehicle id + currently-holding statuses. If no row exists, return null.
2. Open `SystemTenancyScope.For(tenantId)` bounded strictly to the Vehicles read.
3. Project to DTO.

The lease-side EXISTS check is the trust anchor; the Vehicles read can never return a vehicle the caller doesn't have a current lease on. Phase 2's customer-derived RLS on Vehicles collapses this to a single LINQ join.

## Plan (RED → GREEN)

1. **RED** — `services/bff.tests/Endpoints/MyVehicleDetailEndpointTests.cs`: anon → 401, unknown id → 404, known id → 200 with shape.
2. **RED** — `packages/application/AutoLeaseNet.Infrastructure.Tests/Me/GetMyVehicleDetailQueryHandlerTests.cs`: returns DTO when caller has a current lease on the vehicle, returns null when only a Closed lease exists, returns null when no lease at all, throws on missing CustomerId.
3. **GREEN** — `MyVehicleDetailQuery.cs` (Application.Me).
4. **GREEN** — `GetMyVehicleDetailQueryHandler.cs` (Infrastructure.Me).
5. **GREEN** — `MapGet("/vehicles/{id:guid}", …)` in `MeEndpoints.cs`.
6. **GREEN** — `bff-client.ts`: `MyVehicleDetail` interface + `getMyVehicleDetail(id)`.
7. **GREEN** — `app/vehicles/[id]/page.tsx` mirroring the leases-detail card layout.
8. **GREEN** — `app/vehicles/page.tsx`: wrap plate cell in `<Link>`.
9. **GREEN** — AR + EN i18n for `vehicleDetail.*`.
10. **Verify** — `dotnet test AutoLeaseNet.sln` (344 + 6 = 350); `pnpm --filter customer-portal build`.
11. Retrospective, ai_context bump, commit, PR, squash-merge.

## Risks

- **Returning null for "closed lease" could surprise** the customer — they might expect to see a car they returned last week. Mitigation: the My Vehicles list already filters by the same rule, so the symmetry is consistent. Worst case is a 404 from a link they typed by hand; acceptable in Phase 1.
- **Insurance policy number is mildly sensitive**. Decided to expose since the customer already received the policy at issuance; the portal doesn't reveal anything new.

## Definition of Done

- [ ] 6 new tests pass (3 endpoint + 3 handler).
- [ ] `dotnet build` + `pnpm build` clean.
- [ ] Vehicles list plates clickable; detail page renders identification/regulatory/service; 404 path doesn't crash.
- [ ] retrospective.md filed.
- [ ] ai_context.md bumped.
- [ ] PR opened, CI green, squash-merged.
