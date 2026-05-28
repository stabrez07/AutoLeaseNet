# Retrospective — Inspection Aggregate

**Closed**: 2026-05-25
**Plan**: [plan.md](./plan.md)
**Outcome**: shipped per plan. Domain → Application → Infrastructure →
Seed → BFF stack lit up in one workstream; saga integration intentionally
deferred to the next.

## What we delivered

- `Inspection` aggregate (~30 fields per Spec 01 §5.6) + `InspectionPhoto` +
  `InspectionDamageMarker` child entities with Tajeer-canvas (893×429) bounds
  enforced at the domain layer.
- 4 enums: `InspectionType`, `InspectionStatus`, `FuelLevel`, `DamageMarkerType`.
- `InspectionCompletedDomainEvent` (no Phase-1 subscriber; saga workstream will
  wire one — the existing `DomainEventDispatchInterceptor` publishes with zero
  consumers cleanly, so this is a safe forward declaration).
- `IInspectionRepository` port + `EfInspectionRepository`.
- 5 MediatR commands + handlers (Start / AddPhoto / AddDamageMarker / Complete /
  Abandon), all idempotency-cached through the shared `IIdempotencyStore`.
- 2 MediatR queries + handlers (GetById, Search) + `InspectionSummaryDto` /
  `InspectionDetailDto`.
- EF migration `Add_Inspection_Aggregate` applied to local `AutoLeaseNet_Dev`.
- Seed data: 1 CHECK_OUT per non-terminal lease + CHECK_OUT + CHECK_IN for
  closed leases, with deterministic damage markers.
- 7 BFF endpoints under `/api/v1/inspections` + `/api/v1/lookups/inspections`.
- 7 BFF endpoint tests + 17 domain unit tests.

## What was easy

- TDD turned around fast: 17 RED tests → all green after the aggregate landed in
  ~10 min. The aggregate's invariants were already well-specified in Spec 01 §5.6
  + Spec 02 §4.6, so almost no design decisions had to happen mid-flow.
- The `AddAutoLeaseNetDbContext` helper from PR #9 paid off immediately: the
  test factory was a one-liner swap, no inline `AddInterceptors` to worry about.
- The `DomainEventDispatchInterceptor` (PR #7) absorbed `InspectionCompletedDomainEvent`
  with zero changes — exactly the "any caller of SaveChangesAsync gets
  transparent dispatch" promise.

## What bit us

- **`PerformedByUserId != Guid.Empty` invariant + dev JWT stub default**:
  the dev JWT stub's `UserId` claim defaults to a per-process Guid, but the
  `ClaimsTenantContext` returned `null` for it in the test path (no
  `X-Dev-User-Id` header set). 3 endpoint tests failed with 422 on first run.
  Fix: test factory now sets `X-Dev-User-Id` explicitly. Real fix for prod
  would be to make the `ClaimsTenantContext.UserId` resolution fall back to
  the synthesized default, but that's a separate small follow-up.

- **EF migration tooling**: same caveat as prior migrations — running
  `dotnet ef migrations add ... --startup-project services/bff` fails because
  the BFF csproj doesn't reference `Microsoft.EntityFrameworkCore.Design`. The
  workaround is `--startup-project packages/application/AutoLeaseNet.Infrastructure`
  (using Infrastructure as both project + startup). Documented in the plan;
  a tiny follow-up could add the Design package to the BFF as a private asset.

## Scope discipline notes

The plan held tight — no scope creep into:
- Photo upload via `Adapters.Storage` (still URI-string only).
- Lease invariants 2/3 (CHECK_OUT-required-for-ACTIVE, CHECK_IN-required-for-CLOSED)
  — those land with the check-out / check-in sagas.
- Incident aggregate.

## Tests

| Project                  | Before | After |
| ------------------------ | ------ | ----- |
| Adapters.Tajeer          | 45     | 45    |
| Adapters.Common          | 20     | 20    |
| Infrastructure           | 3      | 3     |
| Application              | 43     | 60    |
| Bff                      | 40     | 47    |
| **Total**                | **151**| **175**|

+24 (17 domain + 7 BFF). Zero regressions.

## Follow-ups noted (not in scope here)

- **Check-out saga** (Spec 02 §6.3): wire `Lease.MarkIssued` to require a
  matching `CHECK_OUT` `Inspection` row. New workstream — likely combined
  with the check-in saga since they share infrastructure.
- **Check-in saga** (Spec 02 §6.4): `Lease.MarkClosed` requires a `CHECK_IN`
  row (or a `SUSPENDED → CLOSED` path).
- **Photo upload through `Adapters.Storage.AzureBlob`** with a SAS-URL flow.
- **`Incident` aggregate** (Spec 01 §5.6, Spec 02 §4.7).
- **Renter e-signature capture** + image upload.
- **`InspectionCompletedDomainEvent` subscriber** — likely the saga handler
  that flips the Lease to a state-machine-permissible status.
- **Add `Microsoft.EntityFrameworkCore.Design` to the BFF csproj** so the
  `dotnet ef migrations add --startup-project services/bff` form works.
- **Fix `ClaimsTenantContext.UserId` default** to match the dev JWT stub's
  per-process default so the test factory doesn't need to set the header
  explicitly.
