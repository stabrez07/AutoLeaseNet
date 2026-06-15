# Retrospective: Government Integration Dependency Tracking — RFQ Refresh (2026-06-15)

**Workstream**: `2026-06-15-dependency-rfq-refresh`
**Closed**: 2026-06-15
**Scope**: Read-only analysis + doc updates — no code changes

---

## What was done

Produced a complete, repository-grounded dependency status snapshot for all eight
requested integration areas (Tajeer/Elm Rabet, TAMM/NAQL, SHAMOOS, Nafath, SIMAH,
Entra, Unifonic, Azure landing zone). Updated `Plans/05-dependency-onboarding-checklist.md`
and `Plans/07-risk-register.md` based on facts found in the repository.

---

## Key findings

### Findings that change the plan

1. **`infra/bicep/` does not exist.** `Plans/05-dependency-onboarding-checklist.md` #5
   references `infra/bicep/main.bicep`, but neither the directory nor the file exists in
   the repository. This is an infrastructure-as-code gap, not just a pending credential.
   Logged as new risk **DEP-04** in the risk register.

2. **`Adapters.Sms.Unifonic` exists but is a placeholder.** The package is on disk and
   compiles (`throw new NotImplementedException`). Plan 05 #8 tracks the *credential*
   onboarding; a parallel code task is needed to implement `UnifonicSmsSender.SendAsync`
   once the sandbox account arrives. These are two separate unlocks, not one.

3. **SHAMOOS and SIMAH have zero repo presence.** Neither is referenced in any Spec, Plan,
   domain model, or code file. They cannot be tracked under the current checklist.
   Owner decision needed: are they in scope? If yes, add them to Plan 05 with phase +
   lead time. If no, they are out of scope and no action is needed.

4. **TAMM and NAQL are not standalone Phase 1/2 onboarding items.**
   - TAMM: subsumed by Tajeer's `tammExternalAuthorizationCountries` in Phase 1. Standalone
     adapter is Phase 3+ / UAE expansion only.
   - NAQL: Phase 1 vehicle ownership lookups flow through Tajeer internally. Tajeer adapter
     already handles `server.error.naql.not.available` gracefully. No separate onboarding needed.

5. **Manual Tajeer staging exercise (TODO #2 in `ai_context.md`) is the single highest-value
   action still outstanding.** Five items in `Plans/05-dependency-onboarding-checklist.md`
   are directly unblocked by running `STAGING-SMOKE.md` once with real Rabet creds + ngrok.
   The adapter code is complete; only the credential + network path is missing.

---

## Status per item (summary)

| Dependency | Phase | Adapter on disk? | Credential status | Action type |
|---|---|---|---|---|
| Tajeer / Elm Rabet | 1 (active) | ✅ Real + InMemory (full) | Staging ✅; Actions secrets = dummies; prod pending | Run staging smoke; rotate secrets |
| TAMM | 3+ / UAE | ❌ (not needed Phase 1/2) | N/A | None before Phase 3+ |
| NAQL | 3+ (standalone) | ❌ (proxied via Tajeer P1) | N/A | Monitor Tajeer staging NAQL errors |
| SHAMOOS | **Not in scope** | N/A | N/A | Owner: decide if in scope; add Plan 05 item |
| Nafath | 3 | ❌ (planned) | Not started | Submit NIC request Week 5 of Phase 1 |
| SIMAH | **Not in scope** | N/A | N/A | Owner: decide if in scope; add Plan 05 item |
| Entra ID (internal) | 1 | ❌ (planned) | IT Admin pending | IT Admin: create app registration |
| Entra External ID | 1 | ❌ (planned) | IT Admin pending | Needs Unifonic sandbox first |
| Unifonic | 1 | ⚠️ Placeholder stub | Sandbox pending | Get sandbox creds; implement `SendAsync` |
| Azure landing zone | Pre-Phase 1 | N/A | Subscription pending; `infra/bicep/` absent | Create `infra/bicep/main.bicep` |

---

## Carry-forward actions (owner: user / IT Admin)

These are **external / manual** actions — not code tasks:

| # | Action | Owner | Blocks |
|---|---|---|---|
| CF-1 | Execute `STAGING-SMOKE.md` with real Tajeer Rabet creds + ngrok | User | 5 Plan 05 items |
| CF-2 | Rotate 5 dummy `TAJEER_*` GitHub Actions secrets with real Rabet values | User | CI-based staging smoke |
| CF-3 | Sign up for Unifonic sandbox; obtain AppSid + sender ID | User | SMS adapter implementation + Entra External ID SMS OTP flow |
| CF-4 | IT Admin: create Entra ID app registration (BFF staff JWT bearer) | IT Admin | Internal staff auth |
| CF-5 | IT Admin: create Entra External ID tenant + email+SMS OTP custom flows | IT Admin + Unifonic (CF-3 first) | Customer Portal auth |
| CF-6 | Internal: obtain Azure dev subscription + resource group | User / Internal | Cloud deploy |
| CF-7 | Create `infra/bicep/main.bicep` (code task — 1 day) | User (code) | Azure landing zone |
| CF-8 | Submit Nafath integration request to NIC/SDAIA (by Week 5 of Phase 1) | User | Phase 3 B2C login |
| CF-9 | Owner decision: is SHAMOOS in scope? | Product owner | Plan 05 item creation |
| CF-10 | Owner decision: is SIMAH in scope? | Product owner | Plan 05 item creation |

---

## What went well

- Repository documents (`ai_context.md`, Plan 05, Plan 07, Specs 01/03/04) had enough
  factual detail to produce accurate status for 8 of the 10 requested items without
  speculative inference.
- Adapter directory enumeration immediately clarified that Unifonic is a stub, not a gap
  in the package list.

## What to improve

- **`infra/bicep/` should be created as a code task** in the near-term backlog. The
  reference to `infra/bicep/main.bicep` in Plan 05 has existed since the original checklist
  without a matching code workstream to create it.
- **SHAMOOS and SIMAH should be explicitly scoped** (in or out) so the checklist stops
  silently missing them. If they are a future integration requirement, add them to Plan 05
  now with a phase marker, even if status stays ⏳ Pending.
- **Plan 05 #8 (Unifonic) should call out the two separate unlocks**: (a) sandbox account
  (vendor onboarding) and (b) implement `UnifonicSmsSender.SendAsync` (code work). Conflating
  them obscures that the code can and should be written the moment the AppSid arrives.

---

## No changes to code or tests

This workstream produced only documentation updates (Plan 05, Plan 07, this workstream folder).
`dotnet build` and `dotnet test` are unaffected. No migrations, no schema changes.
