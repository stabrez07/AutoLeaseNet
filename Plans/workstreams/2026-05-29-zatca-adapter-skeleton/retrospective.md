# Retrospective — ZATCA adapter skeleton

**Date closed**: 2026-05-30
**Branch**: `feat/zatca-adapter-skeleton`
**Predecessors**: All Customer Portal demo PRs (#22–#27)
**Test delta**: 351 → **368 green** (+17)

## What landed

- **Three new packages** (one PR, all wired through DI + tests + sln):
  - `packages/adapters/AutoLeaseNet.Adapters.Zatca` — Pattern B contract surface.
  - `packages/adapters/AutoLeaseNet.Adapters.Zatca.InMemory` — deterministic fake.
  - `packages/adapters/AutoLeaseNet.Adapters.Zatca.Tests` — 12 tests (5 ChainState entity + 4 InMemory client + 1 real-client stub + 2 idempotency / observability).
- **Domain**: `AutoLeaseNet.Domain.Zatca.ZatcaChainState` — per-tenant aggregate-of-one with `AdvanceTo(hash, when)` + `Reset(when)`. The "only advance on CLEARED" rule is documented on the entity but enforced at the saga layer (Week-4), mirroring `Lease.MarkIssued`.
- **Port**: `AutoLeaseNet.Application.Ports.Persistence.IZatcaChainStateRepository` — `GetOrCreateAsync` + `Save`.
- **Infrastructure**: `EfZatcaChainStateRepository`, `ZatcaChainStateConfiguration` (PK on `TenantId`), DbContext registration, migration `20260529232659_Add_ZatcaChainState`. 5 EF repo tests in `Infrastructure.Tests`.
- **Adapter surface**: `IZatcaClient.SubmitInvoiceAsync(SubmitInvoiceRequest, ct) → IntegrationResult<SubmitInvoiceResponse>`. DTOs `SubmitInvoiceRequest { Uuid, InvoiceType (Tax|Simplified), InvoiceXml, InvoiceHash, PreviousInvoiceHash }`, `SubmitInvoiceResponse { Uuid, Status, ClearedAtUtc?, Warnings[] }`, enums `ZatcaResultStatus { Cleared | Reported | WarningCleared | Rejected }`, `ZatcaMode`, `ZatcaEnvironment`.
- **Real `ZatcaClient` HTTP class**: fully wired (named HttpClient + `ZatcaAuthHandler` + Polly resilience), `SubmitInvoiceAsync` returns `IntegrationResult.Failure("zatca.not_yet_implemented", isTransient: false)` and logs a structured warning. Pinned by `ZatcaClientStubReturnsNotImplementedTests`.
- **`InMemoryZatcaClient`**: defaults to `Cleared`; `SeedRejection(uuid)` overrides; `SubmitCalls` history; idempotent on UUID.
- **DI**: `AddZatca`, `AddInMemoryZatca`, `AddZatcaWithModeSwitch(section)` mirroring Tajeer exactly.
- **BFF wiring**: `Program.cs` calls `AddZatcaWithModeSwitch`; `ZatcaHealthCheck` registered alongside `SqlHealthCheck` on the readiness probe.
- **Config**: `appsettings.Development.json` gains a `Zatca` section (`Mode=InMemory` by default); `appsettings.json` already had a `Zatca` section that the new shape extended with `AuthorizationToken` + `TimeoutSeconds` + `Mode`.
- **Test host defaults**: `BffTestHostDefaults.Defaults()` gains 4 keys (`Zatca:BaseUrl`, `Zatca:Environment`, `Zatca:AuthorizationToken`, `Zatca:Mode`) so existing factories pick the InMemory adapter without per-factory boilerplate.

## What worked

- **`BffTestHostDefaults` paid off the moment it was tested**. Adding the 4 ZATCA keys was a 5-line edit in one place; the 13 BFF test factories from PR #25 inherited the new defaults with zero per-factory changes. No retro item about "I forgot to add Zatca to factory N". The `dotnet test` totals went from 351 → 368 with no factory-level fix needed.
- **`dotnet ef migrations add` Just Worked** with the existing `AutoLeaseNetDbContextFactory` design-time factory — no startup project flag confusion. The migration shape matched expectations (PK on `TenantId`, all auditing columns, RowVersion).
- **Mirroring Tajeer wholesale paid for itself.** The `AddZatcaWithModeSwitch` / `ReadMode` / `Replace<IZatcaClient>` pattern was a near-line-for-line port of the Tajeer pattern. Zero new design decisions to make at composition time — anyone who's read the Tajeer wiring already understands the Zatca wiring.
- **Default `ZatcaMode.Real` (not `InMemory`) made sense.** Real defaults to the clear-error stub, so a production env that forgot to set `Zatca:Mode=InMemory` for staging tests would fail loudly with `zatca.not_yet_implemented` instead of silently no-op'ing against the fake. The dev `appsettings.Development.json` flips to InMemory so the demo keeps working.

## What surprised me

- **`Microsoft.Extensions.Options.DataAnnotations` is a separate NuGet package.** Not transitively pulled by `Microsoft.Extensions.Options.ConfigurationExtensions`. Tajeer's csproj DOES NOT explicitly reference it either — it appears to come transitively through `Microsoft.Extensions.Http.Resilience` 9.0.0 (or one of its companions). Adapters.Zatca with the same package list initially failed to compile until I added the explicit reference. Worth a tiny investigation later — probably the Resilience package's transitive graph differs slightly between Tajeer's environment cache and the fresh Zatca restore.
- **CA1848 (LoggerMessage source-gen) is enforced as an error.** A single `_logger.LogWarning(...)` call in `ZatcaClient` failed the build. Easy fix (convert to `partial class` + `[LoggerMessage]`) but the friction matters: any future stub that wants a "this is unfinished" log line has to pay the source-gen tax. The existing convention is consistent (Tajeer uses `[LoggerMessage]` exclusively), so the right move is just to follow it.
- **Saga-layer policy on a passive entity reads better than I expected.** The original temptation was to enforce "only advance on Cleared" inside `ZatcaChainState.AdvanceTo` itself — but doing so would have couplied the entity to the `ZatcaResultStatus` enum (which lives in the adapter package, not Domain). Keeping the entity passive and documenting the policy as the saga's job kept the dependency direction clean. The XML docs on `AdvanceTo` are explicit enough that any future editor sees the "what the saga must do" contract immediately.

## What I'd do differently next time

- **Pre-check existing csproj package refs against `Directory.Packages.props` before adding a new adapter.** Would have caught the `Microsoft.Extensions.Options.DataAnnotations` miss before the first build run.
- **Write a 5th test for the empty-string hash early.** The Theory-with-InlineData pattern was added late; should be the first defensive check on every entity-mutation method.

## Carry-forward for next workstream(s)

These are intentionally out of scope for the Phase-1 prep PR, and will be picked up by the Week-4 invoice workstream:

- **UBL 2.1 XML generation + xAdES-BES signing + TLV QR encoding** (the actual ZATCA Phase-2 deliverable). Hooks: `SubmitInvoiceRequest.InvoiceXml` carries the rendered UBL; the saga builds it via a soon-to-be-created `IUblInvoiceBuilder`.
- **`ZatcaSubmission` aggregate** (Spec 02 §4.5 state machine: PENDING/PROCESSING/CLEARED/WARNING/REPORTED/REJECTED/NETWORK_ERROR/DEAD_LETTER). Lands together with the Invoice aggregate.
- **`IZatcaChainBreakDetector`** — saga-level concern, requires ZatcaSubmission first.
- **CSR / CSID lifecycle** (`OnboardEgsAsync`).
- **`appsettings.Development.json` real CSID material** — currently `"Bearer dev-placeholder-token"`; flip when we move from sandbox-stub to sandbox-real.
- **Apply the migration to local `AutoLeaseNet_Dev`** — the migration was generated but not applied to local SQL in this PR. Will be applied when the next dev session runs `dotnet ef database update`. The model is exercised end-to-end via EF Core InMemory in the test suite, so the table absence on local SQL doesn't block anything.

## Carry-forward (already-named items, not advanced this PR)

- `BffTestSeedWaiter` extract — FOUR retros asking. Pattern: every factory that needs demo data does a poll loop until `Leases.AnyAsync(l => l.Status == Active)`. Should be a `BffTestHostDefaults.WaitForDemoSeedAsync()` helper.
- Vehicle Replacement Saga (subscribes to `IncidentReportedDomainEvent`).
- Close-saga refactor → centralise on `TajeerStatusMapper`.
- Phase-2 Vehicles RLS extension to support customer-derived predicates — would collapse all three trust-boundary handlers (`GetMyVehicles`, `GetMyLeaseDetail`, `GetMyVehicleDetail`) to single LINQ joins.
- Always Encrypted on PII columns (gated on Azure Key Vault or local-cert).
- next-intl + `[locale]` segments migration for the customer portal.
