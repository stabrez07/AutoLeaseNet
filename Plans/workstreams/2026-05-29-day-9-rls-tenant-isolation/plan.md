# Day-9 RLS — SQL Row-Level Security for tenant isolation

**Date**: 2026-05-29
**Branch**: `feat/day-9-rls`
**PR**: TBD

## Why this, why now

`ai_context.md` flagged the 1️⃣ recommendation: the BFF currently relies entirely on
application-side `TenantId` filtering. Every new aggregate (5 in last 6 PRs) widens
the defense-in-depth gap. Before any non-dev tenant is provisioned, RLS needs to
enforce isolation at the DB layer — a buggy `Where(x => x.TenantId == ctx.TenantId)`
that someone forgets to add cannot leak cross-tenant rows.

**Spec reference**: [Spec 01 §3.4](../../../Specs/01-multi-tenancy-and-domain-model.md#34-sql-row-level-security)
defines the predicate. The `SqlSessionContext` helper + tests already exist (PR #?).
The `TenancyMiddleware` only opens a logging scope today — it does NOT wire
`SESSION_CONTEXT` into the DbContext connection. That's the gap.

## Scope (this PR)

- ✅ RLS predicate function (`dbo.fn_TenancyPredicate`) + `CREATE SECURITY POLICY`
  applied to 9 aggregate-root tables.
- ✅ EF Core `DbConnectionInterceptor` that calls `SqlSessionContext.SetTenancyAsync`
  on `ConnectionOpenedAsync`, scoped to the current request's tenancy.
- ✅ `ITenancyAccessor` port + `SystemTenancyScope` (AsyncLocal) for non-request
  callers (seeder, webhook tenant resolution).
- ✅ Two-tenant isolation integration test (Category=Integration; runs locally only).
- ✅ Migration applied to local `AutoLeaseNet_Dev`; full test suite green.

## NOT in scope (defer)

- ❌ **Always Encrypted on PII columns** — user-confirmed split. Needs CMK/CEK
  strategy decision (local cert vs Azure Key Vault); AKV not yet provisioned.
  Follow-up workstream.
- ❌ **RLS on child tables** (`InspectionPhotos`, `InspectionDamageMarkers`).
  They lack `TenantId` today; adding it is a backfill migration. App-level
  defense-in-depth is sufficient for Phase-1 because children are only loaded
  via their aggregate root. Phase-2 follow-up.
- ❌ **RLS on `WebhookLog`** — webhooks arrive anonymously; the row exists
  precisely so the cross-tenant lookup `GetByTajeerContractNumberAcrossTenantsAsync`
  can resolve the owning tenant. Phase-2 will encode tenant in the webhook URL
  and retire the cross-tenant lookup.
- ❌ **Audit log on PII access** — Spec 01 §3.6 mitigation; separate workstream.
- ❌ **Per-tenant webhook URL** — Phase-2.

## Design

### Three-layer enforcement

1. **App layer** — `EfXxxRepository.GetByIdAsync(id, tenantId, ct)` continues to
   filter by `TenantId`. No change. (Keeps the API surface clean and gives early
   failures when the wrong tenancy slips in.)
2. **DB connection layer (new)** — `TenancyConnectionInterceptor` sets
   `SESSION_CONTEXT('TenantId' | 'CustomerId' | 'UserType')` on every connection
   the DbContext opens.
3. **DB row layer (new)** — RLS predicate function reads `SESSION_CONTEXT` and
   filters every row, regardless of what the app's WHERE clause looks like.

### Tenancy resolution order

```
SystemTenancyScope.Current        // Set by seeder / webhook bypass — wins
  → request JWT claims            // Set by DevJwtStubHandler (Phase 1) / JwtBearer (Phase 2)
    → null                        // Anonymous request; interceptor skips SESSION_CONTEXT.
                                  //   RLS predicate evaluates to 0 → zero rows visible.
                                  //   Webhook receiver uses SystemTenancyScope BEFORE
                                  //   touching repositories, so it never hits this state.
```

### Predicate (matches Spec 01 §3.4 verbatim)

```sql
CREATE FUNCTION dbo.fn_TenancyPredicate(
    @TenantId   UNIQUEIDENTIFIER,
    @CustomerId UNIQUEIDENTIFIER
) RETURNS TABLE WITH SCHEMABINDING
AS RETURN
    SELECT 1 AS result
    WHERE
        @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER)
        AND (
            CAST(SESSION_CONTEXT(N'UserType') AS NVARCHAR(50)) IN ('INTERNAL_STAFF', 'SYSTEM')
            OR @CustomerId = CAST(SESSION_CONTEXT(N'CustomerId') AS UNIQUEIDENTIFIER)
        );
```

Internal staff + SYSTEM see all rows in their tenant. External users
(`EXTERNAL_FLEET_ADMIN`, `EXTERNAL_DRIVER`, `EXTERNAL_INDIVIDUAL`) see only rows
matching their `CustomerId`.

### Tables in scope

| Table              | Has CustomerId? | Notes |
|--------------------|-----------------|-------|
| Leases             | yes             | |
| Customers          | self            | predicate against self-Id for CustomerId column |
| Vehicles           | nullable        | external user sees only own-customer-assigned |
| Drivers            | nullable        | same |
| Branches           | null            | predicate degenerates to "internal/system only" for externals (intended — branches are internal config) |
| RentPolicies       | null            | same |
| ExtendedCoverages  | null            | same |
| Inspections        | nullable        | linked via Lease.CustomerId once linked |
| Incidents          | nullable        | same |

For aggregates without a `CustomerId` column, the predicate uses `NULL` and
external users get no rows — which is correct for branches / rent policies /
extended coverages (those are tenant-internal lookups that external portal users
shouldn't see). If/when external users need read access (e.g. customer portal
shows "your branch is X"), we'll add a `CustomerCanRead` view.

## Tasks (RED → GREEN)

1. **Domain port** — add `ITenancyAccessor` + `Tenancy(TenantId, CustomerId?, UserType)`
   record to `Application.Ports.Tenancy`.
2. **System override** — `SystemTenancyScope : IDisposable` (AsyncLocal-backed)
   in same namespace.
3. **Infrastructure interceptor** — `TenancyConnectionInterceptor :
   DbConnectionInterceptor` overriding `ConnectionOpenedAsync`. Resolves
   `ITenancyAccessor`, no-ops on non-`SqlConnection`, no-ops when accessor returns
   null.
4. **Register** in `AddAutoLeaseNetDbContext` alongside `DomainEventDispatchInterceptor`.
5. **BFF accessor impl** — `ClaimsAndSystemTenancyAccessor` reads
   `SystemTenancyScope.Current` first, falls back to JWT claims, returns null when
   neither yields a tenant.
6. **Seeder bypass** — wrap `IDataSeeder.SeedAsync` call site (Program.cs) in
   `using var sys = SystemTenancyScope.For(seedOptions.TenantId)`.
7. **Webhook bypass** — wrap the cross-tenant Lease lookup in
   `SystemTenancyScope.For(Phase1FallbackTenantId)` so the lookup itself isn't
   blocked by RLS; once tenant is resolved from the Lease row, push a real
   tenancy scope for the rest of the handler.
8. **Migration** — `Add_RLS_TenancyPolicy` with raw SQL (`migrationBuilder.Sql`)
   for the predicate function + `CREATE SECURITY POLICY dbo.TenancyPolicy`
   applying filter + block predicates to 9 tables. Down migration drops policy +
   function.
9. **Apply migration** to local `AutoLeaseNet_Dev`.
10. **Test** — `RlsIsolationTests` (Category=Integration): seed 2 tenants × 1 lease
    each, open connection under tenant A's SESSION_CONTEXT, assert query returns
    only tenant A's row. Then re-open under tenant B's context, assert only
    tenant B's row. Then re-open without SESSION_CONTEXT, assert zero rows.
11. **Run full suite** locally (`dotnet test`) — should be 256 still + new
    Integration test (gated on local SQL).
12. **Update ai_context.md** + write retrospective.
13. **Commit, PR, squash-merge.**

## Risks

- **Existing handler tests use EF InMemory** → interceptor must early-return on
  non-`SqlConnection`. Verified in design.
- **Connection pooling** → `SESSION_CONTEXT` is per-connection. EF Core typically
  uses one connection per scope; if a long-lived scope opens multiple
  connections, each gets re-set on open via the interceptor. Safe.
- **`@read_only=1`** means once set, it cannot be changed for the lifetime of
  that connection. If the interceptor fires twice on the same connection
  (shouldn't, but defensive) the second call throws 15664. Mitigation: catch
  15664 silently — the read-only set already happened, so we're fine.
- **Backwards compatibility for tests** → all existing test factories use EF
  InMemory which has no `SqlConnection` → interceptor no-ops. Zero test changes
  expected.
- **Seed running at app startup** → before any HTTP request, `IHttpContextAccessor`
  is null. Without `SystemTenancyScope` bypass, accessor returns null →
  interceptor skips SESSION_CONTEXT → SqlConnection has no tenancy → RLS hides
  all rows including the seeder's freshly-inserted rows. The
  `SystemTenancyScope.For(seedTenantId)` wrap is **required for seeding to work
  at all** under RLS.
- **Migration runs against existing seeded data** → RLS only filters reads; it
  does NOT delete existing rows. Migration apply is safe.

## Definition of done

- [ ] All 9 tasks above complete.
- [ ] `dotnet test AutoLeaseNet.sln --settings .runsettings` green (256+ tests).
- [ ] Local integration test (`Category=Integration`) green against
      `AutoLeaseNet_Dev` proving two-tenant isolation.
- [ ] Local BFF startup against `AutoLeaseNet_Dev` works (seed runs, lookups
      return rows, save-contract succeeds).
- [ ] `ai_context.md` updated with the new architecture decision + migration
      pointer.
- [ ] Retrospective written.
- [ ] PR squash-merged via branch protection.
