# Workstream: New-PC Developer Tooling Resync (2026-06-15)

**Goal**: Verify every required tool version, local-run command, and secret key a developer needs
on a fresh Windows PC to start contributing. Produce a concrete checklist; fix safe build
regressions; document every remaining manual action.

**Scope boundary**: read-only verification + doc update + safe code fixes (CA1873 analyzer
regressions triggered by newer SDK); no feature code, no migrations, no schema changes.

---

## Tasks

| # | Task | Status |
|---|------|--------|
| T1 | Audit tool versions required (global.json, package.json, .nvmrc) | ✅ done |
| T2 | Run `dotnet --version` / `dotnet --list-sdks` | ✅ done |
| T3 | Run `node --version`, `pnpm --version` | ✅ done (❌ not installed) |
| T4 | Run `docker --version` | ✅ done (❌ not installed) |
| T5 | Check `dotnet tool list -g` for `ef`, `husky` | ✅ done (❌ not installed) |
| T6 | Verify BFF user secrets exist | ✅ done (❌ not set up) |
| T7 | Run `dotnet restore AutoLeaseNet.sln` | ✅ done (✅ succeeded) |
| T8 | Run `dotnet build AutoLeaseNet.sln -c Release` | ✅ done (❌ see blockers) |
| T9 | Fix CA1873 build regression (SDK 10 stricter analyzer) | ✅ done |
| T10 | Document duplicate migration blocker | ✅ done |
| T11 | Produce `checklist.md` in this workstream | ✅ done |
| T12 | Update `ai_context.md` | ✅ done |

---

## Required tool versions

| Tool | Required | Source |
|------|----------|--------|
| .NET SDK | `8.0.206` min, rolls forward to latest major | `global.json` |
| Node.js | `>=20.11.0` (`.nvmrc` pins `20.11.1`) | `package.json` engines + `.nvmrc` |
| pnpm | `9.0.0` (exact `packageManager` field) | `package.json` |
| Docker + Compose plugin | any recent | `compose/docker-compose.yml` |
| `dotnet-ef` global tool | latest v8 | `package.json` `db:migrate` script |

---

## Verification results (2026-06-15, this PC)

| Check | Result | Notes |
|-------|--------|-------|
| .NET SDK installed | ✅ `10.0.301` | Satisfies `latestMajor` roll-forward |
| Node.js installed | ❌ not found | `node` not on PATH |
| pnpm installed | ❌ not found | `pnpm` not on PATH |
| Docker installed | ❌ not found | `docker` not on PATH |
| `dotnet-ef` global tool | ❌ not installed | No global tools found |
| BFF user secrets exist | ❌ not set up | Path: `%APPDATA%\Microsoft\UserSecrets\autoleasenet-bff-9d6e0c1f-3a4b-4d2c-9a0d-2f7e8b1c3a45\secrets.json` |
| `dotnet restore` | ✅ succeeded | All `project.assets.json` generated |
| `dotnet build -c Release` | ❌ fails | Two blockers — see below |

---

## Build blockers (must fix before `dotnet build` is green)

### Blocker 1 — CA1873 (FIXED in this workstream)

**Cause**: .NET SDK 10's Roslyn analyzer is stricter than SDK 8 about CA1873
("Expensive string argument evaluated before logger checks level").  
`IncidentCommandHandlers.cs` line 82 and `InspectionCommandHandlers.cs` line 116 both
passed `.ToString()` on enum values into `[LoggerMessage]` partial methods whose signatures
declared `string` parameters.

**Fix applied**: Changed `LogReported` and `LogStarted` partial method signatures to accept
the enum types directly (`IncidentType`, `IncidentSeverity`, `InspectionType`);
removed `.ToString()` from the three call sites. The source generator now handles lazy
stringification. Files changed:
- `packages/application/AutoLeaseNet.Application/Operations/IncidentCommandHandlers.cs`
- `packages/application/AutoLeaseNet.Application/Operations/InspectionCommandHandlers.cs`

### Blocker 2 — Duplicate EF migration class (MANUAL action required)

**Cause**: The local branch `feat/day23-approval-saga` has two git-tracked migration files
with the same C# class name `Add_Quotation_Aggregate`:
- `20260607172013_Add_Quotation_Aggregate.cs` — merged to `main` in PR #32
- `20260607174509_Add_Quotation_Aggregate.cs` — added later on the feature branch (schema diff:
  adds `EnsureSchema(name:"dbo")` call and widens `RequiredRoleCode` from `nvarchar(50)` to
  `nvarchar(100)`)

The compiler sees duplicate `Up`, `Down`, `BuildTargetModel` methods in the same namespace.
This is a **branch-level conflict** in `feat/day23-approval-saga`, not a new-PC issue.

**Required manual action**:
1. Decide which migration file wins (likely rename the newer one to a new migration name,
   e.g. `Add_ApprovalTier_SchemaFix`, or delete it and inline the column-width fix in the
   existing PR's migration before it is applied to any environment).
2. Remove the superseded file from the branch.
3. After resolution, `dotnet build AutoLeaseNet.sln -c Release` should succeed.

---

## BFF user-secrets keys

The BFF project (`UserSecretsId: autoleasenet-bff-9d6e0c1f-3a4b-4d2c-9a0d-2f7e8b1c3a45`)
reads all configuration from `IConfiguration` (env vars override appsettings).  
On a new dev PC there is **no** committed `appsettings.Development.json` with real values.
The developer must populate user secrets (or a `.env.local` file) for the BFF to start
against a real database and Tajeer/ZATCA sandbox.

### Minimum secrets for local BFF startup (InMemory adapters, no Tajeer calls)

```shell
dotnet user-secrets set "ConnectionStrings:AutoLeaseNet" \
  "Server=localhost,1433;Database=AutoLeaseNet;User Id=sa;Password=LocalDev_P@ssw0rd_2026;TrustServerCertificate=true;Encrypt=false" \
  --project services/bff

dotnet user-secrets set "Tajeer:AppId"             "local-stub"  --project services/bff
dotnet user-secrets set "Tajeer:AppKey"            "local-stub"  --project services/bff
dotnet user-secrets set "Tajeer:Authorization"     "Basic local-stub" --project services/bff
dotnet user-secrets set "Tajeer:BranchId"          "1"           --project services/bff
dotnet user-secrets set "Tajeer:WebhookSharedSecret" "local-dev-webhook-secret" --project services/bff
dotnet user-secrets set "Tajeer:Mode"              "InMemory"    --project services/bff
dotnet user-secrets set "Zatca:Mode"               "InMemory"    --project services/bff
dotnet user-secrets set "Seed:Mode"                "Demo"        --project services/bff
```

### Additional secrets for real-adapters mode (Tajeer Rabet staging)

See `compose/.env.example` for the full key list:
- `Tajeer:AppId` — from Tajeer portal → Users → API Registration
- `Tajeer:AppKey` — same
- `Tajeer:Authorization` — Basic token
- `Tajeer:BranchId` — your branch GUID
- `Zatca:SandboxCsid` — from ZATCA Fatoorah sandbox
- `ConnectionStrings:Redis` — `localhost:6379` (default)

---

## Local run commands (verified against `package.json`)

| Command | What it does | Prerequisites |
|---------|-------------|---------------|
| `pnpm infra:up` | Starts SQL Edge, Redis, Azurite, MailHog via Docker Compose | Docker installed + running |
| `pnpm bff` | `dotnet run` the BFF at `http://localhost:5000` | `dotnet restore` + user secrets populated |
| `pnpm bff:watch` | Hot-reload BFF | same |
| `pnpm build:dotnet` | Release build of full solution | `dotnet restore` |
| `pnpm test:dotnet` | Unit tests (excludes Integration + Smoke) | none |
| `pnpm db:migrate` | Apply EF migrations | Docker SQL running + connection string set |
| `pnpm dev` | Turbo dev (all Next.js portals) | Node + pnpm installed |
| `pnpm build` | Turbo build (JS) | Node + pnpm installed |

BFF swagger UI (when running): `http://localhost:5000/swagger`  
Dev whoami endpoint: `GET /api/v1/dev/whoami` with `X-Dev-UserId: <any-guid>` and `X-Dev-TenantId: 00000000-0000-0000-0000-000000000001`
