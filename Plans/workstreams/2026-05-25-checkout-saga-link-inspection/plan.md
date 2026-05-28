# Workstream — Check-out Saga: Link CHECK_OUT Inspection → Lease (Day 18 slice)

**Started**: 2026-05-25
**Closes**: Day 18 of [`Plans/02-phase-1-mvp-week-by-week.md`](../../02-phase-1-mvp-week-by-week.md) (check-out saga, slim slice).
**Owner**: solo dev + Claude

## Goal

Tie the existing `Inspection` aggregate (PR #12) to the `Lease` it belongs to so the
spec invariant from [Spec 01 §invariant 2](../../../Specs/01-multi-tenancy-and-domain-model.md#8-aggregate-rules--invariants)
is materialized: a Lease coming out of `SaveContract` must reference its
CHECK_OUT Inspection. Phase 1.x keeps the link **optional** (caller may omit)
because existing seed + tests already create the aggregates separately; once the
web portal is updated to drive the full saga, the field flips to required.

## Scope (in)

1. **Domain**: `Inspection.LinkToLease(Guid leaseId, DateTimeOffset nowUtc)` —
   only legal when `Status == Completed && Type ∈ {CheckOut, PreDelivery} && LeaseId is null`.
   Idempotent on same-leaseId re-entry; rejects if linking to a different lease.
2. **Application.Ports**: `IInspectionRepository.GetLatestCheckOutForVehicleAsync(...)` —
   returns the most recent COMPLETED + un-linked CHECK_OUT for a vehicle (used
   by SaveContract auto-link when the caller doesn't pass an id explicitly).
3. **Application**: `SaveContractCommand` gains optional `CheckOutInspectionId`;
   `SaveContractCommandHandler` validates + links on success. If the explicit id
   is provided but invalid (not found / wrong vehicle / wrong type / already
   linked elsewhere) → fail with `lease.checkout_inspection_invalid`. If omitted,
   handler does an auto-lookup; missing inspection is **non-fatal in Phase 1.x**.
4. **Seed**: `BogusDataSeeder` backfills `Inspection.LeaseId` on the seeded
   CHECK_OUT inspections (and CHECK_IN ones for closed leases) so the existing
   demo data reflects the new model.
5. **BFF**: `SaveContractDevRequest` gains optional `CheckOutInspectionId`.
6. **Tests**: domain unit tests for `LinkToLease`; handler tests for the
   link-on-success + invalid-id paths; existing tests stay green.

## Scope (out)

- Making `CheckOutInspectionId` required — Phase 1.y (after the web portal
  drives the full saga).
- Enforcing invariant 2 at `Lease.MarkIssued` time (the webhook path). Today
  the link is set at SaveContract; webhook just transitions an already-linked
  Lease. Tracked as a follow-up.
- Pessimistic vehicle lock at saga start — the existing unique filtered index
  on `(VehicleId, Status IN ('PendingIssuance','Active','Extended','Suspended'))`
  already prevents double-booking at commit time; pessimistic lock is a Phase 2
  optimization for UX feedback.
- Tajeer `CalculateContractPayment` + `Close Contract` — Day 19 workstream.
- `Incident` aggregate, suspend/resume sagas — separate workstreams.

## Risks

- **Backfilling seed adds 1 extra round-trip on dev startup** — only on seed
  Mode=Demo; trivial cost.
- **Old SaveContract tests already pass without an inspection** — keep them
  green by making the field optional; net new tests cover the new behavior.
- **InspectionRepository injection into SaveContractCommandHandler** —
  one more constructor arg; the handler already takes 10+ ports, this is in
  scope for the existing pattern.

## RED → GREEN → REFACTOR

- [x] **T1** — branch `feat/checkout-saga-link-inspection`.
- [x] **T2 RED** — 7 new tests in `InspectionTests` (happy path, idempotent
  same-id, already-linked rejection, not-completed rejection, wrong-type
  rejection, empty-id rejection, PreDelivery support). Existing test
  helper `NewInProgress` updated to leave `LeaseId` null by default.
- [x] **T3 GREEN** — `Inspection.LinkToLease(...)` + `LeaseLinkedAtUtc` audit
  timestamp. All 24 Inspection tests green.
- [x] **T4** — `IInspectionRepository.GetLatestUnlinkedCheckOutForVehicleAsync(...)`
  + `EfInspectionRepository` impl. Tracked (not AsNoTracking) so the
  SaveContract handler can mutate within the same UoW.
- [x] **T5** — `SaveContractCommand.CheckOutInspectionId` optional field;
  handler validates the 4 negative paths (not found / wrong vehicle / not
  completed / wrong type / already linked) and auto-looks-up the most recent
  un-linked CHECK_OUT when the id is omitted. `IInspectionRepository`
  injected as the 9th constructor port.
- [x] **T6** — `SaveContractDevRequest.CheckOutInspectionId` optional field
  on the BFF DTO; wired through to the command.
- [x] **T7** — `BogusDataSeeder.BuildCheckOut` now drives the link via
  `Inspection.LinkToLease` so seeded rows carry `LeaseLinkedAtUtc`. CHECK_IN
  inspections keep the direct `LeaseId` assignment (Day 19 is when the
  check-in saga formalizes that path).
- [x] **T8** — 5 new handler tests: explicit-link-success / auto-link-success
  / not-found-422 / vehicle-mismatch-422 / no-inspection-still-succeeds (Phase
  1.x semantics). Existing 6 SaveContract tests stay green.
- [x] **T9** — `dotnet test AutoLeaseNet.sln --settings .runsettings` green:
  **187 tests** (175 → 187 = +12: 7 domain + 5 handler).
- [x] **T10** — `ai_context.md` updated with the link semantics + the
  Phase-1.y-required follow-up.
- [x] **T11** — PR opened + merged on green CI; post-merge ai_context bump
  bundled.

## Definition of done

- All checkboxes ticked.
- Full suite green (175 + new tests).
- Clean build (WarnAsError).
- CI green on the PR.
- `ai_context.md` updated.
- `Plans/workstreams/2026-05-25-checkout-saga-link-inspection/retrospective.md`
  written.
