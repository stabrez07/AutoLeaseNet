# AutoLeaseNet — Planning & Specification Docs

This folder is the **source of truth for architecture and design** of the AutoLeaseNet vehicle leasing platform (KSA). Read in order; each doc builds on the previous.

## Status: Phase 1 foundation

Docs 01–06 are **locked specifications** (drafts open for change but signed-off as the working baseline). Docs 07–08 are **placeholders** — to be written just-in-time before their Phase 1 workstream begins. Doc 09 is **active draft** for pricing and projection implementation.

## Reading order

| #                                                     | Title                                       | Status         | Purpose                                                                                                 |
| ----------------------------------------------------- | ------------------------------------------- | -------------- | ------------------------------------------------------------------------------------------------------- |
| [01](./01-multi-tenancy-and-domain-model.md)          | Multi-tenancy & Domain Model                | ✅ Locked      | Tenancy (single DB + RLS), 36 entities across 8 bounded contexts, ERDs, invariants                      |
| [02](./02-state-machines-and-sagas.md)                | State Machines & Sagas                      | ✅ Locked      | Lifecycle for Quotation/Vehicle/Lease/Invoice; 6 named sagas (Lease Issuance, Replacement, ZATCA, etc.) |
| [03](./03-tajeer-adapter-design.md)                   | Tajeer Adapter Design                       | ✅ Locked      | Implementation-ready C# interfaces, error catalog, resilience, helpers (canonical Pattern B example)    |
| [04](./04-integration-architecture.md)                | Integration Architecture (Ports & Adapters) | ✅ Locked      | The standard every adapter follows; 27-item integration catalog; pluggability rules                     |
| [05](./05-monorepo-layout-and-build-system.md)        | Monorepo Layout & Build System              | ✅ Locked      | Repo tree, pnpm + Turborepo + .NET, all root configs, local dev workflow, CI shape                      |
| [06](./06-bff-api-surface.md)                         | BFF API Surface                             | ✅ Locked      | REST conventions, permissions, ~80 Phase 1 endpoints, critical endpoint examples, OpenAPI skeleton      |
| [07](./07-zatca-invoice-generation.md)                | ZATCA Invoice Generation                    | ⏳ Placeholder | UBL XML, cryptostamp, PIH chain, EGS lifecycle, library choice                                          |
| [08](./08-approval-workflow-engine.md)                | Approval Workflow Engine                    | ⏳ Placeholder | Config-driven tiered approvals, evaluator, delegation, audit                                            |
| [09](./09-quotation-pricing-and-projection-engine.md) | Quotation Pricing and Projection Engine     | 🟡 Draft       | Pricing setup masters, waterfall algorithm, projection model, validation/testing                        |

## Cross-cutting decisions

These were locked across multiple docs and should not be revisited without sign-off:

- **Stack**: Azure + .NET 8 + Next.js 14 + Entra ID + Entra External ID; Microsoft-first for D365 alignment
- **Region**: KSA-first; architected for multi-country (UAE/GCC) later
- **Tenancy**: Single DB + Row-Level Security; two-level isolation (Tenant + Customer); SQL Always Encrypted for sensitive PII
- **Integration pattern**: Hexagonal — every external system in its own package, pluggable via DI
- **Build**: pnpm workspaces + Turborepo + single root .NET .sln + Bicep IaC
- **Primary integrations Phase 1**: Tajeer (staging), ZATCA (sandbox), Unifonic SMS, Azure Blob, Redis, Entra
- **Approvals**: 3-tier tiered by amount, config-driven
- **B2C login Phase 1**: Email + SMS OTP via Unifonic (Nafath deferred to Phase 3 due to NIC onboarding lead time)

## Decision records

Architecture Decision Records will live in [`adr/`](./adr/). To be added as decisions arise during build.

## Conventions

- **File naming**: `NN-short-title.md` (two-digit prefix, kebab-case)
- **Status header on each doc**: includes Status, Phase, Owner, Dependencies, Last updated
- **Sign-off checklist** at the end of each doc — defaults are accepted unless explicitly overridden
- **Cross-references**: use relative links (`[doc 03](./03-tajeer-adapter-design.md)`)

## How to evolve these docs

1. Open a branch named `docs/<slug>`.
2. Edit the relevant doc; bump `Last updated` and version in header.
3. If the change affects another doc, update that one too.
4. PR with title `docs(NN): <what changed>`.
5. Merge to `main` after self-review.
