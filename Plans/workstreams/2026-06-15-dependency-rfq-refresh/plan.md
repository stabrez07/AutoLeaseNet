# Workstream: Government Integration Dependency Tracking — RFQ Refresh (2026-06-15)

**Goal**: Produce an accurate, repository-grounded status snapshot for every external
government / partner dependency, identify concrete next actions per item, and update
`Plans/05-dependency-onboarding-checklist.md` + `Plans/07-risk-register.md` to reflect
the current state of the codebase and pending manual work.

**Scope boundary**: read-only analysis of repo docs + adapter packages + infra directory;
status updates to Plan 05 + Plan 07; new risk entry for missing `infra/bicep`; workstream
plan + retrospective. No feature code, no migrations, no schema changes.

---

## Source documents read

| Document | Key fact extracted |
|---|---|
| `Plans/05-dependency-onboarding-checklist.md` | Baseline status of items #1–24 |
| `Plans/07-risk-register.md` | Active risks DEP-01/02/03 + last-reviewed 2026-05-17 |
| `ai_context.md` (last updated 2026-06-15) | Current blocker profile + adapter inventory |
| `packages/adapters/` directory listing | Which adapter packages exist on disk |
| `Plans/01-comprehensive-vehicle-lease-customer-portal-plan.md` | Phase 4 UAE expansion (TAMM) |
| `Plans/04-phase-3-plus-roadmap.md` | TAMM (UAE), Nafath, Wasl, Telematics phasing |
| `Specs/04-integration-architecture.md` | Integration catalog items #1–24 + Pattern A/B classification |
| `Specs/01-multi-tenancy-and-domain-model.md` | NAQL/Yakeen Vehicle fields, auth user types |
| `Specs/03-tajeer-adapter-design.md` | NAQL error code handled in Tajeer adapter |
| `Plans/workstreams/2026-06-15-pc-tooling-resync/plan.md` | Azure / Entra / Unifonic manual blockers confirmed |

---

## Dependency status — findings per item

### 1. Tajeer / Elm Rabet

| Attribute | Fact |
|---|---|
| Plan 05 items | #1 ✅ staging creds in hand; #2 ⏳ auth token (self-service, generate on first run); #15 ⏳ production review (post-Phase-1 UAT) |
| Adapter packages | `Adapters.Tajeer` + `Adapters.Tajeer.InMemory` — on disk, building, 5 ITajeerContractClient methods implemented |
| GitHub Actions secrets | 5 dummy `TAJEER_*` secrets seeded (see `ai_context.md` TODO #3); must rotate with real Rabet creds before any CI-run staging smoke |
| Manual staging exercise | TODO #2 in `ai_context.md` — still outstanding; requires ngrok + real Tajeer Rabet staging creds at `https://tajeerstg.logisti.sa` |
| Risk register | REG-01 (spec drift) + TECH-01 (webhooks) + DEP-03 (production creds delay) — all active and mitigated by design |
| **Next owner action** | (a) Generate Tajeer authorization token via portal on first real staging run; (b) rotate 5 dummy GitHub Actions secrets; (c) execute `STAGING-SMOKE.md` runbook (~45-90 min) |

### 2. TAMM (KSA owner authorization)

| Attribute | Fact |
|---|---|
| Repo references | Spec 04 catalog #21 (Phase 3+); `Plans/04-phase-3-plus-roadmap.md` §UAE — "TAMM (Abu Dhabi gov services)"; domain seed model has `TammAuthorizationStatus` enum on Driver |
| Phase assignment | **Phase 3+**; in Phase 1 / Phase 2 it is subsumed by Tajeer's `tammExternalAuthorizationCountries` field — no separate adapter or onboarding needed until Phase 3+ or UAE expansion |
| Adapter packages | None on disk; Spec 04 notes "standalone only if needed" |
| Plan 05 item | None currently; not a Phase 1/2 dependency |
| **Next owner action** | No action required before Phase 3. If UAE expansion is confirmed earlier, add a Plan 05 item with owner + lead time. |

### 3. NAQL (vehicle ownership lookup)

| Attribute | Fact |
|---|---|
| Repo references | `Specs/01-multi-tenancy-and-domain-model.md` (BrandAr/BrandEn "From Naql/Yakeen"; OwnerNumber "Naql owner ID"); `Specs/03-tajeer-adapter-design.md` (`server.error.naql.not.available` error code mapped as `TajeerErrorCategory.ExternalDependency`) |
| Phase assignment | Phase 1 NAQL lookups are **proxy-accessed through Tajeer** (Tajeer internally calls Naql/Yakeen for vehicle registration). Spec 04 catalog #24 plans a standalone `Adapters.Naql` for Phase 3+. |
| Adapter packages | None on disk; Tajeer adapter already handles NAQL-unavailable errors gracefully |
| Plan 05 item | None currently; not a separate Phase 1/2 dependency |
| **Next owner action** | No separate action. Monitor Tajeer adapter's NAQL error handling under staging. Add Plan 05 item when standalone NAQL integration is planned (Phase 3+). |

### 4. SHAMOOS

| Attribute | Fact |
|---|---|
| Repo references | **Zero** — no mention in any `.md` file or source file in this repository |
| Plan 05 item | None |
| Adapter packages | None |
| **Finding** | SHAMOOS is not currently in scope per any repository document. Cannot assign a status. Owner should add a Plan 05 item with phase assignment + lead time if this integration is confirmed needed. |

### 5. Nafath (KSA national digital identity — NIC/SDAIA)

| Attribute | Fact |
|---|---|
| Plan 05 items | #16 ⏳ integration agreement with NIC/SDAIA (4–8 weeks, submit Week 5 of Phase 1); #17 ⏳ UAT credentials + IP whitelisting (part of #16) |
| Phase assignment | Phase 3 — `Plans/04-phase-3-plus-roadmap.md` §"Nafath B2C login (Week 11)" |
| Current fallback | Email + SMS OTP via Entra External ID + Unifonic; isolate Nafath behind feature flag |
| Adapter packages | `Adapters.Nafath` — **not yet on disk** (placeholder planned in Spec 05 layout) |
| Risk register | DEP-01 — "Nafath onboarding delays B2C launch" — Likelihood High, Impact Medium, mitigated by phasing |
| **Next owner action** | Submit Nafath integration request to NIC/SDAIA by Week 5 of Phase 1 (as originally planned); track reply date. No code work until credentials arrive. |

### 6. SIMAH (Saudi Credit Bureau)

| Attribute | Fact |
|---|---|
| Repo references | **Zero** — no mention in any `.md` file or source file in this repository |
| Plan 05 item | None |
| Adapter packages | None |
| **Finding** | SIMAH is not currently in scope per any repository document. Cannot assign a status. Owner should add a Plan 05 item with phase assignment + lead time if credit-check integration is confirmed needed (likely Phase 2 or Phase 3 for B2C lessee risk scoring). |

### 7. Entra ID (internal staff — corporate tenant)

| Attribute | Fact |
|---|---|
| Plan 05 item | #6 ⏳ pending — IT Admin owner; 1–2 days; "App registration for BFF + Web Portal" |
| Current workaround | `DevJwtStubHandler` used for all dev + test auth; `X-Dev-UserId` / `X-Dev-TenantId` headers; production guard test asserts stub is off in Production environment |
| Adapter packages | `Adapters.Identity.Entra` — **not yet on disk** (Spec 03 layout shows `Adapters.Entra/` as placeholder; Spec 04 catalog #6 assigns it Phase 1) |
| Integration catalog | Spec 04 #6 — Pattern B, Phase 1 |
| **Next owner action** | (a) IT Admin: create app registration in corporate Entra tenant for BFF (scope: staff JWT bearer); (b) once app registration exists, implement `Adapters.Identity.Entra` package (1 day per Spec 04 §time-budget); (c) set `ENTRA__TENANTID` + `ENTRA__CLIENTID` in GitHub Actions secrets + Azure Key Vault. |

### 8. Entra External ID / CIAM (B2B fleet admin + B2C lessee)

| Attribute | Fact |
|---|---|
| Plan 05 item | #7 ⏳ pending — IT Admin owner; 2–3 days; "Custom flows for email + SMS OTP via Unifonic" |
| Current workaround | Same `DevJwtStubHandler` as internal; Customer Portal auth is a future concern (portal is scaffolded but unauthed) |
| Adapter packages | `Adapters.Identity.EntraExternal` — not yet on disk; Spec 04 catalog #7, Pattern B, Phase 1 |
| Dependency chain | Requires Unifonic sandbox (item #8) to wire SMS OTP flows |
| **Next owner action** | IT Admin: create Entra External ID tenant; configure email OTP + SMS OTP custom flow; provide tenant ID + client ID. Unifonic sandbox must land first for SMS OTP flow testing. |

### 9. Unifonic SMS

| Attribute | Fact |
|---|---|
| Plan 05 item | #8 ⏳ pending — Unifonic vendor; 3–5 days; "Sandbox first; production sender ID via separate approval" |
| Adapter package on disk | `Adapters.Sms.Unifonic` — **exists** with `UnifonicOptions`, `AddUnifonicSms` DI helper, and `UnifonicSmsSender` class; **however** the implementation body is `throw new NotImplementedException("Unifonic adapter implementation pending Phase 1 Week 2.")` — it is a placeholder, not functional |
| InMemory companion | `Adapters.Sms.InMemory` — fully functional; `InMemorySmsSender.Sent` captures messages; used in all current dev + tests |
| Integration catalog | Spec 04 #3 — Pattern A, Phase 1, "Unifonic in prod; InMemory in tests" |
| **Next owner action** | (a) Obtain Unifonic sandbox account + AppSid + sender ID (vendor portal sign-up, ~3-5 days); (b) implement `UnifonicSmsSender.SendAsync` HTTP call against Unifonic REST API; (c) add health check + Polly pipeline per Spec 04 §5 recipe; (d) add integration test (`Category=Integration`) for real sandbox send. |

### 10. Azure Landing Zone

| Attribute | Fact |
|---|---|
| Plan 05 items | #4 ⏳ Azure subscription (Internal; 1–2 days; cost center + access); #5 ⏳ landing zone RG / KeyVault / App Service / SQL / Redis / Storage / App Insights (Internal; Day 1 of Week 1) |
| Bicep template | Plan 05 #5 references `infra/bicep/main.bicep`; **this file and directory do not exist in the repository** |
| Current state | Local dev runs on `STABREZ-LAPTOP` local SQL Server (Docker-free path documented in `ai_context.md` TODO #8); `compose/docker-compose.yml` provides the Docker path |
| Azure Storage adapter | `Adapters.Storage.AzureBlob` — package on disk; requires real Azure Storage connection string |
| Azure Cache | `Adapters.Cache.Redis` — package on disk; requires Redis (`localhost:6379` in dev) |
| **Next owner action** | (a) Internal: obtain Azure subscription + resource group; (b) create `infra/bicep/` directory and `main.bicep` defining: RG, Key Vault, App Service Plan (P1v3), Azure SQL, Redis Cache, Storage Account, App Insights, Log Analytics; (c) run `az deployment group create` from CI; (d) set Azure deploy credentials in GitHub Actions secrets. |

---

## Tasks completed in this workstream

| # | Task | Status |
|---|------|--------|
| T1 | Read `Plans/05-dependency-onboarding-checklist.md` and record current status per item | ✅ done |
| T2 | Read `Plans/07-risk-register.md` and identify stale risks | ✅ done |
| T3 | Read `ai_context.md` current blocker profile section | ✅ done |
| T4 | Enumerate `packages/adapters/` directory — identify which adapter packages exist on disk | ✅ done |
| T5 | Read `Adapters.Sms.Unifonic/UnifonicSmsSender.cs` — confirm placeholder vs implemented | ✅ done |
| T6 | Search all `.md` files for TAMM / NAQL / SHAMOOS / SIMAH / Nafath references | ✅ done |
| T7 | Confirm `infra/bicep/` does not exist | ✅ done |
| T8 | Update `Plans/05-dependency-onboarding-checklist.md` with accurate status + owner actions | ✅ done |
| T9 | Update `Plans/07-risk-register.md` — bump last-reviewed + add DEP-04 (missing infra/bicep) | ✅ done |
| T10 | Write `retrospective.md` | ✅ done |

---

## Definition of Done

- [x] Status snapshot grounded in repo facts for all 10 dependency areas
- [x] Plan 05 updated with accurate status column + next-action notes per item
- [x] Plan 07 last-reviewed date bumped + new risk DEP-04 added
- [x] SHAMOOS and SIMAH noted as "not in scope per repo docs" — no speculative claims
- [x] Workstream `retrospective.md` written
