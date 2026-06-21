# 09 — Quotation Pricing and Projection Engine

**Status**: Draft v0.1
**Phase**: Phase 1 continuation
**Owner**: Architecture
**Depends on**: [01](./01-multi-tenancy-and-domain-model.md), [02](./02-state-machines-and-sagas.md), [04](./04-integration-architecture.md), [06](./06-bff-api-surface.md)
**Last updated**: 2026-06-21

---

## 1. Purpose

Define the canonical pricing setup model, waterfall calculation sequence, and income projection behavior for quotations and lease-rate computation in AutoLeaseNet.

This document standardizes:

1. Setup masters and effective-dating requirements for reproducible pricing.
2. Waterfall algorithm inputs, sequence, and output contract.
3. Projection logic for periodized contract and fleet profitability views.
4. Implementation boundaries across domain, application, adapters, and portals.

This spec is derived from [Pricing build source](../Plans/AutoLeaseNet_Pricing_Engine_Build_Spec.md) and aligned to repository architecture rules.

---

## 2. Principles

| #   | Principle                  | Rationale                                                                                                                     |
| --- | -------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| 1   | Deterministic pricing      | Same input + same effective setup rows must always produce the same output.                                                   |
| 2   | Historical reproducibility | Setup rates are effective-dated and never hard-deleted when used by existing calculations.                                    |
| 3   | Backend is canonical       | Final pricing authority must live in backend contracts; frontend calculator is for preview/UX only.                           |
| 4   | Explicit strategy modeling | Interest and maintenance strategies (A/B) and replacement strategy (OPEN/PERMANENT) are first-class fields, not hidden flags. |
| 5   | Tenant-safe setup          | Setup is tenant-scoped and must respect tenancy middleware and row isolation.                                                 |
| 6   | Snapshot traceability      | Every finalized quote pricing stores a breakdown snapshot for audit and reconciliation.                                       |
| 7   | Incremental rollout        | Existing quotation flow remains operational while setup parity and backend hardening land in slices.                          |

---

## 3. Setup Master Model

The pricing setup contract includes these master collections:

1. Lease Terms.
2. Interest Rate Table.
3. Residual Value Table.
4. Replacement Policy.
5. Fee Master.
6. Commission Rate Table.
7. Profit Margin Setup.
8. Calendar Periods.

### 3.1 Required behavior

1. All rate-like rows include `effectiveFrom` and optional `effectiveTo`.
2. Edits are non-destructive for historical rows already used in calculations.
3. Setup saves require idempotency and schema validation.
4. Setup payload includes version metadata for migration safety.
5. Setup writes are auditable (`updatedBy`, `updatedAt`, `version`).

### 3.2 Selection precedence

When multiple rows are eligible for a lookup:

1. Prefer rows where calculation date is inside effective window.
2. Prefer most recent `effectiveFrom`.
3. Prefer active rows over inactive rows.
4. If still ambiguous, reject with validation error (do not choose arbitrarily).

---

## 4. Waterfall Calculation Contract

### 4.1 Inputs

Minimum calculation input:

1. Vehicle financial values (acquisition/list/additions).
2. Lease term in months.
3. Selected strategies (interest, maintenance, replacement).
4. Fee and commission context (channel and setup references).
5. Contract context (down payment, period date, vehicle age context).

### 4.2 Sequence (must not reorder)

1. Total Financed Value (TFV).
2. Residual value and additions residual derivation.
3. Net financed base.
4. Interest by strategy (A/B).
5. Insurance (declining balance basis).
6. Maintenance by strategy (A/B).
7. Admin fee.
8. Profit margin.
9. Registration and fees.
10. Card fee.
11. Tracking fee.
12. Car wash/manpower fee.
13. Replacement amount by policy.
14. Pre-commission periodic rate.
15. Commission amount.
16. Final periodic customer rate.

### 4.3 Outputs

Calculation output must expose:

1. Monthly final rate.
2. Pre-commission rate.
3. Commission amount.
4. Each waterfall component amount.
5. Intermediate financial bases (TFV, net financed amount, residual values).
6. Applied setup keys/versions for traceability.

### 4.4 Rounding policy

1. Internal math precision: at least 4 decimals.
2. Money output precision: 2 decimals (SAR).
3. Rounding mode: midpoint away from zero.
4. Round at output boundaries, not after each sub-step unless regulation requires.

---

## 5. Projection Model

Income statement projection is periodized by calendar periods.

### 5.1 Contract-level period projection

Per period, compute:

1. Revenue = final rate.
2. Expenses = interest + insurance + maintenance + admin + registration + card + tracking + car wash/manpower + replacement + depreciation + commission.
3. Net profit = revenue - expenses.

Depreciation basis for Phase 1:

`(TFV - residualValue - residualOnAdditions) / termMonths`

### 5.2 Fleet-level projection

1. Aggregate period rows across active contracts.
2. Preserve source contract references for drill-down.
3. Keep contract and fleet views generated from the same component outputs.

---

## 6. API and Module Boundaries

### 6.1 Backend

1. Pricing setup endpoint persists validated setup payload.
2. Pricing calculation endpoint returns deterministic breakdown for quote preview and save.
3. Finalized quotation persists pricing snapshot for replay/audit.

### 6.2 Frontend

1. Setup UI edits master tables only through BFF.
2. Quotation new page uses backend calculator when available.
3. Local preview calculator may be used as fallback in development mode only.

### 6.3 Architecture guardrails

1. No direct vendor calls from BFF or domain for pricing setup/calculation.
2. No hardcoded thresholds or rates in UI or domain code.
3. Strategy and fee behavior comes from setup data and validated defaults.

---

## 7. Validation and Errors

Validation failures return Problem Details with machine-readable error codes.

Examples:

1. Missing required setup rows for selected category/term.
2. Overlapping effective windows for the same lookup key.
3. Invalid strategy/rate type combinations.
4. Unsupported term for selected pricing inputs.

Calculation must fail fast for invalid prerequisites and must not silently drop components.

---

## 8. Testing Strategy

### 8.1 Unit tests

1. Waterfall golden vectors for strategy A/B combinations.
2. Replacement OPEN vs PERMANENT scenarios.
3. Fee method scenarios (`FIXED_AMOUNT`, `PERCENT_OF_TFV`, `PERCENT_OF_INSTALLMENT`).
4. Effective-date row selection precedence.

### 8.2 Integration tests

1. Setup save/load with schema version enforcement.
2. Quotation repricing when term changes.
3. Snapshot persistence on final quote save.
4. Projection aggregation consistency (contract sum equals fleet rows).

---

## 9. Delivery Plan Link

Execution workstream for this spec:

- [Pricing engine redevelopment plan](../Plans/workstreams/2026-06-21-pricing-engine-redevelopment/plan.md)

---

## 10. Open Decisions

1. Whether commission supports flat-amount mode in addition to percentage.
2. Whether non-monthly billing frequencies are needed in Phase 1.
3. Whether setup payload moves from object-storage JSON to normalized SQL tables in Phase 1 or Phase 2.
4. Whether frontend fallback calculator remains after backend canonical endpoint is live.
