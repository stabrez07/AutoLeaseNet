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
