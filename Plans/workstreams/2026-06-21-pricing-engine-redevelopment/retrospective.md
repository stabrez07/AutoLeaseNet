# Retrospective — 2026-06-21 Pricing Engine Redevelopment

## Summary

- Workstream objective: Deliver standards-aligned quotation pricing with deterministic waterfall calculations, complete setup coverage for all pricing masters, backend validation, and initial projection model.
- Delivery window: 2026-06-21 (single session)
- Final outcome: All tasks A–G complete. Typecheck passing, 15 projection tests green, 9 BFF endpoint tests green.

## What Went Well

1. Type-first approach — defining all 15 setup row types in `quotation-pricing-catalog.ts` first meant engine, UI, and projection modules consumed the same contract with zero drift.
2. Incremental verification — running typecheck after each task block caught exactOptionalPropertyTypes issues early.
3. CSV bulk upload reuse — `parseCsvWithHeader` pattern scaled cleanly across all pricing tables without needing a generic abstraction.

## What Did Not Go Well

1. No test runner existed for the web portal — had to install vitest mid-workstream. Should be part of scaffold.
2. `setup/page.tsx` grew to ~2700 lines. Acceptable for delivery but should be decomposed in next iteration.
3. BFF debug process file lock forced Release-config builds for verification.

## Key Decisions Made

1. Used `public` visibility on `ValidateSetupPayload` and `CurrentSchemaVersion` in BFF rather than InternalsVisibleTo — simpler for a static endpoint class.
2. Projection uses flat per-period amounts from the waterfall (monthly average) rather than per-period declining-balance recalculation — sufficient for Phase 1 preview; backend will be canonical.
3. Calendar period generation uses explicit year buttons rather than auto-detection — lets operators generate next year's periods proactively.

## Risks Encountered And Mitigations

1. Risk: exactOptionalPropertyTypes causing `effectiveTo: undefined` type errors.
   Mitigation: Used destructuring pattern `{ effectiveTo: _, ...rest }` to omit the property rather than assigning undefined.
2. Risk: Shared test state between BFF endpoint tests (IClassFixture).
   Mitigation: Used unique tenant GUIDs for tests that need empty-storage assertions.

## Verification Results

1. `pnpm --filter @autoleasenet/web-portal typecheck`: PASS
2. `dotnet build services/bff -c Release`: PASS (Build succeeded)
3. `dotnet test services/bff.tests -c Release --filter QuotationPricingSetup`: PASS (9/9)
4. `pnpm --filter @autoleasenet/web-portal test`: PASS (15/15 projection tests)

## Follow-Up Actions

1. Extract each pricing table component from setup/page.tsx into individual files.
2. Add end-to-end test: save setup → create quotation → verify pricing matches waterfall.
3. Implement backend canonical pricing endpoint (currently frontend-only calculator).
4. Wire projection view into quotation or reporting page.
5. Add pricing snapshot persistence on final quote save for audit trail.

## Lessons Learned

1. Always set up a test runner (vitest) in the project scaffold — retrofitting mid-workstream is friction.
2. The exactOptionalPropertyTypes destructuring pattern should be standardized project-wide — it recurs.
3. Single-file UI components are acceptable for rapid delivery but should be split before the next person touches the feature.
