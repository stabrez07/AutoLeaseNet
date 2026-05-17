# 03 — Phase 2: D365 Integration & ZATCA Production Roadmap

**Status**: ✅ Locked
**Phase**: 2 (Weeks 5–8)
**Goal**: D365 F&O/CRM/Fixed Assets integration wired; ZATCA production CSID + clearance submission; hardening for UAT

---

## Pre-week 5 (parallel onboarding)

- [ ] D365 dev tenant access (CRM + F&O); API user with appropriate roles
- [ ] D365 OData/Dataverse endpoints documented + sample data
- [ ] ZATCA production EGS onboarding submitted via Fatoorah portal (allow 3-6 weeks)
- [ ] Tajeer production credentials review submitted

## Week 5 — D365 CRM customer sync

**Goal**: Customer entity in our system stays in sync with D365 CRM Contact (read-mostly Phase 2; write in Phase 3 if needed).

| Day | Tasks |
|---|---|
| **29** | Adapter scaffold: `AutoLeaseNet.Adapters.D365.Crm` + InMemory; `ID365CrmClient` interface |
| **30** | Dataverse OData client setup (Microsoft.Identity.Client for auth); contact entity DTOs |
| **31** | `GetContactById`, `SearchContactsByMoiOrNid` implementations |
| **32** | Sync job: on Customer create → mirror to D365 CRM (Phase 2 write); on D365 update → reflect locally (webhook or polling) |
| **33** | Conflict resolution: D365 wins on shared fields (legal name, VAT, address); we own credit limit + status |
| **34** | Integration tests with real D365 dev tenant |
| **35** | UAT: Sales rep creates customer → appears in D365; D365 update → reflects locally |

## Week 6 — D365 F&O invoice + Fixed Assets sync

| Day | Tasks |
|---|---|
| **36** | Adapter: `AutoLeaseNet.Adapters.D365.Fo` + InMemory; `ID365FoClient` interface |
| **37** | F&O Customer + Invoice entity DTOs; OData endpoints for Sales Invoice journal |
| **38** | On invoice ZATCA-cleared: post AR journal entry to D365 F&O (mirror invoice as Customer Invoice) |
| **39** | Fixed Assets: on Vehicle.Status → READY (first time), create Fixed Asset record in D365 (with VIN, plate, customer link) |
| **40** | Vehicle lease lifecycle → Fixed Asset transactions (assignment, depreciation triggers — F&O handles depreciation, we trigger events) |
| **41** | Reconciliation report: any invoice not in F&O within 24h flagged |
| **42** | UAT: full lease cycle → invoice cleared → posted to F&O AR; vehicle attached to customer → Fixed Asset assignment in F&O |

## Week 7 — ZATCA production + replacement saga

| Day | Tasks |
|---|---|
| **43** | ZATCA production CSID onboarding (per Fatoorah portal — assuming review complete) |
| **44** | Switch adapter env from Sandbox to Production (`ZatcaOptions.Environment`); test single clearance against prod with a small invoice |
| **45** | Replacement Saga: full implementation per [Spec 02 §6.5](../Specs/02-state-machines-and-sagas.md#65-vehicle-replacement-saga); test all compensation paths |
| **46** | Saga persistence (`SagaInstance` table); resume-after-restart capability |
| **47** | Damaged-vehicle replacement flow end-to-end (incident → replacement → old close → new save → pro-rata invoices) |
| **48** | Operator dashboard: pending sagas, dead-letter outbox events, ZATCA chain status |
| **49** | UAT: trigger replacement; verify both lease records, invoices, vehicle statuses; verify D365 sync |

## Week 8 — Hardening + observability + UAT readiness

| Day | Tasks |
|---|---|
| **50** | OpenTelemetry + Application Insights end-to-end; distributed trace from Web Portal → BFF → Tajeer/D365/ZATCA |
| **51** | Health checks for every adapter (per [Spec 04 §7](../Specs/04-integration-architecture.md#7-health-checks)); `/health/integrations` dashboard endpoint |
| **52** | Performance: load test save-contract flow (target: 50 concurrent saves with p95 < 5s); identify bottlenecks |
| **53** | Security review: scan for hardcoded secrets, verify Key Vault usage, validate RLS policies under load |
| **54** | Append-only audit log review tooling (filterable by entity, time, actor); export to CSV for compliance |
| **55** | Outbox dead-letter recovery procedure; runbook for ZATCA chain break recovery |
| **56** | UAT signoff with stakeholders; production deployment runbook reviewed |

## Done criteria for Phase 2

- [ ] D365 CRM customer sync working (write-side from us, read-side from D365)
- [ ] D365 F&O receives invoice postings within 1h of ZATCA clearance
- [ ] Fixed Asset records created for every prepared vehicle
- [ ] ZATCA production CSID issued and successfully submitting B2B clearances
- [ ] Replacement saga survives failure injection tests (kill BFF mid-saga → resumes correctly)
- [ ] All P0 risks from Phase 1 mitigated or accepted (documented in [Plan 07](./07-risk-register.md))
- [ ] UAT users (3–5 internal users) successfully execute the full demo flow without dev support
- [ ] Production deployment plan signed off

## Out of Phase 2

- D365 HR & Payroll (Phase 3)
- Car Servicing App integration (Phase 3 — may move earlier if a vendor is available)
- Real telematics integration
- Mobile apps
- Nafath B2C login
- Payment gateways
- AI features

## Critical risks Phase 2

- **ZATCA prod CSID delay**: 3-6 week review cycle. If delayed past Week 7, ship Phase 2 without prod ZATCA — flag as known gap.
- **D365 schema discovery**: F&O entities are notoriously complex. Time-box D365 schema mapping to 2 days; if blocked, defer Fixed Assets sync to Phase 3.
- **Replacement saga complexity**: Spans 2 leases, 2 vehicles, 2 Tajeer contracts. Highest defect risk. Pair test (Claude + user) all compensation paths.
