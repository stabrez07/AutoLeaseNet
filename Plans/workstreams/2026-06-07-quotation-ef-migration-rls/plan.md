# Workstream — Quotation Persistence: EF Config + Migration + RLS

**Date**: 2026-06-07
**Slug**: `2026-06-07-quotation-ef-migration-rls`
**Branch**: `feat/quotation-ef-migration-rls`
**Follows**: [`2026-06-07-quotation-aggregate`](../2026-06-07-quotation-aggregate/) (PR #31 — domain foundation)
**Plan ref**: [Plan 02 — Week 4, Day 22](../../02-phase-1-mvp-week-by-week.md#week-4--quotation--zatca-invoice--demo-polish)

---

## Goal

Make the Quotation aggregate persistable: EF Core configurations for the four
new tables, DbSets on the context, the `Add_Quotation_Aggregate` migration, and
the **RLS extension** wiring those tables into `dbo.TenancyPolicy` (CLAUDE.md
rule #4 — the DB-engine tenancy floor must not regress when tables are added).

## Scope

- `QuotationConfiguration.cs` — `Quotation` root + `QuotationLine` +
  `QuotationApproval` children (backing-field navigations) + `ApprovalTier`
  config entity. Money `DECIMAL(18,2)`, percent `DECIMAL(5,2)`, enums → `int`/
  `byte`, `RowVersion`, tenant-scoped indexes.
- 4 DbSets on `AutoLeaseNetDbContext`.
- Migration `Add_Quotation_Aggregate` (EF-generated table DDL) with a
  hand-appended `ALTER SECURITY POLICY dbo.TenancyPolicy ADD …` block for the
  four tables (and matching `DROP`/re-`ADD`-less `Down`).

## RLS decision

All four tables scope **internal-only** in Phase 1 via
`fn_TenancyPredicate(TenantId, NULL)` — same conservative stance as
Inspections / Incidents (Day-9). Quotation *has* a `CustomerId`, but no customer
portal surface reads or accepts quotes yet, so external read stays closed until
that slice lands (Phase-2 follow-up: swap Quotations to a CustomerId predicate
when the customer accept-quote view exists). Children + config: `NULL`.

## RED → GREEN tasks

- [ ] T1. EF configs compile; model builds.
- [ ] T2. DbSets added.
- [ ] T3. `dotnet ef migrations add Add_Quotation_Aggregate` succeeds
      (design-time, no DB needed).
- [ ] T4. Append RLS `ALTER SECURITY POLICY` to migration `Up`; `Down` drops the
      four predicate pairs before the table drops.
- [ ] T5. `dotnet build -warnaserror` clean.
- [ ] T6. `dotnet test --settings .runsettings` → full suite green (EF InMemory
      tests ignore the migration; behaviour unchanged).

## Definition of done

- [ ] Migration + snapshot generated and reviewed (4 tables, FKs, indexes).
- [ ] RLS predicates present for all four tables; `Down` reverses them.
- [ ] Build clean, suite green (was 384).
- [ ] `ai_context.md` + `retrospective.md` updated.
- [ ] PR green; squash-merge.

## Out of scope / carry-forward

- `ApprovalTier` seed data (Tier 1/2/3 by amount) in `Adapters.Seed` — next
  slice, finishes Day 22.
- Repository + query handlers + endpoints + saga — Day 23.
- Applying the migration to a SQL DB / Integration RLS tests — runs on a
  SQL-equipped machine (gated `Category=Integration`), not in CI.
