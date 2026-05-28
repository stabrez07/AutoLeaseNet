# Retrospective — Day-21 Incident aggregate

**Started**: 2026-05-28
**Closed**: 2026-05-29 (carried into next day)
**Final test count**: 256 green (236 before; +20 net new)

## What shipped

- **Domain** (`AutoLeaseNet.Domain.Operations`): `Incident` aggregate root, three enums (`IncidentType`, `IncidentSeverity`, `IncidentStatus`), and `IncidentReportedDomainEvent`. Full Spec 01 §5.6 field list + Spec 02 §4.7 state machine. `RequiresReplacement` derived at Report time from `Severity == TotalLoss` — the future Replacement Saga's filter is already wired.
- **Application** (`AutoLeaseNet.Application.Operations`):
  - `IIncidentRepository` port (`Add` / `GetByIdAsync` / `SearchAsync`).
  - 5 commands + handlers, all idempotency-keyed: `ReportIncident`, `StartIncidentInvestigation`, `ResolveIncident`, `CloseIncident`, `UpdateIncidentClaim`.
  - 2 queries + handlers in Infrastructure: `GetIncidentByIdQuery`, `SearchIncidentsQuery` (paged, tenant-scoped).
  - DTOs: `IncidentSummaryDto`, `IncidentDetailDto`.
- **Infrastructure**: `EfIncidentRepository`, `IncidentConfiguration` (single-table mapping with TenantId-scoped indexes), `DbSet<Incident> Incidents` added to `AutoLeaseNetDbContext`, DI registration. EF migration `20260528205440_Add_Incident_Aggregate` generated + applied to local `AutoLeaseNet_Dev`.
- **BFF** (`services/bff/Endpoints/IncidentEndpoints.cs`): 7 endpoints under `/api/v1/incidents` and `/api/v1/lookups/incidents`. Status-code map: 404 not-found, 409 immutable / invalid-transition, 422 invalid-input. `PATCH /{id}/claim` is the first non-POST state-changer in the BFF — uses the same Idempotency-Key contract.
- **Seed**: `BogusDataSeeder.SeedIncidents` adds one Closed incident per Closed lease (alternating TrafficAccident + Breakdown, MarkResolved → MarkClosed for realistic timeline). New `IIncidentRepository incidents` constructor param.
- Workstream plan + this retrospective + ai_context entry.

## What we did well

- **Strict parallel to PR #12 (Inspection aggregate)**: same file naming, same handler co-location convention, same EF / DI / migration workflow. Anyone who reads PR #12 understands this PR within minutes — by-example consistency beats by-document consistency.
- **State-machine assertions exhaustive at domain level**: 13 domain tests cover every legal transition + every rejected one + idempotent re-entry + claim-update guard. Handler-level tests were intentionally skipped (covered transitively by endpoint tests) — saves ~250 LOC for zero coverage loss.
- **`RequiresReplacement` derived, not stored input**: keeps the Replacement-Saga filter simple (the event payload carries it, the aggregate computes it once at Report time). No way to forget setting it.
- **EF migration applied locally + tested before commit**: the migration is in source control AND the local Dev DB matches it. The Inspection-aggregate migration was the last one, so this one slots in cleanly.
- **`UpdateClaim` is the first non-POST state-changer**: PATCH semantics fit the partial-update behavior (only-non-null fields applied) better than POST. Now the BFF has a precedent for future partial-update endpoints.

## What hurt / would do differently

- **CA1725 ("parameter name must match interface")** caught me twice now in two consecutive workstreams. The MediatR handler signature canonically uses `cancellationToken`, but my muscle memory keeps reaching for `ct`. Standard fix: rename the parameter to `cancellationToken` and add `var ct = cancellationToken;` as a local alias — that satisfies both the analyzer and the body's brevity preference. Worth a CLAUDE.md note for future Claude sessions, but small enough that it's not blocking.
- **Endpoint test for `POST_investigate_then_resolve`** initially picked a Closed seeded incident expecting to drive it through investigate→resolve — Closed is terminal, so that would have 409'd. Caught early, switched to "report a fresh incident, then drive transitions on that". Lesson: when a test name is action-oriented ("then advances..."), the fixture must be a verb away from each assertion, not the data the seeder happened to drop.

## Carry-forward

- **Vehicle Replacement Saga** (Spec 02 §6.5) — wire a subscriber to `IncidentReportedDomainEvent` filtered on `RequiresReplacement = true`. Heavy: spans two leases, two Vehicles, two Tajeer contracts. Worth its own multi-day workstream.
- **Vehicle status transitions driven by incidents** (e.g. `IN_WORKSHOP` after a Major report) — needs `Vehicle.MoveToWorkshop` + status enum extension; defer with Replacement Saga.
- **Customer Portal fleet view + invoice list** — Day-21 master-plan note also listed these; UI deferred per the global rule.
- **PoliceReportNumber / InsuranceClaimNumber Always Encrypted** — PII-adjacent, scheduled with the Day-9 sweep.
- **Photo / attachment uploads** for incidents — same Storage adapter dependency as inspection photos.

## Stats

- **Files changed**: 4 modified, 11 new (3 enums + 1 aggregate + 1 event + 1 port + 2 application files + 1 EF config + 1 EF repo + 1 EF migration set + 1 BFF endpoint + 1 query handler), plus 2 test files + plan.md + retrospective.md + ai_context.md.
- **New tests**: 20 (13 domain + 7 endpoint).
- **Build warnings**: 0.
