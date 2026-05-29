# Retrospective — Outbox + drain

**Date closed**: 2026-05-29
**PR**: TBD
**Commit**: TBD

## What shipped

The post-commit inline dispatch path (`DomainEventDispatchInterceptor` →
synchronous `IPublisher.Publish`) was replaced with a transactional outbox:

- **Capture** — `OutboxWriteInterceptor` runs in `SavingChangesAsync`, walks
  `ChangeTracker.Entries<Entity>()`, serializes each raised `IDomainEvent` to
  JSON, and inserts an `OutboxEvent` row in the SAME UoW transaction as the
  business state change. Atomicity: business write ↔ outbox row are now
  inseparable.
- **Drain** — `OutboxDrainService : BackgroundService` polls every
  `Outbox:DrainIntervalSeconds` (default 5s), pulls up to `Outbox:BatchSize`
  rows whose `AvailableAtUtc <= now AND ProcessedAtUtc IS NULL AND
  Attempts < MaxAttempts`, deserializes each via assembly-qualified type name,
  publishes through MediatR (same `DomainEventNotification<TEvent>` wrapper as
  before), marks processed; on handler exception, increments `Attempts` and
  pushes `AvailableAtUtc` out by exponential backoff (1→1s, 2→2s, 3→4s,
  4→8s, 5→16s, capped at 60s). After `MaxAttempts` (default 5) the row is
  parked with `LastError` set.
- **Per-tenant publish scope** — each drained row wraps its publish in
  `SystemTenancyScope.For(row.TenantId)` so RLS-protected reads inside
  handlers (e.g. `LeaseIssuedSmsHandler` querying `Customers`) work as
  intended.
- **Migration** — `Add_OutboxEvent` applied to local `AutoLeaseNet_Dev`.
- **Test factory sweep** — 8 existing factories opted out of the drain
  (`Outbox:Enabled=false`); `SmsE2EFactory` deliberately keeps it on with a
  1-second drain interval and the test polls for completion.

## Honest scoping (recap)

This Outbox closes the **domain-event delivery window** — handlers run reliably
with retry instead of silent fire-and-forget. It does **not** close the
**Tajeer↔local commit window** for saga handlers; that's a different pattern
(command-table). The plan documents this explicitly.

## What went well

- The OutboxWriteInterceptor was 80 lines and tracked exactly the
  `DomainEventDispatchInterceptor` structure that PR #7 introduced — the
  template was already there.
- `DrainOnceAsync` factored out as `internal` (with `InternalsVisibleTo` to
  Infrastructure.Tests) made for clean deterministic tests of one cycle, no
  hosted-service loop in unit tests.
- The post-Day-9 `SystemTenancyScope` is paying back already — the drain
  needs cross-tenant SYSTEM scoping per row, and it composes for free.
- 5 OutboxDrainService tests + 3 OutboxWriteInterceptor tests cover capture,
  re-save idempotence, no-op-when-no-events, happy-path publish, no-rows-due,
  publish-failure-backoff, max-attempts-park, and the backoff curve itself.
- Retiring `DomainEventDispatchInterceptor` + its tests was a clean delete —
  the new path subsumes both.

## What surprised me

- **Test factory sweep was 9 files**, not 2-3 as I'd estimated. Worth a
  pattern note: every factory's inline config block grows with every
  cross-cutting opt-in/opt-out feature. A shared base factory or shared
  helper for the common Tajeer/Seed/Outbox config dictionary would prevent
  the next sweep being 12 files.
- **`AddHostedService<T>` does not register T as resolvable** — only
  registers it under `IHostedService`. For tests that want to poke at the
  drain directly, you have to either expose via singleton + factory pair,
  or do `GetServices<IHostedService>().OfType<OutboxDrainService>()`. Worth
  remembering.
- The `LeaseIssuedSmsEndToEndTests.Webhook_contract_create_with_no_renter_customer_still_returns_200_and_updates_lease`
  test previously relied on the inline handler firing synchronously to assert
  "no SMS sent." Post-Outbox the handler runs on the drain's thread; the test
  needed a wait loop on `ProcessedAtUtc` to make the negative assertion
  meaningful.

## What I'd do differently

- **Shared test factory base** before the next cross-cutting feature lands.
  Otherwise the next workstream will sweep N+1 files.
- **`appsettings.json` Outbox default** — production runs need
  `Outbox:Enabled=true`. I left the OutboxOptions class default true; an
  explicit appsettings entry would be safer-by-convention.

## Numbers

- Files added: 8 (port + scope + aggregate + EF config + repo + interceptor +
  service + service-collection extension), plus plan/retro and 2 test files.
- Files modified: 1 wiring (`ServiceCollectionExtensions.cs`), 1 wiring
  (`Program.cs`), 1 DbContext (DbSet), 1 csproj (`InternalsVisibleTo`),
  9 test factories (`Outbox:Enabled` toggle + the SMS E2E drain wait).
- Files deleted: 2 (retired `DomainEventDispatchInterceptor` + its tests).
- Migration: `20260529020317_Add_OutboxEvent` applied locally.
- Tests: 264 → **269** default (+8 outbox unit/integration, -3 retired).
- Total elapsed: ~90 min.

## Hand-off

Phase-1 hardening sprint is now 2 of 3 done (Day-9 RLS, Outbox+drain). Next:

1. **Reconciliation BackgroundService skeleton** — ½ day. Now that the
   `BackgroundService` registration pattern is established, dropping a second
   one in is straightforward.
2. **Customer Portal scaffold** — Phase-1 demo unblocker.
3. **ZATCA adapter** — Week-4 critical path.

Each its own PR per the cadence.
