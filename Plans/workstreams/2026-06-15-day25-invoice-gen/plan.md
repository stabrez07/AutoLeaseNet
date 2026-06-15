# Day 25: Invoice Generation on LeaseIssued Event

**Scope:** Create Invoice domain aggregate and auto-generate invoice when lease transitions to Active (Issued).  
**Ref:** [Spec 02 §4.4](../../Specs/02-state-machines-and-sagas.md#44-invoice-lifecycle-state-machine) — Invoice state machine, ZATCA integration architecture.  
**Build Order:** Foundation → Day-25 → Day-26 (ZATCA submission) → Day-27 (UBL + signing).  

---

## Objectives

1. **Invoice Domain Aggregate** (AutoLeaseNet.Domain/Billing/Invoice.cs)
   - Root aggregate per Spec 02 §4.4: `Draft` → `Submitted` → `Cleared` → `Finalized` (+ error states)
   - Fields: InvoiceNumber (auto-generated per tenant), LeaseId, CustomerId, TenantId, CreatedAtUtc, Status
   - Line items mapped from Lease + RentPolicy + ExtendedCoverage (Phase 2 detail)
   - Implements `AggregateRoot` / `Entity` pattern (Spec 01 §2)

2. **Domain Event: InvoiceGeneratedDomainEvent**
   - Fired when Invoice created (Draft state)
   - Payload: InvoiceId, LeaseId, CustomerId, InvoiceNumber, TotalSar

3. **Application Service: CreateInvoiceFromLeaseCommand** + Handler
   - MediatR command; triggered by LeaseIssuedDomainEvent subscriber
   - Idempotency: prevents duplicate Invoice per Lease (Redis cache)
   - Logs via [LoggerMessage] pattern

4. **Repository: IInvoiceRepository** + EF Core impl (EfInvoiceRepository)
   - CRUD operations + GetByLeaseIdAsync(), GetByNumberAsync()
   - RLS enforcement (Spec 01 §3)

5. **BFF Endpoint: GET /api/v1/leases/{id}/invoice** (read-only Phase 1)
   - Returns Invoice DTO with status + totals
   - 404 if lease not found or invoice not yet generated

6. **Unit + Integration Tests**
   - Invoice.CreateFromLease() domain logic
   - CreateInvoiceFromLeaseCommandHandler with inMemory repos + clock
   - EfInvoiceRepository CRUD + RLS
   - BFF endpoint GET /leases/{id}/invoice

---

## Tasks (2–5 min each)

- [ ] **1. Invoice Domain Aggregate** — `Domain/Billing/Invoice.cs` (60 lines)
- [ ] **2. InvoiceGeneratedDomainEvent** — `Domain/Billing/InvoiceGeneratedDomainEvent.cs` (20 lines)
- [ ] **3. IInvoiceRepository Port** — `Application.Ports/Persistence/IInvoiceRepository.cs` (30 lines)
- [ ] **4. EfInvoiceRepository + DbContext** — Add DbSet + EF configuration
- [ ] **5. CreateInvoiceFromLeaseCommand + Handler** — `Application/Billing/InvoiceCommands.cs` + handlers (80 lines)
- [ ] **6. DomainEventHandlers registration** — Subscribe LeaseIssuedDomainEvent → CreateInvoiceFromLeaseCommand
- [ ] **7. BFF Endpoint** — `services/bff/Endpoints/LeaseInvoiceEndpoints.cs` (40 lines)
- [ ] **8. Unit Tests** — Invoice.CreateFromLease(), repo CRUD, handler idempotency
- [ ] **9. Integration Tests** — Full flow: Lease → LeaseIssued event → Invoice created
- [ ] **10. Build + commit** — `dotnet build`, `dotnet test`, git commit

---

## Key Decisions

1. **Auto-generation on LeaseIssued:** Invoice is a derived aggregate; created automatically when lease transitions to Active (not on user demand).
2. **Idempotency:** Prevents race condition if LeaseIssuedDomainEvent is replayed. Key = `tenant:{tenantId:N}:invoice-from-lease:{leaseId:N}` (24h TTL).
3. **InvoiceNumber generation:** Tenant-scoped sequential number (e.g., "INV-2026-0001"). Via INumberGenerator port (Phase 1: in-memory counter; Phase 2: DB sequence).
4. **Line Items:** Phase 1 scope: single line per invoice (monthly base rent). Phase 2: add insurance, extensions, adjustments.
5. **RLS:** Every Invoice row has TenantId; EF RLS enforces TENANT_ID = SESSION_CONTEXT['tenant_id'].

---

## References

- **Spec 02 §4.4:** Invoice state machine + lifecycle
- **Spec 01 §2:** Aggregate root patterns
- **Spec 01 §3:** Multi-tenancy + RLS
- **CLAUDE.md §3:** TDD + repository pattern
- **Day-5 Lease Issuance Saga:** [Spec 02 §6.2](../../Specs/02-state-machines-and-sagas.md#62-lease-issuance-saga-the-critical-one)

---

## Notes

- **Phase 2 enhancements:** Multi-line invoices, credit memos, payment allocation, recurring invoicing
- **ZATCA readiness:** Invoice.UblXml field populated by Day-26 (UBL builder)
- **No email dispatch yet** — invoices stored; customer retrieval via BFF (Day-28 notifications roadmap)

---

## Completed

- [x] Day-24: Quote PDF generation (baseline approach for document generation)
- [ ] **Day-25: This workstream** ← START
- [ ] Day-26: ZATCA submission saga + UBL builder
- [ ] Day-27: Approval tiers + routing
