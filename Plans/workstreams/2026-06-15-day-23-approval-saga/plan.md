# Day-23 — Quote Approval Saga + Inbox + Endpoints

## Goal
Implement the Quotation approval workflow: command handlers (Application layer), approver inbox query (Infrastructure layer), notification handler, and all BFF CRUD+state-change endpoints.

## Dependencies
- **Day-22 completion** — `IQuotationRepository`, `IApprovalTierRepository`, EF implementations, ApprovalTier seed. **Now merged** into `main`.
- **Quotation aggregate** (PRs #31, #32) — `Quotation.cs`, `ApprovalTierEvaluator`, domain events. Already in `main`.
- `IQuoteNumberGenerator` port — new port introduced in this workstream.

## Architecture reminders
- Command handlers → `AutoLeaseNet.Application` (no DbContext)
- Query handlers → `AutoLeaseNet.Infrastructure` (need DbContext)
- No `DateTime.UtcNow` — always `IClock`
- `[LoggerMessage]` source generators, event IDs 8xxx
- Every state-changing BFF endpoint requires `Idempotency-Key` header
- `Result<T>` / `IncidentCommandResult`-shaped result, not exceptions for business errors

## Task list (TDD: RED → GREEN each)

### Step 1 — Port: IQuoteNumberGenerator
- [ ] Create `Application.Ports/Sales/IQuoteNumberGenerator.cs` (port interface)
- [ ] Create `Infrastructure/Sales/SequentialQuoteNumberGenerator.cs` (impl: `Q-{yyyyMMdd}-{sequence:D4}`)

### Step 2 — Application: Sales commands/queries
- [ ] `Sales/QuotationCommands.cs` — all 6 commands + `QuotationCommandResult` record
- [ ] `Sales/QuotationQueries.cs` — `GetApprovalInboxQuery` + `ApprovalInboxItemDto`
- [ ] `Sales/CreateQuotationCommandHandler.cs`
- [ ] `Sales/AddQuotationLineCommandHandler.cs`
- [ ] `Sales/SubmitQuotationForApprovalCommandHandler.cs`
- [ ] `Sales/RecordApprovalDecisionCommandHandler.cs`
- [ ] `Sales/RecallQuotationCommandHandler.cs`
- [ ] `Sales/MarkQuotationSentToCustomerCommandHandler.cs`

### Step 3 — Infrastructure: Inbox query handler
- [ ] `Infrastructure/Sales/QuotationQueryHandlers.cs` — `GetApprovalInboxQueryHandler`

### Step 4 — Notification handler
- [ ] `Application/Sales/Notifications/QuotationSubmittedForApprovalNotificationHandler.cs`

### Step 5 — BFF endpoint
- [ ] `services/bff/Endpoints/QuotationEndpoints.cs` — 8 endpoints + request records

### Step 6 — Wire DI
- [ ] Register `IQuoteNumberGenerator`, `IQuotationRepository`, `IApprovalTierRepository` in `ServiceCollectionExtensions`
- [ ] Register `MediatR` handlers from Infrastructure Sales namespace

### Step 7 — Unit tests (Application layer)
- [ ] `Application.Tests/Sales/CreateQuotationCommandHandlerTests.cs`
- [ ] `Application.Tests/Sales/SubmitQuotationForApprovalCommandHandlerTests.cs`
- [ ] `Application.Tests/Sales/RecordApprovalDecisionCommandHandlerTests.cs`
- [ ] `Application.Tests/Sales/RecallQuotationCommandHandlerTests.cs`

### Step 8 — BFF integration tests
- [ ] `bff.tests/Endpoints/QuotationEndpointTests.cs`

### Step 9 — Verify
- [ ] `dotnet build AutoLeaseNet.sln -warnaserror` — clean
- [ ] `dotnet test AutoLeaseNet.sln --settings .runsettings` — all pass

## Definition of Done
- [ ] All tests green (including existing 384+)
- [ ] `dotnet build -warnaserror` clean
- [ ] `ai_context.md` updated with new endpoints + state
- [ ] `retrospective.md` written
