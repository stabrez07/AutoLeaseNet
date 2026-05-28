# Retrospective — Tajeer Close Saga (Calculate + Close vendor commit)

**Started**: 2026-05-28
**Closed**: 2026-05-28 (same-session)
**Final test count**: 214 green (197 before; +17 net new)

## What shipped

- Two new methods on `ITajeerContractClient`:
  - `CalculatePaymentAsync(CalculatePaymentRequest, ct) → IntegrationResult<CalculatePaymentResponse>`
  - `CloseAsync(CloseContractRequest, ct) → IntegrationResult<CloseContractResponse>`
- Real `TajeerContractClient` impls hit `PUT /api/contracts/calculate-payment` + `PUT /api/contracts/closure`. Single shared `SendAsync<TReq,TRes>` helper now powers Save + Calculate + Close — one set of vendor-error / HTTP / network / timeout / JSON-parse branches instead of three copies.
- `InMemoryTajeerContractClient` records calls in `CalculateCalls` + `CloseCalls`; constructor accepts per-method optional override factories so a single InMemory instance can simulate "Calculate succeeds, Close fails" without recompiling.
- `CheckInLeaseCommandHandler` rewired to **Calculate → Close → local commit**. If Tajeer fails (either step), no local mutation happens and a stable error code surfaces (`tajeer.calculate.{transient,failure}` / `tajeer.close.{transient,failure}`).
- `CheckInLeaseCommandResult` gained `CheckInPaymentBreakdown? Payment` — a 10-field projection of Tajeer's Calculate + Close responses, surfaced verbatim in the BFF response body.
- BFF endpoint adds 503 mapping for the two `transient` error codes.
- Workstream plan + this retrospective + ai_context bump.

## What we did well

- **TDD-first cadence held**: every method got a RED test before the GREEN impl. The Calculate-failure / Close-failure / replay / missing-contract-number handler tests all caught a real bug each (the initial draft passed `request.OdometerKm` as int to `decimal` and lost a digit in a comparison).
- **Shared spine refactor paid off**: collapsing the three Save/Calculate/Close error-mapping branches into one helper meant adding the next contract method (Extend / Suspend) will be one tiny new method, not 100 LOC.
- **Vendor-first ordering**: deferring outbox while still making the saga vendor-aware kept the workstream shippable in one session. The recovery story (idempotent replay self-heals) is documented in the handler XML doc + the workstream plan so the next person inherits the constraint.
- **One PR, one purpose**: scoped tight — no Extend, no Suspend, no Outbox, no LeaseClosed event. All explicitly deferred.

## What hurt / would do differently

- **Test-helper plumbing churn** (~10 min): the handler ctor gained `ITajeerContractClient`; the harness needed updating before any existing test ran. Mitigation next time: pass the InMemory client as a property on the harness from day one of any new handler so adding deps doesn't ripple.
- **`TajeerContractNumber` guard test required reflection** to flip the property to null on a tracked entity. The domain doesn't currently let you build a Lease with a null contract number, so the test is artificial. Acceptable for guard coverage but a future Phase-2 cleanup could fold the guard into `Lease.MarkIssued` so the handler-side guard becomes belt-and-braces.
- **InMemory two-ctor design got me**: I initially wrote both a single-arg-override ctor and an all-optional ctor side-by-side; the C# compiler picks the call-site ambiguously. Simplification to one all-optional ctor was the right move and matches the way the InMemory client is actually used in tests.

## Carry-forward (next sessions)

- **Outbox + BackgroundService drain** — close the cross-system inconsistency window for real.
- **`LeaseClosed` domain event** → invoicing trigger (Week 4 dependency).
- **Suspend → Close direct path** — separate command, same shape.
- **Extend Contract + Suspend Contract** adapter methods (Day 20 of the master plan).
- **Real staging smoke** against Tajeer Rabet — user-gated (needs Rabet creds + ngrok). Will validate the two URL path constants.

## Stats

- **Files changed**: 11 modified, 6 new (4 DTOs + 2 test files); plus plan.md + retrospective.md + ai_context.md.
- **New tests**: 17 (7 real-client + 6 InMemory + 4 handler + minor endpoint extension).
- **Old tests broken by handler ctor change, then fixed**: 6 (all in `CheckInLeaseCommandHandlerTests`).
- **Build warnings**: 0.
