# Workstream — Day-20: Extend + Suspend Contract

**Started**: 2026-05-28 (same day as PR #15)
**Closes**: Day 20 of [`Plans/02-phase-1-mvp-week-by-week.md`](../../02-phase-1-mvp-week-by-week.md)
— Extend Contract + Suspend Contract endpoints (reconciliation job + UI deferred).
**Owner**: solo dev + Claude.

## Goal

Spec 02 §4.2 transitions `ACTIVE → EXTENDED → EXTENDED (max 25)` and `ACTIVE/EXTENDED → SUSPENDED` both need vendor commits (Tajeer
`ExtendContract` + `SuspendContract`). The domain already has
`Lease.IncrementExtension` + `Lease.MarkSuspended`; this workstream just wires
the adapter + application + BFF layers and adds the missing **MaxExtensions = 25**
invariant on the domain method.

Reuses the `SendAsync<TReq,TRes>` spine shipped in PR #15 — adding the two new
Tajeer methods is one call site each, no duplicated error-mapping branches.

## Scope (in)

### Adapter — `AutoLeaseNet.Adapters.Tajeer`

- New DTOs in `Contracts/Dtos/`:
  - `ExtendContractRequest` — `{ contractNumber, newContractEndDate, extensionReasonCode?, additionalChargesAmount?, paymentMethodCode? }`
  - `ExtendContractResponse` — `{ contractNumber, contractStatusCode, newContractEndDate, totalDue, vatAmount, grandTotal }`
  - `SuspendContractRequest` — `{ contractNumber, suspensionReasonCode, suspensionNotes?, suspendedAt }`
  - `SuspendContractResponse` — `{ contractNumber, contractStatusCode, suspendedAt }`
- Add `ExtendAsync` + `SuspendAsync` to `ITajeerContractClient`.
- Real `TajeerContractClient`:
  - `PUT /api/contracts/extend` for Extend
  - `PUT /api/contracts/suspend` for Suspend
  - Both via `SendAsync<TReq,TRes>` — no new error branches; the shared
    vendor-envelope / HTTP / network / timeout / JSON-parse paths cover both.
- InMemory client:
  - New `ExtendCalls` + `SuspendCalls` lists.
  - Optional per-method override factories (extend the existing ctor signature).
  - Defaults: Extend echoes `newContractEndDate` + assigns contractStatusCode = 4 (Extended); Suspend echoes `suspendedAt` + contractStatusCode = 3 (Suspended).

### Domain — `Lease`

- Add `public const int MaxExtensions = 25;`
- `IncrementExtension(newEndUtc, nowUtc)` rejects when `ExtensionCount >= MaxExtensions` with `lease.extensions_exhausted` semantics.
- `IncrementExtension` rejects when `newEndUtc <= ContractEndUtc` to prevent same-or-earlier dates (extension must move the date forward).

### Application

- `ExtendLeaseCommand` + handler:
  - Inputs: `IdempotencyKey`, `LeaseId`, `NewContractEndUtc`, `ExtensionReasonCode?`, `AdditionalCharges?`, `PaymentMethodCode?`.
  - Flow: lease lookup → status guard (Active / Extended) → Tajeer.ExtendAsync → `Lease.IncrementExtension` → UoW save → idempotency cache.
  - Error codes: `lease.not_found`, `lease.invalid_state_for_extend`, `lease.extensions_exhausted`, `lease.invalid_new_end_date`, `tajeer.contract_number_missing`, `tajeer.extend.{transient,failure}`.
- `SuspendLeaseCommand` + handler:
  - Inputs: `IdempotencyKey`, `LeaseId`, `SuspensionReasonCode`, `Notes?`.
  - Flow: lease lookup → status guard (Active / Extended) → Tajeer.SuspendAsync → `Lease.MarkSuspended` → UoW save → idempotency cache.
  - Error codes: `lease.not_found`, `lease.invalid_state_for_suspend`, `tajeer.contract_number_missing`, `tajeer.suspend.{transient,failure}`.

### BFF — `services/bff/Endpoints/LeaseEndpoints.cs`

- `POST /api/v1/leases/{id}/extend` (Idempotency-Key required, dev JWT stub).
- `POST /api/v1/leases/{id}/suspend` (Idempotency-Key required, dev JWT stub).
- Status-code map: 404 for `lease.not_found`; 503 for `tajeer.*.transient`; 422 for everything else (including `lease.extensions_exhausted`, `lease.invalid_new_end_date`).
- Response shapes (200): `{leaseId, status: "Extended"|"Suspended", contractEndUtc?, extensionCount?, suspensionReasonCode?, ...}`

### Tests (~14 new)

- **Real client** (`AutoLeaseNet.Adapters.Tajeer.Tests`): one happy path + one vendor-error envelope test per method (4 tests). Reuses the StubHttpMessageHandler pattern from PR #15.
- **InMemory** (`AutoLeaseNet.Adapters.Tajeer.Tests`): default-shape + override + call-recording for each method (4 tests, condensed).
- **Domain** (`AutoLeaseNet.Application.Tests/Domain`): `IncrementExtension` rejects on 25th extension + on non-monotonic newEndUtc (2 tests).
- **ExtendLeaseCommandHandlerTests** (`AutoLeaseNet.Application.Tests/Leases`): happy path; Tajeer transient → no local mutation; extensions-exhausted short-circuits before Tajeer; idempotent replay (4 tests).
- **SuspendLeaseCommandHandlerTests** (`AutoLeaseNet.Application.Tests/Leases`): happy path; invalid state → no Tajeer call; Tajeer non-transient → no local mutation; idempotent replay (4 tests).
- **BFF endpoint tests** (`services/bff.tests/Endpoints/LeaseExtendSuspendEndpointTests.cs`): one happy-path test per endpoint + a missing-Idempotency-Key 400 test (3 tests).

Target: 214 → ~235 tests green.

### Docs

- `Plans/workstreams/2026-05-28-day-20-extend-suspend/{plan.md, retrospective.md}`
- `ai_context.md` Last-updated entry + API surface table addition.

## Scope (out)

- **Reconciliation job (15-min scheduled)** — Day-20 plan mentions this; deferred to its own workstream (needs a hosted-service skeleton).
- **UI for extend/suspend** — front-end work, deferred per the global UI-deferred rule.
- **Suspend → Resume** — Tajeer doesn't support reverse transition (Spec 02 §768); domain `MarkResumed` stays unwired by the BFF.
- **LeaseExtended / LeaseSuspended domain events** — wire when invoicing needs them (Week 4).

## Task list

- [ ] T1 Workstream plan.md (this file)
- [ ] T2 DTOs (4 new files in `Contracts/Dtos/`)
- [ ] T3 Port-level `ITajeerContractClient` additions
- [ ] T4 Real client `ExtendAsync` + `SuspendAsync` via `SendAsync<TReq,TRes>`
- [ ] T5 InMemory client additions
- [ ] T6 Domain: `Lease.MaxExtensions` + `IncrementExtension` invariants
- [ ] T7 `ExtendLeaseCommand` + handler
- [ ] T8 `SuspendLeaseCommand` + handler
- [ ] T9 BFF endpoints + request DTOs
- [ ] T10 Real-client tests (4)
- [ ] T11 InMemory tests (4)
- [ ] T12 Domain tests (2)
- [ ] T13 Extend handler tests (4)
- [ ] T14 Suspend handler tests (4)
- [ ] T15 Endpoint tests (3)
- [ ] T16 `ai_context.md` + retrospective
- [ ] T17 `dotnet build` + `dotnet test` green
- [ ] T18 Commit + PR + squash-merge

## Done criteria

- `ITajeerContractClient` has 5 methods: `Save`, `CalculatePayment`, `Close`, `Extend`, `Suspend`.
- Both new BFF endpoints accept Idempotency-Key, surface stable error codes, and round-trip a Tajeer commit before any local mutation.
- All prior 214 tests still green; ~21 new tests added (~235 total).
- `ai_context.md` reflects the new contract surface + endpoint table.

## Known limits / risks

- **Vendor URL paths** still best-guess (`/api/contracts/extend`, `/api/contracts/suspend`); centralised constants so one-line correction on staging round-trip.
- **No outbox** — same self-healing-via-idempotent-replay story as PR #15.
- **Suspension reason codes** are pass-through ints today; we don't validate them against a Tajeer-provided lookup yet (deferred; that'd need the lookups cache populated for reason codes).
