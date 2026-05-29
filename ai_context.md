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
5. **Domain events dispatched by a DbContext interceptor.** `Lease.MarkIssued` raises
   `LeaseIssuedDomainEvent`. `DomainEventDispatchInterceptor` (Infrastructure) hooks
   `SavedChangesAsync` (post-commit), walks `ChangeTracker.Entries<Entity>()`, and
   publishes each event via MediatR as `DomainEventNotification<TEvent>` — the generic
   wrapper lives in Application so Domain stays MediatR-free. Per-event wrapper classes
   are no longer needed; handlers register as
   `INotificationHandler<DomainEventNotification<TConcreteEvent>>`. Any caller of
   `SaveChangesAsync` (sagas, dev endpoints, future workers) gets transparent dispatch.
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
12. **Tenant isolation is three layers, with the DB engine layer as the floor.** App-layer
    `TenantId` WHERE clauses remain; on top, `TenancyConnectionInterceptor` writes SQL
    `SESSION_CONTEXT` on every connection open, and `dbo.TenancyPolicy` RLS filters
    every read + blocks every cross-tenant write. The interceptor is registered in
    `AddAutoLeaseNetDbContext` alongside `DomainEventDispatchInterceptor`, so prod +
    tests share one wiring path. `ITenancyAccessor` returns `null` on anonymous
    requests (RLS then hides all rows — safe by default); the seeder and webhook
    receiver bypass via `SystemTenancyScope.For(tenantId)` /
    `SystemTenancyScope.ForWebhookBootstrap()` (`UserType=WEBHOOK_BOOTSTRAP` is a
    predicate override that Phase 2 retires when webhook URLs encode tenant).

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
| `POST` | `/api/v1/inspections`                | dev JWT stub + `Idempotency-Key` header | `StartInspectionRequest`               | 201 `{id, status}` ; 400 / 404 / 409 / 422 on failure                           |
| `POST` | `/api/v1/inspections/{id}/photos`    | dev JWT stub + `Idempotency-Key` header | `AddPhotoRequest`                      | 200 `{id, status}` ; 404 / 409 / 422 on failure                                 |
| `POST` | `/api/v1/inspections/{id}/damage-markers` | dev JWT stub + `Idempotency-Key` header | `AddDamageMarkerRequest`           | 200 `{id, status}` ; 404 / 409 / 422 on failure                                 |
| `POST` | `/api/v1/inspections/{id}/complete`  | dev JWT stub + `Idempotency-Key` header | empty                                  | 200 `{id, status: Completed}` ; 404 / 409 on failure                            |
| `POST` | `/api/v1/inspections/{id}/abandon`   | dev JWT stub + `Idempotency-Key` header | `AbandonInspectionRequest`             | 200 `{id, status: Abandoned}` ; 404 / 409 / 422 on failure                      |
| `GET`  | `/api/v1/inspections/{id}`           | dev JWT stub                            | —                                      | `InspectionDetailDto` (with photos + damage markers) ; 404 if unknown           |
| `GET`  | `/api/v1/lookups/inspections`        | dev JWT stub                            | `?page=1&pageSize=50&vehicleId=&leaseId=&type=&status=` | `PagedResult<InspectionSummaryDto>`                |
| `POST` | `/api/v1/leases/{id}/check-in`       | dev JWT stub + `Idempotency-Key` header | `CheckInLeaseRequest`                  | 200 `{leaseId, inspectionId, status: Closed, payment: {rentAmount, paidAmount, lateHoursFee, extraKmFee, damagesFee, discountAmount, totalDue, vatAmount, grandTotal, finalPaidAmount}}` ; 404 unknown ; 422 invalid state / odometer regression / Tajeer non-transient ; 503 Tajeer transient |
| `POST` | `/api/v1/leases/{id}/extend`         | dev JWT stub + `Idempotency-Key` header | `ExtendLeaseRequest` (`newContractEndUtc`, optional charges + reason)  | 200 `{leaseId, status: Extended, newContractEndUtc, extensionCount, charges?: {totalDue, vatAmount, grandTotal}}` ; 404 unknown ; 422 invalid state / `lease.extensions_exhausted` / `lease.invalid_new_end_date` / Tajeer non-transient ; 503 Tajeer transient |
| `POST` | `/api/v1/leases/{id}/suspend`        | dev JWT stub + `Idempotency-Key` header | `SuspendLeaseRequest` (`suspensionReasonCode`, optional notes)         | 200 `{leaseId, status: Suspended, suspensionReasonCode, suspendedAtUtc}` ; 404 unknown ; 422 invalid state / Tajeer non-transient ; 503 Tajeer transient |
| `POST` | `/api/v1/incidents`                  | dev JWT stub + `Idempotency-Key` header | `ReportIncidentRequest`                | 201 `{id, status: Open}` ; 422 invalid input |
| `POST` | `/api/v1/incidents/{id}/investigate` | dev JWT stub + `Idempotency-Key` header | empty                                  | 200 `{id, status: UnderInvestigation}` ; 404 / 409 |
| `POST` | `/api/v1/incidents/{id}/resolve`     | dev JWT stub + `Idempotency-Key` header | `ResolveIncidentRequest` (notes)       | 200 `{id, status: Resolved}` ; 404 / 409 |
| `POST` | `/api/v1/incidents/{id}/close`       | dev JWT stub + `Idempotency-Key` header | empty                                  | 200 `{id, status: Closed}` ; 404 |
| `PATCH`| `/api/v1/incidents/{id}/claim`       | dev JWT stub + `Idempotency-Key` header | `UpdateIncidentClaimRequest`           | 200 `{id, status}` ; 404 / 409 (immutable when Closed) |
| `GET`  | `/api/v1/incidents/{id}`             | dev JWT stub                            | —                                      | `IncidentDetailDto` ; 404 if unknown |
| `GET`  | `/api/v1/lookups/incidents`          | dev JWT stub                            | `?page=&pageSize=&leaseId=&vehicleId=&status=&severity=` | `PagedResult<IncidentSummaryDto>` |

## Current repo state

- **Branch**: `main` at commit `d8d315f` (`feat(infra): Reconciliation BackgroundService skeleton (#21)`) — pending PR for Customer Portal scaffold workstream.
- **CI on main**: ✅ all three jobs green — `.NET (build -warnaserror + test)`, `JS (lint + typecheck + build)` (note: JS gate runs with `continue-on-error: true` on every step; both portals now build cleanly locally so dropping the flag is a near-term cleanup), `Tajeer staging smoke (Category=Smoke)` (cleanly skipped via the `TAJEER_REAL_SMOKE_ENABLED` gate).
- **Tests**: **279 green** across 5 test projects (Adapters.Common 20, Adapters.Tajeer 66, Infrastructure 17, Application 113, Bff 63). Run via `dotnet test --filter "Category!=Smoke&Category!=Integration"`. Plus 5 `Category=Integration` RLS tests gated on local SQL only.
- **Merged PRs since checkpoint `35ecbae`**: #1–#10 (Week-1 stabilisation / governance / scaffold / seed). #11 (ai_context refresh), #12 (Inspection aggregate), #13 (Day-18 CHECK_OUT → Lease link), #14 (Day-19 check-in saga local close), #15 (Tajeer Calculate + Close saga), #16 (Day-20 Extend + Suspend), #17 (Day-21 Incident aggregate), #18 (ai_context refresh).
- **Active aggregates**: Lease, Customer, Vehicle, Driver, Branch, RentPolicy, ExtendedCoverage, WebhookLog, Inspection (+ children), Incident.
- **`ITajeerContractClient` surface**: `SaveAsync`, `CalculatePaymentAsync`, `CloseAsync`, `ExtendAsync`, `SuspendAsync` (5 methods; all share the `SendAsync<TReq,TRes>` error-mapping spine). InMemory sibling honours per-method override factories for negative-path tests.
- **Tenancy enforcement (Day-9, new)**: three layers — repository `TenantId` filter (unchanged), `TenancyConnectionInterceptor` setting SQL `SESSION_CONTEXT` on every connection open, and `dbo.TenancyPolicy` RLS on 9 aggregate-root tables (`Leases`, `Customers`, `Vehicles`, `Drivers`, `Branches`, `RentPolicies`, `ExtendedCoverages`, `Inspections`, `Incidents`). `SystemTenancyScope` (AsyncLocal) provides the bypass path used by the demo seeder and the Tajeer webhook receiver (`WEBHOOK_BOOTSTRAP` user-type override; Phase-2 retires it).
- **EF migrations applied to local `AutoLeaseNet_Dev`** (latest): `20260529020317_Add_OutboxEvent` (this commit's), preceded by `20260529012701_Add_RLS_TenancyPolicy`, `20260528205440_Add_Incident_Aggregate`, `20260528131820_Add_Inspection_Aggregate`, `20260523163430_Add_Core_Aggregates`.
- **Branch protection**: enforced on `main`. Direct push blocked; every change is `gh pr create` → `gh pr merge --squash --delete-branch`.
- **Repo visibility**: public. L.G2/L.G3 closed at $0 cost.
- **Current blocker profile**: no active code/CI blockers. Remaining external work: manual Tajeer Rabet staging exercise (needs creds + ngrok), Azure / Entra / Unifonic onboarding. Phase-1 hardening sprint done; first demo-unblocking slice (Customer Portal scaffold) shipped. Carry-forward: ZATCA adapter (Week-4 critical path), `ITajeerContractClient.GetAsync` (turns reconciliation stub into real drift detector), Vehicle Replacement Saga (subscribes to `IncidentReportedDomainEvent`), Customer Portal — My Vehicles / Lease detail (needs `/me/vehicles` endpoint or RLS extension), `BffTestHostDefaults` shared config helper (three retros have asked), Always Encrypted on PII (gated on Azure Key Vault or local-cert), drop `continue-on-error: true` from JS CI typecheck+build (both portals now build cleanly), RLS on Inspection child tables (Phase 2 backfill).

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

- ✅ **DONE 2026-05-25** — Replaced inline domain-event dispatch with
  `DomainEventDispatchInterceptor` hooked into `SavedChangesAsync`. Per-event
  wrappers (e.g. `LeaseIssuedNotification`) retired in favour of generic
  `DomainEventNotification<TEvent>`. See workstream
  [`2026-05-25-dbcontext-interceptor-domain-events/`](./Plans/workstreams/2026-05-25-dbcontext-interceptor-domain-events/).
  Test factories swapping `DbContextOptions<AutoLeaseNetDbContext>` to EF Core
  InMemory once had to re-bind the interceptor inline; PR #9 extracted
  `services.AddAutoLeaseNetDbContext(configureProvider)` so prod + tests both
  flow through one place. Adding a new interceptor in Week 2 Day 9 (RLS /
  SESSION_CONTEXT) now lands in exactly one method.
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
3. **Read** the latest workstream under `Plans/workstreams/` (sorted by date) — its
   retrospective lists carry-forward items the way the conversation thread does.
   Today's tip-of-tree: [`2026-05-28-day-21-incident-aggregate`](./Plans/workstreams/2026-05-28-day-21-incident-aggregate/).
4. **If user says "continue"** with no other context: propose options via
   `AskUserQuestion` from the deferred carry-forward list (recommended first), then
   start the chosen workstream. The established pattern from PRs #12 → #17 is one
   PR per workstream: open a `Plans/workstreams/{YYYY-MM-DD-slug}/plan.md`, branch,
   ship the slice, write `retrospective.md`, bump this file, squash-merge.
5. **Currently deferred carry-forward** (any of these is a legit "next"):
   - **Outbox + BackgroundService drain** — closes the cross-system inconsistency
     window for all four Tajeer-touching commands (Save / Close / Extend / Suspend).
   - **Vehicle Replacement Saga** (Spec 02 §6.5) — subscribes to
     `IncidentReportedDomainEvent` filtered on `RequiresReplacement = true`.
   - **`LeaseClosed` / `LeaseExtended` / `LeaseSuspended` domain events** → Week-4
     invoicing trigger. The aggregates don't raise them yet; thin layer to add.
   - **Reconciliation job** (15-min scheduled) — Day-20 master-plan note; needs a
     hosted-service skeleton.
   - **Day 9 RLS + Always Encrypted PII** (Vehicle Replacement Saga is independent
     of this; PII columns include PoliceReportNumber + InsuranceClaimNumber from
     PR #17's Incident aggregate).
6. **If user says "continue Week 2 UI"**: confirm `design.md` exists; if not,
   surface that blocker and ask. Don't generate UI without it.
7. **If user mentions a new external dep is unblocked** (Azure / Entra / Unifonic):
   look for the corresponding placeholder in the latest workstream retrospective
   or in Plan 05 (`Plans/05-dependency-onboarding-checklist.md`).
8. **Every meaningful change → update this file**. Don't keep architecture or
   status in chat memory.

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

2026-05-29 — Tech-debt sweep: `BffTestHostDefaults` extracted + CI strict
mode enabled. Closes the FIVE-retros-in-a-row complaint that every new BFF
endpoint workstream copies the same ~30-line `ConfigureWebHost` block.
**New helper**: `services/bff.tests/Support/BffTestHostDefaults.cs` exposes
`Defaults()` (the 13 always-shared keys, Seed:Mode=Empty), `DemoSeedDefaults
(tenantId, randomSeed)` (Defaults + Seed:Mode=Demo + the three seeder keys),
and `ReplaceDbContextWithInMemory(services, dbName)` (the standard EF
InMemory swap ceremony). Per-factory overrides are now `var settings = …; 
settings["X"] = "y";`. **13 factories retrofitted** across 11 files:
MeFactory, MyVehiclesFactory, IncidentFactory, InspectionFactory,
CheckInFactory, ExtendSuspendFactory, SaveContractEndpointFactory,
WebhookFactory, SmsE2EFactory, HealthTestFactory, BrokenSqlHealthTestFactory,
DevWebApplicationFactory, EnvironmentWebApplicationFactory. Each
`ConfigureWebHost` shrank from ~30 to ~6–10 lines; net `-230` lines across
the test project. Pure refactor: 337 tests still green, no behavioural
change. **CI strict mode**: `.github/workflows/ci.yml` dropped
`continue-on-error: true` from JS Typecheck + Build steps (kept on Lint +
Test which are still skeletal). Both portals strict-typecheck + build
cleanly locally (verified pre-flight). Carry-forward updated:
`BffTestSeedWaiter` (the next-biggest factory copy-paste pattern) is now
the top tech-debt item; ZATCA adapter still Week-4 critical path.

2026-05-29 — Customer Portal **My Vehicles** shipped end-to-end. Second
demo-unblocking slice (after PR #22 scaffold). **Backend**:
`Application.Me.GetMyVehiclesQuery` + `MyVehicleDto`;
`Infrastructure.Me.GetMyVehiclesQueryHandler`. The handler is a deliberate
two-step because Day-9 RLS on `Vehicles` is internal-staff-only
(`fn_TenancyPredicate(TenantId, NULL)` blocks external reads): step 1 reads
`Leases` under the natural request scope (RLS scopes to my customer) filtered
to `Active | Extended | Suspended` with `VehicleId != null`, projecting to a
distinct id set; step 2 opens a `SystemTenancyScope.For(tenantId)` bounded
strictly to a `Vehicles.Where(v => ids.Contains(v.Id))` read. **Trust boundary
documented in the handler XML doc**: the id set comes from the RLS-scoped
lease query (NOT inside the SystemTenancyScope), so it's algebraically
impossible to return a vehicle the caller doesn't have a lease on. Three
invariants must hold under future edits: keep the SystemTenancyScope bounded
to the Vehicles read, keep the lease query outside it, keep the WHERE-IN
clause in place. Phase-2 cleanup path: extend the Vehicles RLS predicate with
a customer-derived clause (the Day-9 migration comment already flags this);
when that lands the handler collapses to a single LINQ join and the bypass
goes away. Endpoint: `GET /api/v1/me/vehicles` in `MeEndpoints.cs` mirrors
the `/leases` shape — 401 anon, 400 `me.requires_customer_context` for
INTERNAL_STAFF, 200 array for EXTERNAL_INDIVIDUAL. **Frontend
(customer-portal)**: `app/vehicles/page.tsx` table (plate triple in
`dir="rtl"` span, make/model, year, color, KM, license + insurance expiry),
loading / empty / error states; dashboard expanded to 4 stat cards (added
"Currently driving" from `/me/vehicles` count) + second CTA link; "My
Vehicles" added to nav. AR+EN i18n strings added. **Tests**: 337 total
green (+6: 3 endpoint, 3 handler — Active/Extended/Suspended only filter,
empty-when-no-leases, throws-on-missing-CustomerId). Customer-portal build:
4 routes. Web-portal build: 7 routes. RED→GREEN ~75 min including plan +
retro. Carry-forward updated: ZATCA adapter (Week-4 critical), close-saga
refactor to TajeerStatusMapper (5-line cleanup, bundle), Vehicle Replacement
Saga, customer-portal lease/vehicle detail pages, `BffTestHostDefaults`
helper (5th retro now flagging it), drop `continue-on-error` from JS CI,
Always Encrypted on PII (gated on AKV).

2026-05-29 — Tajeer `GetAsync` + real status-mirror drift detection shipped.
Closes the gap left by PR #21 (reconciliation stub was log-only because the
client had no read method). **Adapter surface**: new `GetContractResponse` DTO
(lean projection — status code + reason codes + extensionCount + updatedAt);
`ITajeerContractClient.GetAsync(long contractNumber)` returning
`IntegrationResult<GetContractResponse>`. Real `TajeerContractClient.GetAsync`
hits `/api/contracts/{contractNumber}` via a new `SendNoBodyAsync` overload of
the failure-mapping spine; 404 maps to `tajeer.vendor.contract.not_found`
(non-transient drift signal). `InMemoryTajeerContractClient.GetAsync` projects
the most recent Save/Close/Suspend/Extend call back as a synthetic response;
new `getFactory` ctor override + public `SeedProjection(...)` helper for
drift-test wiring. **Status mapping centralised**: `TajeerStatusMapper.FromTajeer`
+ `ApplyLocalRefinements` in `Infrastructure/Tajeer/` per Spec 03 §7.2 / §1
principle #10 (mapper lives in Infrastructure, not the adapter, because
adapters here are kept Domain-free; the vendor codes live in the adapter DTOs,
the translation to `LeaseStatus` lives one layer up). `InvalidTajeerStatusException`
fires on unknown triples — caught by the reconciliation loop. **Reconciliation
upgrade**: `TajeerStatusMirrorCheck` now takes `ITajeerContractClient`, walks
Active+Extended leases with `TajeerContractNumber != null`, compares via the
mapper, classifies each row as match / drift / vendor-failure-drift /
transient-blip / unrecognised-state (warn on every drift, debug on match, debug
on transient). Phase 1 is detect-only by design — auto-correcting risks masking
missed webhooks (Phase 2 lands an action policy). **320 tests green** (was 279;
+12 mapper, +6 real GET, +9 InMemory GET, +5 mirror, plus subtractions in
mirror retrofit). Carry-forward next: ZATCA adapter (Week-4 critical path),
Customer Portal — My Vehicles via lease join, Vehicle Replacement Saga,
`BffTestHostDefaults` shared helper (4 retros now), drop `continue-on-error`
from JS CI.

2026-05-29 — Customer Portal scaffold shipped. First demo-unblocking slice
after the Phase-1 hardening sprint. **Backend**: new `Application.Me`
namespace + `GetMyLeasesQuery` + `MyLeaseDto`; `Infrastructure.Me`
`GetMyLeasesQueryHandler` (trusts Day-9 RLS for CustomerId scoping — no
app-side WHERE); endpoint `GET /api/v1/me/leases` (group:
`MeEndpoints.MapMeEndpoints`). 3 endpoint tests cover anonymous→401,
internal-staff-without-customer→400 `me.requires_customer_context`, and
external-customer→200 lease list. **Frontend (customer-portal)**:
Tailwind + brand palette + LocaleProvider + AR/EN dictionaries (with status
labels for all 7 LeaseStatus values), typed BFF client always sending
`X-Dev-User-Type=EXTERNAL_INDIVIDUAL` + `X-Dev-Customer-Id`, app shell with
Dashboard / My Leases + AR/EN toggle + signed-in-as ribbon + amber dev
banner, dashboard with 3 stat cards (total/active/closed) + CTA, leases
table with status badges and dates. Demo customer hardcoded in
`lib/dev-customer.ts` (id `cc368b8b-...`; env-overridable). **Adjacent fix**:
removed `as const` from BOTH portals' `messagesEn` — silently fixed a
type-narrowing bug that had been making web-portal build fail under
`continue-on-error: true` in CI. Both portals build green now (web-portal:
8 routes; customer-portal: 3 routes). **279 tests green** (+3 MeEndpoint).
First PR in the post-hardening "demo-unblocking" arc.

2026-05-29 — Reconciliation BackgroundService skeleton shipped. **Phase-1
hardening sprint complete** (Day-9 RLS + Outbox + Reconciliation, three PRs).
`IReconciliationCheck` abstraction + `ReconciliationOptions` (default
15-minute cadence per Plan 02 Day 20) + `ReconciliationService :
BackgroundService` (second instance on the OutboxDrainService pattern;
per-cycle DI scope, per-check try/catch). One stub check
`TajeerStatusMirrorCheck` iterates configured `Reconciliation:Tajeer:TenantIds`
under `SystemTenancyScope.For(tenantId)`, queries `MaxLeasesPerCycle` most
recently-updated `Active` leases, logs visibility. Does NOT yet call Tajeer —
needs `ITajeerContractClient.GetAsync` which is a separate workstream.
`AddReconciliation(section)` extension; wired in `Program.cs`. Test factory
sweep: 9 factories opted out via `Reconciliation:Enabled=false`. **276 tests
green** (+4 ReconciliationService + 3 TajeerStatusMirrorCheck). Next
workstreams (Phase-1 hardening done): Customer Portal scaffold, ZATCA adapter
(Week-4 critical path), Vehicle Replacement Saga, Always Encrypted (pending
AKV or local-cert decision), Tajeer GetContract method.

2026-05-29 — Outbox + BackgroundService drain shipped. Replaces the inline
post-commit `DomainEventDispatchInterceptor` with a transactional outbox:
`OutboxWriteInterceptor` captures domain events into rows in the same UoW
as the business change; `OutboxDrainService : BackgroundService` polls every
`Outbox:DrainIntervalSeconds` (default 5s) and dispatches via MediatR with
exponential backoff retry (1→1s, 2→2s, 3→4s, 4→8s, 5→16s capped at 60s).
After `Outbox:MaxAttempts` (default 5) rows are parked with `LastError`.
Per-tenant publish scope via `SystemTenancyScope.For(row.TenantId)` so
RLS-protected reads inside handlers (e.g. `LeaseIssuedSmsHandler` querying
`Customers`) work end-to-end. **Scoping note**: this closes the domain-event
delivery window — handlers retry instead of silent log+drop — but does NOT
close the Tajeer↔local commit window for saga handlers (that needs a
command-table redesign, not a write-side outbox). The old
`DomainEventDispatchInterceptor` class + its 3 tests deleted; behaviour
subsumed by the new path. EF migration `20260529020317_Add_OutboxEvent`
applied to local Dev. 8 test factories sweep-updated to opt out of the
drain (`Outbox:Enabled=false`); `LeaseIssuedSmsEndToEndTests` keeps it on
with 1s interval + polls for completion as the full-pipeline regression
guard. **269 tests green** (was 264; +8 outbox unit/integration, -3 retired).

2026-05-29 — Day-9 RLS tenant-isolation workstream shipped. **Tenant isolation
now enforced at the DB layer** for the first time. New port `ITenancyAccessor`
+ `Tenancy(TenantId, CustomerId?, UserType)` record + `SystemTenancyScope`
(AsyncLocal) in `Application.Ports.Tenancy`. New interceptor
`TenancyConnectionInterceptor` writes `SESSION_CONTEXT('TenantId' | 'CustomerId'
| 'UserType')` on every `SqlConnection` open; registered alongside
`DomainEventDispatchInterceptor` via `AddAutoLeaseNetDbContext` so prod + tests
agree. EF migration `20260529012701_Add_RLS_TenancyPolicy` creates
`dbo.fn_TenancyPredicate` + `dbo.TenancyPolicy` covering 9 aggregate-root
tables. Phase-1 webhook bypass via `WEBHOOK_BOOTSTRAP` UserType override
(retires in Phase 2 with per-tenant webhook URLs). End-to-end smoke proven:
unknown-tenant header against `/api/v1/lookups/customers` returns
`totalCount:0` even without any app-side filter. **264 tests green** (+6
SystemTenancyScope + 2 TenancyConnectionInterceptor unit tests); +5
`Category=Integration` RLS isolation tests proving cross-tenant read filter,
cross-tenant write block, and the WEBHOOK_BOOTSTRAP override. Always Encrypted
split to a follow-up workstream pending Azure Key Vault provisioning.

2026-05-29 — Day-21 Incident aggregate shipped. Mirrors PR #12's Inspection
aggregate structurally — `Incident` aggregate root with full Spec 01 §5.6
field list + Spec 02 §4.7 state machine (`Open → UnderInvestigation | Resolved
| Closed`; `Resolved → Closed`; `Closed` terminal). 5 commands + handlers +
2 queries + handlers; `IIncidentRepository` port; `EfIncidentRepository`;
new EF migration `Add_Incident_Aggregate` applied to local Dev DB.
7 BFF endpoints under `/api/v1/incidents` (including the first PATCH —
`UpdateClaim`). Seed adds one Closed incident per Closed lease (alternating
TrafficAccident + Breakdown). `IncidentReportedDomainEvent` forward-declared
with no subscriber — Replacement Saga (Spec 02 §6.5) is its future consumer.
**256 tests green** (+20: 13 domain + 7 endpoint).

2026-05-28 — Day-20 Extend + Suspend workstream shipped (same session as PR #15).
`ITajeerContractClient` grew to 5 methods (`Save`, `CalculatePayment`, `Close`,
`Extend`, `Suspend`); both new methods reuse the `SendAsync<TReq,TRes>` helper
so zero new error-mapping code was added. Domain gained
`Lease.MaxExtensions = 25` + two new `IncrementExtension` invariants
(non-monotonic date / extensions exhausted). Two new BFF endpoints
(`POST /api/v1/leases/{id}/extend` + `/suspend`), each going Tajeer-first then
local commit, both idempotency-cached. `ExtendSuspendFactory` applied the
explicit-InMemory-swap pattern from PR #15's hotfix up-front. **236 tests
green** (+22: 4 real-client + 4 InMemory + 2 domain + 9 handler + 3 endpoint).

2026-05-28 — Tajeer Close Saga workstream shipped. The Day-19 check-in saga is
now a true vendor commit, not local-only. `ITajeerContractClient` gained
`CalculatePaymentAsync` + `CloseAsync` (real client `PUT`s to
`/api/contracts/calculate-payment` + `/api/contracts/closure`; InMemory sibling
records calls + accepts per-method override factories). `CheckInLeaseCommandHandler`
now calls Tajeer Calculate → Tajeer Close → local commit; vendor failure
short-circuits before any local mutation, so the inconsistency window is
scoped to "Tajeer 200 → local SaveChanges" (self-heals on idempotent replay).
Endpoint response gained a `payment` block and a 503 mapping for transient
Tajeer failures. **Outbox + BackgroundService drain still deferred** — runbook
note in [`Plans/workstreams/2026-05-28-tajeer-close-saga/plan.md`](./Plans/workstreams/2026-05-28-tajeer-close-saga/plan.md).
Real-client envelope/error-mapping spine refactored into a shared
`SendAsync<TReq,TRes>` helper so all three contract methods (Save / Calculate
/ Close) share one set of vendor-error / HTTP / network / timeout / JSON-parse
branches. **214 tests green** (+17: 7 real-client + 6 InMemory + 4 handler;
endpoint test extended for `payment` block).

2026-05-25 — Day-19 check-in saga (local slice) shipped. New
`CheckInLeaseCommand` + `POST /api/v1/leases/{id}/check-in` create the
CHECK_IN inspection, link it via the now-broadened `Inspection.LinkToLease`,
close the lease (`Lease.MarkClosed`), and return the vehicle
(`Vehicle.Return`) — all in one UoW commit. Seed adapter updated to walk
Vehicle through Reserve/StartRental/Return so seeded Active/Extended/
Suspended/Closed leases reflect realistic Vehicle state. **197 tests green**
(+10: 1 domain + 6 handler + 3 endpoint). Tajeer adapter calls
(CalculateContractPayment + CloseContract) deferred to the next workstream.

2026-05-25 — Day-18 check-out saga (slim slice) shipped. New domain method
`Inspection.LinkToLease(leaseId, nowUtc)` enforces COMPLETED + CheckOut/PreDelivery
+ no-existing-link invariants; new `LeaseLinkedAtUtc` audit timestamp.
`SaveContractCommand` gained optional `CheckOutInspectionId` — handler validates
4 negative paths (not-found / vehicle-mismatch / not-completed / wrong-type /
already-linked → `lease.checkout_inspection.{code}`) and auto-looks-up the most
recent un-linked CHECK_OUT for the vehicle when the id is omitted. Phase 1.x
keeps the link **optional** (existing seed + test callers unaffected); flips to
required in Phase 1.y once the web portal drives the full saga. Seed adapter
now drives the link via `Inspection.LinkToLease` for parity with production flow.
**187 tests green** (+12: 7 domain + 5 SaveContract integration). Previous:
PR #12 Inspection aggregate.

2026-05-25 — Inspection aggregate workstream closed. First Week-3 slice:
`Inspection` aggregate + `InspectionPhoto` + `InspectionDamageMarker` with the
full Spec 01 §5.6 field surface; state machine per Spec 02 §4.6 (IN_PROGRESS →
COMPLETED / ABANDONED); 5 commands + 2 queries + 7 BFF endpoints under
`/api/v1/inspections` + `/api/v1/lookups/inspections`; new EF migration
`Add_Inspection_Aggregate` applied to local `AutoLeaseNet_Dev`; seed adapter now
populates 1 CHECK_OUT per non-terminal lease + CHECK_OUT+CHECK_IN per closed
lease with deterministic damage markers; `InspectionCompletedDomainEvent`
forward-declared (no Phase-1 subscriber — saga workstream wires one).
**175 tests green** (+24: 17 domain + 7 endpoint). Saga integration (Lease
invariants 2 / 3) intentionally deferred. Previous checkpoints: PRs #9 + #10
(`3638137`) — DbContext helper + nested-clone cleanup.

**Outstanding nit (not blocking)**: a stray `AutoLeaseNet/` subfolder at the repo root
appeared during the VSCode shift — it's a nested clone with its own `.git/` pointing at
the same origin. Untracked today; should be either deleted or added to `.gitignore`
before someone IDE-opens it and gets confused about which copy is canonical.
