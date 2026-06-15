# Vehicle Replacement Saga

**Goal**: implement Spec 02 §6.5 end-to-end for total-loss incidents and explicit replacement requests.

## Scope

1. Add a saga handler for `IncidentReportedDomainEvent` when `RequiresReplacement = true`.
2. Add the explicit replacement trigger path from BFF.
3. Reserve a replacement vehicle, create the new lease, and link it back to the source incident.
4. Close or compensate the original flow through Tajeer.
5. Extend Tajeer adapter surface for contract cancel if needed by compensation.
6. Cover the happy path, no-vehicle path, and compensation paths with tests.

## Constraints

- Keep orchestration in application/infrastructure, not domain.
- Preserve tenant scoping and idempotency.
- Use existing Vehicle and Lease aggregate methods; avoid direct state mutation.
- Follow the existing MediatR notification-handler pattern used by lease issuance SMS.

## RED → GREEN tasks

- Add failing tests for:
  - total-loss incident starts replacement saga
  - no replacement vehicle available
  - new lease save succeeds, old close fails, compensation runs
  - old close succeeds, new save fails, saga records stuck state
- Implement the saga handler.
- Extend Tajeer client with cancel support if compensation requires it.
- Wire the handler into DI and verify build/tests.

