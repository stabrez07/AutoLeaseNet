# Workstream — Inspection Aggregate (E-Check foundation)

**Started**: 2026-05-25
**Closes**: First slice of Week 3 Operations work per
[`Plans/02-phase-1-mvp-week-by-week.md`](../../02-phase-1-mvp-week-by-week.md) — the
domain + persistence + minimal API surface for `Inspection`. Saga integration
(check-out / check-in) is the **next** workstream after this lands.
**Owner**: solo dev + Claude

## Goal

Stand up the `Inspection` aggregate as a first-class citizen of the domain so
later Week 3 sagas (check-out, check-in) have something to attach to. Match the
spec to the letter — Tajeer canvas dimensions, fuel-level enum, condition
TINYINT columns — so seed + real Tajeer data line up without translation.

## Scope (in)

1. **Domain** (`AutoLeaseNet.Domain.Operations`):
   - `Inspection` aggregate root with every BI field from
     [Spec 01 §5.6](../../../Specs/01-multi-tenancy-and-domain-model.md#56-operations)
     (VehicleId, optional LeaseId, type, performed-at, performed-by, odometer,
     fuel, all the condition TINYINTs, sketch JSON, signature blob URI).
   - `InspectionType` enum: `PRE_DELIVERY`, `CHECK_OUT`, `CHECK_IN`, `INCIDENT`,
     `PERIODIC`, `CHECK_OUT_CORRECTION`.
   - `InspectionStatus` enum: `IN_PROGRESS`, `COMPLETED`, `ABANDONED` per
     [Spec 02 §4.6](../../../Specs/02-state-machines-and-sagas.md#46-inspection).
   - `FuelLevel` enum: `Full=1, ThreeQuarter=2, Half=3, Quarter=4, Empty=5`
     (Tajeer mapping).
   - Child entities: `InspectionPhoto` (BlobUri, Sequence) +
     `InspectionDamageMarker` (Type, PositionX 0–893, PositionY 0–429).
   - State transitions:
     - `Start(...)` → factory; returns IN_PROGRESS.
     - `AddPhoto(blobUri, sequence)` — IN_PROGRESS only.
     - `AddDamageMarker(type, x, y)` — IN_PROGRESS only; bounds-check coords.
     - `Complete(nowUtc)` — IN_PROGRESS → COMPLETED; raises
       `InspectionCompletedDomainEvent`; immutable thereafter.
     - `Abandon(reason, nowUtc)` — IN_PROGRESS → ABANDONED.
   - `InspectionCompletedDomainEvent { InspectionId, TenantId, VehicleId,
     LeaseId, Type, PerformedAtUtc, CompletedAtUtc }` — published post-commit by
     the existing `DomainEventDispatchInterceptor`.
   - Idempotent same-state re-entry on `Complete` + `Abandon` (defends against
     replay).

2. **Application** (`AutoLeaseNet.Application.Operations`):
   - `IInspectionRepository` port (`Add`, `GetByIdAsync`, `ListByLeaseAsync`,
     `ListByVehicleAsync`, paged `SearchAsync`).
   - MediatR commands: `StartInspectionCommand`, `AddInspectionPhotoCommand`,
     `AddDamageMarkerCommand`, `CompleteInspectionCommand`,
     `AbandonInspectionCommand` — each returns the aggregate id / status.
   - Lookup query: `ListInspectionsQuery` (paged; filter by leaseId, vehicleId,
     type, status).
   - DTOs: `InspectionSummaryDto`, `InspectionDetailDto` (with embedded photos +
     markers).
   - **No** SMS / notification handler yet (Inspection events have no Phase-1
     subscriber; the saga workstream will wire them).

3. **Infrastructure** (`AutoLeaseNet.Infrastructure.Persistence`):
   - `InspectionConfiguration : IEntityTypeConfiguration<Inspection>` —
     tenant-scoped indexes on (LeaseId, Type) + (VehicleId, PerformedAtUtc).
   - Owned/related configurations for `InspectionPhoto` +
     `InspectionDamageMarker`.
   - New EF migration `Add_Inspection_Aggregate` — applied to local
     `AutoLeaseNet_Dev`.
   - `EfInspectionRepository` implementing the port.
   - `DbSet<Inspection>` on `AutoLeaseNetDbContext`.

4. **Seed** (`Adapters.Seed/BogusDataSeeder`):
   - One `CHECK_OUT` inspection per seeded ACTIVE / EXTENDED / SUSPENDED lease
     (so the existing 10 leases get realistic operations history).
   - One `CHECK_IN` inspection per CLOSED lease.
   - One `PRE_DELIVERY` inspection per READY vehicle that has no lease yet.
   - Deterministic via `SeedOptions.RandomSeed`. Photos = empty list (no blob
     adapter wired yet); damage markers = 0–3 random per inspection.

5. **BFF** (`services/bff/Endpoints/InspectionEndpoints.cs`):
   - `POST /api/v1/inspections` — start; returns `{id, status}` + `201 Location`.
   - `POST /api/v1/inspections/{id}/photos` — add photo.
   - `POST /api/v1/inspections/{id}/damage-markers` — add marker.
   - `POST /api/v1/inspections/{id}/complete` — submit.
   - `POST /api/v1/inspections/{id}/abandon` — cancel.
   - `GET /api/v1/inspections/{id}` — detail (with photos + markers).
   - `GET /api/v1/lookups/inspections` — paged list with filters.
   - All authenticated via dev JWT stub; tenant resolved from claims;
     `Idempotency-Key` required on the 5 state-changing POSTs (matching the
     existing dev/save-contract pattern).

6. **Tests**:
   - Domain unit tests in `Application.Tests/Operations` (covering: factory
     happy path; AddPhoto blocked after Complete; coords out of range throws;
     Complete from IN_PROGRESS raises event; Complete from COMPLETED no-ops;
     Complete from ABANDONED throws; Abandon from IN_PROGRESS works).
   - BFF endpoint tests in `bff.tests/Endpoints/InspectionEndpointTests.cs`
     using the existing `AddAutoLeaseNetDbContext` helper.
   - Target: +20 new tests minimum; existing 151 still green.

## Scope (out — flagged for follow-up workstreams)

- **Saga wiring**: enforcing the spec invariants (Lease can't go ACTIVE
  without a CHECK_OUT row; can't CLOSE without a CHECK_IN row) lives in the
  next workstream where the check-out + check-in sagas land.
- **Photo upload**: today the endpoint accepts a pre-computed `blobUri` string.
  Real upload through `Adapters.Storage.AzureBlob` is its own workstream.
- **Renter e-signature capture**: same as photos — store URI only; capture
  flow is later.
- **Offline-mobile sync + ABANDONED 24h timer**: out (Phase 1 doesn't have
  mobile yet).
- **`Incident` aggregate**: separate workstream (Spec 01 §5.6 has its own
  table).
- **Photo AI damage detection**: Phase 3.

## Risks

- **Big EF mapping surface**: ~20 columns + 2 owned collections. Mitigation —
  pattern-match the existing `LeaseConfiguration` we already know works.
- **Migration drift**: a new aggregate adds tables. Make sure
  `Add_Inspection_Aggregate` is the only migration in this PR (run it locally
  before commit).
- **Idempotency-Key handling on 5 endpoints**: replay the existing
  `Idempotency-Key` middleware pattern from `dev/save-contract` rather than
  rebuild.
- **Domain event consumer absence**: `InspectionCompletedDomainEvent` will be
  published with no subscribers in this PR. That's fine — the interceptor
  handles zero-subscriber events cleanly (verified by interceptor tests). The
  next saga workstream adds the consumer.

## RED → GREEN → REFACTOR (tasks 2–5 min each)

- [x] **T1** — branch `feat/inspection-aggregate`.
- [x] **T2 RED** — domain unit tests in
  `Application.Tests/Operations/InspectionTests.cs` (17 tests covering Start,
  AddPhoto, AddDamageMarker with canvas-bounds Theory, Complete, Abandon,
  idempotent same-state re-entry, illegal-transition throws). Build failed on
  the missing types as expected.
- [x] **T3 GREEN** — `Domain/Operations/{Inspection, InspectionPhoto,
  InspectionDamageMarker, InspectionType, InspectionStatus, FuelLevel,
  DamageMarkerType, InspectionCompletedDomainEvent}.cs`. 17 tests green.
- [x] **T4** — `Application.Ports/Persistence/IInspectionRepository.cs` with
  `Add` / `GetByIdAsync` / `SearchAsync`.
- [x] **T5** — `Application/Operations/InspectionCommands.cs` (5 commands +
  `InspectionCommandResult`) + `InspectionCommandHandlers.cs` (5 handlers,
  all idempotency-cached via the shared `InspectionIdempotency.Key`).
- [x] **T6** — `Application/Operations/InspectionQueries.cs` (Get + Search
  queries + Summary / Detail / Photo / DamageMarker DTOs).
- [x] **T7** — `Infrastructure/Persistence/Configurations/InspectionConfiguration.cs`
  carrying all three EF mappings (Inspection + Photo + DamageMarker) with
  tenant-scoped indexes on `(LeaseId, Type)` and
  `(VehicleId, PerformedAtUtc)`.
- [x] **T8** — `EfInspectionRepository.cs` + three `DbSet<>` properties on
  `AutoLeaseNetDbContext`. `GetByIdAsync` eager-loads children;
  `SearchAsync` projects counts.
- [x] **T9** — `dotnet ef migrations add Add_Inspection_Aggregate` (via
  `--startup-project AutoLeaseNet.Infrastructure` — startup-as-self works,
  BFF startup project still missing the Design package; same caveat as
  prior migrations). Applied to local `AutoLeaseNet_Dev`.
- [x] **T10** — `BogusDataSeeder` now seeds 1 CHECK_OUT per
  Active/Extended/Suspended lease + CHECK_OUT+CHECK_IN per Closed lease,
  with 0–3 deterministic damage markers per inspection (seeded from
  `SeedOptions.RandomSeed ^ 0x1517`). `IInspectionRepository` added to the
  constructor.
- [x] **T11** — `services/bff/Endpoints/InspectionEndpoints.cs` with 7 routes
  (POST start, add-photo, add-damage-marker, complete, abandon; GET by id,
  list-paged at `/lookups/inspections`). `Idempotency-Key` required on all
  state-changing POSTs; status-code mapping table for the 3 error families.
  Registered in `Program.cs` via `v1.MapInspectionEndpoints()`.
- [x] **T12** — `services/bff.tests/Endpoints/InspectionEndpointTests.cs`
  (7 tests: start 201, idempotency replay, missing-key 400, complete
  idempotency cache, complete-unknown 404, get-by-id, paged list). Test
  factory bumped to include `X-Dev-User-Id` header so the domain's
  `PerformedByUserId != Guid.Empty` invariant is satisfied.
- [x] **T13** — full suite: **175 tests green** (20 + 45 + 3 + 60 + 47).
  Baseline 151 → 175 = +24 (17 domain + 7 endpoint).
- [x] **T14** — `ai_context.md` updated: API contracts table got the 7 new
  endpoints; current-repo-state SHA bumped post-merge.
- [x] **T15** — checkboxes ticked + `retrospective.md` written.
- [x] **T16** — PR opened + merged on green CI; post-merge ai_context PR
  bundled where appropriate.

## Definition of done

- All checkboxes ticked.
- Full suite green (151 + new Inspection tests).
- `dotnet build AutoLeaseNet.sln -c Release` clean (WarnAsError).
- CI green on the PR.
- `ai_context.md` updated with the 6 new endpoints + current SHA.
- `Plans/workstreams/2026-05-25-inspection-aggregate/retrospective.md` written.
