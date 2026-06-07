# Retrospective — Quotation Aggregate (Day 22 foundation)

**Date**: 2026-06-07
**Branch**: `feat/quotation-aggregate`
**Outcome**: ✅ Shipped — Quotation aggregate foundation, pure domain, 27 new tests.

---

## What landed

- `Domain/Sales/` namespace (10 files):
  - Enums: `QuotationStatus`, `QuotationApprovalStatus`, `QuotationContractType`, `QuotationItemType`.
  - `ApprovalTier` (per-tenant config entity) + `ApprovalTierEvaluator` (pure: total + tiers → ordered required tiers).
  - `QuotationLine` (computes `LineTotalSar`), `QuotationApproval` (decision state machine), `Quotation` (root, full Spec 02 §4.1 lifecycle + 15% VAT pricing).
  - Events: `QuotationSubmittedForApprovalDomainEvent`, `QuotationApprovedDomainEvent`.
- Tests: `Application.Tests/Sales/` — `ApprovalTierEvaluatorTests` (6) + `QuotationTests` (21).
- Spec 02 §4.1 note documenting the under-threshold submit refinement.

## Verification

- `dotnet build AutoLeaseNet.sln -warnaserror` → 0 warnings, 0 errors.
- `dotnet test AutoLeaseNet.sln --settings .runsettings` → **384 green / 0 failed** (Application 113 → 140, +27).

## Decisions

- **Auto-approve under threshold** instead of jumping straight to `SentToCustomer` — keeps send a separate explicit action. Documented in Spec 02 §4.1.
- **QuoteNumber supplied to `CreateDraft`** — sequence generation is an app/repo concern, keeps the aggregate pure (same call as `Lease`/`TajeerContractNumber`).
- **Evaluator handed in, not called inside the aggregate** — domain stays config-free; the submit-time snapshot is immune to later `ApprovalTier` edits (Spec 02 §4.1 invariant).
- **Sequential tier decisions** — a decision must target the lowest still-`Pending` tier; re-deciding a settled row is idempotent (retry/webhook safety, matching the repo-wide same-state-re-entry rule).

## Carry-forward (the immediate next slices)

1. **EF config + migration** `Add_Quotation_Aggregate` (4 tables) + RLS predicates (Quotation/Line/Approval are tenant-scoped; ApprovalTier is tenant config). `Quotation`/`QuotationLine` external-customer read predicate TBD (likely internal-staff-only like Branches).
2. **ApprovalTier seed** (Tier 1/2/3 by amount) in `Adapters.Seed` — completes Day 22.
3. **Day 23**: approval workflow saga + `GET /approvals/pending` inbox + submit/approve/reject endpoints + UI. Role authorization against current DB state (Spec 08 §11) lives in the command handlers, not the aggregate.
4. **Remaining lifecycle events** (`QuotationSentToCustomer`, `QuotationAcceptedByCustomer` → lease provisioning trigger, `QuotationClosed{reason}`) added when their consumers land (Day 24–25), per the repo's forward-declared-event pattern.

## Notes for next session

The aggregate is intentionally persistence-agnostic. Next slice is mechanical (EF config mirrors Incident/Inspection configs) but must add the four tables to `dbo.TenancyPolicy` RLS — don't skip that or the tenancy floor regresses.
