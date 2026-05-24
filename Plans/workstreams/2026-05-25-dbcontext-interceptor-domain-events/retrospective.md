# Retrospective — DbContext Interceptor for Domain Event Dispatch

**Closed**: 2026-05-25
**Plan**: [plan.md](./plan.md)
**Outcome**: shipped — all checkboxes ticked. Domain-event dispatch now happens transparently inside the EF Core DbContext.

## What we delivered

- `Application/Notifications/DomainEventNotification<TEvent>` — single generic
  MediatR wrapper around any `IDomainEvent`.
- `Infrastructure/Persistence/Interceptors/DomainEventDispatchInterceptor` —
  hooks `SavedChangesAsync` (post-commit), enumerates `ChangeTracker.Entries<Entity>()`,
  publishes one notification per raised event, clears events from the entity.
- `LeaseIssuedSmsHandler` now binds to `INotificationHandler<DomainEventNotification<LeaseIssuedDomainEvent>>`.
- `LeaseIssuedNotification` (per-event wrapper) deleted.
- `TajeerWebhookEndpoints.HandleAsync` lost its `IPublisher` parameter and the
  `DispatchDomainEventsAsync` private method — the inline comment now points to
  the interceptor.

## What was easy

- Existing `Entity` base class already had `DomainEvents` + `ClearDomainEvents()`,
  so the interceptor's snapshot-and-clear loop is a few lines.
- TDD turn-around was tight: write 3 RED tests, build to confirm 2 missing types,
  add types, green. ~15 minutes from RED to GREEN.

## What bit us (note for future workstreams)

- **Test factory DbContext swaps lose the interceptor binding.** Three test
  factories (`SmsE2EFactory`, `WebhookFactory`, `SaveContractEndpointFactory`)
  all do `services.RemoveAll<DbContextOptions<AutoLeaseNetDbContext>>()` then
  re-add with `UseInMemoryDatabase(...)`. This wipes the production
  `(sp, opt) => opt.AddInterceptors(...)` wiring. The first regression caught
  it (SMS E2E expected 1 captured message, got 0); fix was switching each
  factory to the `(sp, opt) =>` overload and re-adding the interceptor from DI.

  **Implication for Week 2 saga work**: if we add a second interceptor (e.g. a
  tenancy interceptor that pushes `SESSION_CONTEXT`), every test factory that
  swaps the DbContext will need to remember to re-bind it. The cleanest fix
  long-term is extracting a `services.AddAutoLeaseNetDbContext(options)` helper
  that's reusable from both prod and test composition.

## Tests

| Project                  | Before workstream | After workstream |
| ------------------------ | ----------------- | ---------------- |
| Adapters.Tajeer          | 45                | 45               |
| Adapters.Common          | 20                | 20               |
| Infrastructure           | 4 (Integration)   | 3 + 4 Integration (new interceptor tests run in unit lane) |
| Application              | 43                | 43               |
| Bff                      | 40                | 40               |
| **Total (unit + smoke filter)** | **148**           | **151**          |

3 new tests, no regressions, no deletions.

## Follow-ups noted (not in scope here)

- Extract `services.AddAutoLeaseNetDbContext(...)` helper to centralize interceptor wiring (see Week 2 saga prep).
- Add a tenancy `SESSION_CONTEXT` interceptor when RLS lands (Week 2 Day 9).
- Outbox pattern (Week 4 reliability work) — current post-commit publish has at-most-once semantics; if a handler throws after commit, the event is lost.
