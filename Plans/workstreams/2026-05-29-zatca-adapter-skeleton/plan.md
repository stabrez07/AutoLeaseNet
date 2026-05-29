# Workstream — ZATCA adapter skeleton (Week-4 prep)

**Date opened**: 2026-05-29
**Predecessors**: All Customer Portal demo PRs (#22–#27); ZATCA was the longest-pole carry-forward.
**Goal**: Land the shape — interface + InMemory + chain-state invariants + DI wiring — so the Week-4 invoice workstream has a runway. **Explicitly NOT** shipping UBL 2.1 generation, ECDSA signing, TLV QR codes, or actual sandbox round-trips.

## Why now

CLAUDE.md §6 + Spec 02 §4.5 lock in the **chain integrity** rule: per-tenant `ZatcaChainState` advances only on `CLEARED`. A rejected submission must NOT advance the chain. Detection of chain breaks halts new submissions. Getting this invariant tested and the InMemory path running NOW means the Week-4 work focuses on the actual UBL + signing problem, not on re-litigating the invariant under deadline pressure.

ZATCA was the largest "still zero code" item on the Phase-1 plan and is firmly on the Week-4 critical path. Calendar-wise we're ~2 weeks out; cutting now leaves headroom for the inevitable vendor surprises.

## Scope

**In** (this PR):
- **New packages**:
  - `packages/adapters/AutoLeaseNet.Adapters.Zatca` (Pattern B per Spec 04 §3.2).
  - `packages/adapters/AutoLeaseNet.Adapters.Zatca.InMemory` (companion).
  - `packages/adapters/AutoLeaseNet.Adapters.Zatca.Tests` (unit tests for the adapter + InMemory).
- **Domain**:
  - `AutoLeaseNet.Domain.Zatca.ZatcaChainState` aggregate-of-one — per-tenant row with `LastClearedInvoiceHash`, `LastClearedAtUtc`. Single mutation method `AdvanceTo(newClearedHash, occurredAtUtc)` with the invariant that callers can only advance via a `CLEARED`-status payload (enforced at the application layer; the entity stays passive). `Reset()` for chain-break recovery (operator-only).
- **Application port**:
  - `AutoLeaseNet.Application.Ports.Persistence.IZatcaChainStateRepository` — `GetOrCreateAsync(tenantId, ct)`, `SaveAsync(state, ct)`.
- **Infrastructure**:
  - `EfZatcaChainStateRepository`, EF configuration for `ZatcaChainState`, registration in `AutoLeaseNetDbContext`, migration `Add_ZatcaChainState`.
- **Adapter contract surface**:
  - `IZatcaClient` interface — one method: `SubmitInvoiceAsync(SubmitInvoiceRequest, ct)`. Spec 02 §4.5 lists more states (PROCESSING / NETWORK_ERROR / DEAD_LETTER) but those are saga-layer concerns; the adapter's responsibility ends at translating ZATCA's HTTP response.
  - DTOs: `SubmitInvoiceRequest { Uuid, InvoiceType (Tax|Simplified), InvoiceXml, InvoiceHash, PreviousInvoiceHash }`, `SubmitInvoiceResponse { Uuid, Status, ClearedAtUtc?, Warnings[] }`, `ZatcaResultStatus { Cleared | Reported | WarningCleared | Rejected }` enum.
  - `ZatcaOptions { Mode (Real|InMemory), BaseUrl, Environment (Sandbox|Production), RequestTimeout, AuthorizationToken }`.
- **Real client (stub)**:
  - `ZatcaClient` real HTTP class, fully wired HttpClient/Polly/auth, but `SubmitInvoiceAsync` returns `IntegrationResult.Failure("zatca.not_yet_implemented", "Real ZATCA clearance lands in the Week-4 workstream — InMemory is wired for now.", isTransient: false)`. This makes "forgot to switch to InMemory" loudly fail rather than silently no-op.
- **InMemory client**: full simulation. Default response is `Cleared`; `SeedRejection(uuid)` override for negative-path tests; `SubmitCalls` history list for assertions.
- **DI**:
  - `AddZatca`, `AddInMemoryZatca`, `AddZatcaWithModeSwitch` mirroring the Tajeer pattern.
  - BFF `Program.cs` wires `AddZatcaWithModeSwitch` from `Zatca:*` config section.
  - `BffTestHostDefaults` gains 4 new keys (`Zatca:Mode=InMemory`, `Zatca:BaseUrl`, `Zatca:Environment=Sandbox`, `Zatca:AuthorizationToken="Bearer test"`).
- **Tests** (3 files):
  - `ZatcaChainStateTests` — pins the invariant (AdvanceTo updates state; Reset clears it; two AdvanceTo calls keep the latest).
  - `InMemoryZatcaClientTests` — Submit returns Cleared by default; SeedRejection makes the next Submit return Rejected; SubmitCalls records each call; idempotent on same uuid.
  - `ZatcaClientStubReturnsNotImplementedTests` — the real HTTP client returns the `not_yet_implemented` failure on Submit (proves the stub is wired correctly).
  - Plus a small `EfZatcaChainStateRepositoryTests` against EF InMemory pinning the round-trip + the single-row-per-tenant constraint.

**Out** (deferred to Week-4 workstream):
- UBL 2.1 XML generation, schema validation.
- ECDSA P-256 signing + xAdES-BES.
- TLV-encoded QR code.
- Cleared XML response parsing.
- Tax vs Simplified path semantics in the real client.
- ZATCA CSR/CSID lifecycle.
- ZatcaSubmission aggregate (Spec 02 §4.5 state machine) — requires Invoice aggregate too; both land together.
- Saga (Spec 02 §6.6).
- Spec 07 expansion.

## Design notes

### Real client returns `not_yet_implemented` rather than `throw`

A stub that throws `NotImplementedException` propagates up as a 500 and looks like a bug. A stub that returns `IntegrationResult.Failure` with a recognizable error code lets a future test fail loudly in the right place (the saga that bridges Invoice → ZatcaSubmission will assert on `Cleared` vs anything else), without crashing dev servers. Less rude failure mode, same fail-loud guarantee.

### `ZatcaChainState` as Domain not Infrastructure

Spec 02 calls it a "table" but CLAUDE.md §6 promotes it to an invariant. A POCO with `AdvanceTo(hash, when)` keeps the invariant local to the entity; EF persistence is just plumbing. The invariant "only advance on CLEARED" is enforced at the application layer (the saga), not on the entity itself — `ZatcaChainState.AdvanceTo` accepts any hash; the saga is responsible for only calling it on `Cleared` results. This matches the existing pattern (e.g. `Lease.MarkIssued` accepts any timestamp; the saga calls it only when Tajeer confirmed issuance).

### Chain-break detection is NOT in this PR

Spec 02 §6.6 mentions "If PIH chain is detected as broken (mismatch on submit), alert immediately and halt new submissions until reconciled." That's a saga-level concern — needs an `IZatcaChainBreakDetector` or similar. Implementing it here would require ZatcaSubmission + Invoice. Deferred.

### Health check stubbed

Spec 04 §7 requires every adapter to expose an `IHealthCheck`. For now: a `ZatcaHealthCheck` that returns `Healthy()` always when `Mode=InMemory`, and `Degraded("not yet implemented")` when `Mode=Real`. Avoids breaking the readiness probe pattern while making it visible that production ZATCA isn't ready.

## Plan (RED → GREEN where applicable)

1. Create the three csproj files + add to `AutoLeaseNet.sln`.
2. `ZatcaOptions` + enums (Mode, Environment, ResultStatus).
3. **RED** — `ZatcaChainStateTests` (3 tests: AdvanceTo updates fields; second AdvanceTo overrides; Reset clears).
4. **GREEN** — `Domain.Zatca.ZatcaChainState` entity.
5. **RED** — `IZatcaChainStateRepository` port interface only; build fails until repo exists.
6. **GREEN** — `EfZatcaChainStateRepository` + `ZatcaChainStateConfiguration` + DbContext registration.
7. EF migration `Add_ZatcaChainState`. (Single PK column on TenantId per Spec.)
8. **RED** — `InMemoryZatcaClientTests` (4 tests: default Cleared; SeedRejection; SubmitCalls history; idempotent on same uuid).
9. **GREEN** — `IZatcaClient` interface + DTOs + `InMemoryZatcaClient`.
10. **RED** — `ZatcaClientStubReturnsNotImplementedTests` (1 test).
11. **GREEN** — real `ZatcaClient` HTTP class + `ZatcaAuthHandler` skeleton + the stubbed Submit body.
12. `ServiceCollectionExtensions` for both packages.
13. **RED** — `EfZatcaChainStateRepositoryTests` (round-trip + single-row-per-tenant).
14. **GREEN** — finalize repo impl if any gaps.
15. Wire `Program.cs` (`AddZatcaWithModeSwitch`); add Zatca config keys to `appsettings.Development.json`; extend `BffTestHostDefaults.Defaults()` with the 4 new Zatca keys.
16. Full `dotnet test` clean (351 + ~9 new = ~360); verify Program.cs builds + ValidateOnStart passes for `ZatcaOptions`.
17. `ZatcaHealthCheck` registered alongside the existing SQL health check.
18. Retrospective + ai_context bump + commit + PR + squash-merge.

## Risks

- **`BffTestHostDefaults` becomes the choke point for new adapters** — every new adapter that does `ValidateOnStart` on its options forces the helper to grow. Acceptable: that's the helper's job. Watch for the dictionary getting > 30 keys.
- **Real client stub could mislead** — someone might wire production thinking it works. Mitigation: the error code `zatca.not_yet_implemented` is explicit; default `Zatca:Mode=InMemory` in dev. Production deployment will need an ADR + an explicit Mode override.
- **EF migration touches the prod migration history** — straightforward additive table, no risk to existing data.
- **Scope creep**: keep refusing UBL generation, signing, QR codes in this PR. The plan above does NOT include them; the retro will flag if anything snuck in.

## Definition of Done

- [x] 3 new packages on disk, in solution, building clean.
- [x] 17 new tests passing (7 ChainState + 4 InMemory + 1 stub + 5 EfRepo) — target was ~9, came in higher because Theory inline-cases for the AdvanceTo empty-hash check counted separately and the EF repo got an extra Reset-persistence test for symmetry.
- [x] EF migration generated (`20260529232659_Add_ZatcaChainState`); local SQL apply deferred to next dev cycle — InMemory provider validates the model end-to-end.
- [x] BFF `Program.cs` wires Zatca; ValidateOnStart green; default `Zatca:Mode=InMemory` in dev (`appsettings.Development.json`).
- [ ] Both portals build clean — no portal changes in this PR.
- [x] retrospective.md filed.
- [x] ai_context.md bumped (decision #13 added; current repo state + migrations list updated).
- [ ] PR opened, CI green, squash-merged.
