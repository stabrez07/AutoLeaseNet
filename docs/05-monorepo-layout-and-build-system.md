# 05 — Monorepo Layout & Build System

**Status**: Draft v0.1 — locks before any scaffolding
**Phase**: Foundation
**Owner**: Architecture / DevEx
**Depends on**: [04-integration-architecture.md](./04-integration-architecture.md)
**Last updated**: 2026-05-17

---

## 1. Purpose

Lock the **physical repository structure**, **package boundaries**, **build orchestration**, and **local-dev workflow** so that:

1. A solo dev with Claude Code can navigate, build, and run the whole platform without confusion.
2. The hexagonal pattern from [doc 04](./04-integration-architecture.md) is enforced by directory structure, not just convention.
3. CI builds the right things in the right order with caching.
4. Local development is a single command: clone → install → run.
5. Adding a new adapter or app follows a copy-and-modify recipe.

---

## 2. Top-level decisions (locked)

| Concern | Decision | Rationale |
|---|---|---|
| **JS package manager** | **pnpm** with workspaces | Fast, disk-efficient (content-addressable), strict by default (no phantom deps). Best monorepo support. |
| **JS task orchestrator** | **Turborepo** | Lighter than Nx for a small team; great caching; minimal config. |
| **.NET version** | **.NET 8 (LTS)** pinned via `global.json` | LTS through Nov 2026; aligns with Azure App Service supported runtimes. |
| **.NET solution structure** | **Single root `AutoLeaseNet.sln`** referencing all `.csproj` | Solo dev — keep it simple. Splits per-service only if/when team scales. |
| **NuGet package management** | **Central Package Management** (`Directory.Packages.props`) | One version per dependency across the whole monorepo. No "v8 here, v9 there" drift. |
| **MSBuild defaults** | **`Directory.Build.props`** at root | Shared nullable/lang/analyzers across all .csproj. |
| **Frontend framework** | **Next.js 14+ (App Router) + React 18** | App Router is the future; SSR/RSC suit forms-heavy enterprise UI; great DX. |
| **UI library** | **shadcn/ui + Tailwind CSS + Radix primitives** | shadcn is copy-paste components into our codebase (we own them); Radix gives accessible primitives; Tailwind for utility-first styling with RTL support. |
| **i18n** | **next-intl** | Better App Router integration than react-i18next; built-in ICU MessageFormat for AR plurals/genders. |
| **TypeScript** | **Strict mode**, shared base `tsconfig` in `packages/tsconfig-superplexity/` | No partial-typing escape hatches. |
| **Local DB** | **Azure SQL Edge** in Docker | Closest to prod Azure SQL (same T-SQL, same RLS support). |
| **Local cache** | **Redis 7** Docker official image | |
| **Local blob** | **Azurite** (Azure Storage emulator) | Real Blob API |
| **Local email** | **MailHog** | Captures outgoing mail to web UI |
| **Local SMS** | **InMemory adapter** (no Docker) | Captures in `InMemorySmsSender.Sent` for tests; dev UI shows last 50 |
| **Pre-commit hooks** | **Husky + lint-staged** | Block bad commits early |
| **Commit style** | **Conventional Commits** | Enables changelog generation later if needed |
| **CI** | **GitHub Actions** | Matches typical KSA enterprise + Microsoft tooling |
| **IaC** | **Bicep** (in `infra/bicep/`) | First-class Azure tooling; cleaner than Terraform for Azure-only |
| **Secrets** | **Azure Key Vault** with managed identity | No `.env` files committed; local dev uses `.env.local` (gitignored) |
| **API contracts** | **OpenAPI 3.1** authored manually in `packages/contracts/openapi.yaml`, served by BFF for parity check | Single source of truth; FE generates TS types from it; BE asserts spec matches code |

---

## 3. Repository structure

```
AutoLeaseNet/                                  # repo root
├── README.md                                  # quickstart, contributing
├── LICENSE
├── .gitignore
├── .gitattributes                             # line endings, language stats
├── .editorconfig                              # IDE-agnostic format settings
├── .nvmrc                                     # Node version (e.g. 20.11.0)
├── .npmrc                                     # pnpm strict + hoisting config
├── .prettierrc.mjs                            # JS/TS/MD/YAML formatting
├── .prettierignore
├── pnpm-workspace.yaml                        # JS workspaces
├── pnpm-lock.yaml
├── package.json                               # root scripts + devDeps only
├── turbo.json                                 # pipeline + caching config
├── global.json                                # .NET SDK version pin
├── AutoLeaseNet.sln                           # .NET solution
├── Directory.Build.props                      # MSBuild defaults for all .csproj
├── Directory.Build.targets                    # shared MSBuild targets
├── Directory.Packages.props                   # central NuGet versions
├── nuget.config                               # internal feeds if any
│
├── .github/
│   └── workflows/
│       ├── ci.yml                             # PR check: lint + test + build
│       ├── deploy-dev.yml                     # auto-deploy main → dev
│       ├── deploy-staging.yml                 # manual deploy → staging
│       └── deploy-prod.yml                    # manual deploy + approval gate
│
├── .vscode/
│   ├── settings.json                          # editor settings (formatOnSave, etc.)
│   ├── extensions.json                        # recommended extensions
│   └── launch.json                            # debug profiles
│
├── docs/                                      # planning + ADRs + API docs
│   ├── 01-multi-tenancy-and-domain-model.md
│   ├── 02-state-machines-and-sagas.md
│   ├── 03-tajeer-adapter-design.md
│   ├── 04-integration-architecture.md
│   ├── 05-monorepo-layout-and-build-system.md  ← this file
│   ├── 06-bff-api-surface.md                  (next)
│   ├── 07-zatca-invoice-generation.md
│   ├── 08-approval-workflow-engine.md
│   └── adr/                                   # architecture decision records
│       ├── 0001-use-pnpm-and-turborepo.md
│       ├── 0002-azure-sql-with-rls.md
│       └── ...
│
├── apps/                                      # frontend apps (Next.js)
│   ├── web-portal/                            # sales + ops (internal)
│   │   ├── package.json
│   │   ├── next.config.mjs
│   │   ├── tsconfig.json
│   │   ├── tailwind.config.ts
│   │   ├── postcss.config.mjs
│   │   ├── messages/
│   │   │   ├── ar.json
│   │   │   └── en.json
│   │   ├── public/
│   │   ├── app/                               # App Router
│   │   │   ├── [locale]/
│   │   │   │   ├── (auth)/
│   │   │   │   ├── (dashboard)/
│   │   │   │   │   ├── customers/
│   │   │   │   │   ├── vehicles/
│   │   │   │   │   ├── drivers/
│   │   │   │   │   ├── quotations/
│   │   │   │   │   ├── leases/
│   │   │   │   │   ├── inspections/
│   │   │   │   │   ├── invoices/
│   │   │   │   │   └── settings/
│   │   │   │   └── layout.tsx
│   │   │   ├── api/                           # Next.js route handlers (auth only; data via BFF)
│   │   │   │   └── auth/
│   │   │   └── layout.tsx
│   │   ├── components/                        # app-specific components
│   │   ├── lib/                               # app-specific helpers
│   │   │   ├── auth/
│   │   │   ├── api-client.ts                  # generated from packages/contracts
│   │   │   └── i18n.ts
│   │   └── tests/
│   │       ├── unit/                          # Vitest
│   │       └── e2e/                           # Playwright
│   │
│   └── customer-portal/                       # B2B fleet admins + B2C lessees
│       └── (same structure)
│
├── services/                                  # backend services (.NET)
│   └── bff/
│       ├── AutoLeaseNet.Bff.csproj
│       ├── Program.cs                         # composition root
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Endpoints/                         # Minimal API endpoint groups
│       │   ├── CustomersEndpoints.cs
│       │   ├── VehiclesEndpoints.cs
│       │   ├── LeasesEndpoints.cs
│       │   ├── WebhooksEndpoints.cs           # /webhooks/tajeer, /webhooks/zatca
│       │   └── HealthEndpoints.cs
│       ├── Middleware/
│       │   ├── TenancyMiddleware.cs           # sets SQL SESSION_CONTEXT
│       │   ├── CorrelationIdMiddleware.cs
│       │   └── ErrorHandlingMiddleware.cs
│       ├── Authorization/
│       │   ├── PolicyDefinitions.cs
│       │   └── PermissionRequirement.cs
│       ├── Background/                        # BackgroundService workers
│       │   ├── OutboxDrainerService.cs
│       │   ├── TajeerWebhookProcessorService.cs
│       │   ├── ZatcaSubmissionRetryService.cs
│       │   ├── LeaseExpiryWatcherService.cs
│       │   └── TajeerReconciliationService.cs
│       └── Properties/
│
├── packages/
│   ├── application/                           # .NET — domain + application + ports
│   │   ├── AutoLeaseNet.Domain/
│   │   │   ├── AutoLeaseNet.Domain.csproj
│   │   │   ├── Customers/
│   │   │   ├── Vehicles/
│   │   │   ├── Leases/
│   │   │   ├── Quotations/
│   │   │   ├── Inspections/
│   │   │   ├── Invoices/
│   │   │   └── Shared/                        # ValueObjects, DomainEvents base
│   │   ├── AutoLeaseNet.Application/
│   │   │   ├── AutoLeaseNet.Application.csproj
│   │   │   ├── UseCases/                      # MediatR handlers per use case
│   │   │   ├── Sagas/                         # named saga classes
│   │   │   └── EventHandlers/
│   │   ├── AutoLeaseNet.Application.Ports/    # interfaces only — Pattern A ports
│   │   │   ├── AutoLeaseNet.Application.Ports.csproj
│   │   │   ├── Messaging/
│   │   │   │   ├── ISmsSender.cs
│   │   │   │   ├── IEmailSender.cs
│   │   │   │   └── IMessagingChannel.cs
│   │   │   ├── Storage/
│   │   │   │   └── IObjectStorage.cs
│   │   │   ├── Cache/
│   │   │   │   └── ICacheStore.cs
│   │   │   ├── Idempotency/
│   │   │   │   └── IIdempotencyStore.cs
│   │   │   ├── Pdf/
│   │   │   │   └── IPdfRenderer.cs
│   │   │   └── Persistence/
│   │   │       ├── IUnitOfWork.cs
│   │   │       └── repositories/
│   │   └── AutoLeaseNet.Infrastructure/       # EF Core, repository impls, DbContext
│   │       ├── AutoLeaseNet.Infrastructure.csproj
│   │       ├── Persistence/
│   │       │   ├── AutoLeaseNetDbContext.cs
│   │       │   ├── Configurations/
│   │       │   ├── Migrations/
│   │       │   └── Repositories/
│   │       └── ServiceCollectionExtensions.cs
│   │
│   ├── adapters/                              # .NET — all integration adapters
│   │   ├── AutoLeaseNet.Adapters.Common/
│   │   │   ├── Resilience/
│   │   │   ├── Idempotency/
│   │   │   ├── Credentials/
│   │   │   ├── Observability/
│   │   │   ├── Outbox/
│   │   │   └── Result/
│   │   │
│   │   ├── AutoLeaseNet.Adapters.Tajeer/                    # Phase 1
│   │   ├── AutoLeaseNet.Adapters.Tajeer.InMemory/
│   │   ├── AutoLeaseNet.Adapters.Zatca/                     # Phase 1
│   │   ├── AutoLeaseNet.Adapters.Zatca.InMemory/
│   │   ├── AutoLeaseNet.Adapters.Sms.Unifonic/              # Phase 1
│   │   ├── AutoLeaseNet.Adapters.Sms.InMemory/
│   │   ├── AutoLeaseNet.Adapters.Storage.AzureBlob/         # Phase 1
│   │   ├── AutoLeaseNet.Adapters.Storage.InMemory/
│   │   ├── AutoLeaseNet.Adapters.Cache.Redis/               # Phase 1
│   │   ├── AutoLeaseNet.Adapters.Cache.InMemory/
│   │   ├── AutoLeaseNet.Adapters.Email.AzureCommunication/  # Phase 1
│   │   ├── AutoLeaseNet.Adapters.Email.InMemory/
│   │   ├── AutoLeaseNet.Adapters.Pdf.QuestPdf/              # Phase 1
│   │   │
│   │   ├── AutoLeaseNet.Adapters.D365.Fo/                   # Phase 2
│   │   ├── AutoLeaseNet.Adapters.D365.Crm/                  # Phase 2
│   │   ├── AutoLeaseNet.Adapters.CarServicing/              # Phase 2
│   │   ├── AutoLeaseNet.Adapters.Payments.HyperPay/         # Phase 2
│   │   ├── AutoLeaseNet.Adapters.Messaging.WhatsApp/        # Phase 2
│   │   │
│   │   ├── AutoLeaseNet.Adapters.Telematics.Mix/            # Phase 3
│   │   ├── AutoLeaseNet.Adapters.Wasl/                      # Phase 3
│   │   ├── AutoLeaseNet.Adapters.Nafath/                    # Phase 3
│   │   ├── AutoLeaseNet.Adapters.Moi/                       # Phase 3
│   │   └── AutoLeaseNet.Adapters.Ai.AzureOpenAi/            # Phase 3
│   │
│   ├── ui/                                    # React component library
│   │   ├── package.json                       # "@superplexity/ui"
│   │   ├── tsconfig.json
│   │   ├── src/
│   │   │   ├── components/
│   │   │   │   ├── sketch-canvas/             # the Tajeer damage marker component
│   │   │   │   ├── plate-input/               # KSA plate entry with char picker
│   │   │   │   ├── hijri-date-picker/
│   │   │   │   ├── rtl-aware-icons/
│   │   │   │   └── ...
│   │   │   ├── hooks/
│   │   │   ├── lib/
│   │   │   └── index.ts
│   │   └── tests/
│   │
│   ├── contracts/                             # OpenAPI + generated types
│   │   ├── package.json                       # "@superplexity/contracts"
│   │   ├── openapi.yaml                       # source of truth — manually authored
│   │   ├── scripts/
│   │   │   └── generate.mjs                   # runs openapi-typescript + openapi-fetch
│   │   ├── generated/
│   │   │   ├── schema.d.ts
│   │   │   └── client.ts
│   │   └── tests/
│   │
│   ├── eslint-config-superplexity/            # shared lint config
│   │   ├── package.json                       # "@superplexity/eslint-config"
│   │   ├── index.mjs
│   │   ├── next.mjs                           # Next.js extension
│   │   └── react.mjs
│   │
│   └── tsconfig-superplexity/                 # shared tsconfig base
│       ├── package.json                       # "@superplexity/tsconfig"
│       ├── base.json
│       ├── nextjs.json
│       └── react-library.json
│
├── infra/                                     # Infrastructure as Code
│   ├── bicep/
│   │   ├── main.bicep                         # entry point per env
│   │   ├── modules/
│   │   │   ├── apim.bicep
│   │   │   ├── app-service.bicep
│   │   │   ├── sql.bicep
│   │   │   ├── keyvault.bicep
│   │   │   ├── redis.bicep
│   │   │   ├── storage.bicep
│   │   │   ├── front-door.bicep
│   │   │   ├── app-insights.bicep
│   │   │   └── communication-service.bicep
│   │   └── parameters/
│   │       ├── dev.bicepparam
│   │       ├── staging.bicepparam
│   │       └── prod.bicepparam
│   ├── scripts/
│   │   ├── bootstrap-keyvault-secrets.sh      # one-off seed of dev secrets
│   │   └── grant-managed-identity-roles.sh
│   └── docs/
│       └── runbook-deploy.md
│
├── tools/                                     # dev-only utilities
│   ├── seed-data/                             # scripts to seed dev DB
│   │   ├── seed-tenants.ts
│   │   ├── seed-tajeer-lookups.ts
│   │   └── seed-demo-data.ts
│   ├── tajeer-stub/                           # local Tajeer mock for offline dev
│   │   ├── package.json
│   │   ├── server.ts                          # Express/Fastify mock
│   │   └── fixtures/
│   └── db-tools/
│       ├── reset-dev-db.ps1
│       └── apply-rls-policies.sql
│
└── compose/                                   # docker-compose for local dev
    ├── docker-compose.yml                     # SQL Edge + Redis + Azurite + MailHog
    ├── docker-compose.tajeer-stub.yml
    └── .env.example
```

---

## 4. Workspace files (key configs)

### 4.1 `pnpm-workspace.yaml`

```yaml
packages:
  - "apps/*"
  - "packages/ui"
  - "packages/contracts"
  - "packages/eslint-config-superplexity"
  - "packages/tsconfig-superplexity"
  - "tools/*"
```

> .NET projects are excluded — they're a separate workspace managed by `AutoLeaseNet.sln`.

### 4.2 Root `package.json`

```json
{
  "name": "superplexity",
  "private": true,
  "version": "0.1.0",
  "packageManager": "pnpm@9.0.0",
  "scripts": {
    "dev": "turbo dev",
    "build": "turbo build",
    "test": "turbo test",
    "lint": "turbo lint",
    "typecheck": "turbo typecheck",
    "format": "prettier --write .",
    "format:check": "prettier --check .",
    "infra:up": "docker compose -f compose/docker-compose.yml up -d",
    "infra:down": "docker compose -f compose/docker-compose.yml down",
    "infra:reset": "docker compose -f compose/docker-compose.yml down -v && pnpm infra:up",
    "tajeer-stub": "docker compose -f compose/docker-compose.tajeer-stub.yml up",
    "bff": "dotnet run --project services/bff/AutoLeaseNet.Bff.csproj",
    "bff:watch": "dotnet watch --project services/bff/AutoLeaseNet.Bff.csproj",
    "db:migrate": "dotnet ef database update --project packages/application/AutoLeaseNet.Infrastructure --startup-project services/bff",
    "db:add-migration": "dotnet ef migrations add --project packages/application/AutoLeaseNet.Infrastructure --startup-project services/bff",
    "openapi:gen": "pnpm --filter @superplexity/contracts generate",
    "test:dotnet": "dotnet test AutoLeaseNet.sln",
    "build:dotnet": "dotnet build AutoLeaseNet.sln -c Release",
    "prepare": "husky"
  },
  "devDependencies": {
    "@superplexity/eslint-config": "workspace:*",
    "@superplexity/tsconfig": "workspace:*",
    "husky": "^9.0.0",
    "lint-staged": "^15.0.0",
    "prettier": "^3.2.0",
    "turbo": "^2.0.0",
    "typescript": "^5.4.0"
  },
  "lint-staged": {
    "*.{js,jsx,ts,tsx,mjs,cjs}": ["prettier --write", "eslint --fix"],
    "*.{json,md,yaml,yml,css}": ["prettier --write"],
    "*.cs": ["dotnet format --include"]
  }
}
```

### 4.3 `turbo.json`

```json
{
  "$schema": "https://turbo.build/schema.json",
  "globalDependencies": ["**/.env.*", "tsconfig.json"],
  "tasks": {
    "build": {
      "dependsOn": ["^build"],
      "outputs": [".next/**", "!.next/cache/**", "dist/**", "generated/**"],
      "env": ["NODE_ENV"]
    },
    "test": {
      "dependsOn": ["^build"],
      "outputs": ["coverage/**"]
    },
    "test:e2e": {
      "dependsOn": ["^build"],
      "cache": false
    },
    "lint": {
      "outputs": []
    },
    "typecheck": {
      "dependsOn": ["^build"],
      "outputs": []
    },
    "dev": {
      "cache": false,
      "persistent": true
    },
    "generate": {
      "outputs": ["generated/**"],
      "inputs": ["openapi.yaml", "scripts/**"]
    }
  }
}
```

### 4.4 `global.json` (pin .NET SDK)

```json
{
  "sdk": {
    "version": "8.0.300",
    "rollForward": "latestFeature"
  }
}
```

### 4.5 `Directory.Build.props` (root)

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors>CS1591</WarningsNotAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    <AnalysisLevel>latest-Recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <NeutralLanguage>en</NeutralLanguage>
    <InvariantGlobalization>false</InvariantGlobalization>  <!-- Need Arabic culture support -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### 4.6 `Directory.Packages.props` (central NuGet versions — partial)

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Core -->
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Http" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.0.0" />

    <!-- Web -->
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.5" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="8.0.5" />
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="6.6.2" />

    <!-- Data -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.5" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.5" />

    <!-- Resilience -->
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="8.5.0" />
    <PackageVersion Include="Polly" Version="8.4.0" />

    <!-- Telemetry -->
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.8.1" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.8.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.8.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.8.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.SqlClient" Version="1.8.0-beta.1" />

    <!-- Azure -->
    <PackageVersion Include="Azure.Identity" Version="1.11.3" />
    <PackageVersion Include="Azure.Security.KeyVault.Secrets" Version="4.6.0" />
    <PackageVersion Include="Azure.Storage.Blobs" Version="12.20.0" />
    <PackageVersion Include="Azure.Messaging.ServiceBus" Version="7.18.1" />
    <PackageVersion Include="Azure.Communication.Email" Version="1.0.1" />

    <!-- Cache -->
    <PackageVersion Include="StackExchange.Redis" Version="2.7.33" />

    <!-- Application patterns -->
    <PackageVersion Include="MediatR" Version="12.3.0" />
    <PackageVersion Include="FluentValidation" Version="11.9.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.1" />

    <!-- PDF -->
    <PackageVersion Include="QuestPDF" Version="2024.6.0" />

    <!-- ZATCA -->
    <!-- TODO: pick library: Zatca.EInvoice.SDK community or build minimal in-house -->

    <!-- Testing -->
    <PackageVersion Include="xunit" Version="2.8.0" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageVersion Include="FluentAssertions" Version="6.12.0" />
    <PackageVersion Include="Verify.Xunit" Version="25.0.0" />
    <PackageVersion Include="Testcontainers.MsSql" Version="3.8.0" />
    <PackageVersion Include="Bogus" Version="35.5.1" />
    <PackageVersion Include="NSubstitute" Version="5.1.0" />
  </ItemGroup>
</Project>
```

---

## 5. Project naming & references

### 5.1 .NET project naming

| Project | Folder | Assembly name |
|---|---|---|
| Domain | `packages/application/AutoLeaseNet.Domain/` | `AutoLeaseNet.Domain` |
| Application | `packages/application/AutoLeaseNet.Application/` | `AutoLeaseNet.Application` |
| Application Ports | `packages/application/AutoLeaseNet.Application.Ports/` | `AutoLeaseNet.Application.Ports` |
| Infrastructure (EF Core) | `packages/application/AutoLeaseNet.Infrastructure/` | `AutoLeaseNet.Infrastructure` |
| Adapter | `packages/adapters/AutoLeaseNet.Adapters.{Name}/` | `AutoLeaseNet.Adapters.{Name}` |
| Adapter (InMemory) | `packages/adapters/AutoLeaseNet.Adapters.{Name}.InMemory/` | `AutoLeaseNet.Adapters.{Name}.InMemory` |
| Common adapter infra | `packages/adapters/AutoLeaseNet.Adapters.Common/` | `AutoLeaseNet.Adapters.Common` |
| BFF | `services/bff/AutoLeaseNet.Bff.csproj` | `AutoLeaseNet.Bff` |
| Tests | sibling `*.Tests.csproj` per project | matching `AutoLeaseNet.X.Tests` |

### 5.2 Reference rules (enforced via analyzer or convention)

```
Domain                  → (no dependencies — pure POCO)
Application             → Domain
Application.Ports       → Domain
Infrastructure          → Application, Domain, Application.Ports (uses ports)
Adapters.Common         → (no dependencies on app code; just Microsoft.Extensions.*, Polly, Redis)
Adapters.{Name}         → Adapters.Common, Application.Ports (Pattern A only — to implement port)
                          OR
                          Adapters.Common only (Pattern B — defines own interface)
Adapters.{Name}.InMemory→ same as Adapters.{Name}
Bff                     → Application, Application.Ports, Infrastructure, Adapters.* (composition root only)
```

**Disallowed**:
- Domain → anything else
- Application → Infrastructure (or any Adapters.*)
- Adapters.{Name} → Domain or Application or Infrastructure
- Cross-adapter references (Adapters.Tajeer → Adapters.Zatca)

These rules can be enforced via [NetArchTest](https://github.com/BenMorris/NetArchTest) in a `AutoLeaseNet.ArchTests` project.

### 5.3 JS package naming

| Package | Folder | npm name |
|---|---|---|
| Web Portal | `apps/web-portal/` | `@superplexity/web-portal` |
| Customer Portal | `apps/customer-portal/` | `@superplexity/customer-portal` |
| UI library | `packages/ui/` | `@superplexity/ui` |
| Contracts | `packages/contracts/` | `@superplexity/contracts` |
| ESLint config | `packages/eslint-config-superplexity/` | `@superplexity/eslint-config` |
| TSConfig | `packages/tsconfig-superplexity/` | `@superplexity/tsconfig` |
| Tajeer stub | `tools/tajeer-stub/` | `@superplexity/tajeer-stub` |

---

## 6. Local development workflow

### 6.1 First-time setup

```bash
# 1. Install required tooling (one-time)
#    - Node 20.x (from .nvmrc)
#    - pnpm 9 (corepack enable && corepack prepare pnpm@9 --activate)
#    - .NET 8 SDK
#    - Docker Desktop

# 2. Clone and install
git clone <repo>
cd AutoLeaseNet
pnpm install
dotnet restore

# 3. Copy env template
cp compose/.env.example .env.local

# 4. Start local infrastructure
pnpm infra:up   # SQL Edge, Redis, Azurite, MailHog

# 5. Run DB migrations + seed
pnpm db:migrate
pnpm tsx tools/seed-data/seed-tajeer-lookups.ts
pnpm tsx tools/seed-data/seed-demo-data.ts

# 6. Start everything
pnpm dev
```

### 6.2 `pnpm dev` behavior (via Turborepo)

Starts in parallel:
- `apps/web-portal` — Next.js dev server on :3000
- `apps/customer-portal` — Next.js dev server on :3001
- `services/bff` — .NET via `dotnet watch` on :5000 (HTTPS :5001)
- (Optional) `tools/tajeer-stub` — Fastify mock on :8080 if you want to develop offline

### 6.3 Selective dev

```bash
turbo dev --filter=@superplexity/web-portal     # only web portal + its deps
turbo dev --filter=@superplexity/customer-portal
pnpm bff:watch                                  # just the BFF
```

### 6.4 Docker Compose (`compose/docker-compose.yml`)

```yaml
services:
  sql:
    image: mcr.microsoft.com/azure-sql-edge:latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=${SQL_SA_PASSWORD:-LocalDev_P@ssw0rd_2026}
    ports:
      - "1433:1433"
    volumes:
      - sql-data:/var/opt/mssql

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

  azurite:
    image: mcr.microsoft.com/azure-storage/azurite:latest
    command: "azurite-blob --blobHost 0.0.0.0 --blobPort 10000"
    ports:
      - "10000:10000"
    volumes:
      - azurite-data:/data

  mailhog:
    image: mailhog/mailhog:latest
    ports:
      - "1025:1025"   # SMTP
      - "8025:8025"   # Web UI at http://localhost:8025

volumes:
  sql-data:
  redis-data:
  azurite-data:
```

### 6.5 `.env.local` (gitignored, generated from `compose/.env.example`)

```bash
# === Connection strings ===
DB_CONNECTION_STRING="Server=localhost,1433;Database=AutoLeaseNet;User Id=sa;Password=LocalDev_P@ssw0rd_2026;TrustServerCertificate=true"
REDIS_CONNECTION_STRING="localhost:6379"
BLOB_STORAGE_CONNECTION_STRING="UseDevelopmentStorage=true"

# === Tajeer (sandbox) ===
TAJEER__APPID="<your-staging-app-id>"
TAJEER__APPKEY="<your-staging-app-key>"
TAJEER__AUTHORIZATION="Basic <your-generated-token>"
TAJEER__BASEURL="https://tajeer-stg.api.elm.sa"
TAJEER__ISSUANCEURLBASE="https://tajeerstg.logisti.sa"
TAJEER__WEBHOOKSHAREDSECRET="local-dev-webhook-secret"

# === ZATCA (sandbox) ===
ZATCA__BASEURL="https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal"
ZATCA__CSID="<sandbox-csid>"
ZATCA__ENVIRONMENT="Sandbox"

# === SMS — use InMemory locally, Unifonic creds optional ===
SMS__PROVIDER="InMemory"
# SMS__UNIFONIC__APPSID="..."

# === Email — MailHog locally ===
EMAIL__PROVIDER="Smtp"
EMAIL__SMTP__HOST="localhost"
EMAIL__SMTP__PORT="1025"

# === Identity (Entra) ===
ENTRA__TENANTID="<dev-tenant>"
ENTRA__CLIENTID="<dev-client>"
# Tenancy bypass for local dev only:
LOCAL_DEV__DEFAULT_TENANT_ID="00000000-0000-0000-0000-000000000001"
```

> `AutoLeaseNet.Bff` reads these via ASP.NET Core's standard config layering. Section keys use `__` as the colon separator on Linux/macOS shells.

---

## 7. CI/CD

### 7.1 GitHub Actions — `ci.yml` (runs on PR)

```yaml
name: CI

on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

env:
  NODE_VERSION: "20.11.0"
  DOTNET_VERSION: "8.0.x"

jobs:
  js:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: ${{ env.NODE_VERSION }}
      - uses: pnpm/action-setup@v3
        with:
          version: 9
      - run: pnpm install --frozen-lockfile
      - run: pnpm lint
      - run: pnpm typecheck
      - run: pnpm test
      - run: pnpm build

  dotnet:
    runs-on: ubuntu-latest
    services:
      sql:
        image: mcr.microsoft.com/azure-sql-edge
        env:
          ACCEPT_EULA: Y
          MSSQL_SA_PASSWORD: TestPass_2026!
        ports: ["1433:1433"]
        options: >-
          --health-cmd "/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P TestPass_2026! -Q 'SELECT 1'"
          --health-interval 10s --health-timeout 5s --health-retries 5
      redis:
        image: redis:7-alpine
        ports: ["6379:6379"]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - run: dotnet restore AutoLeaseNet.sln
      - run: dotnet build AutoLeaseNet.sln -c Release --no-restore
      - run: dotnet test AutoLeaseNet.sln -c Release --no-build --filter "Category!=Integration"
      - name: Integration tests (Tajeer sandbox)
        if: ${{ secrets.TAJEER_APPKEY != '' }}
        run: dotnet test AutoLeaseNet.sln -c Release --no-build --filter "Trait=Integration"
        env:
          TAJEER__APPID: ${{ secrets.TAJEER_APPID }}
          TAJEER__APPKEY: ${{ secrets.TAJEER_APPKEY }}
          TAJEER__AUTHORIZATION: ${{ secrets.TAJEER_AUTHORIZATION }}

  infra:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: azure/setup-bicep@v1
      - run: bicep build infra/bicep/main.bicep
      - run: bicep lint infra/bicep/main.bicep
```

### 7.2 Deploy workflows

- `deploy-dev.yml` — auto-triggers on push to `main`, deploys to dev environment
- `deploy-staging.yml` — manual trigger (workflow_dispatch), runs against staging slot
- `deploy-prod.yml` — manual + environment protection rule (requires approval from CODEOWNERS)

Pattern: build artifacts once, promote through environments. No rebuild per environment.

---

## 8. Conventions

### 8.1 Branch naming

- `main` — always deployable
- `feature/<short-slug>` for development
- `fix/<short-slug>` for bug fixes
- `chore/<short-slug>` for non-code (deps, config)
- `docs/<short-slug>` for planning docs only

### 8.2 Commit messages (Conventional Commits)

```
feat(tajeer): add Save Contract idempotency wrapper
fix(bff): handle null customerId in tenancy middleware
chore(deps): bump Polly from 8.3 to 8.4
docs(adr): record decision to use Azure SQL Edge for local dev
test(lease): add saga test for replacement scenario
```

Types: `feat`, `fix`, `chore`, `docs`, `test`, `refactor`, `perf`, `build`, `ci`.

### 8.3 PR rules

- Must pass `ci` workflow.
- Squash-merge into `main` (linear history).
- PR title becomes the commit subject — must follow Conventional Commits.

### 8.4 Code style

- **TypeScript**: Prettier (default) + ESLint (`@superplexity/eslint-config`); no semicolons, single quotes, trailing commas — set in `.prettierrc.mjs`.
- **C#**: `dotnet format` + EditorConfig; 4 spaces, file-scoped namespaces, expression-bodied where it improves readability, `var` for obvious types.
- **CSS/Tailwind**: utility-first; no custom CSS unless layout-specific. Tailwind logical properties only (`ms-*` not `ml-*`) for RTL safety.
- **Naming**: PascalCase for C# types & React components; camelCase for variables & functions; kebab-case for filenames in JS, PascalCase in C#.

---

## 9. Building a new adapter — quick recipe (10 minutes)

```bash
# 1. Create the project structure (or copy from another adapter)
cd packages/adapters
mkdir AutoLeaseNet.Adapters.MyVendor
cd AutoLeaseNet.Adapters.MyVendor
dotnet new classlib -f net8.0
dotnet sln ../../../AutoLeaseNet.sln add .

# 2. Reference Adapters.Common
dotnet add reference ../AutoLeaseNet.Adapters.Common

# 3. Scaffold standard folders
mkdir Configuration Client Resilience ErrorHandling Observability Health Authentication

# 4. Create the InMemory companion
cd ..
mkdir AutoLeaseNet.Adapters.MyVendor.InMemory
cd AutoLeaseNet.Adapters.MyVendor.InMemory
dotnet new classlib -f net8.0
dotnet sln ../../../AutoLeaseNet.sln add .
dotnet add reference ../AutoLeaseNet.Adapters.MyVendor

# 5. Create the test project
mkdir ../AutoLeaseNet.Adapters.MyVendor.Tests
cd ../AutoLeaseNet.Adapters.MyVendor.Tests
dotnet new xunit -f net8.0
dotnet sln ../../../AutoLeaseNet.sln add .
dotnet add reference ../AutoLeaseNet.Adapters.MyVendor
dotnet add reference ../AutoLeaseNet.Adapters.MyVendor.InMemory

# 6. Implement per doc 04 standard (Configuration → Client → Resilience → Error → DI extension)
# 7. Wire into BFF Program.cs: services.AddMyVendor(config);
# 8. Add to integration catalog in doc 04
```

A scaffolder script (`tools/scaffold-adapter.ps1`) can be added later to automate steps 1–5.

---

## 10. Versioning

- Single root `version: 0.1.0` in `package.json` (mirrored in `Directory.Build.props` `<Version>`).
- Bump on each prod release.
- We are **not** doing independent per-package versioning. All packages move together.
- If/when an adapter is extracted to a separate repo, then it gets its own SemVer track.

---

## 11. Open questions

| # | Question | Default |
|---|---|---|
| Q1 | OpenAPI authoring — manual YAML or generate from BFF code via Swashbuckle? | Manual YAML for Phase 1 (single source of truth, FE/BE both consume); validate match in BFF tests |
| Q2 | UI component delivery — pre-built shadcn copies in `packages/ui` or per-app? | Pre-built in `packages/ui` (shared component library); apps consume |
| Q3 | E2E test framework — Playwright or Cypress? | Playwright (better TS support, faster, multi-browser) |
| Q4 | Use Architecture Tests (NetArchTest) to enforce dependency rules? | Yes — add `AutoLeaseNet.ArchTests` in Phase 1 Week 1 |
| Q5 | Hot-reload for Bff — `dotnet watch` or use Aspire? | `dotnet watch` Phase 1 (simpler); consider .NET Aspire when team grows |
| Q6 | Local secrets — `.env.local` or user-secrets (`dotnet user-secrets`)? | Both supported: `.env.local` for JS apps + Docker Compose; user-secrets for BFF; same key names via config layering |

---

## 12. Sign-off checklist

- [ ] Repository structure approved (top-level + apps/services/packages split)
- [ ] pnpm + Turborepo + .NET single-sln approach approved
- [ ] Naming conventions (assembly names, npm names, folder structure) approved
- [ ] Reference rules (dependency direction) approved + NetArchTest enforcement agreed
- [ ] Local-dev Docker Compose stack approved (SQL Edge + Redis + Azurite + MailHog)
- [ ] CI workflow shape approved
- [ ] Open questions §11 answered
