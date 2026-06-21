# Workstream — Pricing Engine Redevelopment (Phase 1 continuation)

**Date**: 2026-06-21
**Slug**: `2026-06-21-pricing-engine-redevelopment`
**Branch**: `feat/pricing-engine-redevelopment`
**Spec refs**: [Pricing build source](../../AutoLeaseNet_Pricing_Engine_Build_Spec.md), [Spec 01 §5.4](../../../Specs/01-multi-tenancy-and-domain-model.md#54-sales-quotation), [Spec 02 §4.1](../../../Specs/02-state-machines-and-sagas.md#41-quotation), [Spec 06 §5.6](../../../Specs/06-bff-api-surface.md#56-approvals-the-workflow-engine-surface)

---

## Goal

Deliver a standards-aligned quotation pricing capability with deterministic waterfall calculations, complete setup coverage for pricing masters, and clear backend contracts so quote pricing is reproducible over time.

## Scope

### In scope

1. Pricing setup schema alignment in web portal and BFF payloads.
2. Deterministic monthly waterfall calculator for quotation pricing.
3. Setup admin UI for all pricing masters from the pricing build source doc.
4. Backend validation and versioning for pricing setup persistence.
5. Initial projection model design for periodized contract profitability.
6. Unit and integration tests for core pricing behaviors.

### Out of scope (this workstream)

1. D365 accounting postings.
2. ZATCA invoicing behavior changes.
3. Customer-portal billing UX redesign.

## Tasks (2-5 min granularity)

### A. Baseline and contract alignment

- [x] A1. Capture pricing source requirements from `Plans/AutoLeaseNet_Pricing_Engine_Build_Spec.md`.
- [x] A2. Add missing setup model arrays to pricing catalog typing.
- [x] A3. Add normalization defaults for backward compatibility with existing saved payloads.
- [x] A4. Ensure setup API helper load/save paths carry new arrays.

### B. Waterfall calculator

- [x] B1. Create dedicated pricing engine module for waterfall computation.
- [x] B2. Implement TFV and net financed amount derivation.
- [x] B3. Implement strategy A/B interest behavior.
- [x] B4. Implement strategy A/B maintenance behavior.
- [x] B5. Implement replacement open/permanent behavior.
- [x] B6. Implement fee, margin, commission, and final rate composition.
- [x] B7. Return structured breakdown values for future projection usage.

### C. Quotation integration

- [x] C1. Integrate pricing engine into quotation line unit-price calculation.
- [x] C2. Reprice selected quotation lines when duration changes.
- [x] C3. Keep fallback behavior when setup data is incomplete.

### D. Setup admin parity

- [x] D1. Add Lease Terms CRUD section in setup UI.
- [x] D2. Add Interest Rate Table CRUD section in setup UI.
- [x] D3. Add Residual Value Table CRUD section in setup UI.
- [x] D4. Add Replacement Policy CRUD section in setup UI.
- [x] D5. Add Fee Master CRUD section in setup UI.
- [x] D6. Add Commission Rate CRUD section in setup UI.
- [x] D7. Add Profit Margin CRUD section in setup UI.
- [x] D8. Add Calendar Period CRUD/generation section in setup UI.

### E. Backend hardening

- [x] E1. Add validation rules for setup payload shape and required fields.
- [x] E2. Add setup payload version field and migration guard.
- [x] E3. Add setup audit metadata (`updatedBy`, `updatedAt`, version).
- [x] E4. Add tests for setup endpoint schema enforcement.

### F. Projection foundation

- [x] F1. Define projection DTOs for period-level revenue and expenses.
- [x] F2. Implement contract-level period projection from waterfall outputs.
- [x] F3. Implement fleet-level rollup by calendar period.
- [x] F4. Add tests for projection math consistency.

### G. Verification and closeout

- [x] G1. Run web portal typecheck after pricing model/engine integration.
- [x] G2. Run BFF build and tests after backend hardening tasks.
- [x] G3. Add/update spec document for pricing engine and setup model.
- [x] G4. Write workstream retrospective at closure.

## Verification

1. `pnpm --filter @autoleasenet/web-portal typecheck` passes.
2. Quotation new flow reprices lines on term change and vehicle selection change.
3. Setup load/save remains functional through `/api/v1/admin/quotation-pricing-setup`.
4. `dotnet build AutoLeaseNet.sln` passes after backend changes.
5. `dotnet test AutoLeaseNet.sln --filter "Trait!=Integration"` passes after backend changes.

## Dependencies

1. Pricing source document: `Plans/AutoLeaseNet_Pricing_Engine_Build_Spec.md`.
2. Existing quotation aggregate and quotation endpoints in BFF.
3. Setup storage endpoint `/api/v1/admin/quotation-pricing-setup`.
4. Tenant and dev-header conventions from BFF middleware.

## Risks

1. Formula drift risk between frontend and backend calculators once both exist.
   Mitigation: define a single canonical backend calculation contract and regression vectors.
2. Historical reproducibility risk if setup rows are edited destructively.
   Mitigation: enforce effective date windows and soft-deactivation in setup UI.
3. Runtime behavior risk if setup payload is partially populated.
   Mitigation: strict validation plus explicit defaults and safe fallback pricing behavior.
4. Scope creep risk from financial projection requirements.
   Mitigation: land projection in incremental slices (contract-level first, fleet rollup next).
