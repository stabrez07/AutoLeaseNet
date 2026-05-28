# Retrospective — Check-out Saga: Link CHECK_OUT Inspection → Lease

**Closed**: 2026-05-25
**Plan**: [plan.md](./plan.md)
**Outcome**: shipped per plan. Day 18 of Week 3 (slim slice) closed.

## What we delivered

- `Inspection.LinkToLease(leaseId, nowUtc)` domain method on the aggregate
  from PR #12. Enforces: COMPLETED status, CheckOut/PreDelivery type,
  no existing link, non-empty leaseId. Idempotent on re-link to the same
  Lease.
- New `LeaseLinkedAtUtc` audit timestamp.
- `IInspectionRepository.GetLatestUnlinkedCheckOutForVehicleAsync(...)`
  port + EF impl (tracked so the link mutation rides the same UoW).
- `SaveContractCommand.CheckOutInspectionId` (optional) + handler
  validation + auto-lookup fallback. Five error codes added:
  `lease.checkout_inspection.{not_found, vehicle_mismatch, not_completed,
  wrong_type, already_linked}`.
- BFF `SaveContractDevRequest.CheckOutInspectionId` (optional) wires it end-
  to-end.
- `BogusDataSeeder` drives the link through `LinkToLease` for CHECK_OUT
  inspections so seeded rows carry the new audit field.

## What was easy

- The plan's "Phase 1.x optional, Phase 1.y required" framing made the change
  non-breaking from the start — none of the 6 existing SaveContract tests
  needed any modification.
- The `DomainEventDispatchInterceptor` (PR #7) + `AddAutoLeaseNetDbContext`
  helper (PR #9) didn't need any touch — the new mutation persists naturally
  because the resolved Inspection is change-tracked in the same DbContext.

## What bit us

- **Test helper pre-set `LeaseId`** in `NewInProgress`, which broke the
  "reject re-link to different Lease" test (the helper had already linked
  the aggregate at `Start` time, so the first explicit `LinkToLease` call hit
  the "already linked" branch instead of the test's intended second call).
  Fix: helper now defaults `leaseId` to null; the one test that wanted the
  pre-link passes it explicitly via `leaseId: LeaseId`.

- **`SaveContractCommandHandlerTests` harness wired the SUT with hand-rolled
  ctor args**, so adding the `IInspectionRepository` constructor parameter
  required updating the harness too. Caught immediately by the build break
  (CS7036). One-line fix.

## Tests

| Project                  | Before | After |
| ------------------------ | ------ | ----- |
| Adapters.Tajeer          | 45     | 45    |
| Adapters.Common          | 20     | 20    |
| Infrastructure           | 3      | 3     |
| Application              | 60     | 72    |
| Bff                      | 47     | 47    |
| **Total**                | **175**| **187**|

+12 (7 domain LinkToLease + 5 SaveContract integration). Zero regressions.

## Follow-ups noted (not in scope here)

- **Phase 1.y — flip `CheckOutInspectionId` to required**. Done after the
  web portal drives the full check-out flow end-to-end (so existing CLI /
  test callers don't break in the meantime).
- **Day 19 — Check-in saga**. `Lease.MarkClosed` requires a COMPLETED
  CHECK_IN inspection per Spec 01 §invariant 3. Will mirror this slice's
  structure: a `CheckInLeaseCommand`, link via a new method on Inspection,
  and Tajeer adapter calls for `CalculateContractPayment` + `Close Contract`.
- **Enforce invariant 2 at `Lease.MarkIssued` (webhook path)**. Today the
  Tajeer webhook calls `MarkIssued` without checking that a CHECK_OUT
  inspection is linked. Once the SaveContract gate flips to required, the
  webhook path becomes naturally safe (the Lease can't exist without an
  Inspection link). Until then, this is a latent gap.
