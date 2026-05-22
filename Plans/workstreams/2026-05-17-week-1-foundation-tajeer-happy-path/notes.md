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
