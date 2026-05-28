# Workstream — Day-21: Incident aggregate (foundation)

**Started**: 2026-05-28 (same session as PRs #15 / #16)
**Closes**: the Incident-aggregate slice of Day 21 in
[`Plans/02-phase-1-mvp-week-by-week.md`](../../02-phase-1-mvp-week-by-week.md).
**Owner**: solo dev + Claude.

## Goal

Ship the Incident aggregate with the full Spec 01 §5.6 field list and the
Spec 02 §4.7 state machine, behind a stable BFF surface. Mirrors the Inspection
aggregate from PR #12 structurally — same domain pattern, same command-handler
co-location convention, same EF-migration-applied-to-local-DB workflow.

**Out of scope** for this PR (master-plan Day 21 also lists these — keeping them
deferred per the user's UI / Customer-Portal global rule):

- Customer Portal fleet view + invoice list (Next.js work; UI deferred globally).
- TOTAL_LOSS-triggered Vehicle Replacement Saga (Spec 02 §6.5). The
  `IncidentReportedDomainEvent` is forward-declared with no subscriber — same
  shape as `InspectionCompletedDomainEvent`. Saga lands when invoicing /
  reconciliation are ready to consume it.
- Vehicle status transitions driven by incidents (e.g. `OPEN` → Vehicle =
  `IN_WORKSHOP`) — depends on Replacement Saga + Vehicle.MoveToWorkshop being
  modelled.

## Scope (in)

### Domain — `AutoLeaseNet.Domain.Operations`

- `IncidentType` enum: `TrafficAccident`, `NonTrafficDamage`, `Breakdown`, `Theft`, `Vandalism`, `Other`.
- `IncidentSeverity` enum: `Minor`, `Major`, `TotalLoss`.
- `IncidentStatus` enum: `Open`, `UnderInvestigation`, `Resolved`, `Closed`.
- `Incident` aggregate root:
  - All Spec 01 §5.6 fields (LeaseId, VehicleId, ReportedByPersonId, ReportedAtUtc, IncidentTimeUtc, Type, Severity, LocationLat/Lng, LocationDescription, Description, PoliceReportNumber, InsuranceClaimNumber, Status, RequiresReplacement, ReplacementLeaseId).
  - `Report(input)` factory — creates in `Open`, raises `IncidentReportedDomainEvent`.
  - `StartInvestigation(nowUtc)` — `Open` → `UnderInvestigation`. Idempotent re-entry.
  - `MarkResolved(resolutionNotes, nowUtc)` — `Open` | `UnderInvestigation` → `Resolved`. Idempotent.
  - `MarkClosed(nowUtc)` — `Open` | `UnderInvestigation` | `Resolved` → `Closed`. Idempotent.
  - `UpdateClaim(policeReportNumber?, insuranceClaimNumber?, nowUtc)` — mutate while not Closed.
  - `LinkReplacementLease(leaseId, nowUtc)` — sets `ReplacementLeaseId`; idempotent on same id, rejects on mismatch (lays the groundwork for the Replacement Saga).
- `IncidentReportedDomainEvent(IncidentId, TenantId, LeaseId, VehicleId, Type, Severity, ReportedAtUtc, RequiresReplacement)`.

### Application — `AutoLeaseNet.Application.Operations`

- Commands (all idempotency-keyed):
  - `ReportIncidentCommand` + handler
  - `StartIncidentInvestigationCommand` + handler
  - `ResolveIncidentCommand` + handler
  - `CloseIncidentCommand` + handler
  - `UpdateIncidentClaimCommand` + handler
- Queries:
  - `GetIncidentByIdQuery` + handler (Infrastructure-side per Spec convention)
  - `ListIncidentsQuery` + handler (paged, tenant-scoped, filter by LeaseId / VehicleId / Status / Severity)
- Result envelope `IncidentCommandResult { Success, IncidentId, Status, ErrorCode, ErrorMessage }`.
- `IIncidentRepository` port — `Add`, `GetByIdAsync`, `SearchAsync`.

### Infrastructure

- `EfIncidentRepository` (mirrors `EfInspectionRepository`).
- `IncidentConfiguration` (one table — no child collections).
- EF migration `Add_Incident_Aggregate` applied to local `AutoLeaseNet_Dev`.
- `AutoLeaseNetDbContext`: add `DbSet<Incident> Incidents`.
- Query handlers in Infrastructure (mirrors the Inspection convention).

### BFF — `services/bff/Endpoints/IncidentEndpoints.cs`

- 7 endpoints under `/api/v1/incidents`:
  - `POST /` — Report (Idempotency-Key required)
  - `POST /{id:guid}/investigate` — StartInvestigation
  - `POST /{id:guid}/resolve` — Resolve
  - `POST /{id:guid}/close` — Close
  - `PATCH /{id:guid}/claim` — UpdateClaim
  - `GET /{id:guid}` — detail
  - `GET /api/v1/lookups/incidents` — paged list (matches the Inspection lookups pattern)
- Status-code map: 404 not_found ; 409 immutable / state-conflict ; 422 invalid input.

### Seed

- `BogusDataSeeder` adds 1 incident per CLOSED lease (deterministic), one TRAFFIC_ACCIDENT + one BREAKDOWN balance, all in `Closed` status. ~3-5 seeded rows total.

### Tests (~24)

- Domain transitions (10): start state, each allowed transition, each rejected transition, idempotency, UpdateClaim non-Closed-only guard, LinkReplacementLease mismatch.
- Handler tests (5): one happy path per command (Report covers `IncidentReportedDomainEvent` is raised).
- Endpoint tests (5): each POST happy + one missing-Idempotency-Key 400 + one GET happy + one list happy.
- EF round-trip / mapping test (1): hard-coded entity, save + reload, fields preserved.
- Repository search filter tests (3): by LeaseId, by Status, by Severity.

Target: 236 → ~260 tests green.

### Docs

- `Plans/workstreams/2026-05-28-day-21-incident-aggregate/{plan.md, retrospective.md}`
- `ai_context.md`: Last-updated entry + endpoint table additions.

## Done criteria

- 5 commands + 2 queries + 7 BFF endpoints behind authorization + Idempotency-Key (on POSTs/PATCH).
- EF migration applied locally; DbContext exposes `Incidents`.
- Seed produces deterministic incident rows on closed leases.
- 214 → 236 → ~260 tests green.
- `IncidentReportedDomainEvent` forward-declared (no Phase-1 subscriber — same pattern as `InspectionCompletedDomainEvent`).

## Risk / known limits

- **No Replacement Saga wiring** — TOTAL_LOSS incidents just sit at `Open` with `RequiresReplacement = true`. Saga workstream activates the event.
- **Vehicle/Lease status not cascaded** — an incident reported against an Active lease does NOT change Vehicle.Status. Phase 2 work tied to the saga.
- **No photo / attachment upload** — same Storage adapter dependency as the deferred Inspection photo upload; both arrive together.
- **PII**: PoliceReportNumber + InsuranceClaimNumber are PII-adjacent (Spec 01 §6) — Phase 1 stores them plaintext, Day-9 Always Encrypted migration sweeps them when it ships.
