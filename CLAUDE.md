# CLAUDE.md — Working with this repo

This file is loaded automatically into Claude Code sessions for the AutoLeaseNet project. It captures **project-specific working rules** that override defaults.

---

## What this project is

AutoLeaseNet is a **KSA vehicle leasing platform** with two web portals and deep integrations with Tajeer, ZATCA, D365, telematics, and Nafath. Read [`Plans/01-comprehensive-vehicle-lease-customer-portal-plan.md`](./Plans/01-comprehensive-vehicle-lease-customer-portal-plan.md) for the full vision.

## Where to find things

| Need | Look in |
|---|---|
| Architecture, domain, state machines, adapter design | [`Specs/`](./Specs/) (`01`–`08` + ADRs) |
| Roadmaps, phasing, dependency checklist, risk register | [`Plans/`](./Plans/) |
| Per-workstream task-level plans (superpowers style) | [`Plans/workstreams/{slug}/plan.md`](./Plans/workstreams/) |
| Domain code | `packages/application/AutoLeaseNet.Domain/` |
| Application logic / use cases / sagas | `packages/application/AutoLeaseNet.Application/` |
| Port interfaces (Pattern A — swappable) | `packages/application/AutoLeaseNet.Application.Ports/` |
| EF Core / DbContext / migrations | `packages/application/AutoLeaseNet.Infrastructure/` |
| External integration adapters | `packages/adapters/AutoLeaseNet.Adapters.*/` |
| BFF (HTTP API for portals) | `services/bff/` |
| Next.js frontends | `apps/web-portal/`, `apps/customer-portal/` |
| Local dev infra (SQL Edge, Redis, Azurite, MailHog) | `compose/docker-compose.yml` |

## Working rules (must follow)

### 1. Hexagonal — adapter pattern is non-negotiable

- **Never** call an external service from BFF/domain code directly. Always via a port (Pattern A) or typed adapter client (Pattern B).
- Every new external integration goes in its own `Adapters.*` package per [Spec 04 §11 recipe](./Specs/04-integration-architecture.md#11-recipe-adding-a-new-integration).
- Every adapter ships with a sibling `InMemory` companion for tests/dev.

### 2. TDD discipline (RED → GREEN → REFACTOR)

- Write a failing test FIRST. No exceptions for "obvious" code.
- One test = one behavior. Tests must be independent and deterministic.
- Use `IClock` for time; never `DateTime.UtcNow` in domain code.
- Run `dotnet test` and `pnpm test` before marking any task done.

### 3. Plans of tasks (superpowers methodology)

- Any work taking >1 day starts with a `Plans/workstreams/{date-slug}/plan.md`.
- Tasks are 2–5 minutes each. Verifiable per task.
- Check off as you go. Update if scope shifts mid-stream.
- Write a `retrospective.md` when the workstream closes.

### 4. Tenancy is sacred

- Every domain table has `TenantId` and (where applicable) `CustomerId`.
- Every BFF endpoint runs through `TenancyMiddleware` setting SQL `SESSION_CONTEXT`.
- RLS policies enforce isolation at the DB layer. **Defense in depth.**
- See [Spec 01 §3](./Specs/01-multi-tenancy-and-domain-model.md#3-multi-tenancy-model).

### 5. Tajeer is system of record for contracts

- We **mirror** Tajeer's `Lease.Status`; we never invent.
- On reconciliation conflicts, **Tajeer wins**.
- The Lease Issuance Saga ([Spec 02 §6.2](./Specs/02-state-machines-and-sagas.md#62-lease-issuance-saga-the-critical-one)) is the canonical orchestration — don't bypass it.

### 6. ZATCA chain integrity

- Per-tenant `ZatcaChainState` updated atomically only on CLEARED.
- A failed submission does NOT advance the chain.
- Chain break detection halts new submissions + raises alert. **Don't override this.**

### 7. PII handling

- `Person.IdNumber`, `Driver.DriverLicenseNumber`, IBAN → SQL Server Always Encrypted columns.
- Logs masked via `Adapters.Common.Observability.PiiMasking`.
- Any access to sensitive entity writes an append-only audit row.

### 8. Idempotency on every state-changing API

- BFF endpoints require `Idempotency-Key` header on POST/PUT.
- Adapters wrap state-changing Tajeer/ZATCA calls in idempotency decorators.
- Cached 24h in Redis (`Adapters.Cache.Redis`).

### 9. Verification before marking done

A task is done only when ALL of:
- [ ] Code merged after PR review
- [ ] New tests pass; existing tests still pass
- [ ] `dotnet build` and `pnpm build` succeed (treat-warnings-as-errors)
- [ ] Manual smoke test on staging passes (for any user-facing change)
- [ ] OpenAPI spec updated if BFF endpoints changed
- [ ] Relevant Spec doc updated if design evolved

### 10. Anti-patterns — flag immediately

If you find yourself doing any of these, **stop and reconsider**:

- ❌ `HttpClient` to vendor directly from BFF endpoint
- ❌ Application/domain code referencing `Adapters.*` package
- ❌ Hardcoded credentials in code
- ❌ Throwing exceptions for business errors (use `IntegrationResult<T>` / `Result<T>`)
- ❌ Inline `Task.Delay` retry loops (use Polly pipeline)
- ❌ `if (env == "Production")` branches (use config switches)
- ❌ Skipping the InMemory companion adapter
- ❌ Hardcoded approval thresholds (use `ApprovalTier` config)
- ❌ Updating `Lease.Status` directly from random code (use saga)

## Tech stack quick reference

- **Frontend**: Next.js 14 (App Router) + TypeScript + Tailwind + shadcn/ui + next-intl (AR/EN RTL)
- **Backend**: .NET 8 (LTS), Minimal API, EF Core, MediatR, FluentValidation
- **DB**: Azure SQL (SQL Server) with Row-Level Security
- **Cache / idempotency / sessions**: Redis (StackExchange.Redis)
- **Storage**: Azure Blob (Azurite locally)
- **Auth**: Entra ID (internal staff) + Entra External ID (B2C/B2B); Nafath federation Phase 3
- **Observability**: OpenTelemetry → Application Insights + Serilog
- **Resilience**: Polly v8 pipelines per adapter
- **Build**: pnpm + Turborepo + single .NET .sln
- **IaC**: Bicep
- **CI**: GitHub Actions
- **Versioning**: Conventional Commits + squash-merge

## Phase-1 build order (high level)

1. **Week 1**: Foundation + Tajeer save-contract happy path end-to-end on staging
2. **Week 2**: Real UI for customers/vehicles/drivers; full Save Contract form
3. **Week 3**: Operations — E-Check, check-out/check-in, close, extend, suspend, incident
4. **Week 4**: Quotation + 3-tier approval + ZATCA invoice clearance + demo polish

Detail: [`Plans/02-phase-1-mvp-week-by-week.md`](./Plans/02-phase-1-mvp-week-by-week.md).

## Critical external dependencies (track these — they gate the schedule)

See [`Plans/05-dependency-onboarding-checklist.md`](./Plans/05-dependency-onboarding-checklist.md). Already in hand:
- ✅ Tajeer Rabet staging credentials
- ✅ ZATCA Fatoorah sandbox CSID

Pending (start now, parallel to Week 1):
- ⏳ Unifonic SMS sandbox account
- ⏳ Entra External ID tenant
- ⏳ Azure dev landing zone

## How Claude Code should help

- **Ask clarifying questions** before writing code when scope is ambiguous (the user prefers this — they explicitly adopted the brainstorming pattern)
- **Use `Plans/workstreams/{slug}/plan.md`** to scope multi-day work before touching code
- **Use TodoWrite** for active work tracking
- **Default to subagents** (Agent tool) for parallelizable research/build tasks
- **Verify before completing** — never mark a task done without running the verification
- **Update Specs** when design decisions evolve; don't drift silently
- **Update Plans** when sequencing changes; don't let plans get stale
- **Reference docs explicitly** — `[Spec 02 §6.2](./Specs/02-state-machines-and-sagas.md#62-lease-issuance-saga-the-critical-one)` not "the lease saga doc"

## What NOT to do without asking the user

- Make UI design decisions (await `design.md`)
- Push to `main` directly (always via PR)
- Run destructive git operations (force-push, reset --hard, branch -D)
- Modify D365 schema (other team owns D365)
- Submit anything to Tajeer/ZATCA production (sandbox only until UAT signoff)
- Add new vendor integrations without writing the Spec first

## When in doubt

1. Re-read the relevant Spec
2. Check Plans for the build order
3. Check the integration catalog ([Spec 04 §10](./Specs/04-integration-architecture.md#10-the-integration-catalog))
4. Ask the user — brainstorm before assumption

---

**This file is the constitution. If it disagrees with intuition, follow the file.**
