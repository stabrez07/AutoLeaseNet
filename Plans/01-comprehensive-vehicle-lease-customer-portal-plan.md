# 01 — Comprehensive Vehicle Lease Customer Portal: Master Plan

**Status**: ✅ Locked (synthesized from planning sessions, 2026-05-17)
**Scope**: KSA vehicle leasing — Web Portal (sales/ops) + Customer Portal (B2B/B2C) + full integration stack
**Owner**: Solo dev (this user) with Claude Code Pro + superpowers framework
**Target**: Phase 1 demo against Tajeer staging in 4 weeks; Phase 2 D365 + ZATCA prod in next 4

---

## 1. The vision in one paragraph

AutoLeaseNet is a multi-portal vehicle leasing platform for the Kingdom of Saudi Arabia. A **Web Portal** for the leasing company's internal sales and operations teams handles the full lease lifecycle from quotation through vehicle delivery, check-out/check-in, incidents, service, and closure. A **Customer Portal** gives B2B fleet administrators (corporate customers managing 10–1000s of vehicles) and B2C retail lessees (individuals with one vehicle) self-service over their fleet, drivers, invoices, telematics, and lease operations. The platform integrates **deeply with Tajeer** (KSA's mandatory unified rental-contract registry), **ZATCA** for Phase 2 e-invoicing, **D365 F&O/CRM/Fixed Assets** for finance and customer master, and—in later phases—**telematics, Wasl, Nafath, MOI fines**, payment gateways, and AI-driven features.

## 2. Who uses it

| Persona | Portal | Daily activities |
|---|---|---|
| **Sales Rep** (internal) | Web | Create quotations, manage customer relationships, follow up on opportunities |
| **Sales Manager / Regional Director** (internal) | Web | Approve quotations per tier thresholds, monitor pipeline |
| **Ops Manager** (internal) | Web | Vehicle preparation, fleet status, check-out / check-in, incident management, service scheduling |
| **Finance** (internal) | Web | Invoices, payments, ZATCA reconciliation, dunning |
| **Fleet Admin** (corporate customer / B2B) | Customer | View own fleet, assign drivers, view invoices, raise tickets, manage replacements |
| **Driver** (corporate's employee) | Customer (mobile-first) | View assigned vehicle, report incidents, request service, see PMS reminders |
| **Individual Lessee** (B2C) | Customer | View own vehicle, pay invoices, report incidents, request service |

## 3. End-to-end business flow

```
[Customer enquiry] → Sales Rep creates Quotation in Web Portal
                  ↓ (3-tier amount-based approval)
              Quotation APPROVED
                  ↓
         Sent to Customer (PDF)
                  ↓
           Customer ACCEPTS
                  ↓
   Procurement raises PO in D365 F&O (Phase 2 sync)
                  ↓
   Vehicles purchased + delivered to leasing co
                  ↓
   Vehicle Preparation: plate registration, GPS, accessories
   → recorded in D365 Inventory + Fixed Assets
                  ↓
   Vehicle attached to Customer → appears in Customer Portal
                  ↓
   E-Check completed (initial vehicle condition)
                  ↓
   Driver Check-Out from Web Portal OR Customer Portal
                  ↓
   ─── Tajeer Save Contract API ───
   Returns contractNumber + token + issuanceURL
                  ↓
   SMS issuance link to renter (via Unifonic)
                  ↓
   Renter completes on Tajeer's web page (Nafath OTP + e-sign)
                  ↓
   Tajeer webhook → contract.create event
                  ↓
   Lease status → ACTIVE; Vehicle status → UNDER_CONTRACT
                  ↓
        ┌─────────────────────────────┐
        │  Operational period         │
        │  - PMS notifications        │
        │  - Incidents / accidents    │
        │  - Service bookings         │
        │  - Replacements (sagas)     │
        │  - Mid-lease invoices       │
        │  - ZATCA submission per     │
        │    invoice (clearance/      │
        │    reporting)               │
        └─────────────────────────────┘
                  ↓
   Vehicle returned → Check-In + final E-Check
                  ↓
   ─── Tajeer Close Contract API ───
                  ↓
   Final invoice generation → ZATCA clearance
                  ↓
   Lease CLOSED; Vehicle → READY for next lease
```

## 4. Architecture in one diagram

```
                ┌────────────────────────────────────────────────────┐
                │  Web Portal (Next.js, AR/EN, RTL)                  │
                │  Customer Portal (Next.js, AR/EN, RTL)             │
                │  Mobile App (Phase 3 — React Native or .NET MAUI)  │
                └──────────────────┬─────────────────────────────────┘
                                   │
                       Azure Front Door (WAF + CDN)
                                   │
                       Azure API Management (rate-limit, auth)
                                   │
              ┌────────────────────┴────────────────────┐
              │  BFF — .NET 8 Minimal API               │
              │  (composition root; orchestrator)       │
              └────────────────────┬────────────────────┘
                                   │
        ┌──── Domain Services (modular monolith → microservices) ────┐
        │  Application: Use cases, sagas, event handlers (MediatR)   │
        │  Domain: Entities, value objects, invariants               │
        │  Infrastructure: EF Core, DbContext, repositories          │
        └────────────────────┬───────────────────────────────────────┘
                             │
            ┌── Azure Service Bus / Event Grid (async backbone) ──┐
            │                                                     │
   ┌────── Adapters (Ports & Adapters, pluggable, separate packages) ──┐
   │ Tajeer  │ ZATCA  │ SMS    │ Storage │ Cache   │ Email  │ PDF      │
   │ D365 FO │ D365   │ D365   │ Car     │ Payment │ WhatsA │ Nafath   │
   │         │ CRM    │ HR&P   │ Service │ Gateway │ pp     │          │
   │ Telem.  │ Wasl   │ MOI    │ AI/AzOAI│ AzVision│ OCR    │ ...      │
   └─────────┴────────┴────────┴─────────┴─────────┴────────┴──────────┘

Data: Azure SQL (OLTP, multi-tenant via RLS) · Cosmos DB / ADX (telematics, Phase 3)
      Blob (docs/photos) · Redis (cache/sessions/idempotency) · Key Vault · App Insights
```

## 5. What we're building (Phase 1, by number)

- **2 frontends**: Web Portal (sales/ops) + Customer Portal (B2B+B2C)
- **1 backend service**: .NET 8 BFF (Minimal API)
- **4 application-layer .NET projects**: Domain, Application, Application.Ports, Infrastructure
- **15 adapter packages**: Tajeer (+InMemory+Tests), Zatca (+InMemory), Sms (Unifonic+InMemory), Storage (AzureBlob+InMemory), Cache (Redis+InMemory), Email (AzCommunication+InMemory), Pdf (QuestPDF), and a Common shared infra package
- **1 .NET solution**: AutoLeaseNet.sln (20 projects total)
- **6 monorepo JS packages**: ui, contracts, eslint-config, tsconfig (+ apps)
- **1 Docker Compose stack** for local dev (SQL Edge + Redis + Azurite + MailHog)
- **1 CI workflow** (GitHub Actions, separate js/dotnet/infra jobs)
- **9 spec documents** in `Specs/` (01–08 + adr/) — locked architecture, state machines, adapter design, integration standard, monorepo layout, BFF API, ZATCA + approvals placeholders
- **7 plan documents** in `Plans/` (this folder)

## 6. What's explicitly OUT of Phase 1

| Item | Phase |
|---|---|
| Real D365 integration (mocked in P1) | 2 |
| ZATCA production CSID (sandbox only in P1) | 2 |
| Nafath B2C login (email+SMS OTP in P1) | 3 (long NIC onboarding) |
| Telematics + Wasl | 3 |
| Real-time GPS / immobilize | 3 |
| Native mobile apps | 3 (responsive web in P1) |
| Replacement saga (manual close + new lease in P1) | 2 |
| Workshop App (Car Servicing) | 2 |
| HR & Payroll mobile app | 3 |
| MOI fines pass-through | 3 |
| Payment gateway integration | 2 |
| AI features (copilot, damage detection, OCR, driver scoring) | 3 |
| Multi-country (UAE/GCC) | 4 |
| Predictive maintenance | 3 |

## 7. Phasing (the headline)

| Phase | Duration | Deliverable | Key dependencies |
|---|---|---|---|
| **0 — Pre-flight** | Week 0 (parallel) | Onboarding: Tajeer Rabet creds ✅, ZATCA sandbox CSID ✅, Unifonic SMS account, Azure tenant setup | External — see [Plan 05](./05-dependency-onboarding-checklist.md) |
| **1 — MVP demo** | Weeks 1–4 | End-to-end lease flow on Tajeer staging, AR/EN, demoable | Phase 0 complete |
| **2 — D365 + ZATCA prod** | Weeks 5–8 | D365 F&O/CRM/Fixed Assets sync, ZATCA production CSID, hardening | D365 team milestones, ZATCA prod approval |
| **3 — Intelligence + mobile** | Weeks 9+ | Telematics, Wasl, Nafath, mobile apps, AI copilot, payments, MOI fines | Per-feature dependencies |
| **4 — Multi-country** | TBD | UAE: TAMM, UAE Pass, Salik/Darb, Mulkiya | Market readiness |

## 8. Build order (the path)

In Phase 1, build in this order to minimize risk and maximize the chance of a working demo:

1. **Week 1**: Foundation + Tajeer happy path (a `dev/save-contract` endpoint that succeeds end-to-end on staging is the litmus test)
2. **Week 2**: Real UI for customers/vehicles/drivers; full Save Contract flow; webhook receiver wired
3. **Week 3**: Operations — E-Check sketch, check-out, check-in, close, extend, suspend, incident report
4. **Week 4**: Quotation + 3-tier approval workflow + ZATCA sandbox invoice clearance + demo polish

Detail in [Plan 02](./02-phase-1-mvp-week-by-week.md).

## 9. Critical risks (top 5)

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| 1 | Tajeer staging API behavior diverges from V9.7 spec | Medium | High | Contract snapshot tests + sandbox integration tests on every PR |
| 2 | Tajeer webhooks delayed / lost / out-of-order | Medium | High | Reconciliation job every 15 min calling GET /rent-contract; webhook is enrichment, not source of truth |
| 3 | ZATCA PIH chain breaks (gap in submitted invoices) | Low | Very High | `ZatcaChainState` per tenant updated atomically only on cleared; alert on mismatch; halt new submissions |
| 4 | Solo-dev burnout / scope creep | High | High | Hard 4-week Phase 1 cutoff; explicit "out of scope" list (§6); TDD discipline via superpowers framework |
| 5 | Nafath onboarding delays B2C launch | High | Medium | Email+SMS OTP fallback for Phase 1; isolate Nafath integration behind feature flag so portal can ship without it |

Full risk register: [Plan 07](./07-risk-register.md).

## 10. Success criteria for Phase 1 demo

The Phase 1 demo is considered successful if the following user journey works end-to-end on staging:

1. ✅ Sales Rep logs into Web Portal (Entra ID)
2. ✅ Creates Quotation for a corporate customer, submits for approval
3. ✅ Sales Manager (Tier 1) approves; Regional Director (Tier 2 if amount > threshold) approves
4. ✅ Quote sent to customer; customer accepts via portal (or marked accepted manually)
5. ✅ Ops user selects vehicle, performs E-Check (sketch + photos), initiates check-out
6. ✅ BFF calls Tajeer Save Contract → receives contractNumber + token + issuanceURL
7. ✅ SMS sent to renter mobile (via Unifonic) with issuance link
8. ✅ Renter opens link → completes on Tajeer's web page (manual UAT step)
9. ✅ Tajeer webhook arrives → Lease becomes ACTIVE; Vehicle becomes UNDER_CONTRACT
10. ✅ Customer Portal (separate login as fleet admin) shows the lease + assigned vehicle + driver
11. ✅ Ops user performs Check-In → Tajeer Close Contract → Lease CLOSED
12. ✅ Final invoice generated in ZATCA-compliant UBL XML + cryptostamp + QR code, submitted to ZATCA sandbox, returns CLEARED with UUID

If 11 of 12 work and the gap is non-blocking (e.g. ZATCA returned WARNING not REJECTED), Phase 1 is shippable to UAT.

## 11. Definition of "done" per workstream

A workstream is **done** only when ALL of:

- [ ] Code merged to `main` (after PR review)
- [ ] All new tests pass (unit + integration where applicable)
- [ ] `dotnet build` and `pnpm build` both succeed with no warnings (treat-warnings-as-errors)
- [ ] Manual smoke test on staging passes
- [ ] OpenAPI spec updated for any new/changed BFF endpoints
- [ ] Relevant Spec doc updated if design evolved
- [ ] No new TODOs without an issue/ADR linked
- [ ] Operator runbook updated if new ops procedures introduced (e.g. ZATCA chain reconciliation)

## 12. Where to look for what

| Question | Where |
|---|---|
| What does the system look like? | [Spec 01](../Specs/01-multi-tenancy-and-domain-model.md) (domain), [Spec 04](../Specs/04-integration-architecture.md) (integrations), §4 above |
| How do contracts flow through Tajeer? | [Spec 02 §6.2](../Specs/02-state-machines-and-sagas.md#62-lease-issuance-saga-the-critical-one), [Spec 03](../Specs/03-tajeer-adapter-design.md) |
| What's in Phase 1 vs later? | §6–7 above + [Plan 02](./02-phase-1-mvp-week-by-week.md) |
| What blocks the schedule? | [Plan 05 — Dependency checklist](./05-dependency-onboarding-checklist.md) |
| How do I add an integration? | [Spec 04 §11 — recipe](../Specs/04-integration-architecture.md#11-recipe-adding-a-new-integration) |
| How do I run it locally? | [README.md](../README.md) — Quick start |
| Where do API contracts live? | `packages/contracts/openapi.yaml` (single source); [Spec 06](../Specs/06-bff-api-surface.md) for design |
| How do we work (TDD, plans, reviews)? | [superpowers framework](https://github.com/obra/superpowers) + project's `CLAUDE.md` (to come) |

## 13. Adoption of superpowers methodology

This build uses [obra/superpowers](https://github.com/obra/superpowers) as the agentic development workflow:

- **TDD discipline (RED-GREEN-REFACTOR)** on every code change
- **Plans broken into 2-5 minute tasks** before any coding starts on a workstream
- **Worktree isolation** for parallel workstreams
- **Subagent dispatch** for parallel independent research/build
- **Verification before completion** — build + tests + lint must pass before marking done
- **Systematic debugging** — failing test reproduces every bug before fix

User to install in Claude Code: `/plugin install superpowers@claude-plugins-official`

## 14. Sign-off

This plan represents the **shared understanding** of what AutoLeaseNet is and how it will be built. Disagreements with this plan should result in updates to this doc OR new ADRs in `Specs/adr/`, not silent drift.

Reviewed by: (solo dev) — locked 2026-05-17
