# Tajeer GetAsync + real drift detection

**Date**: 2026-05-29
**Branch**: `feat/tajeer-get-and-drift`
**PR**: TBD

## Why this, why now

The reconciliation skeleton shipped in PR #21 is honest about being a STUB:
`TajeerStatusMirrorCheck` iterates Active leases per tenant and logs them — it
does **not** call Tajeer, because `ITajeerContractClient` has no `GetAsync`. So
the "Tajeer is system of record" promise (CLAUDE.md §5, Spec 03 §7) is unbacked
by any runtime drift detection today.

Smallest payback to close that gap:

1. Add `GetAsync(long contractNumber)` to `ITajeerContractClient` — Spec 03 §6.3.
2. Centralise the status mapping in `TajeerStatusMapper.FromTajeer` (Spec 03 §7.2)
   — it's been documented for six weeks; today every consumer that needs the
   mapping inlines its own switch (e.g. the close-saga). One canonical mapper.
3. Upgrade the mirror check to call `GetAsync` per row, run rows through the
   mapper, and **log drift only** (no mutation in Phase 1 — the act-on-drift
   policy is a separate decision, see [Defer](#not-in-scope-defer)).

## Scope (this PR)

**Adapter surface (`AutoLeaseNet.Adapters.Tajeer`)**:

- New DTO: `GetContractResponse` mirroring the read-only projection Tajeer
  returns for §6.3. Phase 1 keeps the fields lean — only what drift detection
  needs:
  - `contractNumber`, `contractStatusCode`, `suspensionReasonCode?`,
    `closureReasonCode?`, `closureSubReasonCode?`, `extensionCount?` (for the
    Extended local-refinement), `updatedAt?` (vendor-side timestamp, opaque
    string per Tajeer's date format quirks).
  - The richer projection (renter, vehicle, payment summary) lands when a
    consumer needs it — YAGNI for the drift check.
- `ITajeerContractClient.GetAsync(long contractNumber, CancellationToken ct)`
  → `IntegrationResult<GetContractResponse>`. Failure semantics identical to
  the existing surface (vendor-key 4xx → `tajeer.vendor.{key}`, 5xx/408/429 →
  transient, etc.). Path: `/api/contracts/{contractNumber}` — pinned during
  the first staging round-trip; centralised constant in `TajeerContractClient`
  so a one-line change covers any vendor correction.
- Real implementation in `TajeerContractClient` via a new `SendNoBodyAsync`
  overload of the existing SendAsync spine (GET has no request body).
- `TajeerStatusMapper.FromTajeer(contractStatusCode, suspensionReasonCode,
  closureCode) → LeaseStatus` plus
  `ApplyLocalRefinements(tajeerStatus, localExtensionCount) → LeaseStatus`
  (Extended is local-only — Tajeer keeps Issued=4 even after extensions).
  Throws `InvalidTajeerStatusException` on unknown combinations.

**InMemory adapter (`AutoLeaseNet.Adapters.Tajeer.InMemory`)**:

- `InMemoryTajeerContractClient.GetAsync` returns a deterministic projection
  derived from the most-recent call observed for that contractNumber:
  - If `CloseAsync` was called → `contractStatusCode = 2` (Closed).
  - Else if `SuspendAsync` → `3`.
  - Else if `ExtendAsync` → `4` (Tajeer keeps Issued; the `extensionCount` field
    increments).
  - Else if `SaveAsync` → `1` (Saved/PendingIssuance) — webhook fires later.
  - Else (unknown contract number) → returns
    `IntegrationResult.Failure("tajeer.vendor.contract.not_found")`
    `isTransient: false`. Mirrors what the real vendor would return.
- Optional `Func<long, IntegrationResult<GetContractResponse>>? getFactory`
  override on the ctor for tests that want to force drift / transient failure.
- `GetCalls` collection mirrors the existing instrumentation.

**Reconciliation upgrade (`AutoLeaseNet.Infrastructure.Reconciliation`)**:

- `TajeerStatusMirrorCheck` now takes `ITajeerContractClient` + a callback to
  the mapper. For each Active lease in scope:
  - Skip if `TajeerContractNumber is null` (defensive — Active leases always
    have one, but guard rather than NRE).
  - Call `client.GetAsync(contractNumber, ct)`.
    - On transient failure → log warn (drift unknown), continue.
    - On non-transient vendor failure → log warn (e.g. contract not found is a
      genuine drift signal), continue.
    - On success → apply mapper + local-refinement, compare to local
      `lease.Status`. If equal → debug log. If different → **warn log with both
      sides** (this is the drift signal Phase 2 will act on).
- Per-cycle counters: total inspected, matched, drifted, errored. Logged as a
  single summary line per tenant.

## NOT in scope (defer)

- ❌ **Acting on drift.** Phase 1 logs; Phase 2 decides per-status what to do
  (auto-update local on Tajeer→Closed/Cancelled, fire an Incident on
  Tajeer→Suspended without a local reason, etc.). The action policy is a
  product decision the user will weigh in on once the log signal is real.
- ❌ **`GetSavedByPlateAsync`** (Spec 03 §6.4) — only the close-saga's
  duplicate-detection branch needs it; not on Week-1 hardening path.
- ❌ **`GetContractPdfAsync` / summarised PDF** — Phase 3 customer-portal
  feature.
- ❌ **`CancelAsync` / `UpdatePaidAmount`** — Day-20 surface that wasn't built
  this sprint; separate workstream.
- ❌ **Inline-saga consumer of `TajeerStatusMapper`** — the close-saga still
  has its own switch today. Refactoring it to use the new mapper is a 5-line
  follow-up that doesn't change behaviour; out of scope for the test surface
  here.
- ❌ **Tajeer GET resilience tests** — covered by the same Polly pipeline as
  Save/Close, already pinned by `TajeerContractClientResilienceTests`; GET
  inherits.

## Tasks (RED → GREEN)

1. **Plan** (this file).
2. **DTO** — `GetContractResponse` under
   `packages/adapters/AutoLeaseNet.Adapters.Tajeer/Contracts/Dtos/`.
3. **Interface** — add `GetAsync` to `ITajeerContractClient` with full doc.
4. **Status mapper** — `TajeerStatusMapper` + `InvalidTajeerStatusException` in
   `packages/adapters/AutoLeaseNet.Adapters.Tajeer/Contracts/Mappers/`.
5. **Mapper tests (RED → GREEN)** — exhaustive case table per Spec 03 §7.2.
6. **Real client** — `TajeerContractClient.GetAsync` + `SendNoBodyAsync`
   helper.
7. **Real client tests (RED → GREEN)** — happy-path 200, vendor 404
   `contract.not_found`, 5xx transient, network failure. Stub HTTP factory
   pattern already established.
8. **InMemory client** — `InMemoryTajeerContractClient.GetAsync` with the
   call-history projection.
9. **InMemory client tests (RED → GREEN)** — default-success after Save,
   reflects Close/Suspend/Extend, unknown contract → not_found, factory
   override path.
10. **Mirror check upgrade (RED → GREEN)** — three new tests: match/no-drift,
    drift-on-status, transient-tajeer-failure-keeps-cycle-running.
11. **Build + full test sweep** — `dotnet build`, `dotnet test`. Stay at
    279+ green plus new tests.
12. **Retrospective + ai_context bump.**
13. **Commit, PR, squash-merge.**

## Design notes

### Why GET path = `/api/contracts/{contractNumber}`

Tajeer's documentation for §6.3 isn't on hand at code-time. The existing
constants in `TajeerContractClient` use `/api/contracts/{verb}` shape — but for
a read, the RESTful `/api/contracts/{id}` is the strong convention and what the
spec's example signature implies. If staging returns 404, the constant is one
line; a separate ADR isn't warranted.

### Why no idempotency-key on GET

GETs are nullipotent — Tajeer's idempotency story is write-side only. The cache
decorator pattern stays where it belongs (`Adapters.Cache` for read-side
caching, not idempotency).

### Why log-only drift in Phase 1

Auto-applying Tajeer's view to local risks masking bugs: if our webhook
processor silently dropped a `contract.close` event, the reconciliation job
would "fix" it by force, hiding the upstream bug forever. The right shape is:

1. Phase 1 (this PR): **detect + alarm**.
2. Phase 2: **decide per-status what to act on** with the user, build a
   reconciliation aggregate that captures the decision audit trail.

Skipping straight to auto-act would be the kind of "future-requirements" design
CLAUDE.md §"Don't add features ..." explicitly warns against.

### Why the mapper lives in the adapter, not Domain

`LeaseStatus` is a Domain concept but the *mapping* of vendor codes to it is an
integration concern. Putting `TajeerStatusMapper` next to the DTOs that produce
the codes keeps the domain free of vendor-specific awareness — and means if
Tajeer ever introduces a new code, the change lands in one package, not two.

## Risks

- **Staging path mismatch** — if `/api/contracts/{contractNumber}` is wrong,
  the InMemory tests still pass (path is irrelevant there) but the real adapter
  fails on first staging hit. Mitigation: constant in one place; the Day-1
  staging smoke test (`TajeerStagingSmokeTests`) will surface it as a 404 on
  first reconciliation run.
- **Drift log floods on InMemory mode** — `InMemoryTajeerContractClient.GetAsync`
  returns "Saved" (status 1) for save-only contracts. Local lease is
  `PendingIssuance`. Mapper maps 1 → PendingIssuance → match — no flood. Verified
  via dedicated test before we wire the check into the BFF startup with default
  enabled tenants. Today the seeded tenant is NOT in
  `Reconciliation:Tajeer:TenantIds` (defaults to empty), so even with the
  upgrade nothing fires until a tenant is explicitly added — keeps PR risk low.
- **Spec mapper has `(3, _, null) → Suspended`** but Tajeer can return
  `(3, _, _)`. The mapper accepts any closureCode there; documented as a
  permissive read (we don't want to reject a Suspended-then-Closed flicker).

## Definition of done

- [ ] All tasks complete.
- [ ] `dotnet build AutoLeaseNet.sln` clean (warnings-as-errors).
- [ ] `dotnet test AutoLeaseNet.sln --settings .runsettings` green (≥ 290).
- [ ] New mapper has at least one assertion per documented case in Spec 03 §7.2
      plus the invalid path.
- [ ] `ai_context.md` updated.
- [ ] Retrospective written.
- [ ] PR squash-merged.
