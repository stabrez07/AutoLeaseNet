# Retrospective — BffTestSeedWaiter extract

**Date closed**: 2026-05-30
**Branch**: `chore/bff-test-seed-waiter`
**Predecessors**: ZATCA adapter skeleton (#28); `BffTestHostDefaults` extract (#25).
**Test delta**: 368 → 368 (no change — pure refactor).

## What landed

- New `BffTestHostDefaults.EnsureDemoSeededAsync(factory, readinessCheck, entityName, timeout?, buildTimeoutDetail?)`:
  - Probes the host via `CreateClient()` so the Development startup hook runs.
  - Resolves `IDataSeeder`, calls `SeedAsync`.
  - Polls `readinessCheck(db)` every 100ms until satisfied or `timeout` (default 120s).
  - Throws `"Seeder did not produce '{entityName}' within {N}s."`; optional `buildTimeoutDetail` callback enriches the message with diagnostic context (used by `SaveContractEndpointFactory` and `InspectionFactory`).
- 8 factories migrated. Each `EnsureSeededAsync` body collapsed from ~15 lines to 4–6.

| Factory | Predicate | Diagnostic enrichment |
|---|---|---|
| `MyVehiclesFactory` | `db.Customers.AnyAsync()` | — |
| `MyVehicleDetailFactory` | `db.Leases.AnyAsync(l => l.Status == Active)` | — |
| `MyLeaseDetailFactory` | `db.Leases.AnyAsync()` | — |
| `MeFactory` | `db.Customers.AnyAsync()` | — |
| `SaveContractEndpointFactory` | `db.Customers.AnyAsync()` | `SeederType`, `Seed:Mode`, `Customers` count, `DbName` |
| `CheckInFactory` | `db.Leases.AnyAsync(l => l.Status == Active)` | — |
| `ExtendSuspendFactory` | `db.Leases.AnyAsync(l => l.Status == Active)` | — |
| `InspectionFactory` | `db.Inspections.AnyAsync()` | `Seed:Mode` |
| `IncidentFactory` | `db.Incidents.AnyAsync()` | — |

`SmsE2EFactory` was excluded — it doesn't use `EnsureSeededAsync` at all; it seeds inline via `SeedLeaseAndRenterAsync`. Not a candidate for this helper.

## What worked

- **The `buildTimeoutDetail` callback was the right escape hatch.** The two factories with richer error messages (`SaveContractEndpointFactory` and `InspectionFactory`) kept their full diagnostic without leaking into the common path. The 6 factories that don't care got the shared default for free.
- **8 factories migrated in one PR, no behaviour change.** 368 → 368 means I didn't accidentally drop a Customer/Incident/Active-Lease check, and the deadline poll semantics are identical.
- **Net code reduction is substantial.** Roughly 120 lines removed (15 lines × 8 factories) vs ~70 lines added (helper + 8 collapsed bodies). ~50 net lines deleted from the test surface, and each factory now reads as "I wait for X" not "boilerplate boilerplate predicate boilerplate".

## What I'd do differently next time

- **Should have done this immediately after PR #25 (`BffTestHostDefaults` extract)** instead of letting it pile up four more retros. The pattern was already clear at PR #25; the "fifth retro = act" threshold ended up taking longer than necessary.
- **Cap the timeout at the param's `TotalSeconds:0` format**, not hard-coded 120 in the docstring. Now the helper itself formats it from `effectiveTimeout`.

## Carry-forward (not advanced this PR)

Unchanged from PR #28's retro:
- Phase-2 Vehicles RLS extension (collapses three trust-boundary handlers to single LINQ joins).
- Vehicle Replacement Saga.
- Quotation flow + 3-tier approvals (Week 4).
- ZATCA Week-4 actual: UBL 2.1 + ECDSA + TLV QR + ZatcaSubmission saga.
- Always Encrypted on PII columns.
- next-intl + `[locale]` segments migration for the customer portal.
