# Retrospective — Check-in Saga (Local Close)

**Closed**: 2026-05-25
**Plan**: [plan.md](./plan.md)
**Outcome**: shipped per plan. Day 19's local commit step lit up; Tajeer
adapter integration (CalculateContractPayment + CloseContract) is the
follow-up workstream.

## What we delivered

- `Inspection.LinkToLease` broadened to accept `InspectionType.CheckIn`
  (was CheckOut + PreDelivery only).
- `ILeaseRepository.GetByIdAsync(tenantId, leaseId, ct)` port + EF impl.
- `CheckInLeaseCommand` + handler — validates Lease state (Active/Extended/
  Suspended), Vehicle state (OnRent), odometer non-regression; creates +
  completes + links the CHECK_IN Inspection; calls `Lease.MarkClosed` +
  `Vehicle.Return`; idempotency-cached via the shared store.
- `POST /api/v1/leases/{id}/check-in` — Idempotency-Key required;
  status-code mapping (404 for unknown lease, 422 for validation errors).
- Seed adapter now lifts Vehicle through Reserve → StartRental for
  Active/Extended/Suspended seeded leases, and through Return for Closed.

## What was easy

- The `DomainEventDispatchInterceptor` (PR #7) needed no changes — the
  handler-driven `MarkClosed` doesn't raise events yet (Phase 1.x), but if
  it ever does, dispatch is automatic.
- Idempotency wiring mirrored the SaveContract pattern verbatim; no new
  infrastructure needed.

## What bit us

- **Seed data didn't reflect Vehicle status correctly**: seeded Active
  leases had vehicles still in `Available` state because the prior seed
  loop only ever called `Lease.MarkIssued`, never the matching
  `Vehicle.Reserve + StartRental`. The new check-in handler refuses to run
  on a non-OnRent vehicle, so the BFF endpoint test failed 422 until I
  added the new `EnsureVehicleOnRent` helper. Same fix also walks Closed
  leases through `Vehicle.Return` so their vehicles end up `Available`
  with the bumped `CurrentKm`.
- **Test assertion captured `Vehicle.CurrentKm` after mutation**: the
  endpoint test originally read the property post-SaveChanges and used it
  in the assertion expression, which meant the expected value moved with
  the actual. Captured `startKm` up-front, asserted against the constant.

## Tests

| Project           | Before | After |
| ----------------- | ------ | ----- |
| Adapters.Common   | 20     | 20    |
| Adapters.Tajeer   | 45     | 45    |
| Infrastructure    | 3      | 3     |
| Application       | 72     | 79    |
| Bff               | 47     | 50    |
| **Total**         | **187**| **197**|

+10 (1 domain, 6 handler, 3 BFF endpoint). Zero regressions.

## Follow-ups noted

- **Tajeer `CalculateContractPayment` + `CloseContract` adapter methods**.
  Today the local commit happens before any vendor round-trip; the saga
  needs to call CalculateContractPayment before showing ops the preview,
  then CloseContract after the local commit. Likely structured as: ops
  POSTs `/check-in/preview` (returns Tajeer's payment breakdown), then
  POSTs `/check-in/commit` with the confirmed payment + closure details.
- **Outbox + BackgroundService drain** for the Tajeer CloseContract call
  so the local close isn't blocked on Tajeer latency.
- **`LeaseClosed` domain event** → invoicing trigger (Week 4 work).
- **Suspend → Close path** as its own command (today `Suspended` leases
  go straight to Close via `Lease.MarkClosed`, which the spec allows but
  invariant 3 requires either a CHECK_IN OR a Suspend-then-Close; the
  handler currently enforces the CHECK_IN path for all source states).
