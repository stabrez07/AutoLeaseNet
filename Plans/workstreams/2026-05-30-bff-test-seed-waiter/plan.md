# Workstream — BffTestSeedWaiter extract

**Date opened**: 2026-05-30
**Predecessors**: ZATCA adapter skeleton (#28); BffTestHostDefaults extract (#25).
**Goal**: Collapse the demo-seed bootstrap+poll loop duplicated across 9 BFF test factories into one shared helper. **Explicitly NOT** changing test behaviour, semantics, or timeout.

## Why now

Four consecutive workstream retros (Day-21 Incidents, Customer-Portal scaffold, Tajeer GetAsync, My Vehicles) flagged this dup as overdue. PR #28's ZATCA work was the fifth — adding a fifth Zatca key to `BffTestHostDefaults` was a one-line change because we already extracted the config; but every endpoint factory still carries its own ~15-line `EnsureSeededAsync`. The pattern is identical except for **which entity to wait for**:

| Factory | Waits for |
|---|---|
| `MyVehiclesFactory`, `MyVehicleDetailFactory`, `MyLeaseDetailFactory`, `MeFactory`, `SaveContractEndpointFactory` | `Customers.AnyAsync()` |
| `CheckInFactory`, `ExtendSuspendFactory`, `InspectionFactory`, `SmsE2EFactory` | `Leases.AnyAsync(l => l.Status == Active)` |
| `IncidentFactory` | `Incidents.AnyAsync()` |

Extracting now keeps the pattern fresh and means the **next** workstream that adds a factory just calls `factory.EnsureDemoSeededAsync(db => db.X.AnyAsync(), nameof(db.X))` instead of copying 15 lines.

## Scope

**In** (this PR):
- New static helper `BffTestHostDefaults.EnsureDemoSeededAsync(WebApplicationFactory<Program> factory, Func<AutoLeaseNetDbContext, Task<bool>> readinessCheck, string entityName, TimeSpan? timeout = null)`:
  - Boots the host via a probe `CreateClient()` call
  - Resolves `IDataSeeder` from a scope, calls `SeedAsync`
  - Polls the readiness predicate every 100ms until satisfied or timeout (default 120s)
  - On timeout, throws `InvalidOperationException` with `{entityName}` in the message
- Update 9 factories to delegate to the helper:
  - `MyVehiclesFactory` (`db.Customers.AnyAsync()`)
  - `MyVehicleDetailFactory` (`db.Leases.AnyAsync(l => l.Status == Active)`)
  - `MyLeaseDetailFactory` (`db.Leases.AnyAsync(l => l.Status == Active)`)
  - `MeFactory` (`db.Customers.AnyAsync()`)
  - `SaveContractEndpointFactory` (`db.Customers.AnyAsync()`) — preserves richer error context as a custom message override
  - `CheckInFactory` (`db.Leases.AnyAsync(l => l.Status == Active)`)
  - `ExtendSuspendFactory` (`db.Leases.AnyAsync(l => l.Status == Active)`)
  - `InspectionFactory` (`db.Leases.AnyAsync(l => l.Status == Active)`)
  - `IncidentFactory` (`db.Incidents.AnyAsync()`)
  - `SmsE2EFactory` (whichever predicate it currently uses — TBD on read)
- Each factory keeps its own `_seeded` flag so cross-test caching behaviour is unchanged.

**Out** (deferred):
- Sharing the `_seeded` flag itself — would force a class fixture refactor, much wider blast radius.
- Renaming `EnsureSeededAsync` to a more descriptive name — many test files call it; not worth the churn for naming.
- Changing the default 120s timeout, or any per-factory variation.
- Refactoring `Pick*` helpers (`PickAnyCustomerIdAsync`, etc.) into shared helpers — distinct read queries per factory, no shared shape.

## Design notes

### Why an extension method on `WebApplicationFactory<Program>` over a service-locator pattern

Two options were considered:

(a) **Extension method**: `await factory.EnsureDemoSeededAsync(db => db.Customers.AnyAsync(), "Customers");`
(b) **Static helper**: `await BffTestHostDefaults.EnsureDemoSeededAsync(factory, db => db.Customers.AnyAsync(), "Customers");`

Both work. Going with (b) — static method on the existing `BffTestHostDefaults` class — because:
1. `BffTestHostDefaults` is already the home of the shared test-host wiring (`Defaults()`, `DemoSeedDefaults()`, `ReplaceDbContextWithInMemory`). Adding `EnsureDemoSeededAsync` keeps the surface area in one file.
2. Extension methods on `WebApplicationFactory<Program>` would have to live in a separate static class anyway (extension methods can't go inside the factory class itself); a sibling static class adds a discovery problem ("where is this helper?") that the central helper avoids.

### Predicate vs entity-set parameter

Tempting to make the helper take `Func<DbSet<T>, IQueryable<T>>` or similar, but that locks the entity type at compile time and forces type parameters on every call site. The `Func<AutoLeaseNetDbContext, Task<bool>>` shape is dead simple and works for arbitrary multi-table readiness checks (e.g. "wait until at least one Customer AND one Vehicle exists" — useful if the seeder ever evolves to a stagger).

### Timeout stays 120s

`SaveContractEndpointFactory`'s richer diagnostic comment (mode + count + db name) gets preserved by passing a custom message override — but the timeout itself stays the shared 120s default. Anything shorter starts to flake on contended runners.

## Plan (mechanical)

1. Read every factory's `EnsureSeededAsync` to confirm the readiness predicate.
2. Add `BffTestHostDefaults.EnsureDemoSeededAsync(...)` helper.
3. Update each factory: collapse `EnsureSeededAsync` body to a single `await BffTestHostDefaults.EnsureDemoSeededAsync(...)` call after the `_seeded` short-circuit.
4. `dotnet build` + `dotnet test` — expect 368/368 unchanged.
5. retro + ai_context bump + commit + PR + squash-merge.

## Risks

- **Subtle behaviour change in the SaveContractEndpointFactory error message**. Mitigation: keep the richer diagnostic via an optional `Func<string>?` message override on the helper.
- **Helper API ergonomics could be wrong on first try**. Mitigation: all 9 factories migrate in one PR — if the API is clumsy, the friction is visible immediately.

## Definition of Done

- [x] `BffTestHostDefaults.EnsureDemoSeededAsync(...)` helper added.
- [x] 8 factories migrated (SmsE2EFactory excluded — uses inline seed, not `EnsureSeededAsync`); each `EnsureSeededAsync` body is now 4–6 lines.
- [x] Full `dotnet test` still 368/368 green; no behaviour change.
- [x] retrospective.md filed.
- [x] ai_context.md note updated (current repo state line).
- [ ] PR opened, CI green, squash-merged.
