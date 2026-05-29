# Retrospective — Reconciliation BackgroundService skeleton

**Date closed**: 2026-05-29
**PR**: TBD
**Commit**: TBD

## What shipped

- `IReconciliationCheck` abstraction (Name + RunAsync) — future workstreams
  add checks via `services.AddScoped<IReconciliationCheck, …>()`.
- `ReconciliationOptions` — Enabled (default true), IntervalSeconds (default
  900 = 15 min per Plan 02 Day 20), JitterSeconds (default 30), plus a
  nested `Tajeer.MaxLeasesPerCycle` + `Tajeer.TenantIds`.
- `ReconciliationService : BackgroundService` — second `BackgroundService` on
  the OutboxDrainService pattern: cooperative cancellation, per-cycle DI scope,
  per-check try/catch.
- `TajeerStatusMirrorCheck` (stub) — iterates configured tenants under
  `SystemTenancyScope.For(tenantId)`, pulls up to `MaxLeasesPerCycle` most
  recently-updated `Active` leases, logs `(LeaseId, TajeerContractNumber,
  UpdatedAtUtc)` per row. **Does not yet call Tajeer** — that needs
  `ITajeerContractClient.GetAsync` which is a separate workstream.
- `AddReconciliation(section)` extension; wired in `Program.cs`.
- Test factory sweep: 9 factories opted out of the reconciliation loop
  (`Reconciliation:Enabled=false`).
- Tests: 4 ReconciliationService (runs each registered check; isolates check
  failures; no-op when none registered; disabled-skip via ExecuteAsync start/stop),
  3 TajeerStatusMirrorCheck (no-op when no tenants configured; tenant-scoped
  query bounds; pre-cancelled token doesn't throw).

## Honest scoping (recap)

Pure skeleton. The locked-in pattern is what matters: future drift checks
(Tajeer GetContract, ZATCA chain probe, stuck-OutboxEvent alert) drop in by
implementing one interface and registering via `AddScoped`. The
`TajeerStatusMirrorCheck` logs visibility today; tomorrow it grows the actual
Tajeer comparison without changing the surrounding shape.

## What went well

- The `BackgroundService` pattern from the Outbox workstream made this almost
  copy-paste at the scheduler level. Two services now follow exactly the same
  shape: ExecuteAsync loop, internal RunCycleAsync exposed for testing,
  per-cycle scope, per-item try/catch.
- The `SystemTenancyScope.For(tenantId)` from Day-9 is the right tool exactly
  here. Reconciliation needs cross-tenant work; the helper lets each tenant's
  pass run under correct SESSION_CONTEXT for free.
- 7 tests cover both pieces. Service tests use `Substitute.For<IReconciliationCheck>()`
  so they're pure unit tests; stub-check tests use EF InMemory and assert
  no-throw + boundary behaviour. The stub doesn't have observable output yet,
  so the test contract is "no exceptions, correct DB query bounds";
  when the real Tajeer comparison drops in, a richer test (with an
  `ITajeerContractClient` substitute) will replace it.
- Factory sweep now patterned: every cross-cutting feature gets
  `["Feature:Enabled"] = "false"` next to the existing toggles. The list of
  toggled features in test factories is short (Outbox, Reconciliation) but
  growing — worth a `TestHostConfigDefaults` helper before the third lands.

## What surprised me

- **No surprises this round** — the workstream went straight through. That's
  the value of the Outbox workstream codifying the BackgroundService pattern
  one PR earlier; the second instance was mechanical.

## What I'd do differently

- **Shared test factory config helper** is now overdue (called out in the
  Outbox retro; this is the second sweep that confirms it). Before the next
  cross-cutting toggle, introduce `BffTestHostDefaults.GetCommonConfig()`
  returning the canonical `Dictionary<string, string?>` so each factory's
  config block becomes 1 line instead of 16. Next workstream.

## Numbers

- Files added: 5 (interface + options + service + stub check + extension),
  plus plan/retro and 2 test files.
- Files modified: 1 wiring (`Program.cs`); 9 test factories
  (`Reconciliation:Enabled=false` toggle).
- Tests: 269 → **276** default (+4 ReconciliationService + 3
  TajeerStatusMirrorCheck).
- Total elapsed: ~45 min.

## Phase-1 hardening sprint — complete

- ✅ Day-9 RLS (#19)
- ✅ Outbox + drain (#20)
- ✅ Reconciliation skeleton (this PR)

The three items that comprise Phase-1 hardening are done. The next session
should pivot away from hardening toward demo-unblocking work:

1. **Customer Portal scaffold** — biggest single gap relative to Phase-1
   demo criteria. Read-only fleet/lease/invoice list via existing
   `bff-client.ts` pattern.
2. **ZATCA adapter** — Week-4 critical path; no code today.
3. **Vehicle Replacement Saga** — wires `IncidentReportedDomainEvent` subscriber
   filtered on `RequiresReplacement = true` (Spec 02 §6.5).
4. **Always Encrypted on PII** — split off from Day-9; gated on Azure Key
   Vault provisioning OR a local-cert decision.
5. **`ITajeerContractClient.GetAsync`** — needed to turn the
   TajeerStatusMirrorCheck stub into a real drift detector.

Each its own PR per the established cadence.
