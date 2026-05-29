# Retrospective — Tajeer GetAsync + real drift detection (PR #23)

**Date**: 2026-05-29
**Branch**: `feat/tajeer-get-and-drift` → squash-merged to `main`
**Plan**: [`plan.md`](./plan.md)

## What shipped

- `GetContractResponse` DTO (Spec 03 §6.3 lean projection).
- `ITajeerContractClient.GetAsync(long contractNumber)` →
  `IntegrationResult<GetContractResponse>`.
- `TajeerContractClient.GetAsync` over a new `SendNoBodyAsync` overload of the
  shared failure-mapping spine. 404 mapped distinctly to
  `tajeer.vendor.contract.not_found` (non-transient drift signal).
- `InMemoryTajeerContractClient.GetAsync` projecting the most recent
  state-changing call as a synthetic response; `getFactory` ctor override + a
  public `SeedProjection(...)` test helper for fixtures that need a status
  without first driving a write.
- `TajeerStatusMapper.FromTajeer` + `ApplyLocalRefinements` in
  `Infrastructure/Tajeer/` per Spec 03 §7.2 / §1 principle #10.
  `InvalidTajeerStatusException` on unknown vendor triples.
- `TajeerStatusMirrorCheck` upgraded from log-only stub to a real drift
  detector — walks Active + Extended leases with `TajeerContractNumber != null`,
  calls Tajeer, classifies match / drift / vendor-failure-drift /
  transient-blip / unrecognised-state, structured logs per row + a tenant
  summary + cycle summary.
- 320 tests green (was 279, +41 net).

## What didn't ship (and why)

- **Auto-correct on drift.** Phase 1 is detect-only. Spec'd in the plan
  (`Why log-only drift in Phase 1`) — auto-applying Tajeer's view risks
  silently masking missed webhooks. Phase 2 lands an action policy after the
  user weighs in.
- **`GetSavedByPlateAsync`, `GetContractPdfAsync`, `CancelAsync`,
  `UpdatePaidAmount`.** YAGNI — only the drift detector needed `GetAsync`
  today.
- **Refactoring inline close-saga status switches to use
  `TajeerStatusMapper`.** Documented as a 5-line follow-up; out of scope to
  keep this PR's blast radius confined to the read path.

## What worked

- **Sticking to the dependency rule.** First instinct was to put the mapper
  inside the Tajeer adapter package, but a check of `adapters/*.csproj`
  confirmed only `Adapters.Seed` references `AutoLeaseNet.Domain`. Moving the
  mapper to `Infrastructure/Tajeer/` kept the adapter Domain-free without
  losing the "one canonical mapper" Spec 03 §7.2 requirement.
- **Mirroring the spine, not refactoring it.** First attempt parameterised
  the existing `SendAsync<TRequest, TResponse>` with `object?` body — would
  have changed the wire-shape contract for Save/Close/etc. (subtle:
  `JsonContent.Create(body, body.GetType())` vs the generic-inferred overload).
  Reverted to a sibling `SendNoBodyAsync<TResponse>` so existing tests stayed
  pinned by their own machinery.
- **`SeedProjection` instead of a complex factory parameter.** The InMemory
  client now has a tiny test-only door for "GetAsync should return X" without
  needing to construct the whole Save→Get sequence. The mirror-check tests
  read more naturally because of it.
- **Plan called out the InMemory-flood risk before writing the check.** No
  drift would actually be detected from the InMemory seed today (Save → status
  1 → mapper says PendingIssuance → matches local PendingIssuance). Verified
  by reasoning in the plan, then confirmed by the `RunAsync_when_vendor_matches`
  test. No surprise log floods on startup.

## What surprised me

- **Three CA-rule fixes** before build went clean: CA1863 (CompositeFormat
  for the path format string — solved by simple concatenation, the simpler
  shape was right anyway), CA1305 (locale-invariant `int.ToString`), CA1861
  (array literal in an assertion arg). The bar warnings-as-errors keeps
  catches these but it's worth budgeting roughly 1 fix per ~10 new files when
  estimating.
- **`BeEquivalentTo(new[] { … })`** triggers CA1861. Worked around by
  splitting into `HaveCount` + `Contain` + `NotContain`. More explicit, less
  brittle to ordering.
- **No issue with transitive references** — Infrastructure references
  Application, Application references Adapters.Tajeer (Pattern B exception
  per Spec 04 §3.2), so Infrastructure picked up `ITajeerContractClient`
  transitively. Added the explicit reference to `Infrastructure.Tests` only.

## What I'd do differently

- **Bundle the close-saga's inline status switch into this PR.** It was
  scoped out for blast-radius reasons but the mapper is unused outside the
  reconciliation check today; consolidating the call sites is what would
  make the "one canonical mapper" property load-bearing. Next workstream
  candidate.
- **No `IReadOnlyDictionary<int, LeaseStatus>` lookup** — the switch
  expression is more readable for the documented cases AND it covers the
  invalid-triple branch in the same shape. Don't over-engineer; the mapper
  is going to grow at most one more case (extended-as-distinct-code if
  Tajeer ever introduces one) over this app's life.

## Repeated pain points (carry forward)

- **`BffTestHostDefaults.GetConfigDictionary()` shared helper** — 4 retros
  now (Day-9, Outbox, Reconciliation, Customer Portal, this one if we ever
  add a Reconciliation:Tajeer:Mode toggle that needs sweeping). Each
  cross-cutting toggle requires an N-file BFF test factory sweep. The
  helper would centralise the dictionary so a single line opt-out lands in
  one place. **Promoting to the next "cleanup" PR.**
- **`continue-on-error: true` on the JS CI** — was caught + fixed for
  web-portal during the Customer Portal scaffold but the CI workflow still
  carries the flag. Both portals build clean now; drop the masking flag in
  the cleanup PR.

## Carry-forward picklist (after this PR)

- **ZATCA adapter** (Week-4 critical path; zero code today).
- **Customer Portal — My Vehicles** (needs `/api/v1/me/vehicles`; Vehicles
  RLS excludes externals so the BFF needs an app-side filter via Lease
  join).
- **Customer Portal — Lease detail page** (follow on from My Leases).
- **Vehicle Replacement Saga** (`IncidentReportedDomainEvent` subscriber,
  Spec 02 §6.5).
- **Refactor close-saga to use `TajeerStatusMapper`** (5-line cleanup;
  bundle with another small PR).
- **`BffTestHostDefaults` shared helper + drop `continue-on-error`** (one
  cleanup PR).
- **Always Encrypted on PII** (gated on Azure Key Vault).
- **RLS on Inspection child tables** (Phase 2 backfill).

## Phase 2 trigger (when to act on drift, not just log)

Once we have one tenant with at least one full week of real reconciliation
logs and a manually-validated "expected drift vs unexpected drift" baseline,
the user can pick an action policy per status:

- Tajeer says Closed, local says Active → auto-close locally + raise
  reconciliation audit row.
- Tajeer says Suspended, local says Active → fire an Incident (operations
  needs to investigate the missed Suspend webhook).
- Tajeer says contract.not_found, local says Active → page on-call (data
  integrity).

Until that baseline exists, the warn-log surface in this PR is the right
level of trust.
