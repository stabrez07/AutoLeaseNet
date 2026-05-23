# Week 1 Retrospective — Foundation + Tajeer Happy Path

**Workstream**: `2026-05-17-week-1-foundation-tajeer-happy-path` + the inserted `2026-05-24-domain-deepening-production-seed`
**Opened**: 2026-05-17
**Code-side closed**: 2026-05-24 (Day 7 commit `82bdd0d`)
**Owner**: solo dev + Claude Code Pro
**Final test count**: 153 / 153 green (smoke excluded), 154 / 154 when smoke included
**Commits on `main`**: 8 (`3e145f1` Day 0 infra → `82bdd0d` Day 7 SMS dispatch)

---

## 1. What we shipped (code-side)

| Layer | Built | Tests |
|---|---|---|
| Domain | 7 aggregates (Lease, Customer, Vehicle, Driver, Branch, RentPolicy, ExtendedCoverage, WebhookLog), `LeaseIssuedDomainEvent`, 9-state `LeaseStatus`, transition invariants on every aggregate | 27 |
| Application | `IntegrationResult<T>`, MediatR command + 6 lookup queries + notification handler, `IPagedResult<T>`, ports for every repository + idempotency + SMS + seeding + tenancy | 43 |
| Infrastructure | EF Core DbContext + 8 configs + 2 migrations, 7 EF repositories, design-time factory, local dotnet-ef 8.0.5 tool manifest | 4 |
| Tajeer adapter | `TajeerOptions` + DataAnnotations + `ValidateOnStart`, `TajeerAuthHandler`, named HttpClient + Polly v8 pipeline, `TajeerLookupClient`, `TajeerContractClient` + InMemory sibling + mode switch, `WebhookSignatureValidator`, `TajeerWebhookPayload` + event-type matcher | 45 |
| BFF | DevJwtStub + production guard, `TenancyMiddleware`, health probes, `POST /dev/save-contract`, `POST /webhooks/tajeer`, 6 `/lookups/*` endpoints, MediatR + AddInMemorySms wiring | 41 |
| Seed adapter | `Adapters.Seed` (Bogus-driven KSA-shaped, idempotent, reproducible), `Seed:Mode = Empty | Demo | ImportedFile` | (covered via E2E) |
| Common adapter | Polly v8 default pipeline, `PiiMasking`, `IClock` / `SystemClock`, `ICredentialProvider` | 20 |

**Effective row counts after a fresh `dotnet run` (single seeded tenant)**: 3 Branches / 4 RentPolicies / 3 ExtendedCoverages / 20 Customers (6 B2B + 14 B2C) / 60 Vehicles / 80 Drivers / 10 Leases spanning every status.

## 2. What went well

- **TDD discipline held the whole way.** Every adapter went RED → GREEN. No "let me get it working then maybe add tests" debt. The 153-test suite has never had a flake.
- **The Pattern B sub-client design** (per Spec 04 §3.2) scaled cleanly across `TajeerLookupClient` / `TajeerContractClient` / `WebhookSignatureValidator` / future Tajeer sub-clients. Allowing `ITajeerContractClient` to live in the adapter package (not Application.Ports) was the right pragmatic call.
- **`IntegrationResult<T>` + Polly v8 pipeline pattern** paid for itself immediately. Day 4 resilience tests proved retries / non-retries / transient classification all worked without owning a custom error type per vendor.
- **The user's mid-Week-1 feedback** ("don't miss BI fields, seed production-shaped data") landed cleanly via the superpowers workstream pattern. We didn't sneak the changes into Day 6; we paused, planned, asked five locking questions, executed Days A–F as one commit. Test count grew from 95 → 136 in that one workstream and **every existing test stayed green throughout**.
- **`[LoggerMessage]` source generators everywhere** kept CA1848 satisfied with zero runtime allocation cost. Event ID ranges (5xxx for SaveContract, 6xxx for webhooks, 7xxx for SMS) make ops searchability easy.
- **Day-5 Lease + Day-6 webhook + Day-7 SMS chained end-to-end without re-architecture.** The Lease aggregate raised the domain event, the webhook handler scanned + published, the notification handler dispatched. No saga rework needed.
- **Hexagonal boundary held.** Application never referenced Infrastructure; Domain never referenced anything outside itself; Adapters never reached into Domain. The one deliberate exception (Application → Adapters.Tajeer for `ITajeerContractClient`) is documented inline.
- **Local-SQL substitution** (Docker Desktop unavailable) was a 30-min pivot that did not block any test or any later day's work.

## 3. What surprised us

| Surprise | Impact | What we learned |
|---|---|---|
| Tajeer webhook auth is **shared-secret header equality**, not HMAC-of-body | Plan said HMAC; spec said header. The spec wins. | When the plan and the spec disagree, the spec wins because it reflects vendor reality. Update the plan inline. |
| Global `dotnet-ef` was on .NET 10; our project is .NET 8 | Migrations failed with `FileNotFoundException 'System.Runtime, Version=10.0.0.0'` | Always pin a local `.config/dotnet-tools.json` for tooling that targets the framework. |
| EF Core 8 generated migration files violate CA1707 + CA1861 + CA1062 | Build failed first time | Folder-scoped `NoWarn` in the Infrastructure csproj. Generated code can't satisfy our analyzer baseline. |
| `appsettings.Development.json` is gitignored | New BFF tests failed for anyone who didn't have a local copy | Test factories must inject their own configuration inline. Don't rely on the file existing. |
| BFF DLL lock from a leftover `dotnet run` process | Subsequent `dotnet test` retry loop ate 10 seconds before failing | When a test factory runs the host, kill stale processes before re-running. Add a `Get-Process AutoLeaseNet.Bff | Stop-Process -Force` pre-step where useful. |
| Tajeer's V9.7 spec contains the literal misspelling `addtionalServices` | If we "fixed" it on our side, Tajeer would silently reject the field | Preserved on the wire; documented inline; asserted by a test that the JSON contains the misspelling. |
| The Day-5 minimum `Lease` aggregate would have needed massive backfilling for BI | Caught by user feedback before it compounded | Build entities with full BI granularity from the first save. The `feedback-production-ready-data` memory captures this for future work. |
| `LeaseIssuedDomainEvent` shouldn't take a MediatR dependency in Domain | Domain.csproj has `<!-- Domain has zero external dependencies by design -->` | Wrap domain events in an Application-layer `INotification` rather than make Domain MediatR-aware. The BFF webhook handler scans `lease.DomainEvents` post-save and switch-publishes. |
| EF Core handlers using DbContext can't live in Application (would invert dependency direction) | Day-F lookup query handlers | Queries records + DTOs go in Application; handlers go in Infrastructure. MediatR scans both assemblies. |
| `TestHost.ConfigureTestServices` requires the `Microsoft.AspNetCore.TestHost` package | Compile error first time | Test-host package isn't included with `Microsoft.AspNetCore.Mvc.Testing`. Adding it once unblocks the whole BFF.Tests project. |

## 4. What to change for Week 2 and beyond

| Change | Rationale | Concrete action |
|---|---|---|
| **Schedule the manual staging exercise BEFORE it blocks downstream** | 7 boxes (T3.7 + T5.7-5.8 + T6.7-9 + T7.8) are now Week-1 blockers and will become Week-2/3/4 blockers if not run. | Allocate one ~hour slot with Tajeer creds + ngrok in hand. Run T5.7 → T6.7 → T6.8 → T6.9 → T3.7 → T5.8 → T7.8 in one sitting using the recipes in workstream notes. |
| **Request `design.md` earlier in Week 2** | Week 2 Days 11-14 are entirely UI work that can't start without it. If design.md is delayed, the entire week stalls. | Asking the user at the start of Week 2 should be the first item, not a side note. |
| **Replace inline domain-event dispatch with a DbContext interceptor** | The webhook handler's post-save `DispatchDomainEventsAsync(lease, publisher, ct)` works but is only wired for one call site. Every future save-and-raise path needs to remember to repeat it. | Week 2 saga work — add an `IDomainEventDispatcher` SaveChanges interceptor on the DbContext. Becomes truly transparent. |
| **Add a BackgroundService worker for webhook async dispatch** | Phase-1 inline dispatch was fine for one event/sec; Spec 03 §12.3 calls for an async drain pattern. Volume will demand it eventually. | Stand up a `TajeerWebhookProcessorService : BackgroundService` that drains `WebhookLog` rows where `ProcessedAtUtc IS NULL`. Wire when first non-issuance event (invoice, general) lands. |
| **Use `Verify.Xunit` for snapshot tests on Tajeer DTOs** | The package is pinned but not yet used. Tajeer payloads have a lot of nested shape that's currently asserted field-by-field. | Week 2 — add `SaveContractRequest_serialises_to_canonical_v9_7_shape` using `Verify` so future Tajeer additions only require approving a snapshot. |
| **Move webhook tenant resolution out of the Phase-1 fallback hack** | Today: `if no Lease found, assume seed tenant`. Phase 2 multi-tenant must encode the tenant in the registered URL. | Week 2 — register webhook URL per tenant: `/api/v1/webhooks/tajeer/{tenantId:guid}`. Retire `GetByTajeerContractNumberAcrossTenantsAsync`. |
| **Add Always Encrypted columns for PII** | `Customer.PersonIdNumber`, `Driver.DriverLicenseNumber`, IBAN are plain strings today. CLAUDE.md §7 requires AE. | Week 2 Day 9 alongside the RLS migration. |
| **Wire RLS policies** | Tenant filter is in-app today (every repository where-clause). RLS in the DB is the defence-in-depth layer. | Week 2 Day 9 per the existing plan. |
| **Stand up GitHub Actions CI early** | We've been verifying via local `dotnet test` only. A CI run on every PR is overdue. | Once GitHub admin access lands, run loop-back tasks L.G1-L.G3 immediately. |
| **Consider committing `appsettings.Development.json`** | It's a tracked file that's also gitignored — confusing. New test setup ran into it. | Either remove the file entirely (force tests + dev to inject inline) OR remove the gitignore entry and commit known-safe dev defaults. Pick one; document. |

## 5. Things that turned out NOT to matter

These were front-loaded concerns at Day 0 that haven't bitten us:

- **Docker Desktop unavailable.** Local SQL Server 2019 Developer with Windows Integrated Auth was a strict-upgrade for Dev — no startup latency, no port conflicts, no `docker compose down`-then-up dance.
- **Polly v8 vs v7 retry tuning.** The default exponential-backoff + jitter pipeline (`MaxRetryAttempts=3`, `Delay=2s`) was right out of the gate. The zero-delay test mirror proved we could assert retry behaviour in sub-millisecond tests without touching the production pipeline.
- **MediatR overhead.** No measurable handler-resolution cost vs direct dispatch in the test profiling we did. Worth the source-generated `IRequestHandler<>` registration ergonomics.
- **EF Core InMemory provider quirks.** We were warned it diverges from SQL Server behaviour. For Week-1 happy-path tests it never did — every test that passed on InMemory also passed when applied to local SQL via the migration.

## 6. Open backlog (carried into Week 2+)

### Manual staging exercise (one ~hour session)
- T3.7 — paste PII-masked branches response
- T5.7 — first real Save against Tajeer Rabet staging
- T5.8 — paste sanitized Save request/response
- T6.7 — ngrok tunnel + register webhook URL with Tajeer
- T6.8 — end-to-end smoke (POST save → wait for real webhook → assert Active)
- T6.9 — flip `Tajeer:Webhook:LogOnly = false`
- T7.8 — full Week-1 done-criteria walkthrough + video / screenshots

### Loop-back tasks (gated on external provisioning)
- Azure dev landing zone (L.A1-L.A4) — Bicep + KV + App Insights wiring
- Entra ID (L.E1-L.E3) — replace DevJwtStub
- GitHub CI (L.G1-L.G3) — pipeline + branch protection + secrets
- Real Redis (L.R1-L.R4) — when Docker Desktop fixed or Memurai installed
- Unifonic (L.U1-L.U3) — real `UnifonicSmsSender` against the port

### Week 2 critical-path inputs
- `design.md` from user — required for Days 11-14 (any Next.js / shadcn / form work)

## 7. Final metrics

| Metric | Value |
|---|---|
| Commits on `main` from this workstream | 8 |
| Files added | 76 (Domain + Application + Infrastructure + Adapters.Seed + Adapters.Tajeer + Adapters.Tajeer.InMemory + Adapters.Sms.InMemory + BFF + BFF.Tests + Application.Tests + plan/notes/retro) |
| Net LOC delta vs scaffold | +14,234 / -312 across all 8 commits |
| Production test count | 153 |
| Smoke (excluded by default) | 1 (`TajeerStagingSmokeTests`, ready to run with creds) |
| Build warnings | 0 |
| `dotnet build -warnaserror` | clean |
| Days where I had to revert a commit | 0 |
| Workstreams opened | 2 (`2026-05-17-week-1-foundation-tajeer-happy-path` + inserted `2026-05-24-domain-deepening-production-seed`) |

---

**Verdict**: Week 1's code-side goal — "a `dev/save-contract` endpoint hitting Tajeer Rabet end-to-end" — is **functionally complete**. The remaining boxes are 7 manual staging items + the cross-cutting loop-back work. Both depend on external inputs (real Tajeer credentials, ngrok account, Azure subscription, Entra tenant, Unifonic sandbox, GitHub admin).

Week 1 was a clean win on TDD discipline and on the architecture's ability to absorb mid-workstream scope changes (the Domain Deepening insertion was the proof). The Phase-1 horizon now looks like: 1 manual session unlocks 7 boxes + the demo, then `design.md` unblocks Weeks 2-4.
