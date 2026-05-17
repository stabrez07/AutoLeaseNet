# 02 — Phase 1 MVP: Week-by-Week Plan

**Status**: ✅ Locked
**Phase**: 1 (Weeks 1–4)
**Goal**: Working end-to-end lease flow on Tajeer staging, AR/EN, demoable to stakeholders
**Constraint**: Solo dev + Claude Code Pro

---

## Pre-week 0 (parallel onboarding)

Must be true before Week 1 starts. See [Plan 05 — Dependency Onboarding Checklist](./05-dependency-onboarding-checklist.md):

- [x] Tajeer Rabet credentials issued (App-id, App-key, Authorization token)
- [x] ZATCA Fatoorah sandbox CSID issued
- [ ] Azure dev subscription + landing zone (RG, Key Vault, App Service plan, SQL, Redis, Storage, App Insights)
- [ ] Unifonic SMS sandbox account + sender ID approval (~3 days)
- [ ] Entra ID (corporate) tenant access for internal users
- [ ] Entra External ID (CIAM) tenant provisioned for customer portal
- [ ] GitHub repo created with branch protection + CI secrets configured

## Week 1 — Foundation + Tajeer happy path

**Goal**: A `dev/save-contract` endpoint that hits Tajeer staging successfully end-to-end, returning a real `contractNumber` + `issuanceURL`.

| Day | Tasks (each 2-5 min per superpowers methodology) |
|---|---|
| **1** | Verify scaffold compiles (`dotnet build` + `pnpm install`); Docker stack up locally; Bicep skeleton for dev RG; Application Insights workspace |
| **2** | Entra ID app registration for BFF + Web Portal; JWT validation middleware; TenancyMiddleware reads claims → SQL SESSION_CONTEXT (per [Spec 01 §3.5](../Specs/01-multi-tenancy-and-domain-model.md#35-bff-middleware-sets-session_context-per-request)) |
| **3** | Adapters.Common: Polly pipeline, IntegrationResult, KeyVaultCredentialProvider, PII masking |
| **4** | Tajeer adapter: TajeerOptions, TajeerAuthHandler (HTTP message handler), HttpClient registration; first sandbox call (GET /branch/all to test auth) |
| **5** | Tajeer SaveContract DTOs (per V9.7 spec); BFF `dev/save-contract` endpoint; first successful Save against staging |
| **6** | Webhook receiver: `/api/v1/webhooks/tajeer` with signature verification + dedup via WebhookLog; tunnel via ngrok or Azure dev URL for inbound testing |
| **7** | SMS dispatch: InMemory adapter wired (Unifonic switched on later if sandbox creds ready); integration test for full happy path |

**Done criteria**:
- BFF starts; `/health/liveness` returns 200
- `dev/save-contract` POST returns 202 with `issuanceURL`
- Tajeer webhook arrives → Lease row updated in SQL → audit visible
- All tests pass; CI green

## Week 2 — Core data + customer/vehicle flows

**Goal**: Real UI showing customers, vehicles, drivers, leases. Save Contract works from a real form (not hardcoded payload).

| Day | Tasks |
|---|---|
| **8** | Domain entities for Customer, Vehicle, Driver, Lease (per [Spec 01 §5](../Specs/01-multi-tenancy-and-domain-model.md#5-entity-definitions)); EF Core configurations; first migration |
| **9** | Apply RLS policies via migration script; `dotnet ef database update` succeeds; verify RLS via integration test with two tenants |
| **10** | Repository implementations for Customer + Vehicle; seed data script for dev tenant |
| **11** | Web Portal: Next.js app boots; i18n (AR/EN) with next-intl; RTL CSS via logical properties; design system from `packages/ui` (await design.md from user) |
| **12** | Customer list + detail pages; vehicle list + 360° view; OpenAPI-typed API client in `packages/contracts` |
| **13** | Driver mgmt: list, create, validate (license expiry check); KYC doc upload to InMemoryStorage |
| **14** | Save Contract form: renter-type branching (Saudi/Resident/GCC/Visitor), vehicle picker, rent policy dropdown (from cached Tajeer lookups), payment fields; SMS dispatched on success; 12h expiry countdown in UI |

**Done criteria**:
- Web Portal renders AR/EN with RTL working
- User can navigate customers → vehicles → drivers
- Save Contract form submits → Tajeer → SMS → webhook → state update visible in UI
- Customer Portal scaffold present (auth working) — not feature-rich yet

## Week 3 — Operations: E-Check, check-out, close, extend

**Goal**: All lease lifecycle operations work — full operational portal.

| Day | Tasks |
|---|---|
| **15** | Sketch component: 893×429 canvas, 4 damage marker types per [Spec 03 §11.3](../Specs/03-tajeer-adapter-design.md#113-sketch-json-builder); mobile drag-to-place; JSON output |
| **16** | Photo upload: chunked, resumable; Blob storage (InMemoryStorage in dev); virus-scan stub |
| **17** | Inspection aggregate + repository; CHECK_OUT inspection flow integrated with Save Contract |
| **18** | Check-out saga (per [Spec 02 §6.3](../Specs/02-state-machines-and-sagas.md#63-check-out-saga)); pessimistic lock on vehicle |
| **19** | Check-in flow + Calculate Payment preview (Tajeer API) + Close Contract |
| **20** | Extend Contract + Suspend Contract endpoints; UI for each; reconciliation job (15-min scheduled) |
| **21** | Incident report form; vehicle status transitions; Customer Portal — fleet view + invoice list (read-only) |

**Done criteria**:
- Full check-out → operational period → check-in flow works against staging
- Suspend → Close path works
- Extend works (up to limit)
- Customer Portal shows customer's leases + vehicles + invoices

## Week 4 — Quotation + ZATCA invoice + demo polish

**Goal**: Sales flow ships, invoices submit to ZATCA sandbox, demo prep done.

| Day | Tasks |
|---|---|
| **22** | Quotation aggregate + lines; QuotationApproval entity; ApprovalTier seed data (Tier 1 / 2 / 3 by amount) |
| **23** | Quote approval workflow saga (per [Spec 02 §6.1](../Specs/02-state-machines-and-sagas.md#61-quote-approval-workflow-saga)); approver inbox endpoint; UI for submit + approve + reject |
| **24** | Quote PDF generation (QuestPDF — minimal template; full design via design.md later); send-to-customer flow |
| **25** | Invoice generation on LeaseIssued event; line items computed from lease + extras |
| **26** | ZATCA adapter (real implementation): UBL XML generation; cryptostamp using sandbox CSID; TLV QR; PIH chain init |
| **27** | ZATCA Clearance submission (B2B); ZatcaSubmission state machine; chain integrity reconciliation |
| **28** | Demo data seeding + end-to-end smoke test of all 12 success-criteria steps from [Plan 01 §10](./01-comprehensive-vehicle-lease-customer-portal-plan.md#10-success-criteria-for-phase-1-demo); buffer for bug fixes |

**Done criteria**:
- 12-step success-criteria checklist all passes on staging
- Tests green; CI green; treat-warnings-as-errors clean
- README + Plans + Specs all reflect what was built
- Demo script + recorded walkthrough video ready

## Slip / risk handling

If by end of Week N the goal isn't met:

- **Week 1 slip**: Critical. Tajeer integration is the heart. Halt other tracks, get this working. Worth pulling in a day from Week 4 buffer.
- **Week 2 slip**: Trim non-essential UI polish; mock photos/sketch if time tight.
- **Week 3 slip**: Defer Extend or Suspend to Phase 2 (Close is the priority).
- **Week 4 slip**: ZATCA submission can fall to Phase 2 if cryptostamp gives trouble (we have sandbox CSID but the signing flow is the hardest part). Approval workflow takes precedence.

## Verification cadence

End of every week: run the 12-step demo manually. If any step fails or is faked, document it in [Plan 07 — Risk Register](./07-risk-register.md) and decide whether to fix or defer.

## What to do AFTER Phase 1 closes

Move directly into Phase 2 per [Plan 03](./03-phase-2-d365-integration-roadmap.md). Phase 1 sign-off does NOT mean Phase 2 starts the same day — take 1-2 days to:

- Demo the working system to stakeholders
- Update Specs based on what was learned
- Update CLAUDE.md / superpowers plans for Phase 2 cadence
- Restock on credentials/access for Phase 2 (D365 tenant, ZATCA prod CSID review submission)
