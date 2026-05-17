# 06 — Integration Build Order

**Status**: ✅ Locked
**Cross-ref**: [Spec 04 §10 — Integration catalog](../Specs/04-integration-architecture.md#10-the-integration-catalog)

---

## Why the order matters

Each adapter follows the [doc 04 standard](../Specs/04-integration-architecture.md) but the order in which they're built affects:

1. **Schedule risk** — Tajeer is the most complex; do it first when energy is highest
2. **Dependency unblocking** — SMS needed before Save Contract demo works end-to-end
3. **Demoability** — features that need 2+ adapters can't ship until both exist

## Phase 1 build order (Weeks 1-4)

| # | Adapter package | Why this order | Pattern (A or B) |
|---|---|---|---|
| 1 | `Adapters.Common` | Foundation for every other adapter (Polly, IntegrationResult, KeyVault, PII masking) | (shared) |
| 2 | `Adapters.Cache.InMemory` + `Adapters.Cache.Redis` | Needed for idempotency store and lookup cache | A |
| 3 | `Adapters.Storage.InMemory` + `Adapters.Storage.AzureBlob` | Needed for document/photo uploads (E-Check photos) | A |
| 4 | `Adapters.Tajeer` (+ InMemory + Tests) | The heart. Has its own week. | B |
| 5 | `Adapters.Sms.InMemory` + `Adapters.Sms.Unifonic` | Tajeer issuance URL must reach the renter | A |
| 6 | `Adapters.Email.InMemory` + `Adapters.Email.AzureCommunication` | Quote delivery, invoice email, approval notifications | A |
| 7 | `Adapters.Pdf.QuestPdf` | Quote PDFs, invoice PDFs (ZATCA QR) | A |
| 8 | `Adapters.Zatca` (+ InMemory) | Last in Phase 1 — Week 4 | B |

## Phase 2 build order (Weeks 5-8)

| # | Adapter | Notes |
|---|---|---|
| 9 | `Adapters.D365.Crm` (+ InMemory) | Customer master sync |
| 10 | `Adapters.D365.Fo` (+ InMemory) | Invoice posting, Fixed Asset transactions |
| 11 | (continued) `Adapters.D365.FixedAssets` (subset of `D365.Fo` or separate) | Vehicle ↔ Fixed Asset mapping |
| 12 | `Adapters.CarServicing` | Workshop booking sync (if vendor available) |

## Phase 3 build order (Weeks 9+)

| # | Adapter | Notes |
|---|---|---|
| 13 | `Adapters.Payments.HyperPay` (or Moyasar / PayTabs) + InMemory | Online customer payments |
| 14 | `Adapters.Telematics.{Mix or Geotab}` + InMemory | Vendor-agnostic via `ITelematicsProvider` |
| 15 | `Adapters.Wasl` | KSA TGA fleet tracking (mandatory) — depends on telematics |
| 16 | `Adapters.Nafath` | B2C login federation; long onboarding |
| 17 | `Adapters.Moi` (Absher) | Traffic fines |
| 18 | `Adapters.Messaging.WhatsApp` | Notifications channel |
| 19 | `Adapters.Ai.AzureOpenAi` | Copilot in Customer Portal |
| 20 | `Adapters.Ai.AzureVision` | Damage detection from E-Check photos |
| 21 | `Adapters.Ai.AzureFormRecognizer` | OCR for Iqama/license auto-fill |
| 22 | `Adapters.DocSign.Local` (canvas e-sign) | For non-Tajeer documents (Tajeer handles its own) |
| 23 | `Adapters.D365.HrPayroll` | Driver = employee sync (if needed) |

## Phase 4 (UAE expansion)

| # | Adapter | Notes |
|---|---|---|
| 24 | `Adapters.Tamm` (UAE) | Standalone (currently subsumed in Tajeer via `tammExternalAuthorizationCountries`) |
| 25 | `Adapters.UaePass` | UAE federated identity |
| 26 | `Adapters.Salik` / `Adapters.Darb` | UAE tolls |
| 27 | `Adapters.Rta` (Mulkiya / fines) | UAE vehicle registration + fines |

## Per-adapter checklist (use for each)

When building an adapter, complete:

- [ ] Project scaffolded per [Spec 04 §4 layout](../Specs/04-integration-architecture.md#4-standard-module-layout)
- [ ] `{Name}Options` configured
- [ ] HTTP client (if Pattern B) with `{Name}AuthHandler`
- [ ] `{Name}LoggingHandler` for PII-masked request/response logging
- [ ] Polly resilience pipeline registered
- [ ] Idempotency wrapper (if state-changing operations)
- [ ] Error catalog with top-30 explicit `errorCode → LocalizedMessage` mappings + category fallback
- [ ] OpenTelemetry source + meter + counters
- [ ] Health check implementation
- [ ] `Add{Name}()` extension method (single public entry point)
- [ ] InMemory companion package
- [ ] Tests project (Unit + Contract snapshots + Sandbox integration)
- [ ] README.md with vendor docs, quirks, supported version
- [ ] Added to [Spec 04 §10 catalog](../Specs/04-integration-architecture.md#10-the-integration-catalog) status column
- [ ] Composition root registration in `services/bff/Program.cs`
- [ ] appsettings.json section + Key Vault references documented

## Anti-patterns when building an integration

(From [Spec 04 §13](../Specs/04-integration-architecture.md#13-anti-patterns-to-avoid)):

- ❌ Calling `HttpClient` from a BFF endpoint directly to hit vendor — always go through adapter
- ❌ Application code referencing `Adapters.{Name}` directly — only ports
- ❌ Hardcoded credentials/URLs
- ❌ Throwing exceptions for business errors
- ❌ Skipping the InMemory companion "because we're in a hurry"
