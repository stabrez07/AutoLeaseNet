# AutoLeaseNet — Plans

This folder holds **planning artifacts** — high-level roadmaps, phase plans, dependency checklists, and per-workstream task plans.

Plans answer **"what we'll build, when, and in what order"**. They evolve as work progresses.

For **technical specifications** (the "how it works" — domain model, state machines, adapter design, API surface, etc.), see [`../Specs/`](../Specs/README.md).

## Index

| # | Plan | Status | Purpose |
|---|---|---|---|
| 01 | [Comprehensive Vehicle Lease Customer Portal — Master Plan](./01-comprehensive-vehicle-lease-customer-portal-plan.md) | ✅ Locked | The full vision, scope, and approach for AutoLeaseNet — synthesized from all planning sessions |
| 02 | [Phase 1 MVP — Week-by-Week Plan](./02-phase-1-mvp-week-by-week.md) | ✅ Locked | 4-week breakdown for the Phase 1 demo against Tajeer staging |
| 03 | [Phase 2 — D365 Integration Roadmap](./03-phase-2-d365-integration-roadmap.md) | ✅ Locked | 4-week breakdown for D365 F&O / CRM / Fixed Assets wiring + ZATCA production |
| 04 | [Phase 3+ — Telematics, AI, Mobile, Multi-country](./04-phase-3-plus-roadmap.md) | 📋 Draft | Longer-term roadmap: telematics, Wasl, Nafath, mobile apps, AI, UAE expansion |
| 05 | [Dependency & Onboarding Checklist](./05-dependency-onboarding-checklist.md) | ⚠️ Critical path | Gov + partner onboarding actions (Rabet, ZATCA, Nafath, etc.) — block the schedule if skipped |
| 06 | [Integration Build Order](./06-integration-build-order.md) | ✅ Locked | The order to build the 27 adapter packages across all phases |
| 07 | [Risk Register & Mitigations](./07-risk-register.md) | 📋 Living doc | Known risks (regulatory, technical, integration) and mitigations |

## Workstream plans

Per-workstream **task-level plans** (broken into 2-5 minute tasks per the [superpowers methodology](https://github.com/obra/superpowers)) live in [`workstreams/`](./workstreams/). Created just-in-time before each workstream begins.

## How to use this folder

- **Strategic decisions** (scope, sequencing, phasing): live here as `NN-*.md` documents.
- **Tactical decisions** (architecture, schema, contracts): live in [`../Specs/`](../Specs/).
- **Operational decisions** (immediate task list during a sprint): live in `workstreams/{slug}/plan.md`.
- **ADRs** (architecture decision records): live in `../Specs/adr/`.

## Cross-cutting context (locked decisions)

These were established during planning and apply to all plans:

- **Project**: KSA vehicle leasing platform (B2B fleet admin + B2C retail lessee)
- **Stack**: Azure + .NET 8 + Next.js 14 + Entra ID + Entra External ID
- **Region**: KSA-first; UAE/GCC later
- **Build approach**: Solo dev using Claude Code with [superpowers](https://github.com/obra/superpowers) workflow (TDD, plans-of-tasks, worktree isolation, subagent dispatch)
- **Integration pattern**: Hexagonal — every external system in a separate pluggable adapter package
- **Pre-onboarded**: Tajeer staging credentials, ZATCA sandbox CSID
- **Pending onboarding**: Nafath (long lead — deferred to Phase 3), ZATCA production CSID (Phase 2)
