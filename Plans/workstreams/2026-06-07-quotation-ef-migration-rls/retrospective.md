# Retrospective — Quotation Persistence (EF + Migration + RLS)

**Date**: 2026-06-07
**Branch**: `feat/quotation-ef-migration-rls`
**Outcome**: ✅ Shipped — Quotation aggregate is now persistable + tenant-isolated.

---

## What landed

- `QuotationConfiguration.cs` — four `IEntityTypeConfiguration<T>`: `Quotation`
  (root, backing-field navigations to children), `QuotationLine`,
  `QuotationApproval`, `ApprovalTier`. Money `DECIMAL(18,2)`, percent
  `DECIMAL(5,2)`, enums → `int`/`tinyint`, `RowVersion`, tenant-scoped unique +
  lookup indexes (incl. the approver-inbox index `(TenantId, Status, RequiredRoleCode)`).
- 4 DbSets on `AutoLeaseNetDbContext`.
- Migration `20260607172013_Add_Quotation_Aggregate` — EF-generated table DDL
  (4 tables, 2 child FKs cascade, 8 indexes) **+ hand-appended RLS**:
  `ALTER SECURITY POLICY dbo.TenancyPolicy ADD …` for all four tables in `Up`,
  reversed (`DROP … PREDICATE` before `DropTable`) in `Down`. Snapshot updated.

## Verification

- `dotnet build AutoLeaseNet.sln -warnaserror` → 0/0.
- `dotnet test --settings .runsettings` → **384 green / 0 failed** (no new tests;
  this slice is config validated by EF model-build + migration generation; EF
  InMemory tests ignore migrations). RLS SQL is exercised by gated
  `Category=Integration` tests on a SQL-equipped machine (a future slice can add
  `QuotationsRlsIsolationTests` mirroring `VehiclesRlsIsolationTests`).

## Decisions

- **All four tables internal-only** (`fn_TenancyPredicate(TenantId, NULL)`) —
  consistent with Inspections/Incidents. Quotation has a `CustomerId` but no
  customer-portal quote surface exists yet; closing external read until that
  slice avoids exposing data through an untested path. Documented as a Phase-2
  follow-up in the migration remark.
- **RLS in the same migration as table creation** (not a separate migration like
  Day-9) — these are brand-new tables, so `ADD` predicates is atomic and safe;
  the separate-migration pattern was only needed for the Vehicles in-place
  predicate *swap*.
- **Children carry RLS too** (defense in depth, CLAUDE.md rule #4) — they have
  `TenantId` and writes flow through the tenancy-aware DbContext.

## Carry-forward (unchanged order)

1. **`ApprovalTier` seed** (Tier 1/2/3 by amount) in `Adapters.Seed` — finishes
   Day 22. Without it, the evaluator returns "no tiers" for every quote.
2. **Repository + query handlers + endpoints + approval saga + inbox** (Day 23).
3. **`QuotationsRlsIsolationTests`** (Integration) — when a SQL machine is in the
   loop.
4. Phase-2: swap `Quotations` RLS to a CustomerId predicate for the customer
   accept-quote view.

## Notes for next session

Apply the migration on the local SQL dev DB before the Day-23 endpoints can be
smoke-tested: `dotnet ef database update` (set `AUTOLEASENET_MIGRATIONS_CONNECTION`
or use the local-dev fallback). Not done here — this machine has no local SQL up
(readiness health test logs the expected `SqlException`).
