# Workstream — Tajeer Close Saga (Calculate + Close vendor commit)

**Started**: 2026-05-28
**Closes**: lifts the Day-19 check-in saga from local-only (PR #14) to a true
vendor commit — implements [Spec 02 §6.4](../../../Specs/02-state-machines-and-sagas.md#64-check-in--close-contract-saga)
calls 1 and 2 (`CalculateContractPayment`, `CloseContract`).
**Owner**: solo dev + Claude.

## Goal

Today the BFF `POST /api/v1/leases/{id}/check-in` endpoint only mutates local
state (Lease → Closed, Vehicle → Available, CHECK_IN inspection completed).
Tajeer never hears about it. This workstream fills that gap:

1. Extend the `ITajeerContractClient` port with `CalculatePaymentAsync` and
   `CloseAsync` (the two Tajeer endpoints from Spec 02 §6.4 boxes 4 and 7).
2. Implement them on both `TajeerContractClient` (real HTTP) and
   `InMemoryTajeerContractClient` (deterministic dev/test).
3. Wire them into `CheckInLeaseCommandHandler` so the vendor commit runs
   **before** local close. If Tajeer fails, the local state stays put.

The full outbox + BackgroundService drain pattern from Spec 02 §6.4 boxes
"Insert OutboxEvent" / "Worker drains outbox" is **explicitly deferred** to its
own workstream — Phase 1.x ships the inline call.

## Why call order = Calculate → Close → local commit

- **Calculate first** — non-destructive at vendor; gives us the breakdown to
  show ops/return in the response. Failure aborts the saga cheaply.
- **Close second** — vendor commit. Tajeer's IdempotencyKey header guarantees
  re-entrant safety, so a transient network blip is recoverable.
- **Local commit last** — once Tajeer says CLOSED, we mirror locally and cache
  the idempotent response. If the local SaveChanges fails after a successful
  vendor close, the next retry of the saga with the same `Idempotency-Key`
  replays both Tajeer calls (idempotent) and re-attempts the local commit.

This keeps the cross-system inconsistency window scoped to the moment between
"Tajeer 200 CLOSED" and "local SaveChanges committed".

## Scope (in)

### Adapter — `AutoLeaseNet.Adapters.Tajeer`

- New DTOs in `Contracts/Dtos/`:
  - `CalculatePaymentRequest` — `{ contractNumber, returnedAtUtc?, returnedKm?, returnedFuelLevelCode?, extraKmOverage?, additionalCharges? }`
  - `CalculatePaymentResponse` — `{ rentAmount, paidAmount, lateHoursFee, extraKmFee, damagesFee, discountAmount, totalDue, vatAmount, grandTotal }`
  - `CloseContractRequest` — `{ contractNumber, closureMainReasonCode, closureSubReasonCode?, returnedAtUtc, returnedKm, returnedFuelLevelCode, returnConditionNotes?, damagesObserved?, finalPaidAmount, discountAmount? }`
  - `CloseContractResponse` — `{ contractNumber, contractStatusCode, closedAtUtc, finalPaidAmount }`
- Extend `ITajeerContractClient` with the two methods. Same `IntegrationResult<T>` return shape as `SaveAsync`.
- Extend `TajeerContractClient` with two HTTP impls:
  - `PUT /api/contracts/calculate-payment` (canonical path; one-line correction post-staging).
  - `PUT /api/contracts/closure`.
  - Same vendor-error envelope → HTTP status → network → timeout → JSON parse error branches as `SaveAsync` (logger event IDs 4101-4106 / 4201-4206).
- Extend `InMemoryTajeerContractClient`:
  - Records every call (`CalculateCalls`, `CloseCalls`).
  - Default deterministic responses (round-trip rent + 15% VAT, status code 2 = Closed).
  - Per-call override factory constructor.

### Application — `AutoLeaseNet.Application.Leases.CheckInLeaseCommandHandler`

- New constructor dependency: `ITajeerContractClient tajeer`.
- After local validation, **before** `Lease.MarkClosed`:
  1. If `lease.TajeerContractNumber is null` → fail (`tajeer.contract_number_missing`).
  2. Call `tajeer.CalculatePaymentAsync(...)`. Failure → `tajeer.calculate.failure` (transient → 503, non-transient → 422).
  3. Call `tajeer.CloseAsync(...)`. Failure → `tajeer.close.failure` (transient → 503, non-transient → 422).
  4. Persist the returned `finalPaidAmount` + the calculated breakdown into the response.
- `Lease.MarkClosed` + `Vehicle.Return` run unchanged.
- The cached `CheckInLeaseCommandResult` now carries the `PaymentBreakdown` so idempotent replay returns the exact same payload.

### BFF — `services/bff/Endpoints/LeaseEndpoints.cs`

- Status-code map gains entries for the two new error codes.
- Response body shape becomes `{ leaseId, inspectionId, status, payment: { totalDue, paid, vat, grandTotal, lateHoursFee, extraKmFee, damagesFee } }`.

### Tests

- **Real client** (`AutoLeaseNet.Adapters.Tajeer.Tests`): `HttpMessageHandler` stub asserting URL + method + payload for both calls; happy path; vendor-error envelope on 200; 500 / 408 / 429 transient mapping; deserialization failure.
- **InMemory client** (`AutoLeaseNet.Adapters.Tajeer.InMemory.Tests`): default response is success; per-call override is honoured; calls are recorded in order.
- **Handler** (`AutoLeaseNet.Application.Tests`):
  - Happy path: Calculate called, Close called, local Lease/Vehicle moved, response carries payment breakdown.
  - Tajeer Calculate fails transient → 503-eq error code, NO local mutation, no vendor Close call.
  - Tajeer Close fails non-transient → 422-eq error code, NO local mutation, Calculate was still called.
  - Idempotent replay: second invocation returns the cached payload, makes ZERO new Tajeer calls.
  - Missing `TajeerContractNumber` → `tajeer.contract_number_missing`, no Tajeer calls.
- **Endpoint** (`services/bff.tests/Endpoints/CheckInLeaseEndpointTests.cs`): asserts the new `payment` block is present on 200; assert 422 surfaces for handler-injected Tajeer failure.

### Docs

- `docs/ai_context.md`: update Day-19 saga entry; note Outbox still deferred.
- `Plans/workstreams/2026-05-28-tajeer-close-saga/retrospective.md` on close.

## Scope (out)

- **Outbox + BackgroundService drain** — keep it on the deferred list.
- **`LeaseClosed` domain event → invoicing trigger** — Week 4 work.
- **Real staging smoke test against Tajeer Rabet** — user-gated, needs ngrok.
- **Suspend → Close direct path** — separate command.
- **Tajeer error catalog expansion** — vendor `errorKey` codes pass through opaque per current pattern.

## Task list (2-5 min each, TDD-first)

- [ ] T1.1 RED: real-client unit test asserting `CalculatePaymentAsync` PUTs to `/api/contracts/calculate-payment` with serialized body
- [ ] T1.2 GREEN: implement `TajeerContractClient.CalculatePaymentAsync`
- [ ] T1.3 RED: real-client unit test for vendor-error envelope on Calculate (status 200 + errorKey)
- [ ] T1.4 GREEN: extract envelope-check + error-mapping helper (re-used by Save/Calculate/Close)
- [ ] T1.5 RED: real-client unit test for 500 transient on Calculate
- [ ] T1.6 GREEN: already covered by helper, add logger events 4101-4106
- [ ] T2.1 RED: real-client unit test asserting `CloseAsync` PUTs to `/api/contracts/closure` with IdempotencyKey header
- [ ] T2.2 GREEN: implement `TajeerContractClient.CloseAsync` (event IDs 4201-4206)
- [ ] T3.1 RED: InMemory test — `CalculatePaymentAsync` default returns success + records call
- [ ] T3.2 GREEN: implement default + recorder
- [ ] T3.3 RED: InMemory test — override factory for Calculate negative path
- [ ] T3.4 GREEN: 2-ctor pattern matching existing `Save` shape
- [ ] T3.5 / T3.6 same for `CloseAsync`
- [ ] T4.1 RED: handler test — happy path calls Calculate then Close then commits locally + response carries breakdown
- [ ] T4.2 GREEN: wire `ITajeerContractClient` into handler; add `PaymentBreakdown` to result
- [ ] T4.3 RED: handler test — Calculate transient failure → no Close call, no local mutation
- [ ] T4.4 GREEN: short-circuit on Calculate failure
- [ ] T4.5 RED: handler test — Close non-transient failure → no local mutation, Calculate was called
- [ ] T4.6 GREEN: short-circuit on Close failure (post-Calculate)
- [ ] T4.7 RED: handler test — idempotent replay returns cached payload + zero Tajeer calls
- [ ] T4.8 GREEN: cached result path already exists; just assert
- [ ] T4.9 RED: handler test — missing `TajeerContractNumber` short-circuits before any Tajeer call
- [ ] T4.10 GREEN: guard
- [ ] T5.1 RED: endpoint test — happy path returns `payment` block
- [ ] T5.2 GREEN: serialize `PaymentBreakdown` in endpoint response
- [ ] T5.3 RED: endpoint test — Tajeer-injected failure surfaces 422
- [ ] T5.4 GREEN: status-code map entries for the two new error codes
- [ ] T6.1 Update seed: closed-lease seed path should also stamp `TajeerContractNumber` on the Lease (it already does) — verify and add assertion if missing.
- [ ] T7.1 Update `docs/ai_context.md` with the new dependency chain
- [ ] T7.2 Write `retrospective.md`
- [ ] T8.1 `dotnet build` + `dotnet test` green
- [ ] T8.2 Squash-merge PR

## Done criteria

- `ITajeerContractClient` has 3 methods total (`SaveAsync`, `CalculatePaymentAsync`, `CloseAsync`).
- Real client + InMemory client both implement all 3.
- `POST /api/v1/leases/{id}/check-in` now produces a vendor commit + returns the payment breakdown.
- All prior 197 tests still green; ~10 new tests added.
- `ai_context.md` reflects the new contract surface.

## Risk / known limits

- **No outbox**: a successful Tajeer close + crash before local SaveChanges leaves Tajeer ahead of us. The user-driven retry replays both Tajeer calls (idempotent) and the local commit, so it self-heals on next call — but a stale local Lease.Status=Active until then. Logged + alerted; outbox closes the window.
- **Vendor URL paths** are best-guess (`/api/contracts/calculate-payment`, `/api/contracts/closure`). Confirmed once Rabet staging round-trip succeeds; centralised as constants for one-line correction.
- **`PaymentBreakdown` shape** is our internal projection of Tajeer's response — if Tajeer adds fields, the InMemory default needs to mirror them.
