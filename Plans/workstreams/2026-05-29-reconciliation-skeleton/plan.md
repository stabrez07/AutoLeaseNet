# Reconciliation BackgroundService skeleton

**Date**: 2026-05-29
**Branch**: `feat/reconciliation-skeleton`
**PR**: TBD

## Why this, why now

Phase-1 hardening item #3. The Day-20 master-plan note + multiple workstream
retrospectives call for a scheduled job that detects drift between local state
and vendor (Tajeer / ZATCA) state. Today there's no such job — drift goes
undetected until a customer complains or a webhook arrives.

Building the **skeleton** now (one BackgroundService + an
`IReconciliationCheck` abstraction + one stub check) locks the pattern in place
so future workstreams can drop in real checks (Tajeer status mirror, ZATCA
chain-state probe, OutboxEvent stuck-row alert) without re-litigating the
scheduling shape. The Outbox workstream just landed the `BackgroundService`
pattern; this is the second instance.

## Scope (this PR)

- ✅ `IReconciliationCheck` abstraction — `Name` + `RunAsync(ct)`. Cheap
  abstraction so subsequent workstreams add checks via `services.AddTransient<IReconciliationCheck, …>()`.
- ✅ `ReconciliationOptions` — `Enabled` (default true), `IntervalSeconds`
  (default 900 = 15 min per Plan 02 Day 20), `JitterSeconds` (default 30 to
  prevent thundering-herd if we ever scale out).
- ✅ `ReconciliationService : BackgroundService` — every interval, resolves
  all registered `IReconciliationCheck`s in a fresh scope and invokes each.
  Per-check try/catch so one failing check doesn't kill the cycle. Cancellation
  cooperative.
- ✅ `TajeerStatusMirrorCheck` (stub) — queries up to
  `Reconciliation:Tajeer:MaxLeasesPerCycle` (default 50) most recently-updated
  Active leases, logs `(LeaseId, TajeerContractNumber, Status, UpdatedAtUtc)`
  per row. **Does NOT call Tajeer yet** — `ITajeerContractClient` has no
  `GetContractStatusAsync` today. This stub proves visibility + the scope/log
  plumbing; the real drift comparison lands when the Tajeer Get method is
  added (likely paired with the Vehicle Replacement Saga work).
- ✅ `AddReconciliation(section)` extension — registers options + service +
  the stub check.
- ✅ Wire in `Program.cs`.
- ✅ Tests:
  - Service runs registered checks at the configured interval (unit, time-warped).
  - Service swallows + logs check exceptions, continues cycle.
  - `Enabled = false` skips the loop entirely.
  - Stub check selects N most recent Active leases ordered by `UpdatedAtUtc`.
- ✅ Sweep `Reconciliation:Enabled = false` into the 9 test factories.

## NOT in scope (defer)

- ❌ **Actual Tajeer Get-Contract call** — needs a new `ITajeerContractClient.GetAsync`
  method, vendor response DTO, real-client implementation, InMemory stub.
  Separate workstream (~1 day).
- ❌ **ZATCA chain-state probe** — needs ZATCA adapter to exist first.
- ❌ **OutboxEvent stuck-row alert** — Phase 2 once we have an alerting target.
- ❌ **Distributed lock for multi-instance reconciliation** — Phase 2 same as Outbox.
- ❌ **Auto-correction on drift** — explicit decision: log + alert only;
  human investigates. Auto-correct is a Phase-2 / per-check policy decision.

## Design

### Interface shape

```csharp
public interface IReconciliationCheck
{
    string Name { get; }                               // For logs + future per-check metrics
    Task RunAsync(CancellationToken cancellationToken);
}
```

### Loop shape (mirror of OutboxDrainService)

```
while not cancelled:
    await Task.Delay(interval + jitter, ct)
    using scope = services.CreateAsyncScope()
    foreach check in scope.GetServices<IReconciliationCheck>():
        try: await check.RunAsync(ct)
        catch: log, continue
```

### TajeerStatusMirrorCheck (Phase-1 stub)

```
using systemScope = SystemTenancyScope.For(all-tenants-mirror)  // see note
batch = db.Leases
    .Where(l => l.Status == LeaseStatus.Active)
    .OrderByDescending(l => l.UpdatedAtUtc)
    .Take(MaxLeasesPerCycle)
log: "reconciliation: tracking N Active leases for tenant T"
foreach lease in batch: log debug "would compare status against Tajeer for {LeaseId} ({TajeerContractNumber})"
```

**Tenant scoping nuance**: the reconciliation service has no request tenant.
Two options:
1. **Per-tenant iteration** — query distinct tenants from Leases, then
   `SystemTenancyScope.For(tenantId)` per tenant, run check. Slow if many
   tenants.
2. **Cross-tenant via WEBHOOK_BOOTSTRAP** — single query under
   `SystemTenancyScope.ForWebhookBootstrap()`. Fast but uses the bypass
   override that's supposed to be webhook-only.

Going with **option 1** for cleanliness. Add a small helper that enumerates
known tenants (Phase 1: the seeded tenant + any with Leases recently created).
For the stub this is one tenant; the structure scales.

Wait — even simpler: for Phase 1, hardcode the seeded tenant id via
`Reconciliation:Tajeer:TenantIds` config (string[]). Documented as a Phase-2
follow-up to derive automatically. **This avoids inventing a "list known
tenants" query just for the skeleton.**

### Jitter

`Task.Delay(interval + Random.Shared.Next(0, jitterSeconds * 1000))`. Prevents
multi-instance reconciliations stacking. Phase-1 single-instance doesn't need
it, but free to include.

## Tasks (RED → GREEN)

1. **Plan** (this file).
2. `IReconciliationCheck` + `ReconciliationOptions` in
   `Infrastructure.Reconciliation` (or `Application.Ports.Reconciliation` — going
   with Infrastructure since the abstraction is for infra-side scheduled work,
   not domain).
3. `ReconciliationService : BackgroundService` + tests.
4. `TajeerStatusMirrorCheck` + tests.
5. `AddReconciliation(section)` extension; wire in `Program.cs`.
6. Sweep `Reconciliation:Enabled = false` into 9 test factories.
7. Full suite green (≥ 269).
8. Quick BFF start: log shows reconciliation cycle started + first cycle ran with stub check.
9. Update `ai_context.md` + retrospective.
10. Commit + PR + squash-merge.

## Risks

- **Background loops in test fixtures** — same risk as Outbox. Mitigation: opt
  out via `Reconciliation:Enabled=false` in all factories.
- **Long interval makes the loop slow to verify locally** — use a small
  `Reconciliation:IntervalSeconds=2` for the BFF smoke check.
- **DI scoping** — checks resolve from a fresh scope per cycle, mirror of the
  Outbox drain. Don't reuse a long-lived scope across intervals.

## Definition of done

- [ ] All tasks complete.
- [ ] `dotnet test AutoLeaseNet.sln --settings .runsettings` green (≥ 269).
- [ ] Local BFF startup logs "ReconciliationService starting" + at least one
      "Reconciliation cycle ran (1 check, 0 errors)".
- [ ] `ai_context.md` updated.
- [ ] Retrospective written.
- [ ] PR squash-merged via branch protection.
