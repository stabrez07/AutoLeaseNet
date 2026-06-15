# 05 — Dependency & Onboarding Checklist

**Status**: ⚠️ Critical path — these gate the schedule more than code does
**Phase**: All phases — track as items move from pending → in-progress → done
**Last reviewed**: 2026-06-15 — see workstream [`Plans/workstreams/2026-06-15-dependency-rfq-refresh/`](./workstreams/2026-06-15-dependency-rfq-refresh/)

---

## The hidden critical path

Every external integration requires gov/partner onboarding **before** integration code can be written end-to-end. The onboarding lead times typically dominate the schedule. **Track these like first-class deliverables.**

## Pre-Phase 1 (must be done before Week 1)

| # | Item | Owner | Lead time | Status | Notes |
|---|---|---|---|---|---|
| 1 | **Tajeer Rabet registration** → App-id, App-key, Client-id | Tajeer team (Elm) | 2-4 weeks | ✅ DONE | Staging credentials in hand |
| 2 | **Tajeer Authorization token** generated via portal | Self-service | Same day | ⏳ Generate on first real staging run | https://tajeerstg.logisti.sa → User Management → API Registration. 5 dummy `TAJEER_*` Actions secrets seeded — rotate with real values before CI smoke. |
| 3 | **ZATCA Fatoorah sandbox CSID** | ZATCA | 3-6 weeks | ✅ DONE | Sandbox CSID issued |
| 4 | **Azure subscription** (dev) | Internal | 1-2 days | ⏳ Pending | Cost center + access set up |
| 5 | **Azure landing zone** (RG, KeyVault, App Service plan, SQL, Redis, Storage, App Insights) | Internal (us) | Day 1 of Week 1 | ⏳ Pending — `infra/bicep/` directory and `main.bicep` do not yet exist in repo | Bicep file must be created before any cloud deploy. Local dev runs on `STABREZ-LAPTOP` SQL Server (Docker-free). |
| 6 | **Entra ID corporate tenant** access for internal users | IT Admin | 1-2 days | ⏳ Pending | App registration for BFF + Web Portal. `Adapters.Identity.Entra` package not yet on disk. `DevJwtStubHandler` is current workaround. |
| 7 | **Entra External ID (CIAM) tenant** for B2C/B2B portal users | IT Admin | 2-3 days | ⏳ Pending — requires Unifonic sandbox (#8) first for SMS OTP flows | Custom flows for email + SMS OTP. `Adapters.Identity.EntraExternal` package not yet on disk. |
| 8 | **Unifonic SMS sandbox account** + sender ID approval | Unifonic | 3-5 days | ⏳ Pending — see note | **Two separate unlocks**: (a) sandbox account + AppSid from Unifonic vendor portal; (b) implement `UnifonicSmsSender.SendAsync` (placeholder `NotImplementedException` — package on disk but not functional). `Adapters.Sms.InMemory` covers dev + tests until (a)+(b) land. |
| 9 | **GitHub repo** with branch protection + CI secrets | Self-service | Same day | ✅ DONE | Repo is public; branch protection on `main` with required CI checks; 5 dummy `TAJEER_*` secrets seeded. |
| 10 | **Local dev tooling** (Node 20, pnpm 9, .NET 8 SDK, Docker Desktop, PowerShell 7) | Self | Same day | ⚠️ Partial | .NET SDK 10.0.301 ✅; Node/pnpm/Docker ❌ not installed on this PC; `dotnet-ef` ❌ not installed; BFF user secrets ❌ not set up. See `Plans/workstreams/2026-06-15-pc-tooling-resync/plan.md`. |

## Pre-Phase 2 (must start now, parallel to Phase 1)

| # | Item | Owner | Lead time | Status |
|---|---|---|---|---|
| 11 | **D365 dev tenant** access (F&O + CRM) | D365 team | 1-2 weeks | ⏳ Pending |
| 12 | **D365 API user + role assignments** (Customer Service Representative, Accounts Receivable, Fixed Assets) | D365 team | 1 week | ⏳ Pending |
| 13 | **D365 entity discovery** session (which CRM Contact fields, which F&O Customer fields, Sales Invoice structure) | D365 team + us | 1-2 days workshop | ⏳ Schedule for Week 3 |
| 14 | **ZATCA production CSID** review submission via Fatoorah portal | Self-submit | 3-6 weeks review | ⏳ Submit Week 4 (so it lands by Week 7-8) |
| 15 | **Tajeer production credentials** review | Tajeer team | 2-4 weeks | ⏳ Submit after Phase 1 staging UAT signoff |

## Pre-Phase 3 (start early — longest lead times)

| # | Item | Owner | Lead time | Status |
|---|---|---|---|---|
| 16 | **Nafath integration agreement** with NIC/SDAIA | NIC | 4-8 weeks | ⏳ Submit Week 5 of Phase 1 | No adapter package on disk yet. Phase 1 uses email+SMS OTP via Entra External ID (feature flag shields portal). |
| 17 | **Nafath UAT credentials** + IP whitelisting | NIC | Part of #16 | ⏳ Same | Part of #16 process |
| 18 | **Telematics vendor selection** (Mix Telematics vs Geotab) | Internal decision | 1-2 weeks evaluation | ⏳ Decide by end of Phase 2 |
| 19 | **Telematics dev API account** | Vendor | 1-2 weeks after selection | ⏳ After #18 |
| 20 | **Wasl integration** (KSA TGA fleet tracking, mandatory) | TGA | 2-4 weeks | ⏳ After telematics is live |
| 21 | **Payment gateway** account (HyperPay or Moyasar or PayTabs) | Vendor | 2-3 weeks | ⏳ Submit early Phase 3 |
| 22 | **WhatsApp Business** sender approval | Meta + intermediary | 2-4 weeks | ⏳ Submit when WhatsApp channel needed |
| 23 | **MOI / Absher integration** | MOI | 4-6 weeks | ⏳ Submit early Phase 3 |
| 24 | **Azure OpenAI** quota request (if using GPT-4 class models) | Microsoft | 1-2 weeks | ⏳ Submit when AI features planned |

## Onboarding tracking template (per item)

For each item above, track:

```
Item: [name]
Owner: [who]
Submitted: [date]
Expected by: [date]
Status: pending|in-progress|blocked|done
Blocking factor: [if blocked]
Next action: [specific]
Verification: [how we know it's truly done — e.g. "POST /test returns 200"]
```

## Verification at each stage

**Before starting any code that depends on a credential**: prove the credential works with a smoke test. Don't trust that "I've got the email saying it's issued" until a real API call succeeds.

Example smoke tests:

- **Tajeer**: `curl -H "app-id: ... -H "app-key: ..." -H "Authorization: Basic ..." https://tajeer-stg.api.elm.sa/rental-api/lookups/payment-method` → returns 200 with payment methods array
- **ZATCA**: invoke the EGS Health Check endpoint with the sandbox CSID → returns success
- **Unifonic**: send a test SMS to your own phone → received within 30s
- **Azure Key Vault**: retrieve a test secret using managed identity → returns value
- **D365 F&O**: OData `GET /data/Customers?$top=1` returns at least one customer

## Risk: if dependency slips

| Dependency slip | Phase 1 impact | Mitigation |
|---|---|---|
| Tajeer creds | Critical — halt | Already in hand ✅ |
| Azure landing zone | Critical | Have a dev laptop with Docker; run all locally; defer cloud deploy |
| Entra ID setup | High | Use local dev cert auth (Stub middleware) until ready |
| Unifonic | Medium | InMemory adapter captures SMS; show in dev UI |
| GitHub CI secrets | Low | Run tests locally; PR check is nice-to-have not blocker |
| D365 access (Phase 2) | Phase 2 critical | InMemory adapters keep BFF building; defer D365 sync |
| ZATCA prod CSID (Phase 2) | High | Sandbox keeps working; production submission deferred 1-2 weeks |
| Nafath (Phase 3) | High | Email+SMS OTP keeps the portal working indefinitely |

## Note on TAMM, NAQL, SHAMOOS, and SIMAH

| Integration | Repo status | Action |
|---|---|---|
| **TAMM** (KSA owner authorization) | In scope **Phase 3+ / UAE expansion only**. Phase 1/2: subsumed by Tajeer's `tammExternalAuthorizationCountries`. Domain seed has `TammAuthorizationStatus` on Driver. No onboarding needed before Phase 3+. | Add item when Phase 3 or UAE expansion is confirmed. |
| **NAQL** (vehicle ownership lookup) | In scope **Phase 3+ (standalone)**. Phase 1: vehicle ownership lookups flow through Tajeer internally; Tajeer adapter already handles `server.error.naql.not.available` as `ExternalDependency`. | Monitor Tajeer staging NAQL errors. Add item when standalone NAQL integration is planned. |
| **SHAMOOS** | **Not referenced in any repository document** (no Spec, Plan, or code file). Cannot assign phase or status. | Product owner: decide if in scope; if yes, add an item to this checklist with phase + lead time. |
| **SIMAH** (Saudi Credit Bureau / credit check) | **Not referenced in any repository document**. | Product owner: decide if in scope (likely Phase 2/3 for B2C lessee credit scoring); if yes, add an item to this checklist with phase + lead time. |

## Weekly cadence

Every Friday review:
- Items that moved status this week
- Items expected to complete next week
- Items past their expected date (escalate)
- New dependencies discovered

Add to [Plan 07 — Risk Register](./07-risk-register.md) any dep that slips by >50% of its lead time.
