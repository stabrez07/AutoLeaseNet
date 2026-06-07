# Workstream — Quotation Aggregate (Phase 1, Week 4, Day 22 foundation)

**Date**: 2026-06-07
**Slug**: `2026-06-07-quotation-aggregate`
**Branch**: `feat/quotation-aggregate`
**Plan ref**: [Plan 02 — Week 4, Day 22](../../02-phase-1-mvp-week-by-week.md#week-4--quotation--zatca-invoice--demo-polish)
**Spec refs**: [Spec 01 §5.4](../../../Specs/01-multi-tenancy-and-domain-model.md#54-sales-quotation) (entities), [Spec 02 §4.1](../../../Specs/02-state-machines-and-sagas.md#41-quotation) (state machine), [Spec 02 §6.1](../../../Specs/02-state-machines-and-sagas.md#61-quote-approval-workflow-saga) (saga), [Spec 08](../../../Specs/08-approval-workflow-engine.md) (engine placeholder)

---

## Goal

Land the **pure-domain foundation** for the Sales Quotation aggregate: the
`Quotation` root, its `QuotationLine` + `QuotationApproval` children, the
`ApprovalTier` config entity, the tier evaluator, the full state machine
(Spec 02 §4.1), and the two domain events downstream sagas need. Zero external
dependencies — same "aggregate foundation" slice shape as Inspection (PR #12)
and Incident (PR #17).

This is the first of the Week-4 quotation slices. Explicit **next slices** (out
of scope here, listed so they don't drift):

1. EF Core configuration + migration `Add_Quotation_Aggregate` (4 tables) + RLS
   predicates on the new tenant-scoped tables.
2. `ApprovalTier` seed data (Tier 1/2/3 by amount) in `Adapters.Seed`.
3. Quote approval workflow saga + approver-inbox + submit/approve/reject
   endpoints (Day 23).
4. Quote PDF (QuestPDF) + send-to-customer (Day 24).

## Scope (this PR)

- `Domain/Sales/` namespace:
  - Enums: `QuotationStatus`, `QuotationApprovalStatus`, `QuotationContractType`,
    `QuotationItemType`.
  - `ApprovalTier` (per-tenant config entity) + `ApprovalTierEvaluator` (given
    `TotalSar` + tiers → ordered required tiers).
  - `QuotationLine` (child, computes `LineTotalSar`).
  - `QuotationApproval` (child, decision state machine: Pending →
    Approved/Rejected/Recalled).
  - `Quotation` (aggregate root, full Spec 02 §4.1 state machine + pricing).
  - Events: `QuotationSubmittedForApprovalDomainEvent`,
    `QuotationApprovedDomainEvent`.
- Unit tests in `Application.Tests/Sales/`.

## Design decisions

- **Pricing**: `SubTotalSar = Σ LineTotalSar` (lines already net of line-level
  discount); quote-level `DiscountPercent` applied on subtotal; `VatSar =
  (SubTotal − discount) × 15%` (KSA standard rate, const for Phase 1 — config
  in Phase 2); `TotalSar = taxable base + VAT`. Recomputed on every line
  mutation while in `Draft`.
- **QuoteNumber** is supplied to `CreateDraft` (sequence generation belongs to
  the app/repo layer — keeps the aggregate pure, same reasoning as
  `Lease`/`TajeerContractNumber`).
- **Submit** computes required tiers from the passed-in `ApprovalTier` set
  (evaluator is called by the app layer, result handed in — domain stays
  config-free). Snapshots `QuotationApproval` rows at submit time (Spec 02 §4.1
  invariant). If **no** tier is required → auto-`Approved` + `QuotationApproved`
  event; else → `PendingApproval` + `QuotationSubmittedForApproval` event. (This
  refines the diagram's "Draft → SentToCustomer (under thresholds)": send stays
  a distinct explicit action so PDF/send isn't conflated with approval. Spec
  note added.)
- **Tier ordering**: a decision must target the lowest-level still-`Pending`
  tier; approving tier N+1 before N throws.
- **Idempotency**: same-state re-entry returns silently (`MarkSentToCustomer`,
  `Accept`, etc.), matching every other aggregate in this repo.
- **Recall → Withdrawn**: allowed from Draft, PendingApproval (only if no tier
  approved yet), Approved, SentToCustomer; flips outstanding `Pending`
  approvals to `Recalled`.

## RED → GREEN task list

- [ ] T1. Enums (4) compile.
- [ ] T2. `ApprovalTier` + `ApprovalTierEvaluator`; test: amount selects correct
      ordered tiers / empty when under all thresholds.
- [ ] T3. `QuotationLine.Create` computes `LineTotalSar` incl. line discount.
- [ ] T4. `QuotationApproval` decision transitions + guards.
- [ ] T5. `Quotation.CreateDraft` → Draft, totals = 0, validates inputs.
- [ ] T6. `AddLine` recomputes SubTotal/VAT/Total; rejected outside Draft.
- [ ] T7. `SubmitForApproval` with tiers → PendingApproval + snapshot rows +
      event; with no tiers → Approved + event; rejects empty quote / non-Draft.
- [ ] T8. `RecordApproval` sequential approve → final Approved + event; reject →
      Rejected; out-of-order tier throws; idempotent re-decision.
- [ ] T9. `Recall` → Withdrawn (+ recalls pending rows); blocked once a tier
      approved.
- [ ] T10. `MarkSentToCustomer` / `Accept` / `RejectByCustomer` / `MarkExpired`
      transitions + guards + idempotency.
- [ ] T11. `dotnet test AutoLeaseNet.sln --settings .runsettings` green.

## Definition of done

- [ ] All new unit tests pass; full suite still green (was 368).
- [ ] `dotnet build` clean (treat-warnings-as-errors).
- [ ] `ai_context.md` updated (aggregate added, carry-forward refreshed).
- [ ] `retrospective.md` written.
- [ ] Spec 02 §4.1 note added for the submit-under-threshold refinement.
- [ ] PR opened; CI green; squash-merge.

## Out of scope / carry-forward

EF config + migration + RLS, ApprovalTier seed, saga + endpoints + inbox, PDF +
send, the remaining lifecycle events (`SentToCustomer`/`Accepted`/`Closed`) —
added when their consumers (Day 23–25) land, per the repo's forward-declared
event pattern.
