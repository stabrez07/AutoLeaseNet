# Outbox + BackgroundService drain

**Date**: 2026-05-29
**Branch**: `feat/outbox-drain`
**PR**: TBD

## Why this, why now

Today, domain events are dispatched inline post-commit via
`DomainEventDispatchInterceptor` → `IPublisher.Publish`. That works while in-process,
but:

- If a handler throws (e.g. SMS provider 500), we log + swallow. No retry.
- If the BFF process dies after `SaveChangesAsync` but before the inline publish
  drains, the event is gone.
- We have no audit trail of what events were raised, when they ran, or what
  failed.

The Outbox pattern (Spec 01 §2 principle #7) fixes all three: events become rows
in a table inside the same UoW transaction as the business state change, and a
`BackgroundService` drains them asynchronously with retry semantics.

## Honest scoping note

**This workstream closes the _domain-event delivery_ window, not the
_vendor↔local commit_ window.**

The "Tajeer 200 → local SaveChanges fails" window in the four Tajeer-touching
sagas is **not** something a write-side outbox fixes by itself. Closing it
properly would require a command-table pattern (enqueue "call Tajeer" intent,
drain it, then mutate the local aggregate) — that's a redesign of every saga
handler, not a hardening item. The current self-healing mechanism (Tajeer's
own webhook re-asserts the state via the idempotent `MarkX` transitions)
remains the contract.

What this Outbox **does** give us:

1. **Reliable async fan-out of domain events** — handler failure → retry, not
   silent log + drop.
2. **Audit trail** — every event lives in a queryable table with
   created/processed/failure timestamps.
3. **Decoupling** — the request thread no longer pays for the side-effect
   handler latency.
4. **Foundation** for a future write-side outbox if/when we go to command-table.

## Scope (this PR)

- ✅ `OutboxEvent` aggregate in `Domain/Outbox/`, mirrors `WebhookLog`
  structure: `Id`, `TenantId`, `EventType` (assembly-qualified-ish),
  `PayloadJson`, `CreatedAtUtc`, `AvailableAtUtc` (for backoff), `ProcessedAtUtc?`,
  `LastError?`, `Attempts`.
- ✅ `IOutboxRepository` port + `EfOutboxRepository`.
- ✅ EF migration `Add_OutboxEvent`.
- ✅ **Replace** `DomainEventDispatchInterceptor` with `OutboxWriteInterceptor`
  (`SavingChangesAsync` — runs INSIDE the transaction). Walks ChangeTracker
  → serializes each `IDomainEvent` to JSON → adds `OutboxEvent` rows so the
  events commit atomically with the business state.
- ✅ `OutboxDrainService : BackgroundService` — polls every
  `Outbox:DrainIntervalSeconds` (default 5s), batches by
  `Outbox:BatchSize` (default 50), deserializes by EventType, publishes via
  MediatR's `IPublisher` exactly as the inline interceptor did.
- ✅ Retry / max-attempts: handler failure increments `Attempts`, computes
  `AvailableAtUtc = now + backoff(Attempts)`. After `Outbox:MaxAttempts`
  (default 5) the row is parked with `LastError` set; manual requeue.
- ✅ `Outbox:*` config + `AddOutbox(section)` extension; wire from BFF
  composition root.
- ✅ Tests:
  - Interceptor captures events into rows in the same transaction as the
    business change (unit).
  - Interceptor clears `DomainEvents` on the entity so a second `SaveChanges`
    doesn't double-write (unit).
  - Drain dispatches all unprocessed rows in FIFO and marks them processed
    (integration-shape using EF InMemory).
  - Drain on handler failure: increments Attempts + sets `AvailableAtUtc`
    in the future.
  - Drain skips rows whose `AvailableAtUtc` is in the future (backoff).
  - Drain stops touching a row once `Attempts >= MaxAttempts`.
- ✅ Existing `LeaseIssuedSmsHandler` test continues to pass — drain
  invokes the same `IPublisher.Publish` shape as the retired inline interceptor,
  so the handler is contract-unchanged.

## NOT in scope (defer)

- ❌ **Write-side outbox for Tajeer calls** (vendor↔local commit window).
  Different pattern; needs saga handler rewrites.
- ❌ **Distributed lock for multi-instance drain.** Phase-1 is single-instance.
  Phase-2 with multiple BFF replicas needs `SELECT ... WITH (UPDLOCK, READPAST)`
  or a Redis lock so two drains don't double-publish.
- ❌ **OpenTelemetry tracing of the drain.** Worth a follow-up — drain spans
  with `event.type` + `event.id` tags would be very valuable for ops.
- ❌ **RLS on `OutboxEvents`.** It's an integration table (like `WebhookLog`);
  drain runs cross-tenant under SYSTEM. Documented exclusion in the migration.
- ❌ **Per-event metrics / dashboard.** Phase-2.

## Design

### OutboxEvent shape

```
Id              UNIQUEIDENTIFIER PK  (NEWSEQUENTIALID-friendly)
TenantId        UNIQUEIDENTIFIER NOT NULL   -- not RLS-protected (integration table)
EventType       NVARCHAR(512)     NOT NULL  -- "Namespace.TypeName, Assembly"
PayloadJson     NVARCHAR(MAX)     NOT NULL  -- System.Text.Json.JsonSerializer
CorrelationId   UNIQUEIDENTIFIER NULL       -- future causation linking
CreatedAtUtc    DATETIMEOFFSET    NOT NULL
AvailableAtUtc  DATETIMEOFFSET    NOT NULL   -- defaults to CreatedAtUtc
ProcessedAtUtc  DATETIMEOFFSET    NULL
LastError       NVARCHAR(2000)    NULL
Attempts        INT               NOT NULL  DEFAULT 0
RowVersion      ROWVERSION                  -- optimistic concurrency
```

Indexes:
- `(ProcessedAtUtc, AvailableAtUtc)` filtered on `ProcessedAtUtc IS NULL` —
  drain hot path.
- `(TenantId, EventType, CreatedAtUtc)` — operational query.

### OutboxWriteInterceptor

`SaveChangesInterceptor.SavingChangesAsync` (NOT `SavedChangesAsync` like the
old one). Runs INSIDE the transaction → if the OutboxEvent insert fails, the
business write rolls back too. This is the atomicity guarantee.

Pseudo-code:
```csharp
foreach entity tracked with DomainEvents:
    foreach domainEvent in entity.DomainEvents:
        ctx.Set<OutboxEvent>().Add(OutboxEvent.Capture(domainEvent, json, now))
    entity.ClearDomainEvents()
```

### OutboxDrainService

```
loop while not cancelled:
    rows = repo.GetDue(now, batchSize, ct)
    if rows.empty: await Task.Delay(intervalSeconds)
    foreach row:
        try:
            evt = JsonSerializer.Deserialize(row.PayloadJson, Type.GetType(row.EventType))
            notification = DomainEventNotification<T>.For(evt)
            await publisher.Publish(notification, ct)
            row.MarkProcessed(now)
        catch (Exception ex):
            row.MarkFailed(ex.Message, now, backoff)
    await uow.SaveChangesAsync(ct)
```

`Type.GetType` resolves via assembly-qualified name. We only ever serialize
types we own (in `AutoLeaseNet.Domain.dll`); that assembly is loaded into the
BFF process by definition.

### Backoff curve

Simple exponential with cap: `min(2^(attempts-1), 60) seconds`. So:
1 → 1s, 2 → 2s, 3 → 4s, 4 → 8s, 5 → 16s (parked).

### Type name strategy

Stored format: `Namespace.Type, AssemblyName` (NOT version/culture/key — those
break on rebuilds). Resolved via `Type.GetType(name, throwOnError: true)`.

Risk: domain event class rename breaks replay of in-flight rows. Mitigation
documented; in practice we don't rename event types because that's a public
contract anyway.

## Tasks (RED → GREEN)

1. **Plan** (this file).
2. **Domain `OutboxEvent` aggregate** — `Capture(eventType, json, tenantId, correlationId?, nowUtc)` factory; `MarkProcessed(now)`; `MarkFailed(error, now, retryAtUtc)`.
3. **Application port `IOutboxRepository`** — `Add`, `GetDueAsync(now, batchSize, ct)`.
4. **Infrastructure `EfOutboxRepository`** + `OutboxEventConfiguration` + EF migration `Add_OutboxEvent`.
5. **`OutboxWriteInterceptor`** in `Infrastructure.Persistence.Interceptors` — replaces `DomainEventDispatchInterceptor` in `AddAutoLeaseNetDbContext`.
6. **Retire** `DomainEventDispatchInterceptor` registration. Keep the class file with a `[Obsolete]` (or delete; nothing depends on it after #5).
7. **`OutboxOptions` + `AddOutbox(section)`** in a new `AutoLeaseNet.Application.Outbox` namespace.
8. **`OutboxDrainService : BackgroundService`** — drain loop + backoff.
9. **Wire** in `Program.cs` + appsettings.json defaults.
10. **Tests** — capture (unit), drain happy-path (unit using EF InMemory + a substituted `IPublisher`), drain retry/backoff (unit), drain max-attempts park (unit).
11. **Apply migration** to local `AutoLeaseNet_Dev`.
12. **Full test suite** — must stay 264+ green.
13. **BFF smoke** — start BFF, save a contract, then synthesise a Tajeer webhook → verify the `LeaseIssuedDomainEvent` row appears in `dbo.OutboxEvents` AND the SMS log message fires (via InMemory SMS).
14. **Update `ai_context.md` + retrospective.**
15. **Commit + PR + squash-merge.**

## Risks

- **Type resolution on rebuild**: if we rename a domain event type, in-flight
  rows stop replaying. Acceptable in Phase 1; document.
- **Test isolation**: drain background service running in `WebApplicationFactory`
  tests could surprise existing endpoint tests. Either (a) only register the
  drain service outside `IsDevelopment` test factories, or (b) disable the
  drain via `Outbox:Enabled=false` for tests. Going with (b) — config-driven
  default.
- **Cancellation**: the drain must cooperate with `IHostApplicationLifetime`.
  Use `BackgroundService.ExecuteAsync(CancellationToken)` correctly.
- **Drain re-entrancy / multi-instance**: explicitly Phase-1 single-instance.
  Documented in plan.

## Definition of done

- [ ] All 15 tasks complete.
- [ ] `dotnet test AutoLeaseNet.sln --settings .runsettings` green (≥ 264).
- [ ] EF migration applied to local `AutoLeaseNet_Dev`.
- [ ] BFF smoke: save-contract → webhook → OutboxEvent row + handler log message.
- [ ] `ai_context.md` updated.
- [ ] Retrospective written.
- [ ] PR squash-merged via branch protection.
