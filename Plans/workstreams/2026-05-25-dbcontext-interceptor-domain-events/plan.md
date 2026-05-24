# Workstream — DbContext Interceptor for Domain Event Dispatch

**Started**: 2026-05-25
**Closes**: ai_context.md TODO #5 first bullet ("Replace inline domain-event dispatch with a DbContext interceptor")
**Owner**: solo dev + Claude

## Goal

Move domain-event dispatch from the hand-rolled `lease.DomainEvents` scan inside
`TajeerWebhookEndpoints.HandleAsync` into a DbContext `SaveChangesInterceptor`, so
**every** `SaveChangesAsync` call across the codebase transparently publishes
collected domain events. This unblocks Week 2 saga work (Spec 02 §6.2) where multiple
sites will raise events.

## Scope

- New: `Application/Notifications/DomainEventNotification<T>` — generic MediatR
  wrapper, replaces the per-event `LeaseIssuedNotification`.
- New: `Infrastructure/Persistence/Interceptors/DomainEventDispatchInterceptor`
  — overrides `SavedChangesAsync` (post-commit hook), walks `ChangeTracker.Entries<Entity>()`,
  publishes one `DomainEventNotification<TEvent>` per raised event, clears the entity.
- Wire interceptor in `AddAutoLeaseNetInfrastructure` via `AddDbContext(...).AddInterceptors(...)`.
- Migrate `LeaseIssuedSmsHandler` to `INotificationHandler<DomainEventNotification<LeaseIssuedDomainEvent>>`.
- Delete `LeaseIssuedNotification` (named wrapper) and `TajeerWebhookEndpoints.DispatchDomainEventsAsync`.

## Non-goals

- Outbox pattern (deferred — covered by Week 4 reliability work).
- BackgroundService webhook drain (separate Week 2 follow-up).
- Wrapping the publish in an ambient transaction (post-commit dispatch is fine for
  Phase 1; if a notification handler throws we log + swallow, same posture as the
  existing SMS handler).

## Risks

- **Async deadlock from `SavedChanges` blocking call** — mitigated by using
  `SavedChangesAsync` overload only.
- **Re-entrancy**: notification handlers calling `SaveChangesAsync` again would
  re-trigger the interceptor. Today no handler does that; if one does, the
  re-entrant call sees no events (we clear after snapshot).
- **EF Core InMemory provider** used in tests — confirm interceptors fire there
  (they do; `SavedChangesAsync` is provider-agnostic).
- **MediatR resolution scope** — `IPublisher` must be resolved per-SaveChanges
  call from the same DI scope as the DbContext. Easiest path: register the
  interceptor as scoped and have it take `IPublisher` via ctor.

## RED → GREEN → REFACTOR

- [x] **T1 RED** — `Infrastructure.Tests/Persistence/DomainEventDispatchInterceptorTests.cs`
  with three tests (dispatches on raise, clears so 2nd save no-ops, silent when no
  events). Build failed as expected (CS0234 on `Application.Notifications` /
  `Persistence.Interceptors`).
- [x] **T2 GREEN(a)** — `Application/Notifications/DomainEventNotification<TEvent>`.
- [x] **T3 GREEN(b)** — `Infrastructure/Persistence/Interceptors/DomainEventDispatchInterceptor`
  overriding `SavedChangesAsync` (post-commit hook), walking
  `ChangeTracker.Entries<Entity>()`, publishing via `Activator.CreateInstance` on the
  generic wrapper.
- [x] **T4 GREEN(c)** — `AddAutoLeaseNetInfrastructure` registers the interceptor
  scoped and adds it to the `DbContextOptions` via the `(sp, opt) =>` overload.
- [x] **T5 REFACTOR(a)** — `LeaseIssuedSmsHandler` now implements
  `INotificationHandler<DomainEventNotification<LeaseIssuedDomainEvent>>`.
- [x] **T6 REFACTOR(b)** — `LeaseIssuedNotification.cs` deleted.
- [x] **T7 REFACTOR(c)** — `TajeerWebhookEndpoints.HandleAsync` lost the `IPublisher`
  parameter and the entire `DispatchDomainEventsAsync` private method; the
  inline comment now points to the interceptor as the dispatch source.
- [x] **T8** — `dotnet test AutoLeaseNet.sln --settings .runsettings` green: 151
  tests across 5 projects (Adapters.Tajeer 45, Adapters.Common 20, Infrastructure 3,
  Application 43, Bff 40). Three test factories (`SmsE2EFactory`, `WebhookFactory`,
  `SaveContractEndpointFactory`) had to re-wire the interceptor when they swap the
  DbContext to EF Core InMemory — `RemoveAll<DbContextOptions<...>>` clears the
  production interceptor binding, so each factory now uses the `(sp, opt) =>`
  overload and re-adds `DomainEventDispatchInterceptor` from DI. Captured in the
  retrospective as a known cost of factory-level DbContext swaps.
- [ ] **T9** — Open PR; merge after CI green.
- [ ] **T10** — Update `ai_context.md`: flip the architectural-followup bullet to
  ✅ delivered; record interceptor as the canonical dispatch path.

## Definition of done

- All existing tests still green (153+).
- New interceptor test green.
- Search for `DispatchDomainEventsAsync` returns 0 hits.
- Search for `LeaseIssuedNotification` returns 0 hits.
- CI on the PR branch is green.
- `ai_context.md` updated; this plan's checkboxes all ticked.
