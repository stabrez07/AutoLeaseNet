# Notes — Week 1 Foundation + Tajeer Happy Path

Running log of decisions, surprises, and drift captured during execution.

---

## 2026-05-18 — Day 0 scaffold verification

### T0.1 `dotnet restore` — ✅ passed
- .NET 8.0.206 SDK present.
- 20 projects restored cleanly.
- Surfaced **NU1902 warning** on `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.12.0` (transitive `Grpc.Net.Client` advisory `GHSA-4625-4j76-fww9`). Already acknowledged in the scaffold's `Directory.Build.props` comment as upstream-blocked.

### T0.2 `dotnet build -warnaserror` — ⚠️ initial fail, fixed

Three drift items broke the build under `-warnaserror`:

1. **NU1902 / NU1903** — listed in `<WarningsNotAsErrors>` but still failed under CLI `-warnaserror`. Moved to `<NoWarn>` in [Directory.Build.props](../../../Directory.Build.props) since the upstream OTel exporter has no patched release yet. Tracked: re-enable once OTel ships a transitive bump.
2. **CA1716** on `AutoLeaseNet.Domain.Shared` — namespace name conflicts with reserved keyword `Shared`. This is a deliberate DDD building-block convention; not consumed cross-language. Suppressed globally in `<NoWarn>` with an inline comment explaining why.
3. **CA1707** on test method `InMemoryClient_exposes_all_sub_interfaces` — underscores in test names is the project's chosen xUnit/BDD convention (matches `IntegrationResult_Success_carries_value` style throughout the Week 1 plan). Suppressed only for `*.Tests` projects via a conditional `<PropertyGroup>` in `Directory.Build.props`.

After the analyzer fixes, a real code error surfaced:
- **CS1061** in [services/bff/Program.cs:10](../../../services/bff/Program.cs#L10) — `AddOpenApi()` is a .NET 9 API and we target net8.0. The line was redundant (Swashbuckle already wired on the next line via `AddEndpointsApiExplorer()` + `AddSwaggerGen()`). Removed.

Final build: 0 warnings, 0 errors.

### T0.3 `pnpm install` — ⚠️ initial fail, fixed

Workspace had stale template names from a `superplexity` fork. Two fixes:

1. [pnpm-workspace.yaml](../../../pnpm-workspace.yaml): pointed at `eslint-config-superplexity` / `tsconfig-superplexity` — empty leftover dirs. Repointed to the real `eslint-config-autoleasenet` / `tsconfig-autoleasenet` packages.
2. [package.json](../../../package.json): root name was `"superplexity"`; the `openapi:gen` script filtered `@superplexity/contracts`. Renamed to `autoleasenet` and `@autoleasenet/contracts` respectively.

Leftover empty dirs `packages/eslint-config-superplexity/` and `packages/tsconfig-superplexity/` are not git-tracked. Left in place; safe to delete with `rmdir` if desired.

After fixes: 433 packages installed across 5 workspace projects (root + 2 apps + ui + contracts). No peer-dep blocking errors.

### T0.4 `pnpm build` — ✅ passed
- Both `@autoleasenet/web-portal` and `@autoleasenet/customer-portal` Next.js apps compiled and prerendered static pages successfully. 33s on cold turbo cache.

### T0.5 / T0.6 / T0.7 — 🔀 Compose stack replaced with local infra

Docker Desktop install ran into trouble. User confirmed local SQL Server 2019 Developer Edition is already present. Adopted decision:

| Compose service | Week 1 substitute | Rationale |
|---|---|---|
| `sql` (Azure SQL Edge) | Local SQL Server 2019 Developer, default instance, Windows Integrated Auth | Same engine family (T-SQL, Always Encrypted, RLS all available); no install cost |
| `redis` | `Adapters.Cache.InMemory` via hexagonal swap | Port already exists ([InMemoryCacheStore.cs](../../../packages/adapters/AutoLeaseNet.Adapters.Cache.InMemory/InMemoryCacheStore.cs)); flip `Cache:Mode` config when real Redis arrives |
| `azurite` (blob) | N/A this week | Not exercised until Week 3 photo/sketch upload |
| `mailhog` (SMTP) | N/A this week | No email send-path until Week 4 quotation PDF |

**T0.5-alt** ✅ Created `AutoLeaseNet_Dev` database via `sqlcmd -S localhost -E`.
**T0.6-alt** ✅ Probed `Microsoft.Data.SqlClient` connection with the exact BFF config string — `ServerVersion=15.00.2170, State=Open`.
**T0.7-alt** ✅ Confirmed `AddInMemoryCache()` extension wires both `ICacheStore` and `IIdempotencyStore` — Day 5 idempotency cache will use it.

[services/bff/appsettings.Development.json](../../../services/bff/appsettings.Development.json) updated:
- `ConnectionStrings:AutoLeaseNet` → `Server=localhost;Database=AutoLeaseNet_Dev;Trusted_Connection=true;TrustServerCertificate=true;Encrypt=false;Application Name=AutoLeaseNet.Bff`
- `ConnectionStrings:Redis` removed
- Added `Cache:Mode = "InMemory"` toggle (wired in T1.x ServiceCollection extensions on Day 1)

**Loop-back when Docker resolves OR Memurai installed**: revisit T5.6 + downstream to swap `AddInMemoryCache()` for `AddRedisCache()` and re-run integration tests. Track in plan §6.

**Note**: existing SQL Server hosts other databases (Ajar-related — `EHIAjarDB`, `AJARFRBApplications`, etc.). Confirmed those are not ours; we touch only `AutoLeaseNet_Dev`.

### T0.8 drift summary

| Drift | Severity | Resolution |
|---|---|---|
| NU1902/NU1903 in `WarningsNotAsErrors` doesn't hold under CLI `-warnaserror` | Med | Moved to `<NoWarn>` |
| CA1716 on `Domain.Shared` | Low | Suppressed (deliberate DDD pattern) |
| CA1707 on test methods | Low | Suppressed for `*.Tests` projects only |
| `AddOpenApi()` in BFF Program.cs (net9 API on net8 target) | Med | Removed; Swashbuckle already wired |
| pnpm workspace pointed at empty `superplexity` dirs | Med | Repointed to real `autoleasenet` dirs |
| Root `package.json` name + filter named `superplexity` | Low | Renamed to `autoleasenet` |
| Empty leftover dirs `packages/eslint-config-superplexity/`, `packages/tsconfig-superplexity/` | Low | Left in place (not git-tracked, harmless) |
| Docker Desktop not installed | **High** | Resolved by substitution: local SQL Server 2019 (Windows auth) + `Adapters.Cache.InMemory`; Azurite/MailHog deferred (not needed this week) |

---

## Day 1 — 2026-05-18 (Adapters.Common foundation, TDD)

All ten T1.x tasks worked end-to-end. Test count went from 0 → 20 in `AutoLeaseNet.Adapters.Common.Tests`. Full sweep on `dotnet build -warnaserror` clean (0 warnings, 0 errors); 21/21 tests green across the solution.

### Decisions

- **IntegrationResult redesigned** from the scaffolded discriminated-union (Ok/BusinessError/SystemError + Map) to the plan-required Result type (Success/Failure factories + IsTransient/ErrorCode/CorrelationId properties). Safe — no callers existed yet. Added `[SuppressMessage("CA1000")]` with justification: static factories on generic Result types are idiomatic (see LanguageExt, FluentResults).
- **IClock kept in `Application.Ports/Time/`** rather than moving to `Adapters.Common`. Rationale: domain/application code is the primary consumer; ports live in Application.Ports per Spec 04. Common.Tests references Application.Ports for the FakeClock test.
- **`KeyVaultCredentialProvider` updated to implement the new `ICredentialProvider`** — same shape as `DevEnvironmentCredentialProvider`, nullable returns for "not found" (callers decide fatality).
- **T1.10 skipped** — no call sites for `IntegrationResult` yet (adapters are stubs); refactoring nothing prematurely. Re-evaluate Day 3 when Tajeer adapter starts using it.

### New drift fixed

| Drift | Severity | Resolution |
|---|---|---|
| CA1000 on `IntegrationResult<T>.Success/Failure` | Low | Per-type `[SuppressMessage]` with justification (idiomatic Result pattern) |
| CA1859 on test helper returning interface | Low | Per-method `[SuppressMessage]` — tests intentionally exercise the port surface |

### Verification

```
dotnet build AutoLeaseNet.sln  → 0 warnings, 0 errors
dotnet test  AutoLeaseNet.sln  → 21 passed / 0 failed
  AutoLeaseNet.Adapters.Common.Tests : 20 (IntegrationResult ×2, PiiMasking ×6, PollyPipeline ×3, Clock ×3, DevEnvironmentCredentialProvider ×5, IntegrationResult lifecycle ×1)
  AutoLeaseNet.Adapters.Tajeer.Tests : 1 (InMemory smoke)
```

### Next pickup

Day 2 — TenancyMiddleware (dev-stub mode) + BFF skeleton. First task T2.1: `DevJwtStubHandler` reading tenant claims from `X-Dev-Tenant-*` headers (Development only). The `ITenantContext` port already exists in `Application.Ports/Tenancy/` from scaffolding — just need the BFF-side handler + middleware + tests.

---

## Day 2 — 2026-05-18 (TenancyMiddleware + BFF skeleton, TDD)

All six T2.x tasks complete. Two new test projects (`AutoLeaseNet.Bff.Tests`, `AutoLeaseNet.Infrastructure.Tests`). Test count went from 21 → 43.

### Decisions

- **DevJwtStubHandler in `services/bff/Authentication/`** — reads `X-Dev-Tenant-Id` (+ optional `X-Dev-User-Type`, `X-Dev-Customer-Id`, `X-Dev-Branch-Ids`, `X-Dev-User-Id`, `X-Dev-Roles`). Missing tenant header → `AuthenticateResult.NoResult()` (caller gets 401 if endpoint requires auth). Claim type names match Spec 06 §3.2 (`tenant_id`, `user_type`, `customer_id`, `branch_id`).
- **T2.1 + T2.6 collapsed into one Program.cs registration**: `AddDevJwtStub(environment)` throws `InvalidOperationException` when `env.IsProduction()`. Program.cs always calls `AddDevJwtStub` (Phase 1) so Production attempt → app refuses to start. JwtBearer wiring planned Phase 2+; until then, Production fails loudly by design.
- **ClaimsTenantContext implements `ITenantContext`** by reading from `IHttpContextAccessor.HttpContext.User`. Scoped DI registration; one instance per request. Test discovery: real `HttpContextAccessor` uses `AsyncLocal` which clobbers across two-context tests in the same async flow — stubbed via a tiny `StubHttpContextAccessor` for unit tests.
- **TenancyMiddleware** is thin: passes anonymous requests through unchanged, validates `tenant_id` on authenticated requests (rejects with 400 if missing/malformed), opens a `BeginScope` with `TenantId` so adapter logs auto-tag. Health endpoints stay anonymous so probes work.
- **SqlSessionContext helper in Infrastructure**: `SetTenantIdAsync` + `SetTenancyAsync` (multi-key) + `GetTenantIdAsync`. All sets use `@read_only=1` so RLS predicates can trust the value; subsequent set attempts throw SQL error 15664 (verified by test).
- **Health checks: liveness vs readiness via tags**. `Predicate = _ => false` for liveness (zero downstream checks). `Predicate = c => c.Tags.Contains("ready")` for readiness (SQL today; Redis when Memurai/WSL2 lands).
- **Redis readiness check deferred** because cache is `Adapters.Cache.InMemory` until Docker Desktop is resolved. Will add when we wire `Adapters.Cache.Redis`.

### Drift fixed during Day 2

| Drift | Severity | Resolution |
|---|---|---|
| CA1859 on test-helper return-type | Low | Added `CA1859` to `*.Tests` NoWarn block (test helpers intentionally exercise port abstractions) |
| CA1861 on `tags: new[] { "ready" }` array allocation | Low | Hoisted to `var readyTags = new[] { "ready" };` |
| HttpContextAccessor AsyncLocal clobbering across two contexts in one test | Med | Tiny `StubHttpContextAccessor` per-context for unit tests; real accessor unchanged for integration |

### Verification

```
dotnet build AutoLeaseNet.sln  → 0 warnings, 0 errors
dotnet test  AutoLeaseNet.sln  → 43 passed / 0 failed
  AutoLeaseNet.Adapters.Common.Tests    : 20 (unchanged from Day 1)
  AutoLeaseNet.Adapters.Tajeer.Tests    : 1  (unchanged from Day 1)
  AutoLeaseNet.Bff.Tests (new)          : 18 (DevJwtStub ×3, ProductionGuard ×2, Whoami+Tenancy ×1, ClaimsTenantContext ×8, Health ×4)
  AutoLeaseNet.Infrastructure.Tests (new): 4 (SqlSessionContext integration — needs local SQL)
```

### Next pickup

Day 3 — Tajeer auth + smoke call. First task T3.1: `TajeerOptions` bound from `Tajeer:` config section with required-field validation. Then auth handler, retry pipeline, and an end-to-end smoke call to `GET /api/lookups/branches` against Tajeer Rabet staging.

---

## Day 3 — Tajeer auth handler + lookups smoke (2026-05-22)

### What landed

| Task | Status | Detail |
|---|---|---|
| T3.1 — `TajeerOptions` | ✅ | Required + URL + Range data-annotations; bound from `Tajeer:` section with `ValidateOnStart()`. 9 unit tests. |
| T3.2 + T3.3 — `TajeerAuthHandler` (RED → GREEN) | ✅ | Delegating handler reads `IOptionsMonitor<TajeerOptions>` on every send → injects `App-id`, `App-key`, `Authorization`. Token rotation picked up without DI reload. 3 unit tests (CapturingInnerHandler + OptionsMonitorStub). |
| T3.4 — Named HttpClient registration | ✅ | `AddTajeer` registers `IHttpClientFactory` named client `"tajeer"` with `BaseAddress` + `Timeout`, attaches `TajeerAuthHandler`, then chains `AddResilienceHandler("tajeer-resilience", ...)` reusing `ResiliencePolicies.DefaultHttpPipeline`. 2 registration tests. |
| T3.5 — `TajeerLookupClient.GetAllBranchesAsync` (RED → GREEN) | ✅ | `IntegrationResult<IReadOnlyList<TajeerBranch>>` — 2xx → Success(parsed list), 4xx → Failure non-transient, 5xx → Failure transient, `HttpRequestException` / `TaskCanceledException` (timeout) / `JsonException` mapped to dedicated error codes. `[LoggerMessage]` source generators used (CA1848 clean). 5 unit tests via `StubHttpMessageHandler` + `StubHttpClientFactory`. DI wired (`AddScoped<TajeerLookupClient>`) + 1 resolution test. |
| T3.6 — Staging smoke harness | ✅ scaffold (awaiting cred run) | `Smoke/TajeerStagingSmokeTests.cs` marked `[Trait("Category","Smoke")]`. Reads `Tajeer:*` from user-secrets (UserSecretsId on the test csproj) or `TAJEER_*` env vars; gracefully early-returns when `Tajeer:AppId` is missing so CI stays green. Default `dotnet test` runs filter `Category!=Smoke` via `.runsettings` at the repo root. |
| T3.7 — PII-masked smoke payload | ⏳ awaiting first real call | Template placed below — paste the masked branch list after running the smoke test locally with user-secrets configured. |

### How to run the smoke test

```pwsh
# one-time: drop creds into user-secrets for the test project
cd packages\adapters\AutoLeaseNet.Adapters.Tajeer.Tests
dotnet user-secrets set "Tajeer:AppId" "<app-id>"
dotnet user-secrets set "Tajeer:AppKey" "<app-key>"
dotnet user-secrets set "Tajeer:AuthorizationToken" "Basic <base64>"
dotnet user-secrets set "Tajeer:BranchId" "1"
dotnet user-secrets set "Tajeer:WebhookSharedSecret" "<webhook-secret>"

# run only the smoke tests
cd ..\..\..
dotnet test packages\adapters\AutoLeaseNet.Adapters.Tajeer.Tests --filter Category=Smoke
```

### T3.7 placeholder — paste PII-masked response here after the first staging call

> Run the smoke test above, copy the test-output `Branch count` + `First branch:` lines, then mask any sensitive fields (`licenseNumber` → keep last 4 with `PiiMasking.Mask("licenseNumber", value)`). Replace this block with the masked excerpt and timestamp.

```text
[YYYY-MM-DDTHH:MM:SSZ] GET https://tajeer-stg.api.elm.sa/api/lookups/branches → 200 OK
Branch count: <N>
First branch: id=<int> code=<str> nameEn=<str> city=<str> active=<bool>
licenseNumber (masked): ****<last4>
```

### Verification

```
dotnet build AutoLeaseNet.sln                          → 0 warnings, 0 errors
dotnet test  AutoLeaseNet.sln --settings .runsettings  → 63 passed / 0 failed (smoke excluded)
  AutoLeaseNet.Adapters.Common.Tests    : 20 (unchanged from Day 1)
  AutoLeaseNet.Adapters.Tajeer.Tests    : 21 (was 1; +9 Options, +3 AuthHandler, +3 AddTajeer/LookupResolve, +5 LookupClient)
  AutoLeaseNet.Bff.Tests                : 18 (unchanged from Day 2)
  AutoLeaseNet.Infrastructure.Tests     : 4  (unchanged from Day 2)
```

### Next pickup

Day 4 — Tajeer `SaveContract` adapter (DTOs V9.7 → `ITajeerClient.SaveContractAsync` → `IntegrationResult<SaveContractResponse>`). Follow the same RED → GREEN cadence; wire under `AddTajeer` alongside `TajeerLookupClient`.

---

## Day 4 — Tajeer SaveContract adapter (2026-05-23)

### What landed

| Task | Status | Detail |
|---|---|---|
| T4.1 — `SaveContractRequest` DTO | ✅ | V9.7 shape from Spec 03 §6.2 — top-level + `RenterDto` + `PaymentDetailsDto` + `VehicleDetailsDto` + 6 optional nested DTOs in `OptionalRequestDtos.cs`. Tajeer's documented misspelling `addtionalServices` preserved (asserted on the wire by test). |
| T4.2 — `SaveContractResponse` + envelopes | ✅ | `SaveContractResponse` + `PaymentSummary` per Spec 03 §6.3. New `TajeerErrorEnvelope` carries `errorKey` / `errorCode` / `rawMessage` / `message` (Tajeer uses both) for defensive parsing. |
| T4.3 + T4.4 — RED → GREEN `TajeerContractClient.SaveAsync` | ✅ | 6 unit tests via `StubHttpMessageHandler` + `StubHttpClientFactory`: happy 200, JSON body shape (incl. typo preservation), 4xx vendor error, **200-with-errorKey vendor error**, 5xx transient, network exception transient. POST to `/api/contracts/save` (canonical path TBC at smoke). |
| T4.5 — Vendor error mapping | ✅ | `errorCode = tajeer.vendor.{errorKey}`, `isTransient = false`; defensive — applied even on 200 OK per Spec 03 §8.1 Q4. |
| T4.6 — Polly retry assertion | ✅ | `TajeerContractClientResilienceTests` (3 tests). Uses a parallel zero-delay retry pipeline that mirrors `ResiliencePolicies.DefaultHttpPipeline` predicate so the run is sub-millisecond. Asserts: retries 3 times on 503 then `IsTransient=true`, recovers within retry budget when upstream returns 200, does NOT retry on 4xx business errors. |
| T4.7 — InMemory sibling | ✅ | `InMemoryTajeerContractClient` in `Adapters.Tajeer.InMemory` — default factory returns deterministic Success (contract number `1_000_000_001+seq`, 15% VAT, in-memory issuance URL); injectable factory for failure simulation; captures every call. 4 unit tests. |
| T4.8 — Mode switch | ✅ | `TajeerOptions.Mode` + `TajeerMode { Real, InMemory }` enum. `AddInMemoryTajeerContracts()` uses `IServiceCollection.Replace` to override the real `ITajeerContractClient` registration. `AddTajeerWithModeSwitch(section)` is the one-line composition helper. **Defaults to `Real`** when `Mode` is missing — Production-safe. 3 registration tests. |

### Design decisions

- **Pattern B sub-client lives in `Adapters.Tajeer`**: `ITajeerContractClient` + `TajeerContractClient` are colocated in the vendor adapter package (not in `Application.Ports`). The existing `ITajeerClient` root facade with sub-interfaces is kept for the older legacy InMemory wiring — new sub-clients (Contracts, Lookups, Webhooks, Execution) are individually registered with their own port interfaces. This matches the pragmatic shape established by `TajeerLookupClient` on Day 3.
- **Defensive vendor-error parsing**: every response body is tried as `TajeerErrorEnvelope` first regardless of HTTP status. A 200 OK with `errorKey` is still a `Failure` (Spec 03 §8.1 Q4 — Tajeer occasionally does this). Bodies that aren't JSON fall through silently to the HTTP-status path.
- **Resilience test fidelity vs. speed**: production pipeline (in `ResiliencePolicies.DefaultHttpPipeline`) uses exponential backoff base 2s + jitter, giving ~7–21s of retries. The retry test wires a zero-delay parallel pipeline with the same `ShouldHandle` predicate so the assertion runs in <1ms. Trade-off documented inline in `TajeerContractClientResilienceTests.cs`.
- **Mode switch defaults to `Real`**: `ReadMode` falls back to `TajeerMode.Real` when `Tajeer:Mode` is missing or unparseable. Same fail-loud-on-Production posture as `DevJwtStubHandler` (Day 2): Production must opt-in to a fake, never accidentally fall into one.

### Drift fixed during Day 4

| Item | Severity | Resolution |
|---|---|---|
| `DefaultSaveResponse` referencing instance `_saveCalls` from `static` context | Low | Refactored to accept `sequenceNumber` parameter; eliminates static-instance crossover. |
| `Adapters.Tajeer.InMemory.csproj` missing `Configuration.Abstractions` + `Options` | Low | Added to the package's `<ItemGroup>` so `IConfigurationSection` and `IOptions<TajeerOptions>` resolve cleanly. |

### Verification

```
dotnet build AutoLeaseNet.sln                          → 0 warnings, 0 errors
dotnet test  AutoLeaseNet.sln --settings .runsettings  → 79 passed / 0 failed (smoke excluded)
  AutoLeaseNet.Adapters.Common.Tests     : 20 (unchanged)
  AutoLeaseNet.Adapters.Tajeer.Tests     : 37 (was 21; +6 ContractClient, +3 Resilience, +4 InMemoryContract, +3 ModeRegistration)
  AutoLeaseNet.Bff.Tests                 : 18 (unchanged)
  AutoLeaseNet.Infrastructure.Tests      : 4  (unchanged)
```

### Next pickup

Day 5 — BFF `POST /api/v1/dev/save-contract` endpoint. T5.1 begins with the `Application.SaveContractCommand` + handler that calls `ITajeerContractClient` and persists a minimal `Lease` row with `Status = PendingIssuance`. EF Core migration `Init_Lease` follows in T5.3.

---

## Day 5 — BFF dev/save-contract endpoint + first staging Save (2026-05-23)

### What landed

| Task | Status | Detail |
|---|---|---|
| T5.2 — `Domain.Leases.Lease` + `LeaseStatus` | ✅ | 9-state enum mirroring Tajeer codes (Spec 03 §7). Lease aggregate has `CreatePending` factory + idempotent `MarkIssued` transition. `LeaseConfiguration` maps to `Leases` table with `(TenantId, Status)` index + unique filtered index on `(TenantId, TajeerContractNumber)` so the same Tajeer number can't repeat within a tenant. Row-Level Security policy lands Week 2 Day 9. |
| T5.1 — `Application.SaveContractCommand` + handler | ✅ | MediatR `IRequest<SaveContractCommandResult>`. Handler glues idempotency replay → Tajeer call → Lease persist → result cache. `[LoggerMessage]` source generators (event IDs 5001-5003). New `ILeaseRepository` port + `EfLeaseRepository` impl. New `Application.Tests` project (5 tests using EF Core InMemory + `InMemoryTajeerContractClient` + `InMemoryIdempotencyStore`): happy persist, vendor error → no row, idempotency replay → single Tajeer call, cross-tenant key namespace isolation, empty tenant throws. |
| T5.3 — EF migration `Init_Lease` | ✅ | `Persistence/AutoLeaseNetDbContextFactory.cs` design-time factory reads `AUTOLEASENET_MIGRATIONS_CONNECTION` env var → falls back to local SQL (`Server=localhost;Database=AutoLeaseNet_Dev;Integrated Security=true`). Local tool manifest pins `dotnet-ef 8.0.5` (matches our `net8.0` TFM; global tool was 10.0). Migration `Persistence/Migrations/20260522232532_Init_Lease.cs` creates `Leases` + `__EFMigrationsHistory`. |
| T5.4 — Apply migration to local SQL | ✅ | `dotnet tool run dotnet-ef database update` → `Applying migration '20260522232532_Init_Lease'. Done.` Verified via `sqlcmd -E -d AutoLeaseNet_Dev -Q "SELECT MigrationId FROM __EFMigrationsHistory"`. |
| T5.5 — BFF `POST /api/v1/dev/save-contract` | ✅ | Wired in `services/bff/Endpoints/DevEndpoints.cs` under the existing dev group, `RequireAuthorization()`. Missing `Idempotency-Key` → `400 Problem`; vendor business error → `422`; transient infra failure → `503`; success → `202 Accepted` with `{ leaseId, tajeerContractNumber, issuanceUrl }`. New `SaveContractEndpointTests` (3 tests via `WebApplicationFactory<Program>` with EF InMemory + shared `InMemoryTajeerContractClient`). |
| T5.6 — Idempotency replay | ✅ | Same `Idempotency-Key` returns byte-identical JSON and `Tajeer.SaveCalls.Count == 1` (no second vendor call). Backed by the existing `InMemoryIdempotencyStore` registered via `AddInMemoryCache()` in `Program.cs`. Per Spec 03 §10, TTL = 24h, key namespaced as `tenant:{guid}:save-contract:{client-key}` so cross-tenant collisions are impossible. |
| T5.7 — Real staging Save | ⏳ awaiting manual run | The BFF needs real Tajeer credentials (Spec 03 §4.1) plus `Tajeer:Mode=Real` to talk to Rabet staging. Steps below; placeholder result block under T5.8. |
| T5.8 — Notes template | ✅ scaffold | Placeholder block below — fill in after the first successful staging Save. |

### How to do the first real staging Save (T5.7)

```pwsh
# 1. Put real Tajeer staging creds in BFF user-secrets (NOT in appsettings.Development.json — it's tracked).
cd services\bff
dotnet user-secrets set "Tajeer:AppId" "<staging-app-id>"
dotnet user-secrets set "Tajeer:AppKey" "<staging-app-key>"
dotnet user-secrets set "Tajeer:AuthorizationToken" "Basic <staging-base64>"
dotnet user-secrets set "Tajeer:BranchId" "<your branch>"
dotnet user-secrets set "Tajeer:Mode" "Real"

# 2. Start the BFF locally.
cd ..\..
dotnet run --project services\bff\AutoLeaseNet.Bff.csproj
# (BFF listens on http://localhost:5000 / https://localhost:5001 by default.)

# 3. POST a saved-known-good Tajeer V9.7 body (replace IDs with values from your staging tenant).
$body = @{
  customerId = $null
  request = @{
    renter = @{
      personAddress = "Riyadh, Olaya"
      mobile        = "05XXXXXXXX"
      idTypeCode    = 1
      idNumber      = 1234567890
    }
    paymentDetails = @{ paymentMethodCode = 1; rentAmount = 200; paidAmount = 50 }
    vehicleDetails = @{ vehicleId = <staging vehicle id> }
    workingBranchId   = <branch>
    rentPolicyId      = <staging rent policy>
    contractStartDate = "2026-05-23T10:00"
    contractEndDate   = "2026-05-25T10:00"
    receiveBranchId   = <branch>
    returnBranchId    = <branch>
    contractTypeCode  = 1
    operatorId        = <staging operator id>
  }
} | ConvertTo-Json -Depth 10

Invoke-RestMethod `
  -Uri  "https://localhost:5001/api/v1/dev/save-contract" `
  -Method POST `
  -Headers @{
    "X-Dev-Tenant-Id"  = "00000000-0000-0000-0000-000000000001"
    "X-Dev-User-Type"  = "InternalStaff"
    "Idempotency-Key"  = ([Guid]::NewGuid().ToString("N"))
    "Content-Type"     = "application/json"
  } `
  -Body $body `
  -SkipCertificateCheck
```

**Expected**: `202 Accepted` with `leaseId`, `tajeerContractNumber`, and `issuanceURL` from real Tajeer. Verify the local row landed:

```pwsh
sqlcmd -S localhost -E -d AutoLeaseNet_Dev -Q "SELECT TOP 5 Id, TenantId, TajeerContractNumber, Status, IssuanceUrl FROM Leases ORDER BY CreatedAtUtc DESC"
```

`Status = 2` (PendingIssuance) is the success signal — Tajeer's webhook later (Day 6) flips it to `Active`.

### T5.8 placeholder — paste PII-masked staging Save result here

> Replace this block with the masked response after the first staging Save. Use `PiiMasking.Mask("idNumber", value)` (keeps last 4) for any echoed renter identifiers. Strip the `Authorization`/`App-id`/`App-key` headers — never paste those.

```text
[YYYY-MM-DDTHH:MM:SSZ] POST /api/v1/dev/save-contract → 202 Accepted
Idempotency-Key (sent): <opaque>
Request (masked):
  renter.mobile        = ******1234
  renter.idNumber      = ******7890
  vehicleDetails.vehicleId = <int>
  contractStartDate    = YYYY-MM-DDTHH:mm
  contractEndDate      = YYYY-MM-DDTHH:mm
Response:
  leaseId              = <guid>
  tajeerContractNumber = <int>
  issuanceUrl          = https://tajeerstg.logisti.sa/#/public-contract/<n>/<masked-token>
Local row:
  Leases.Status = 2 (PendingIssuance)
```

### Drift fixed during Day 5

| Item | Severity | Resolution |
|---|---|---|
| Global `dotnet-ef` is 10.0 — targets net10 framework which we don't have | Med | Created `.config/dotnet-tools.json` and installed local `dotnet-ef 8.0.5` matching our `net8.0` TFM. Run migrations via `dotnet tool run dotnet-ef …`. |
| EF-generated migration code triggers CA1707 (underscore) + CA1861 (array literals) + CA1062 | Low | Project-level `NoWarn` in `AutoLeaseNet.Infrastructure.csproj` covering generated migration patterns. |
| `TajeerOptions.ValidateOnStart()` blows up existing BFF tests after wiring `AddTajeerWithModeSwitch` in `Program.cs` | Med | Added complete dummy Tajeer values to `appsettings.Development.json` (Mode=InMemory so they're never used over the wire). The Staging-env test injects the same dummies inline because non-Development envs skip the Development settings file. |
| CA1725 on MediatR `Handle` parameter names | Low | Renamed to `request` / `cancellationToken` and aliased locally for body clarity. |
| CA1512 on manual `throw new ArgumentOutOfRangeException` | Low | Switched to `ArgumentOutOfRangeException.ThrowIfNegativeOrZero`. |

### Verification

```
dotnet build AutoLeaseNet.sln                          → 0 warnings, 0 errors
dotnet test  AutoLeaseNet.sln --settings .runsettings  → 87 passed / 0 failed (smoke excluded)
  AutoLeaseNet.Adapters.Common.Tests     : 20 (unchanged)
  AutoLeaseNet.Adapters.Tajeer.Tests     : 37 (unchanged)
  AutoLeaseNet.Bff.Tests                 : 21 (was 18; +3 SaveContractEndpoint)
  AutoLeaseNet.Infrastructure.Tests      : 4  (unchanged)
  AutoLeaseNet.Application.Tests (new)   : 5  (SaveContractCommandHandler — happy, vendor error, replay, tenant namespace, empty tenant)
```

### Next pickup

Day 6 — Tajeer webhook receiver. T6.1 begins with the `WebhookLog` entity + migration. T6.2 ships the `/api/v1/webhooks/tajeer` endpoint. T6.3 adds HMAC signature verification. End-to-end happy path (T6.8) chains Day 5's `POST /dev/save-contract` → wait for the real webhook → assert `Lease.Status = Active`.
