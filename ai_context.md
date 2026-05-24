# AI Context — AutoLeaseNet (repo source of truth)

Per user's working rules (2026-05-24): keep all reasoning, decisions, and plans
inside the repo so any future Copilot / Claude session can continue cleanly without
relying on chat memory. **This file is the source of truth between sessions.** Read
it first; update it after every meaningful change.

## Working rules (user-set, 2026-05-24)

1. Always read the latest repo state before doing anything.
2. Never rely on chat memory; always use the repo as the source of truth.
3. After every design or reasoning step, update this file (`ai_context.md`) with:
   - Architecture decisions
   - Domain rules
   - API contracts
   - TODOs
   - Next steps for Copilot
4. When modifying code, update the actual files and prepare a commit message.
5. After the user returns from VS Code, read all new commits and update this file.
6. Keep all reasoning, decisions, and plans inside the repo so Copilot Pro can continue.
7. Never keep important information only in the chat.

> **How to apply rule #1**: `git log --oneline -10` + `git status` + `git diff HEAD` are
> always the first three commands of any new session. Then read this file. Then act.

## Project at a glance

- **AutoLeaseNet** — KSA vehicle leasing platform. Solo dev + Claude/Copilot.
- **Repo**: `stabrez07/AutoLeaseNet` (private, free GitHub plan — see §"Open blockers").
- **Branch**: `main`. No PR/branch protection yet (free private repo limit).
- **Tech**: .NET 8 LTS (Minimal API), Next.js 14 (skeletal), EF Core 8 on SQL Server,
  MediatR, Polly v8, Bogus. Hexagonal/Ports & Adapters per `Specs/04-integration-architecture.md`.
- **Phase 1 horizon**: 4 weeks of work; Week 1 code-side is closed.

## Where to find things (don't paraphrase the repo — read these)

| Need                                                    | Read                                                                                                                                                                                                          |
| ------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Project constitution + rules                            | [`CLAUDE.md`](./CLAUDE.md)                                                                                                                                                                                    |
| Domain + state machines + adapter design                | [`Specs/`](./Specs/) — `01`–`08` + ADRs                                                                                                                                                                       |
| Week-by-week MVP plan                                   | [`Plans/02-phase-1-mvp-week-by-week.md`](./Plans/02-phase-1-mvp-week-by-week.md)                                                                                                                              |
| Week 1 workstream (closed code-side)                    | [`Plans/workstreams/2026-05-17-week-1-foundation-tajeer-happy-path/`](./Plans/workstreams/2026-05-17-week-1-foundation-tajeer-happy-path/) — `plan.md` + `notes.md` + `retrospective.md` + `STAGING-SMOKE.md` |
| Domain Deepening workstream (closed)                    | [`Plans/workstreams/2026-05-24-domain-deepening-production-seed/`](./Plans/workstreams/2026-05-24-domain-deepening-production-seed/)                                                                          |
| GitHub CI setup runbook                                 | [`.github/SETUP-GITHUB.md`](./.github/SETUP-GITHUB.md)                                                                                                                                                        |
| Production data rule (BI granularity + KSA-shaped seed) | Captured as a memory and applied repo-wide                                                                                                                                                                    |

## Architecture decisions (locked)

1. **Hexagonal Ports & Adapters per Spec 04.** Two patterns:
   - **Pattern A** (swappable capability, e.g. SMS / Cache / Storage): port lives in
     `AutoLeaseNet.Application.Ports.*`; concrete adapter in `Adapters.<Capability>.<Provider>`.
   - **Pattern B** (vendor-specific, e.g. Tajeer / ZATCA): port + DTOs + impl all in
     the adapter package; Application is allowed to reference Pattern B sub-clients
     directly (Spec 04 §3.2). Use this when the vendor IS the contract.
2. **Domain has zero external dependencies.** No MediatR, no EF Core, nothing. Pure
   POCOs + invariants. Domain events implement `IDomainEvent` only; Application wraps
   them in MediatR `INotification` wrappers (see `LeaseIssuedNotification`).
3. **MediatR lookup-query handlers live in Infrastructure**, not Application, because
   they need `DbContext` directly. Query records + DTOs stay in Application. The BFF
   `AddMediatR` registers both assemblies. This avoids inverting the
   Application → Infrastructure dependency direction.
4. **`IntegrationResult<T>`** in `Adapters.Common.Result` is the universal vendor-call
   result type. `Success` / `Value` / `IsTransient` / `ErrorCode` / `ErrorMessage`. All
   Pattern B adapters return it. Polly handles retry; the adapter classifies the
   final outcome.
5. **`Lease.MarkIssued` raises `LeaseIssuedDomainEvent`.** The BFF webhook handler
   scans `lease.DomainEvents` after `SaveChangesAsync` and switch-publishes to
   MediatR. Future evolution → DbContext interceptor for transparent dispatch.
6. **Webhook receiver is anonymous** (no JWT — Tajeer doesn't carry one). Auth is via
   `secret-key` header validated by `WebhookSignatureValidator.IsValid` with
   `CryptographicOperations.FixedTimeEquals`. **Spec model is shared-secret header
   equality, NOT HMAC-of-body** — the plan said HMAC but the spec / vendor wins.
7. **Tenant id resolution for webhooks** (Phase 1): cross-tenant `Lease` lookup by
   `TajeerContractNumber`. Phase 2 multi-tenant will encode tenant in the registered
   webhook URL and retire `ILeaseRepository.GetByTajeerContractNumberAcrossTenantsAsync`.
8. **`Tajeer:Mode` switch (`Real` | `InMemory`)** picks the `ITajeerContractClient`
   impl at composition time. Defaults to `Real` so Production stays safe-by-default.
   `AddTajeerWithModeSwitch(section)` is the one-line composition helper.
9. **Seed adapter (`Adapters.Seed`) is Pattern A** with `IDataSeeder` port + three
   modes: `Empty | Demo | ImportedFile` (last reserved for the future data-management
   module). Demo populates KSA-shaped data via Bogus; idempotent; deterministic
   via `SeedOptions.RandomSeed`.
10. **`[LoggerMessage]` source generators everywhere** to satisfy CA1848. Event ID
    ranges: 5xxx SaveContract, 6xxx Webhook, 7xxx SMS, 9xxx Seed.
11. **`.runsettings` at repo root filters `Category!=Smoke&Category!=Integration`**
    by default. Smoke tests (`Category=Smoke`) and local-dependency integration tests
    (`Category=Integration`) run only when explicitly filtered IN, so CI stays green
    without vendor secrets or local SQL.

## Domain rules

- **Every state transition is idempotent against same-state re-entry** (defends
  against webhook replays). `MarkIssued` returns silently if already `Active`;
  `MarkSuspended` returns silently if already `Suspended`; etc.
- **PII columns** (`PersonIdNumber`, `DriverLicenseNumber`, future IBAN) are plain
  strings today; **Always Encrypted lands in Week 2 Day 9**. `PiiOptedOut` bool is
  already on `Customer` and `Driver` for the future Right-To-Be-Forgotten flow.
- **`Vehicle.Return(endKm, …)` enforces `endKm >= CurrentKm`** — a bookkeeping
  invariant that prevents fat-finger KM regression at check-in.
- **`Lease.MarkCancelled` only works from `PendingIssuance`** — Tajeer only allows
  cancellation before issuance.
- **`Lease.MarkClosed` accepts `Active`, `Extended`, or `Suspended`** as source
  states.
- **Tajeer's misspelling `addtionalServices`** is preserved on the wire because the
  vendor expects it. Don't "fix" it.

## API contracts (current)

| Verb   | Path                                 | Auth                                    | Body                                   | Returns                                                                         |
| ------ | ------------------------------------ | --------------------------------------- | -------------------------------------- | ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| `GET`  | `/health/liveness`                   | none                                    | —                                      | 200 always                                                                      |
| `GET`  | `/health/readiness`                  | none                                    | —                                      | 200 if SQL reachable, 503 otherwise                                             |
| `GET`  | `/api/v1/dev/whoami`                 | dev JWT stub                            | —                                      | claims echo + resolved `ITenantContext`                                         |
| `POST` | `/api/v1/dev/save-contract`          | dev JWT stub + `Idempotency-Key` header | domain-shaped `SaveContractDevRequest` | 202 `{leaseId, tajeerContractNumber, issuanceUrl}` ; 400 / 422 / 503 on failure |
| `POST` | `/api/v1/webhooks/tajeer`            | `secret-key` header                     | Tajeer V9.7 payload                    | 200 `{status: "received"                                                        | "duplicate-ignored"}` ; 401 on bad sig (unless LogOnly) ; 400 on malformed body |
| `GET`  | `/api/v1/lookups/branches`           | dev JWT stub                            | —                                      | `BranchDto[]`                                                                   |
| `GET`  | `/api/v1/lookups/rent-policies`      | dev JWT stub                            | —                                      | `RentPolicyDto[]`                                                               |
| `GET`  | `/api/v1/lookups/extended-coverages` | dev JWT stub                            | —                                      | `ExtendedCoverageDto[]`                                                         |
| `GET`  | `/api/v1/lookups/customers`          | dev JWT stub                            | `?page=1&pageSize=50&search=`          | `PagedResult<CustomerSummaryDto>`                                               |
| `GET`  | `/api/v1/lookups/vehicles`           | dev JWT stub                            | `?page=1&pageSize=50&search=&status=1` | `PagedResult<VehicleSummaryDto>`                                                |
| `GET`  | `/api/v1/lookups/drivers`            | dev JWT stub                            | `?page=1&pageSize=50&search=`          | `PagedResult<DriverSummaryDto>`                                                 |

## Current repo state

- **Branch**: `main` at commit `709c094` (`ci(github): gate Tajeer smoke job + fix secret names so dummies don't trigger real Tajeer call (#5)`).
- **CI on main**: ✅ all three jobs green — `.NET (build -warnaserror + test)`, `JS (lint + typecheck + build)`, `Tajeer staging smoke (Category=Smoke)` (cleanly skipped via the `TAJEER_REAL_SMOKE_ENABLED` gate).
- **Merged PRs since checkpoint `35ecbae`**: #1 (CI test-host config + seeding), #2 (web-portal Tailwind + AR/EN + Save Contract form), #3 (governance: repo public + branch protection + 5 dummy `TAJEER_*` secrets), #4 (Dev-only CORS + local-SQL runbook), #5 (smoke gate + secret name fix).
- **Branch protection**: enforced on `main`. Direct push blocked; every change is `gh pr create` → `gh pr merge --squash --delete-branch`.
- **Repo visibility**: public. L.G2/L.G3 closed at $0 cost.
- **Current blocker profile**: no active code/CI blockers. Remaining work is the manual staging exercise (T3.7/T5.7/T5.8/T6.7/T6.8/T6.9/T7.8) and external onboarding (Azure / Entra / Unifonic).

## TODOs — in priority order

### 1. ✅ RESOLVED — CI stabilization for test-host config + seeding

**Root cause**: `HealthTestFactory` (services/bff.tests/Health/HealthEndpointsTests.cs)
and `DevWebApplicationFactory` (services/bff.tests/Authentication/DevJwtStubHandlerTests.cs)
both use `WebApplicationFactory<Program>` with only `UseEnvironment("Development")` and
no inline Tajeer config injection. On a dev machine, the local-only
`services/bff/appsettings.Development.json` provides dummy Tajeer values that satisfy
`TajeerOptions.ValidateOnStart()`. On CI (Linux runner, fresh checkout), that file
doesn't exist, so startup fails with `OptionsValidationException` for AppId / AppKey /
AuthorizationToken / BranchId / WebhookSharedSecret.

**Two fix options**:

a) **Update the two test factories to inject Tajeer dummy config inline** (matches
the pattern in `SaveContractEndpointFactory` and `DevJwtStubProductionGuardTests`).
Cleanest; production stays fail-loud when Tajeer config is missing.

b) **Add an `appsettings.Testing.json` (committed) and have test factories use
`UseEnvironment("Testing")`** — single file, all current + future factories pick it up.

**Recommendation: option (a)**. It's already the pattern for the other two factories;
extending it to two more keeps the codebase consistent.

**Where to add the inline config** (mirror this from `SaveContractEndpointFactory.ConfigureWebHost`):

```csharp
builder.ConfigureAppConfiguration((_, config) =>
{
    config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:AutoLeaseNet"] = "Server=ignored;Database=ignored;",
        ["Tajeer:BaseUrl"] = "https://tajeer-stg.api.elm.sa",
        ["Tajeer:IssuanceUrlBase"] = "https://tajeerstg.logisti.sa",
        ["Tajeer:AppId"] = "test-app",
        ["Tajeer:AppKey"] = "test-key",
        ["Tajeer:AuthorizationToken"] = "Basic test",
        ["Tajeer:BranchId"] = "1",
        ["Tajeer:TimeoutSeconds"] = "10",
        ["Tajeer:WebhookSharedSecret"] = "test-secret",
        ["Tajeer:Mode"] = "InMemory",
        ["Seed:Mode"] = "Empty",   // skip Bogus generation for these tests
    });
});
```

Files to update:

- `services/bff.tests/Health/HealthEndpointsTests.cs` — `HealthTestFactory` class
  (~line 66) and `BrokenSqlHealthTestFactory` (~line 75).
- `services/bff.tests/Authentication/DevJwtStubHandlerTests.cs` — `DevWebApplicationFactory`
  class (find at top or bottom of file).

**2026-05-24 implementation update**:

- Applied option (a) inline-config fix in:
  - `services/bff.tests/Health/HealthEndpointsTests.cs`
  - `services/bff.tests/Authentication/DevJwtStubHandlerTests.cs`
- Added the same required `Tajeer:*` dummy keys used by other factories so
  `TajeerOptions.ValidateOnStart()` no longer depends on gitignored
  `appsettings.Development.json`.
- Kept the local-ready SQL connection in `HealthTestFactory` so the existing
  SQL-reachable integration health test remains valid on Windows dev boxes.
- Validation run:
  - `dotnet test services/bff.tests/AutoLeaseNet.Bff.Tests.csproj --filter "FullyQualifiedName~HealthEndpointsTests|FullyQualifiedName~DevJwtStubHandlerTests"` ✅ (8/8)

**2026-05-24 follow-up fix (same TODO #1 lane)**:

- While validating full non-integration tests, SaveContract/Lookup endpoint tests still failed with
  `Seeder did not populate Customers`.
- Root cause: `AddSeed(...)` in `Adapters.Seed` eagerly materialized `SeedOptions` at DI-registration time.
  In test-host composition, this could lock in pre-override values and resolve `IDataSeeder` as `EmptyDataSeeder`
  instead of `BogusDataSeeder` even when tests supplied `Seed:Mode=Demo`.
- Fix: changed seed registration to resolve `SeedOptions` lazily from `IConfigurationSection` at runtime
  (`AddSingleton(_ => configurationSection.Get<SeedOptions>() ?? new SeedOptions())`), and kept seeder selection
  inside the scoped `IDataSeeder` factory.
- Additional hardening: `SaveContractEndpointFactory.EnsureSeededAsync` now explicitly resolves
  `IDataSeeder` and calls `SeedAsync(...)` (idempotent by design) before polling, removing dependency
  on Development startup-hook ordering in parallelized test-host runs.
- CI follow-up: `.runsettings` default filter changed from `Category!=Smoke` to
  `Category!=Smoke&Category!=Integration` so Linux CI does not attempt local SQL-dependent
  infrastructure integration tests.
- Added better diagnostics in `SaveContractEndpointFactory.EnsureSeededAsync` (120s wait + mode/count/db details)
  to make future CI triage fast if seeding regresses.
- Validation runs after fix:
  - `dotnet test services/bff.tests/AutoLeaseNet.Bff.Tests.csproj --filter "FullyQualifiedName~SaveContractEndpointTests.POST_save_contract_with_valid_body_returns_202_and_writes_a_Lease"` ✅
  - `dotnet test services/bff.tests/AutoLeaseNet.Bff.Tests.csproj --filter "Trait!=Integration"` ✅
  - `dotnet test AutoLeaseNet.sln --filter Trait!=Integration` ✅ (all projects green locally)

Outcome: fixes are merged in `main` via PR #1 and CI is green.

Next actionable work moved to TODO #2 (manual staging exercise) and TODO #4 (`design.md` for Week 2 UI).

### 2. Manual Tajeer staging exercise — closes 7 Week-1 boxes in one session

User needs Tajeer Rabet staging credentials + an ngrok account. Full runbook at
[`STAGING-SMOKE.md`](./Plans/workstreams/2026-05-17-week-1-foundation-tajeer-happy-path/STAGING-SMOKE.md).
Closes T3.7 + T5.7 + T5.8 + T6.7 + T6.8 + T6.9 + T7.8 in ~45-90 minutes.

### 3. ✅ DELIVERED — L.G2 / L.G3 — branch protection + repo secrets

Executed 2026-05-24 via governance Option B:

- Repo `stabrez07/AutoLeaseNet` flipped to **PUBLIC** (free branch protection + unlimited Actions).
- Branch protection on `main`: required status checks (`.NET (build -warnaserror + test)`,
  `JS (lint + typecheck + build)`) with `strict=true`, PRs required (review count `0` because
  solo dev can't self-approve — CI + linear history + conversation resolution still gate it),
  `enforce_admins=true`, no force-push, no deletions, linear history, conversation
  resolution required.
- Five dummy `TAJEER_*` Actions secrets seeded (rotate when real Rabet creds arrive):
  `TAJEER_APPID`, `TAJEER_APPKEY`, `TAJEER_AUTHORIZATION_TOKEN`, `TAJEER_BRANCH_ID`,
  `TAJEER_WEBHOOK_SHARED_SECRET`.

**Consequence for future sessions**: direct push to `main` is blocked. Every change goes
through a feature branch + PR. Use `gh pr create` then `gh pr merge --squash --delete-branch`
once CI is green. Self-approval is not required (count=0) but a PR + green CI is.

### 4. ✅ PARTIALLY DELIVERED — Week 2 UI scaffold (without design.md)

User explicitly waived the design.md gate ("currently there is no ui so I can't") and
asked for a best-effort scaffold to iterate against during end-user testing. Delivered:

- **Tailwind 3.4 wired** on `apps/web-portal` (`tailwind.config.mjs`, `postcss.config.mjs`,
  updated `app/globals.css` with `@tailwind` directives, brand palette + RTL font swap).
- **Locale + RTL** via lightweight `LocaleProvider` (`apps/web-portal/lib/locale-provider.tsx` +
  `lib/i18n.ts`) with AR/EN dictionaries — no next-intl `[locale]` segments yet, can migrate
  cleanly later. `<html dir>` flips automatically on locale change; cookie-persisted.
- **Typed BFF client** (`apps/web-portal/lib/bff-client.ts`) calling `/api/v1/lookups/*` +
  `/api/v1/dev/save-contract` with `X-Dev-Tenant-Id` + `X-Dev-User-Type` headers and an
  `Idempotency-Key` for the save. Will be regenerated from `packages/contracts/openapi.yaml`
  once that file is fleshed out.
- **App shell** (`components/app-shell.tsx`) with header nav + AR/EN toggle + active-route highlighting.
- **Pages**: dashboard (`app/page.tsx`), customers, vehicles, drivers, branches lists with
  search + pagination, and `app/leases/new/page.tsx` — full Save Contract form mirroring
  the BFF `SaveContractDevRequest` shape, pre-picking the first seeded values for a
  one-click happy-path submit.

### 5. ✅ DELIVERED — Local Tajeer happy-path smoke with dummy credentials

New `scripts/local-smoke.ps1` boots the BFF in `Tajeer:Mode=InMemory`, POSTs `/dev/save-contract`,
then synthesises a `contract.create` webhook with the dummy shared secret to flip the lease
to Active, then asserts via `sqlcmd`. Switch to real staging is a one-flag flip (`-RealTajeer`)
after the user pastes real Tajeer Rabet credentials into `dotnet user-secrets`.

### 6. ✅ DELIVERED — Governance recommendation update

`.github/SETUP-GITHUB.md` now marks **Option B (public repo)** as the recommended path for
unblocking L.G2/L.G3 at $0 cost, with a pre-flight checklist of the (verified) secret/PII
surfaces. Option A (GitHub Pro) preserved for the NDA case.

### 7. ✅ DELIVERED — Dev CORS policy for portals → BFF

The Next.js portals at `http://localhost:3000` and `:3001` were getting `Failed to fetch`
on every BFF call because no CORS policy was registered. Fix (Development-only):

- `services/bff/Program.cs` registers a `DevPortals` CORS policy when
  `builder.Environment.IsDevelopment()` is true. Origins come from
  `Cors:DevOrigins` (string[]) and default to `http://localhost:3000` + `http://localhost:3001`.
  Policy: `WithOrigins(...).AllowAnyHeader().AllowAnyMethod().AllowCredentials()`.
- `app.UseCors(DevPortals)` is inserted between `UseAuthorization()` and `UseTenancy()`,
  also Dev-only. Production environments do **not** register the policy — by design,
  same fail-loud posture as `DevJwtStubHandler`. Real CORS for prod portals will be a
  Phase-2 task (paired with Entra External ID + the real public BFF hostname).
- Verified: `OPTIONS /api/v1/lookups/branches` with `Origin: http://localhost:3000` →
  `204` + `Access-Control-Allow-Origin: http://localhost:3000` +
  `Access-Control-Allow-Headers: x-dev-tenant-id,x-dev-user-type`.

### 8. ✅ DELIVERED — Local-SQL runbook (Docker-free path)

Docker Desktop refused to start on this workstation, blocking the standard `pnpm infra:up`
path. Confirmed working alternative using the user's pre-existing local SQL Server default
instance (`STABREZ-LAPTOP`, Windows auth, database `AutoLeaseNet_Dev` already at the
latest migration `20260523163430_Add_Core_Aggregates` with realistic seed data —
20 customers / 60 vehicles / 80 drivers / 3 branches / 10 leases / 4 rent policies /
3 extended coverages — under tenant `a1a1a1a1-0001-0000-0000-000000000001`).

BFF user-secrets configured for this machine (no code change, no commit):

- `ConnectionStrings:AutoLeaseNet = Server=.;Database=AutoLeaseNet_Dev;Trusted_Connection=True;TrustServerCertificate=true;Encrypt=false`
- `Seed:Mode = Empty` — **critical**: keep this `Empty` so the existing curated seed
  data is never disturbed. If the DB ever needs to be re-seeded from scratch, drop
  the DB, recreate it, run `dotnet ef database update`, then flip to
  `Seed:Mode=Demo` for one BFF startup, then flip back to `Empty`.
- `Tajeer:Mode = InMemory` + dummy `Tajeer:AppId/AppKey/AuthorizationToken/BranchId/WebhookSharedSecret = dummy-webhook-secret`
  - `Tajeer:Webhook:LogOnly = false`.
- `Seed:TenantId = a1a1a1a1-0001-0000-0000-000000000001`.

Start-up sequence (this machine, Docker-free):

1. `pnpm --filter @autoleasenet/web-portal dev` → http://localhost:3000
2. `$env:ASPNETCORE_URLS='http://localhost:5000'; $env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --project services/bff/AutoLeaseNet.Bff.csproj --no-launch-profile`
   → http://localhost:5000 (user-secrets above already on disk; no further config needed).

When Docker comes back online OR when running on a different machine, the standard path
(`scripts/local-smoke.ps1` → SQL Edge on 1433 with SA auth) still works unchanged — the
user-secret connection string only overrides on this specific machine.

### 5. Architectural follow-ups (Week 2+ — not blockers, write down so they don't drift)

- Replace inline domain-event dispatch with a DbContext interceptor. Today the
  webhook handler hand-rolls `DispatchDomainEventsAsync`. Saga work in Week 2 should
  formalize this into an interceptor that runs on every `SaveChanges`.
- Add a `BackgroundService` worker for webhook async dispatch. Phase-1 inline
  dispatch was fine for one event/sec; Spec 03 §12.3 calls for an async drain pattern.
- Wire RLS policies on every domain table (Week 2 Day 9 per the existing plan).
- Move PII columns to **SQL Server Always Encrypted** (Week 2 Day 9 alongside RLS).
- Move webhook tenant resolution off the Phase-1 cross-tenant lookup → per-tenant
  webhook URL (`/api/v1/webhooks/tajeer/{tenantId:guid}`) in Phase 2.
- Decide on `appsettings.Development.json` strategy: either un-gitignore it with
  documented dummies, or delete it entirely and force all dev/test configs to
  inject inline. Today's split bites — see TODO #1.

## Next steps for Copilot

When you (Copilot / Claude / future me) sit down next:

1. **First**: `git log --oneline -10` + `git status` + `git diff HEAD` to see what
   actually moved.
2. **Read this file top to bottom.**
3. **Read** `Plans/workstreams/2026-05-17-week-1-foundation-tajeer-happy-path/plan.md`
   for the unchecked boxes if you've forgotten where Week 1 ended.
4. **If user says "continue Week 1"**: address TODO #1 (CI red) FIRST. Then offer
   TODO #2 (manual staging) and Week 2 entry (TODO #4).
5. **If user says "continue Week 2"**: confirm `design.md` exists; if not, surface
   that blocker and ask. Don't generate UI without it.
6. **If user mentions a new external dep is unblocked** (Azure / Entra / Unifonic /
   Pro upgrade / public repo): light up the corresponding loop-back tasks from
   `Plans/workstreams/2026-05-17-week-1-foundation-tajeer-happy-path/plan.md` §6.
7. **Every meaningful change → update this file**. Don't keep architecture or status
   in chat memory.

## Working pattern for any non-trivial change

Per CLAUDE.md + the user's superpowers workflow adoption (see `MEMORY.md`):

1. **Open a workstream plan** under `Plans/workstreams/{YYYY-MM-DD-slug}/plan.md`
   with: goal, scope, dependencies, risks, definition-of-done, RED→GREEN task list
   (2-5 min each).
2. **TDD discipline** — failing test first, then make it pass, then refactor.
3. **Run `dotnet test AutoLeaseNet.sln --settings .runsettings`** before each commit.
   Don't push red.
4. **Update this file** at the end of each meaningful step: what changed, why, what's
   still open.
5. **One commit per logical change** with the conventional-commit prefix style this
   repo uses (`feat(area): …`, `fix(area): …`, `docs(area): …`, `ci(area): …`,
   `refactor(area): …`).
6. **Don't merge a code change while CI is red** (once we have CI working). Today CI
   IS red — see TODO #1.

## Last updated

2026-05-24 (late evening) — PR #5 merged: smoke job now gated by `TAJEER_REAL_SMOKE_ENABLED`
secret and reads the correctly-named `TAJEER_AUTHORIZATION_TOKEN` / `TAJEER_BRANCH_ID` /
`TAJEER_WEBHOOK_SHARED_SECRET` (previously mismatched, three of five env vars resolved
to empty, causing the smoke test to call real Tajeer with the two dummy values that
DID resolve, and 401). CI on `main` (`709c094`) now fully green for the first time
on the public repo. Previous checkpoint: PR #4 merge at `547a077` (Dev CORS + local-SQL
runbook).

**Outstanding nit (not blocking)**: a stray `AutoLeaseNet/` subfolder at the repo root
appeared during the VSCode shift — it's a nested clone with its own `.git/` pointing at
the same origin. Untracked today; should be either deleted or added to `.gitignore`
before someone IDE-opens it and gets confused about which copy is canonical.
