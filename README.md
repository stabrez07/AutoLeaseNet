# AutoLeaseNet

> Vehicle leasing platform for the KSA market — Web Portal (sales & ops), Customer Portal (B2B fleet + B2C retail), and integrations with Tajeer, ZATCA, D365, telematics, and more.

## Status

**Phase 1 — Foundation (Weeks 1–4)**

Currently scaffolding monorepo + locking integration designs against Tajeer staging. See [`docs/`](./docs/README.md) for the full spec set.

## Quick start

### Prerequisites

- Node 20.11+ (`.nvmrc`); install via `nvm install` or `fnm install`
- pnpm 9+ — `corepack enable && corepack prepare pnpm@9 --activate`
- .NET 8 SDK (8.0.300 pinned in `global.json`)
- Docker Desktop (for local infra)
- Optional: PowerShell 7+ (Windows), bash/zsh (Linux/macOS), VS Code or JetBrains Rider

### First-time setup

```bash
# 1. Install JS deps + restore .NET packages
pnpm install
dotnet restore AutoLeaseNet.sln

# 2. Copy env template and adjust as needed
cp compose/.env.example .env.local

# 3. Start local infrastructure (SQL Edge, Redis, Azurite, MailHog)
pnpm infra:up

# 4. Apply DB migrations
pnpm db:migrate

# 5. Run BFF once to auto-seed demo data (Seed:Mode=Demo in appsettings.Development.json)
pnpm bff

# 6. Run everything in dev mode
pnpm dev
```

### Useful scripts

| Command | What it does |
|---|---|
| `pnpm dev` | Run all apps + BFF in watch mode |
| `pnpm build` | Build all (JS + .NET) for production |
| `pnpm test` | Run all unit tests |
| `pnpm lint` | Lint everything |
| `pnpm typecheck` | TypeScript checks |
| `pnpm format` | Prettier across the repo |
| `pnpm bff` / `pnpm bff:watch` | Run only the BFF (.NET API) |
| `pnpm infra:up` / `infra:down` / `infra:reset` | Docker Compose stack control |
| `pnpm db:migrate` | Apply EF Core migrations |
| `pnpm db:add-migration` | Create a new migration |

Mock data volume is configurable in `services/bff/appsettings.Development.json` under:
`Seed:CustomerCount`, `Seed:VehicleCount`, `Seed:DriverCount`, `Seed:LeaseCount` (recommended 100–1000).

## Repository layout

```
apps/                          # Next.js frontends
  web-portal/                  # Internal: sales + ops
  customer-portal/             # External: B2B fleet admins + B2C lessees

services/                      # Backend services
  bff/                         # .NET 8 Backend-for-Frontend

packages/
  application/                 # .NET — Domain / Application / Ports / Infrastructure
  adapters/                    # .NET — external integrations (Tajeer, ZATCA, SMS, etc.)
  ui/                          # Shared React component library
  contracts/                   # OpenAPI source of truth + generated TS types
  eslint-config-superplexity/  # Shared lint config
  tsconfig-superplexity/       # Shared tsconfig base

infra/
  bicep/                       # Azure IaC

compose/                       # Local Docker stack
tools/                         # Repo-wide scripts (seed data, stubs)
docs/                          # Architecture & design specs (read these!)
```

See [`docs/05-monorepo-layout-and-build-system.md`](./docs/05-monorepo-layout-and-build-system.md) for full details.

## Architecture & specs

Read in this order:

1. [Multi-tenancy & domain model](./docs/01-multi-tenancy-and-domain-model.md)
2. [State machines & sagas](./docs/02-state-machines-and-sagas.md)
3. [Tajeer adapter design](./docs/03-tajeer-adapter-design.md)
4. [Integration architecture (ports & adapters)](./docs/04-integration-architecture.md)
5. [Monorepo layout & build system](./docs/05-monorepo-layout-and-build-system.md)
6. [BFF API surface](./docs/06-bff-api-surface.md)
7. [ZATCA invoice generation](./docs/07-zatca-invoice-generation.md) (placeholder)
8. [Approval workflow engine](./docs/08-approval-workflow-engine.md) (placeholder)

Full index in [`docs/README.md`](./docs/README.md).

## Contributing

- Branches: `feature/<slug>`, `fix/<slug>`, `chore/<slug>`, `docs/<slug>`
- Commits: [Conventional Commits](https://www.conventionalcommits.org/) (`feat(...)`, `fix(...)`, etc.)
- PRs: squash-merge, must pass CI

## License

Proprietary. All rights reserved.
