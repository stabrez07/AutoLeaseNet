# Week 1 — Foundation + Tajeer Happy Path

**Workstream slug**: `2026-05-17-week-1-foundation-tajeer-happy-path`
**Opened**: 2026-05-17
**Owner**: solo dev + Claude Code
**Source plan**: [Plan 02 — Phase 1 MVP §Week 1](../../02-phase-1-mvp-week-by-week.md#week-1--foundation--tajeer-happy-path)
**Strategy**: **Hybrid** — start at Day 3 work (Adapters.Common, Tajeer adapter, `dev/save-contract`) locally, loop back to Days 1–2 (Bicep, App Insights, Entra app reg) once Azure/Entra land. Day 7 SMS stays on `Adapters.Sms.InMemory` until Unifonic creds arrive.

**Local infra (2026-05-18 update)**: Docker Desktop install hit problems. Replaced compose stack with: local **SQL Server 2019 Developer** (default instance, Windows Integrated Auth, DB = `AutoLeaseNet_Dev`) + `Adapters.Cache.InMemory` (real Redis loop-back when Docker/Memurai land). Azurite + MailHog are not exercised in Week 1 and stay deferred. See [notes.md](./notes.md#t05--t06--t07---compose-stack-replaced-with-local-infra) for rationale.

---

## 1. Goal

A working `POST /api/v1/dev/save-contract` endpoint running locally (Docker Compose stack) that hits **Tajeer Rabet staging** end-to-end, returning a real `contractNumber` + `issuanceURL`, with the inbound Tajeer webhook updating the local `Lease` row and an integration test proving the full happy path.

## 2. Scope

**In scope this workstream:**

- Scaffold verification (`dotnet build`, `pnpm install`, Docker Compose up)
- `Adapters.Common`: Polly v8 pipeline, `IntegrationResult<T>`, PII masking, dev `ICredentialProvider` stub
- `Adapters.Tajeer`: `TajeerOptions`, `TajeerAuthHandler` (App-id / App-key / Authorization), `HttpClient` registration, `GET /branch/all` auth smoke
- Tajeer `SaveContract` DTOs (V9.7) + adapter method
- BFF `dev/save-contract` endpoint (no UI — JSON in, JSON out)
- Tajeer webhook receiver `/api/v1/webhooks/tajeer` with signature verification + `WebhookLog` dedup
- `TenancyMiddleware` driven by a **dev JWT stub** (real Entra wired later)
- InMemory SMS adapter dispatched on `LeaseIssued`
- Integration test: happy-path Save Contract → webhook → state update

**Deferred (loop-back tasks, gated on external deps):**

- Bicep skeleton for dev RG (gated: Azure subscription)
- Application Insights workspace + OTel exporter wiring (gated: Azure subscription)
- Entra ID app registrations + real JWT validation (gated: Entra tenant)
- GitHub Actions CI secrets + branch protection (gated: GitHub admin)
- Unifonic SMS sandbox swap (gated: Unifonic sender ID approval ~3 days)

**Out of scope (later weeks):**

- Domain entities for Customer/Vehicle/Driver/Lease persistence beyond the minimum needed for webhook update (Week 2)
- RLS policies (Week 2 Day 9)
- Any Next.js UI work (Week 2 Day 11)
- ZATCA, Quotation, Inspection (Weeks 3–4)

## 3. Dependencies

| Dependency                                                                                                   | Status                  | Blocks               |
| ------------------------------------------------------------------------------------------------------------ | ----------------------- | -------------------- |
| Tajeer Rabet App-id / App-key / Authorization                                                                | ✅ in hand              | Day 4–5              |
| ZATCA Fatoorah CSID                                                                                          | ✅ in hand              | not needed this week |
| ~~Local Docker (SQL Edge, Redis, Azurite, MailHog)~~ → Local SQL Server 2019 (Windows Auth) + InMemory cache | ✅ in hand (2026-05-18) | Days 2, 5, 6         |
| Public tunnel for webhook (ngrok / Azure dev URL)                                                            | needs setup             | Day 6                |
| Azure dev RG + Key Vault                                                                                     | ⏳ pending              | loop-back §6 only    |
| Entra ID app reg                                                                                             | ⏳ pending              | loop-back §6 only    |
| Unifonic sandbox                                                                                             | ⏳ pending              | loop-back §6 only    |
| GitHub Actions CI secrets                                                                                    | ⏳ pending              | loop-back §6 only    |

## 4. Risks

| Risk                                                                    | Likelihood | Impact   | Mitigation                                                                                                                                 |
| ----------------------------------------------------------------------- | ---------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| Tajeer staging auth handshake fails (App-key signing format)            | M          | Critical | Day 4 first: `GET /branch/all` smoke before touching SaveContract. Capture raw request/response with PII masking.                          |
| Webhook signature spec ambiguous in vendor docs                         | M          | High     | Day 6: implement permissive log-only mode first, tighten to reject once verified against real callback.                                    |
| Polly v8 + `HttpClient` DI ordering trips delegating handler chain      | L          | Med      | Pin order in test: `AddHttpClient` → `AddHttpMessageHandler<TajeerAuthHandler>` → `AddResilienceHandler`. Cover with unit test.            |
| ngrok URL changes between runs invalidating registered webhook URL      | M          | Low      | Use ngrok reserved domain or Azure dev URL when subscription lands; document URL re-registration step.                                     |
| `dotnet build` warnings-as-errors trips on auto-generated EF migrations | L          | Low      | Exclude generated migrations folder from analyzer rules in `Directory.Build.props` per project.                                            |
| TenancyMiddleware dev-stub leaks into staging deploy                    | M          | High     | Stub registered only when `ASPNETCORE_ENVIRONMENT=Development`; fail-fast assertion in `Program.cs` if stub is active outside Development. |

## 5. Tasks

### Day 0 (today, 2026-05-17) — Scaffold verification

- [ ] **T0.1** Run `dotnet restore` at repo root from PowerShell. **Verify**: exit 0, no missing-package errors.
- [ ] **T0.2** Run `dotnet build AutoLeaseNet.sln -warnaserror`. **Verify**: build succeeds with 0 errors, 0 warnings.
- [ ] **T0.3** Run `pnpm install` at repo root. **Verify**: lockfile up to date, no peer-dependency errors blocking install.
- [ ] **T0.4** Run `pnpm build` (Turborepo). **Verify**: all apps + packages compile.
- [x] ~~**T0.5** `docker compose -f compose/docker-compose.yml up -d`.~~ **Replaced T0.5-alt**: Create `AutoLeaseNet_Dev` DB on local SQL Server (Windows Auth). **Verify**: `sqlcmd -S localhost -E -Q "SELECT name FROM sys.databases WHERE name='AutoLeaseNet_Dev'"` returns the row. ✅ 2026-05-18.
- [x] ~~**T0.6** Probe SQL Edge with `sqlcmd -S localhost,1433 -U sa`.~~ **Replaced T0.6-alt**: Probe `AutoLeaseNet_Dev` with `Microsoft.Data.SqlClient` using the exact `appsettings.Development.json` connection string. **Verify**: connection opens, `ServerVersion` non-empty. ✅ 2026-05-18 (`ServerVersion=15.00.2170`).
- [x] ~~**T0.7** Probe Redis with `redis-cli ping`.~~ **Replaced T0.7-alt**: Confirm `Adapters.Cache.InMemory.AddInMemoryCache()` exposes both `ICacheStore` + `IIdempotencyStore` for Day 5 wiring. **Verify**: file inspection. ✅ 2026-05-18.
- [ ] **T0.8** Open `notes.md` and capture any drift discovered (version mismatches, missing scripts). **Verify**: file exists with timestamped entry or "no drift" line.

### Day 1 — Adapters.Common foundation (TDD)

- [x] **T1.1** RED: write failing test `IntegrationResult_Success_carries_value`. **Verify**: `dotnet test` shows 1 failing. ✅ 2026-05-18.
- [x] **T1.2** GREEN: implement `IntegrationResult<T>` (`Success`, `Failure`, `IsTransient`, `ErrorCode`, `CorrelationId`). **Verify**: test passes. ✅ 2026-05-18.
- [x] **T1.3** RED + GREEN: `IntegrationResult_Failure_distinguishes_transient_vs_permanent`. **Verify**: 2 tests pass. ✅ 2026-05-18.
- [x] **T1.4** RED: write failing test `PiiMasking_masks_id_number_keeps_last_4`. **Verify**: 1 failing. ✅ 2026-05-18.
- [x] **T1.5** GREEN: implement `PiiMasking.Mask(string fieldName, string value)` covering ID number, IBAN, license. **Verify**: test passes; add cases for IBAN + license. ✅ 2026-05-18 (added 4 cases incl. null/empty + short-value defensive + unknown-field).
- [x] **T1.6** Add Polly v8 package ref to `Adapters.Common.csproj`. **Verify**: `dotnet restore` clean. ✅ 2026-05-18 (was already referenced from scaffold; bumped to 8.4.2).
- [x] **T1.7** Implement `PollyPipelineFactory.Build(adapterName, options)` with retry (exponential + jitter), timeout, circuit breaker. **Verify**: unit test asserts retries on `HttpRequestException`, no retry on `400`. ✅ 2026-05-18 (3 tests: HttpRequestException retries, 400 no-retry, 500 retries).
- [x] **T1.8** Add `IClock` + `SystemClock` (UTC). **Verify**: unit test injects `FakeClock`, asserts deterministic timestamps. ✅ 2026-05-18 (IClock lives in `Application.Ports/Time/` per Spec 04; Common.Tests references it; 3 tests).
- [x] **T1.9** Add `ICredentialProvider` interface + `DevEnvironmentCredentialProvider` reading from `appsettings.Development.json` / user-secrets (Key Vault impl deferred). **Verify**: unit test reads a known secret from in-memory config. ✅ 2026-05-18 (5 tests; `KeyVaultCredentialProvider` also updated to implement the new interface).
- [x] ~~**T1.10** REFACTOR: extract `ResultExtensions.Bind` / `Map` helpers if call sites duplicate.~~ **Skipped 2026-05-18** — zero call sites for `IntegrationResult` yet (adapters are stubs); no duplication to extract. Will revisit during Day 3 (Tajeer auth + smoke call) when first real consumer appears.

### Day 2 — TenancyMiddleware (dev-stub mode) + BFF skeleton

- [x] **T2.1** Add `DevJwtStubHandler` authentication handler that reads tenant claims from a header `X-Dev-Tenant-Id` / `X-Dev-Tenant-Slug` (Development environment only, fails hard outside). **Verify**: unit test asserts header → ClaimsPrincipal mapping, asserts throw in Production. ✅ 2026-05-18 (handler + AddDevJwtStub extension + 3 happy-path tests via WebApplicationFactory).
- [x] **T2.2** Wire `TenancyMiddleware` in `services/bff/` reading `tenant_id` claim. **Verify**: middleware unit test asserts claim → `TenantContext.Current`. ✅ 2026-05-18 (`ClaimsTenantContext` implements `ITenantContext`; `TenancyMiddleware` enforces tenant_id on authenticated requests + opens logging scope; 8 unit tests + 1 integration test via /dev/whoami).
- [x] **T2.3** Open SQL session, call `EXEC sp_set_session_context @key='TenantId', @value=@tenant`. **Verify**: integration test against ~~Dockerized SQL Edge~~ **local AutoLeaseNet_Dev DB** (Docker unavailable per [[local-dev-infra]]) confirms `SESSION_CONTEXT('TenantId')` returns set value. ✅ 2026-05-18 (`SqlSessionContext` helper in Infrastructure; 4 integration tests: round-trip, read-only enforcement (error 15664), 3-key set, fresh-connection null).
- [x] **T2.4** Add `/health/liveness` minimal endpoint returning 200. **Verify**: `curl http://localhost:5000/health/liveness` → 200. ✅ 2026-05-18 (mapped with `Predicate = _ => false` so no downstream checks gate it; 2 integration tests).
- [x] **T2.5** Add `/health/readiness` checking SQL + ~~Redis~~ connectivity. **Verify**: returns 200 with stack up, 503 with ~~Redis~~ **SQL** down. ✅ 2026-05-18 (`SqlHealthCheck` tagged "ready"; Redis check deferred — using InMemory cache per [[local-dev-infra]]; 2 integration tests inc. broken-conn-string fault injection).
- [x] **T2.6** Add `Program.cs` startup assertion: `if (env.IsProduction() && stubHandlerRegistered) throw`. **Verify**: integration test with `ASPNETCORE_ENVIRONMENT=Production` + stub registered → app fails to start. ✅ 2026-05-18 (assertion lives in `AddDevJwtStub` extension; 2 tests verify Production throws + Staging allowed).

### Day 3 — Tajeer auth + smoke call

- [x] **T3.1** Define `TajeerOptions` (BaseUrl, AppId, AppKey, AuthorizationToken, BranchId, TimeoutSeconds) bound from `Tajeer:` config section. **Verify**: unit test loads from in-memory config, validates required fields. ✅ 2026-05-22 (`Configuration/TajeerOptions.cs` + 9 unit tests).
- [x] **T3.2** RED: write failing test for `TajeerAuthHandler` injecting headers `App-id`, `App-key`, `Authorization` per V9.7 spec. **Verify**: 1 failing. ✅ 2026-05-22 (`TajeerAuthHandlerTests` initially failed on `NotImplementedException`).
- [x] **T3.3** GREEN: implement `TajeerAuthHandler : DelegatingHandler`. **Verify**: test passes; assert headers on outbound `HttpRequestMessage`. ✅ 2026-05-22 (`Authentication/TajeerAuthHandler.cs`; re-reads `IOptionsMonitor` per call so token rotation is live).
- [x] **T3.4** Register `HttpClient` in `Adapters.Tajeer` ServiceCollection extension with `TajeerAuthHandler` + `PollyPipelineFactory.Build("tajeer", ...)`. **Verify**: integration test resolves named client with both handlers in order. ✅ 2026-05-22 (named client `tajeer` + `AddResilienceHandler("tajeer-resilience", ResiliencePolicies.DefaultHttpPipeline)`; 2 registration tests).
- [x] **T3.5** Implement `TajeerLookupClient.GetAllBranchesAsync()` returning `IntegrationResult<IReadOnlyList<TajeerBranch>>`. **Verify**: unit test with `MockHttpMessageHandler` returns canned JSON → mapped DTO. ✅ 2026-05-22 (`Lookups/TajeerLookupClient.cs` + `Lookups/TajeerBranch.cs`; 5 unit tests covering 2xx mapping, canonical path, 4xx non-transient, 5xx transient, network exception transient; `AddScoped<TajeerLookupClient>` wired + resolution test).
- [x] **T3.6** **Smoke test against real Tajeer staging**: dev console runner or xUnit `[Trait("Category","Smoke")]` test that calls `GetAllBranchesAsync()` with real creds from user-secrets. **Verify**: 200 response, non-empty branch list, no PII leaked in logs. ✅ 2026-05-22 scaffold (`Smoke/TajeerStagingSmokeTests.cs` + `.runsettings` excludes `Category=Smoke` by default; reads user-secrets / `TAJEER_*` env vars; gracefully early-returns when `Tajeer:AppId` is absent so CI stays green). Awaiting first manual run with staging creds.
- [ ] **T3.7** Capture smoke-call request/response (PII masked) in `notes.md`. **Verify**: notes appended with timestamp + sanitized payload. ⏳ template placed in notes.md Day 3 section; fill in after first staging run.

### Day 4 — Tajeer SaveContract adapter

- [ ] **T4.1** Define `SaveContractRequest` DTO matching V9.7 (renter info, vehicle ref, branch, contract dates, rent policy, payment). **Verify**: compiles; XML doc comments cite V9.7 section.
- [ ] **T4.2** Define `SaveContractResponse` DTO (`ContractNumber`, `IssuanceUrl`, `ExpiryAt`, error envelope). **Verify**: compiles.
- [ ] **T4.3** RED: write failing test `TajeerContractClient_SaveContract_maps_request_and_parses_response` using `MockHttpMessageHandler`. **Verify**: 1 failing.
- [ ] **T4.4** GREEN: implement `TajeerContractClient.SaveContractAsync(SaveContractRequest)` returning `IntegrationResult<SaveContractResponse>`. **Verify**: test passes.
- [ ] **T4.5** RED + GREEN: error-mapping test — 4xx with vendor error code → `IntegrationResult.Failure(transient: false, errorCode)`. **Verify**: passes.
- [ ] **T4.6** RED + GREEN: transient test — 503 → `IntegrationResult.Failure(transient: true)` and Polly retries N times. **Verify**: passes; retry count asserted via test sink.
- [ ] **T4.7** Sibling `Adapters.Tajeer.InMemory.InMemoryTajeerContractClient` returning canned `SaveContractResponse`. **Verify**: contract test from `Adapters.Tajeer.Tests` runs against both real-mock and InMemory implementations.
- [ ] **T4.8** Register both implementations behind `ITajeerContractClient` port; selection via `Tajeer:Mode` config (`Real` / `InMemory`). **Verify**: integration test asserts mode switching.

### Day 5 — BFF `dev/save-contract` endpoint + first staging Save

- [ ] **T5.1** Add `Application` use case `SaveContractCommand` + handler that calls `ITajeerContractClient`, persists a minimal `Lease` row with `Status = PendingIssuance`. **Verify**: unit test with InMemory client + InMemoryDb confirms row written.
- [ ] **T5.2** Add `Domain.Lease` minimal entity (`Id`, `TenantId`, `CustomerId?`, `TajeerContractNumber?`, `IssuanceUrl?`, `Status`, `CreatedAt`, `UpdatedAt`). **Verify**: EF Core configuration compiles.
- [ ] **T5.3** Generate initial EF migration `Init_Lease`. **Verify**: `dotnet ef migrations add Init_Lease` succeeds; migration SQL inspected.
- [ ] **T5.4** Apply migration to Dockerized SQL Edge. **Verify**: `dotnet ef database update` exits 0; `SELECT * FROM Leases` works.
- [ ] **T5.5** Add BFF endpoint `POST /api/v1/dev/save-contract` requiring `Idempotency-Key` header. **Verify**: missing header → 400; present → 202 with response body.
- [ ] **T5.6** Wire idempotency via `Adapters.Cache.InMemory.AddInMemoryCache()` against the `IIdempotencyStore` port (24h TTL) keyed on `tenant + idempotency-key`. **Verify**: duplicate POST returns same cached response. _(Swap to Redis in §6 loop-back when Docker/Memurai available.)_
- [ ] **T5.7** **First real Save against Tajeer staging** from local BFF (Postman or curl with dev JWT stub header). **Verify**: 202 response with real `ContractNumber` + `IssuanceUrl`; row in `Leases` table with `PendingIssuance`.
- [ ] **T5.8** Capture sanitized request/response in `notes.md` + diagram the call flow if anything surprised. **Verify**: notes updated.

### Day 6 — Tajeer webhook receiver

- [ ] **T6.1** Add `WebhookLog` entity (`Id`, `TenantId`, `Source`, `EventType`, `Signature`, `Payload (encrypted at rest later)`, `ReceivedAt`, `ProcessedAt?`, `DedupKey`). **Verify**: migration `Add_WebhookLog` generated + applied.
- [ ] **T6.2** Endpoint `POST /api/v1/webhooks/tajeer` accepting raw body, returns 200 quickly. **Verify**: returns 200 with empty body in <100ms (work happens async).
- [ ] **T6.3** Signature verification helper — compare HMAC of body using shared secret from `TajeerOptions.WebhookSecret`. **Verify**: unit test: valid sig → ok, tampered body → reject.
- [ ] **T6.4** Permissive log-only mode flag `Tajeer:Webhook:LogOnly` defaulting `true` for first run. **Verify**: with flag on, signature mismatch logs but does not reject (covered by test).
- [ ] **T6.5** Dedup by `DedupKey = source + eventId`; second arrival → 200 no-op. **Verify**: integration test posts same payload twice, only one `Lease` update.
- [ ] **T6.6** On `LeaseIssued` event, update `Lease.Status = Issued`, set `IssuedAt`. **Verify**: integration test posts canned issued-event → row updated.
- [ ] **T6.7** Set up ngrok (or Azure dev URL placeholder) and register webhook URL with Tajeer staging. Document in `notes.md`. **Verify**: ngrok URL captured; record process for re-registration.
- [ ] **T6.8** End-to-end smoke from Day 5: POST `dev/save-contract` → wait for real webhook → assert `Lease.Status = Issued`. **Verify**: smoke passes against staging.
- [ ] **T6.9** Flip `Tajeer:Webhook:LogOnly = false` once first real webhook signature verified. **Verify**: real callback now requires valid HMAC; test re-runs green.

### Day 7 — SMS dispatch + integration test

- [ ] **T7.1** Add `ISmsClient` port in `Application.Ports`. **Verify**: compiles.
- [ ] **T7.2** Implement `Adapters.Sms.InMemory.InMemorySmsClient` capturing sent messages in a thread-safe list. **Verify**: unit test asserts capture.
- [ ] **T7.3** Add `LeaseIssuedDomainEvent` + handler that calls `ISmsClient.SendAsync(customerPhone, template, vars)`. **Verify**: unit test with InMemory port asserts message dispatched once `Lease.Status = Issued`.
- [ ] **T7.4** SMS template `lease_issued_ar` + `lease_issued_en` with `{contractNumber}` + `{issuanceUrl}` placeholders. **Verify**: template renderer test asserts both locales.
- [ ] **T7.5** End-to-end integration test (xUnit + WebApplicationFactory) — POST `dev/save-contract` against InMemory Tajeer adapter + simulate webhook → assert Lease `Issued` + SMS captured. **Verify**: test passes deterministically (no flakes across 10 runs).
- [ ] **T7.6** Run full `dotnet test` + `pnpm test`. **Verify**: 0 failures, 0 skipped without justification.
- [ ] **T7.7** Run `dotnet build -warnaserror` + `pnpm build`. **Verify**: 0 warnings, 0 errors.
- [ ] **T7.8** Manual staging smoke (the **Done criteria** below) — record video or screenshots. **Verify**: every Done-criteria checkbox ticked.

## 6. Loop-back tasks (gated on Pre-week-0 unblocks)

Pulled in as those external deps land. Do **not** block Days 0–7 on these.

### Azure dev landing zone (when subscription arrives)

- [ ] **L.A1** Bicep skeleton in `infra/` for dev RG (RG, KV, App Service plan, SQL, Redis, Storage, App Insights).
- [ ] **L.A2** `azd up` or pipeline deploy to dev RG. **Verify**: all resources provisioned, KV accessible.
- [ ] **L.A3** Migrate secrets from user-secrets to Key Vault; swap `DevEnvironmentCredentialProvider` for `KeyVaultCredentialProvider` in non-Dev envs.
- [ ] **L.A4** Wire OpenTelemetry → Application Insights with Serilog enricher + PII masking sink. **Verify**: trace appears in App Insights for a `dev/save-contract` call.

### Entra ID (when tenant + app reg ready)

- [ ] **L.E1** Register BFF + Web Portal apps in Entra ID. **Verify**: client IDs captured.
- [ ] **L.E2** Replace `DevJwtStubHandler` with `Microsoft.Identity.Web` JWT validation in non-Dev envs. **Verify**: integration test against staging issues real token, claims map to `TenantContext`.
- [ ] **L.E3** Add startup assertion: dev stub forbidden when `ASPNETCORE_ENVIRONMENT != Development`.

### GitHub CI

- [ ] **L.G1** GitHub Actions workflow `ci.yml`: restore → build (-warnaserror) → test → pnpm build/test.
- [ ] **L.G2** Branch protection on `main` requiring CI green + 1 review.
- [ ] **L.G3** CI secrets: Tajeer creds, ZATCA CSID, Azure deploy SP.

### Real Redis (when Docker Desktop fixed OR Memurai installed)

- [ ] **L.R1** Verify `redis-cli ping` returns `PONG`.
- [ ] **L.R2** Add `ConnectionStrings:Redis` to `appsettings.Development.json`; flip `Cache:Mode` to `Redis`.
- [ ] **L.R3** Wire `Adapters.Cache.Redis.AddRedisCache()` in BFF DI when `Cache:Mode == "Redis"`. **Verify**: idempotency integration test passes against real Redis (24h TTL respected).
- [ ] **L.R4** Run T5.6 + T6.x integration tests against Redis; capture any divergence vs InMemory in `notes.md`.

### Unifonic SMS (when sender ID approved ~3 days)

- [ ] **L.U1** Real `Adapters.Sms.Unifonic.UnifonicSmsClient` against `ISmsClient` port. **Verify**: contract tests pass against both InMemory + Unifonic.
- [ ] **L.U2** Sandbox smoke: send `lease_issued_ar` to a test phone. **Verify**: delivery confirmation logged.
- [ ] **L.U3** Switch BFF DI to Unifonic for staging environment only. Dev stays InMemory.

## 7. Done criteria (Week 1 close)

All must be true to close this workstream:

- [ ] BFF starts locally; `/health/liveness` returns 200; `/health/readiness` returns 200 with stack up.
- [ ] `POST /api/v1/dev/save-contract` against real Tajeer staging returns 202 with non-empty `ContractNumber` + `IssuanceUrl`.
- [ ] Tajeer webhook arrives at local ngrok URL → signature verified → `Lease` row updated to `Issued` → `LeaseIssuedDomainEvent` fires → InMemory SMS captures `lease_issued_ar` payload.
- [ ] Audit/log row written, PII masked in all log output (verified by grepping logs for raw ID number — should find none).
- [ ] `dotnet build -warnaserror` and `pnpm build` both green.
- [ ] `dotnet test` and `pnpm test` both green; no flakes across 3 consecutive runs.
- [ ] Integration test exercising the happy path is in `Adapters.Tajeer.Tests` (or BFF test project) and runs in CI once §6 GitHub CI lands.
- [ ] `notes.md` captures the raw Tajeer staging request/response (PII masked) and any spec deviations discovered.
- [ ] `retrospective.md` written: what went well, what surprised us, what to change for Week 2.

## 8. Out-of-band actions Claude must surface (not auto-execute)

- Pushing to `main` (always PR-only)
- Registering the ngrok URL with Tajeer staging on the vendor portal
- Any submission to Tajeer/ZATCA production
- Adding the Tajeer App-key to a non-user-secrets store (must go via Key Vault once §6.A lands)
