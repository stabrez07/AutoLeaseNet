# Workstream — `BffTestHostDefaults` shared helper + drop CI `continue-on-error`

**Date opened**: 2026-05-29
**Predecessors**: PR #24 (My Vehicles), the customer-portal-scaffold retro, the reconciliation retro, the outbox retro, the GetAsync retro.
**Goal**: End the five-retros-in-a-row complaint that every new BFF endpoint workstream pays for the same `ConfigureWebHost` copy-paste by extracting `BffTestHostDefaults`. Bundle the parallel cleanup the customer-portal-scaffold retro flagged: drop `continue-on-error: true` from JS CI's typecheck + build steps now that both portals build cleanly.

## Why now

> **Customer-portal-scaffold retro**: *"Shared test factory base is now THREE workstreams overdue (called out in both Outbox and Reconciliation retros). The MeFactory is yet another copy of the same `ConfigureWebHost` pattern. Next workstream should land `BffTestHostDefaults.GetConfigDictionary()` returning the common keys."*
>
> **My-Vehicles retro**: *"`BffTestHostDefaults` shared helper is now overdue from FIVE retros. `MyVehiclesFactory` is a near-exact copy of `MeFactory` which is a near-exact copy of `IncidentFactory` etc."*

12 `WebApplicationFactory<Program>` subclasses in `services/bff.tests` share ~12 config keys verbatim. Every new BFF endpoint workstream copies them and risks divergence. This is the right size for a one-PR cleanup before more BFF endpoints land.

Also retro-flagged in PR #22: drop `continue-on-error: true` from JS CI's typecheck + build steps. Both portals now build cleanly (verified in PRs #22, #23, #24). The flag is masking real failures.

## Scope

**In**:
- New file `services/bff.tests/Support/BffTestHostDefaults.cs` exposing:
  - `Defaults()` → `Dictionary<string, string?>` with the 12 always-shared keys, Seed:Mode=Empty.
  - `DemoSeedDefaults(Guid tenantId, string randomSeed)` → Defaults + Seed:Mode=Demo + tenant + seed.
  - `ReplaceDbContextWithInMemory(IServiceCollection, string dbName)` — the `RemoveAll<DbContextOptions> + AddAutoLeaseNetDbContext(InMemory)` ceremony.
  - Public constants: `PlaceholderConnectionString`, `DefaultWebhookSharedSecret`.
- Retrofit all 12 factories:
  - `HealthTestFactory`, `BrokenSqlHealthTestFactory` (`HealthEndpointsTests.cs`)
  - `EnvironmentWebApplicationFactory` (`DevJwtStubProductionGuardTests.cs`)
  - `DevWebApplicationFactory` (`DevJwtStubHandlerTests.cs`)
  - `WebhookFactory` (`TajeerWebhookEndpointTests.cs`) — overrides `WebhookSharedSecret`
  - `SaveContractEndpointFactory` (`SaveContractEndpointTests.cs`)
  - `MeFactory` (`MeEndpointTests.cs`)
  - `MyVehiclesFactory` (`MyVehiclesEndpointTests.cs`)
  - `CheckInFactory` (`CheckInLeaseEndpointTests.cs`)
  - `InspectionFactory` (`InspectionEndpointTests.cs`)
  - `IncidentFactory` (`IncidentEndpointTests.cs`)
  - `ExtendSuspendFactory` (`LeaseExtendSuspendEndpointTests.cs`)
  - `SmsE2EFactory` (`LeaseIssuedSmsEndToEndTests.cs`) — overrides Outbox:Enabled=true + WebhookSharedSecret
- `.github/workflows/ci.yml`: drop `continue-on-error: true` from `Typecheck` and `Build` steps in the `js` job. Keep it on `Lint` and `Test` for now (lint config is still skeletal; pnpm test isn't wired).
- Update the job's stale comment that says "apps are skeletal until design.md from user lands" — at least the build is no longer best-effort.

**Out**:
- Any factory behavioural change. Same config keys, same tests passing.
- Refactoring the seeder wait-loop (a separate repeat-pattern across factories — worth its own PR).
- Lint config improvements.
- `pnpm test` wiring (no test infra yet on either portal).

## Risks

- **Factory variations are subtle** — e.g. `SmsE2EFactory` sets `Outbox:Enabled=true` deliberately to exercise the drain. Need to preserve every per-factory override. Mitigation: do them one at a time, run the file's tests after each retrofit.
- **The webhook factory uses a non-default shared secret** because its tests assert signature behaviour with that exact string. The helper's `Defaults()` returns `"test-secret"`; webhook overrides it. Easy as long as we do the override AFTER the helper call.
- **CI strict mode could surface latent failures** — possible since `continue-on-error: true` has been masking real errors. We verified `pnpm --filter customer-portal build` and `pnpm --filter web-portal build` cleanly locally, so the build step should be safe. Typecheck is trickier — if it surfaces something, fix it in this PR.

## Plan (2–5 min tasks)

1. Create `services/bff.tests/Support/BffTestHostDefaults.cs` with the helper API.
2. Retrofit `MeFactory` first (representative Demo-mode case). Run `dotnet test --filter "FullyQualifiedName~MeEndpointTests"`. Green.
3. Retrofit `MyVehiclesFactory`. Run its tests. Green.
4. Retrofit `IncidentFactory`. Tests.
5. Retrofit `InspectionFactory`. Tests.
6. Retrofit `CheckInFactory`. Tests.
7. Retrofit `ExtendSuspendFactory`. Tests.
8. Retrofit `SaveContractEndpointFactory` + `LookupEndpointsTests` (which shares the factory). Tests.
9. Retrofit `HealthTestFactory` + `BrokenSqlHealthTestFactory` (Empty mode). Tests.
10. Retrofit `DevWebApplicationFactory` (Empty mode). Tests.
11. Retrofit `EnvironmentWebApplicationFactory` (Production-guard tests — different staging-secret override). Tests.
12. Retrofit `WebhookFactory` (custom shared secret override). Tests.
13. Retrofit `SmsE2EFactory` (Outbox:Enabled=true override). Tests.
14. Full `dotnet test AutoLeaseNet.sln` — confirm 337 still green.
15. `.github/workflows/ci.yml` — drop `continue-on-error: true` from `Typecheck` + `Build` steps; update the job comment.
16. Local `pnpm --recursive typecheck` + `pnpm --recursive build` to pre-flight what CI will do.
17. Retrospective, ai_context bump, commit, PR, watch CI, squash-merge.

## Definition of Done

- [ ] One source-of-truth file (`BffTestHostDefaults.cs`) for the shared test host config + DbContext swap.
- [ ] 12 factories using it; each file's `ConfigureWebHost` collapses to ~10 lines (was ~30).
- [ ] `dotnet test AutoLeaseNet.sln` — 337 passing (same count, no regressions).
- [ ] `pnpm --recursive typecheck` + `pnpm --recursive build` clean locally.
- [ ] CI's `Typecheck` + `Build` steps run strict (no `continue-on-error`) and pass on the PR.
- [ ] retrospective.md filed.
- [ ] ai_context.md bumped.
- [ ] PR opened, reviewed, squash-merged, branch deleted.
