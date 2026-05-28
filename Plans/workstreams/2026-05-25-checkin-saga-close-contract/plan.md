# Workstream — Check-in Saga: Local Close Contract (Day 19 slice)

**Started**: 2026-05-25
**Closes**: Day 19 of [`Plans/02-phase-1-mvp-week-by-week.md`](../../02-phase-1-mvp-week-by-week.md), local-only.
**Owner**: solo dev + Claude

## Goal

End-to-end "ops returns the vehicle and closes the lease" in one atomic
operation. Creates the CHECK_IN inspection, links it to the lease, closes the
lease, and returns the vehicle. Tajeer `CalculateContractPayment` +
`CloseContract` are deferred — once the adapter ships them, the local close
is the canonical persistence layer for the saga's commit step.

## Scope (in)

- **Domain**: broaden `Inspection.LinkToLease` to also accept
  `InspectionType.CheckIn` (today restricted to CheckOut + PreDelivery).
  Semantics are identical; only the type-guard expands.
- **Application**: `CheckInLeaseCommand` + handler that:
  - Validates Lease in ACTIVE / EXTENDED / SUSPENDED.
  - Validates Vehicle is OnRent.
  - Creates a CHECK_IN `Inspection` (using the caller-provided fields),
    completes it, links it to the lease.
  - Calls `Lease.MarkClosed(...)` with closure code + endKm + fuel + notes.
  - Calls `Vehicle.Return(endKm, nowUtc)`.
  - Idempotency-cached via the shared `IIdempotencyStore`.
- **BFF**: `POST /api/v1/leases/{id}/check-in` (Dev JWT stub +
  Idempotency-Key required). Returns `{leaseId, inspectionId, lease.status}`.
- **Tests**: domain test (CheckIn now linkable); handler tests for happy +
  invalid-status paths; BFF endpoint test for the happy path.

## Scope (out — future workstreams)

- Tajeer `CalculateContractPayment` adapter call (preview damages / late
  hours / extra km).
- Tajeer `Close Contract` adapter call (vendor commit).
- Outbox pattern + BackgroundService drain.
- `LeaseClosed` → invoicing trigger.
- Suspend → Close path (separate workstream).
- Payment collection workflow.

## Tasks

- [x] T1 — branch `feat/checkin-saga-close-contract`.
- [x] T2 RED — `LinkToLease_accepts_CheckIn_for_Day_19_close_saga` domain test
  + 6 handler tests in `CheckInLeaseCommandHandlerTests` covering Active/
  Extended/Suspended success, unknown-lease 422, invalid-state 422, odometer-
  regression 422, idempotency replay.
- [x] T3 GREEN — `Inspection.LinkToLease` broadened to accept `CheckIn`;
  `CheckInLeaseCommand` + handler created; `ILeaseRepository.GetByIdAsync`
  added + `EfLeaseRepository` impl. Seed adapter updated to flow Vehicle
  through Reserve/StartRental/Return so Active/Extended/Suspended/Closed
  leases reflect real Vehicle state (caught by the BFF endpoint test).
- [x] T4 — `services/bff/Endpoints/LeaseEndpoints.cs` with
  `POST /api/v1/leases/{id}/check-in`; registered in `Program.cs`;
  3 endpoint tests (happy path, unknown 404, missing-Idempotency-Key 400).
- [x] T5 — full suite green: **197 tests** (187 → 197 = +10: 1 domain +
  6 handler + 3 endpoint).
- [x] T6 — `ai_context.md` updated + retrospective; PR + merge.
