# Retrospective — Day-9 RLS tenant isolation

**Date closed**: 2026-05-29
**PR**: TBD
**Commit**: TBD

## What shipped

Tenant isolation is now enforced at three layers, with the database-engine layer
landing for the first time:

1. **App layer** (unchanged) — repositories still pass `TenantId` to every query.
2. **Connection layer** — `TenancyConnectionInterceptor` writes
   `SESSION_CONTEXT('TenantId' | 'CustomerId' | 'UserType')` on every
   `SqlConnection` open.
3. **Row layer** — `dbo.fn_TenancyPredicate` + `dbo.TenancyPolicy` filter every
   read and BLOCK every cross-tenant write on 9 aggregate-root tables.

End-to-end proof:
- 5 RLS isolation integration tests (`Category=Integration`) pass against
  `AutoLeaseNet_Dev`: two-tenant filter, write-block, no-session fail-closed,
  WEBHOOK_BOOTSTRAP override.
- Manual BFF smoke: real seeded tenant returns its 20 customers; an unknown
  tenant id returns `{ items: [], totalCount: 0 }` with no app-side filter — RLS
  alone hides the rows.
- Full test suite green: 264 tests (was 256; +6 SystemTenancyScope + 2
  TenancyConnectionInterceptor unit; the 5 RLS integration tests are gated
  out by `Category!=Integration`).

## Carry-forward

- **Always Encrypted on PII** (originally bundled in Day-9 but split per
  user direction; AKV not yet provisioned). `Person.IdNumber`,
  `Driver.DriverLicenseNumber`, future IBAN, plus
  `Incident.PoliceReportNumber` + `InsuranceClaimNumber` from PR #17. Needs a
  CMK/CEK source decision: local self-signed cert (works for dev/CI but won't
  match prod) vs wait for Azure Key Vault provisioning.
- **RLS on child tables** (`InspectionPhotos`, `InspectionDamageMarkers`).
  Both lack `TenantId` today; need a backfill migration before policy can
  apply. App-level defense suffices for Phase 1 because both are only ever
  loaded via the parent aggregate.
- **Per-tenant webhook URL** — retires the `WEBHOOK_BOOTSTRAP` predicate
  override and the cross-tenant `Lease` lookup. Phase 2.
- **Append-only PII access audit** (Spec 01 §3.6 mitigation) — separate
  workstream.
- **Outbox + drain** — still the next item on the Phase-1 hardening list per
  `ai_context.md` recommendation #2.

## What went well

- The existing `SqlSessionContext` helper from earlier work and the
  `DomainEventDispatchInterceptor` template made the new interceptor a 50-line
  file. No infrastructure design needed.
- `SystemTenancyScope` as AsyncLocal worked first try — including the
  flows-across-await-boundaries test, which is the contract the seeder depends
  on.
- Two-tenant integration test using raw ADO.NET (bypassing EF entirely) gives
  the strongest possible proof: even malicious app code that omits
  `WHERE TenantId = …` can't see cross-tenant rows.
- BFF smoke under wrong-tenant header returned `totalCount: 0` — RLS prevents
  leakage even when the app forgets to filter.
- `dotnet ef migrations add` with an empty model diff + raw `migrationBuilder.Sql`
  was the right pattern. Up + Down are 80 lines of SQL and trivially readable.

## What surprised me

- **The `WEBHOOK_BOOTSTRAP` override was forced.** I started thinking we could
  apply RLS to Leases cleanly and resolve the webhook's cross-tenant lookup
  via "just use SYSTEM for the right tenant." But until the webhook resolves
  the tenant, there is no right tenant. The override is acknowledged tech debt
  with a clear Phase-2 retirement plan.
- **Namespace collision**: `Tenancy` (the new record) lives in
  `AutoLeaseNet.Application.Ports.Tenancy`, and the BFF impl lives in
  `AutoLeaseNet.Bff.Tenancy`. Inside the BFF impl, the bare token `Tenancy`
  resolved to the BFF namespace. Solved with a `using TenancyValue = …` alias
  in that one file. Future workstreams adding ports under `*.Tenancy.*` will
  hit the same; worth a one-line note in `CLAUDE.md`.
- **Customers table has more NOT NULL columns than I expected** — `KycVerified`
  and `PiiOptedOut` defaults aren't set at the column level. The integration
  test's raw INSERT failed once before I checked `sys.columns`. Add to the
  unwritten "new aggregate recipe" the rule: any bool column should default
  to `0` at the DB level so raw inserts in test fixtures aren't fragile.

## What I'd do differently

- Spec 01 §3.4 already had a near-final predicate. I could have lifted it
  verbatim instead of re-deriving the structure. Lost ~5 min.
- The PoorMan's "is this CI test gated?" check (running with
  `--filter "Category!=Integration"`) should be the very first thing I do
  before writing any new Integration-marked test. I ran the full suite first
  and momentarily worried about the 5 new RLS tests showing in CI.

## Numbers

- Files added: 7
  (`ITenancyAccessor.cs`, `SystemTenancyScope.cs`, `TenancyConnectionInterceptor.cs`,
  `ClaimsAndSystemTenancyAccessor.cs`,
  `20260529012701_Add_RLS_TenancyPolicy.cs` + `.Designer.cs`,
  `RlsIsolationTests.cs`, `SystemTenancyScopeTests.cs`,
  `TenancyConnectionInterceptorTests.cs`).
- Files modified: 4 (`ServiceCollectionExtensions.cs`, `Program.cs`,
  `TajeerWebhookEndpoints.cs`, plan/retro).
- Migration applied to local `AutoLeaseNet_Dev`.
- Tests: 256 → 264 default (+6 + 2). Plus 5 integration tests gated on local SQL.
- Total elapsed: ~75 min (plan + code + tests + smoke + docs).

## Hand-off

The Phase-1 hardening sprint is one third done. Carry-forward picklist for the
next "continue":

1. **Outbox + DbContext-interceptor write + BackgroundService drain** — closes
   the saga consistency window across all four Tajeer-touching commands.
   Pattern is established (interceptor + scoped pipeline registration in
   `AddAutoLeaseNetDbContext`); this is the second item where the Day-9 plumbing
   pays back.
2. **Reconciliation BackgroundService skeleton** — ½ day; locks in the
   scheduling pattern even before the first reconciliation check is wired.
3. **Customer Portal scaffold** — even without `design.md`, a read-only fleet
   list + invoice list + lease list using the existing `bff-client.ts` pattern.
4. **ZATCA adapter** — Week-4 critical path; ~5-day workstream once it starts.

Each one is its own PR per the established cadence.
