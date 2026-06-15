# Day 26: ZATCA Submission Saga + UBL Builder

**Scope:** Real ZATCA integration for invoice clearance. Auto-submit cleared invoices to ZATCA; sign with ECDSA P-256 sandbox CSID.  
**Ref:** [Spec 02 §4.5](../../Specs/02-state-machines-and-sagas.md#45-zatca-submission-state-machine), [Spec 04 §11](../../Specs/04-integration-architecture.md#11-recipe-adding-a-new-integration) — Integration architecture, [Spec 03 §8.2](../../Specs/03-regulatory-and-data-exchange.md#82-zatca-clearance-flow) — E-Invoicing flow.  
**Build Order:** Day-25 (invoices) → **Day-26 (ZATCA)** → Day-27 (approval tiers).  

---

## Objectives

1. **ZatcaSubmission Domain Aggregate**
   - State machine per Spec 02 §4.5: Draft → Submitted → PendingClearance → Cleared → Finalized
   - Error states: SubmissionFailed, ClearanceFailed
   - Fields: SubmissionId, InvoiceId, UblXml, SignedUblXml, ZatcaTransactionId, InvoiceHash
   - Idempotency: prevents duplicate submission for same invoice

2. **UBL 2.1 XML Builder** (No external library)
   - Convert Invoice domain to standard-compliant UBL XML
   - Per Spec 03 §8.2: Invoice → <root>/<ID>/<IssueDate>/<CustomerParty>/<InvoiceLine>/<LegalMonetaryTotal>
   - Phase 1 scope: mandatory fields only (Phase 2: extended fields, attachments)

3. **ECDSA P-256 Signing**
   - Read sandbox CSID private key (Phase 1: hardcoded in config; Phase 2: Azure Key Vault)
   - Sign canonical UBL XML (SHA-256 hash → ECDSA signature)
   - Per ZATCA spec: signed-xml structure + certificate chain

4. **IZatcaClient Port + Real Implementation**
   - Port: SubmitInvoiceAsync(signedUbl, invoiceHash) → TransactionId + ClearanceStatus
   - Real impl: HTTP POST to ZATCA Fatoorah sandbox
   - InMemory companion: returns mock TransactionId + Cleared status
   - Idempotency: IZatcaIdempotencyStore caches submissions (prevent duplicate posts)

5. **ZatcaSubmissionCommandHandler (Saga Orchestrator)**
   - Triggered by InvoiceGeneratedDomainEvent
   - Workflow: (1) Build UBL, (2) Sign XML, (3) Submit to ZATCA, (4) Poll clearance status, (5) Update Invoice + ZatcaSubmission
   - Polly retry pipeline: transient failures (network, rate limit)
   - Logs via [LoggerMessage] pattern

6. **BFF Endpoint: GET /api/v1/invoices/{id}/zatca-status**
   - Returns ZatcaSubmissionDto with submission state + transaction ID + clearance timestamp
   - 404 if invoice not found or submission not yet attempted

7. **Unit + Integration Tests**
   - UBL XML builder: Invoice → valid XML structure
   - ECDSA signing: produces valid P-256 signature
   - ZatcaSubmissionCommandHandler: idempotency, retry logic
   - BFF endpoint: 200 OK, 404 Not Found scenarios

---

## Tasks (2–5 min each)

- [ ] **1. ZatcaSubmission Domain Aggregate** — `Domain/Zatca/ZatcaSubmission.cs` (100 lines)
- [ ] **2. ZatcaSubmissionStatus Enum** — `Domain/Zatca/ZatcaSubmissionStatus.cs` (20 lines)
- [ ] **3. UBL XML Builder** — `Application/Billing/UblXmlBuilder.cs` (150 lines, no external lib)
- [ ] **4. ECDSA Signing Service** — `Infrastructure/Cryptography/EcdsaSigner.cs` (60 lines)
- [ ] **5. IZatcaClient Port** — `Application.Ports/Integrations/IZatcaClient.cs` (30 lines)
- [ ] **6. ZatcaClient Real Impl** — `Adapters/Zatca/ZatcaClient.cs` (80 lines, HTTP POST)
- [ ] **7. InMemory ZATCA Adapter** — `Adapters/Zatca/ZatcaClientInMemory.cs` (40 lines)
- [ ] **8. IZatcaSubmissionRepository** — `Application.Ports/Persistence/IZatcaSubmissionRepository.cs` (25 lines)
- [ ] **9. EfZatcaSubmissionRepository** — `Infrastructure/Persistence/Repositories/EfZatcaSubmissionRepository.cs` (60 lines)
- [ ] **10. ZatcaSubmissionCommand + Handler** — `Application/Billing/ZatcaCommands.cs` + handlers (120 lines)
- [ ] **11. BFF Endpoint** — `services/bff/Endpoints/ZatcaStatusEndpoints.cs` (50 lines)
- [ ] **12. Unit Tests** — UBL builder, ECDSA signing, handler idempotency
- [ ] **13. Integration Tests** — Full flow: Invoice → UBL → Sign → Submit → Cleared
- [ ] **14. Build + Commit** — `dotnet build`, `dotnet test`, git commit

---

## Key Decisions

1. **UBL 2.1 from scratch:** No third-party library → full control, no dependency lock-in
2. **Signing strategy:** ECDSA P-256 (standard KSA requirement); Phase 2: HSM via Azure Key Vault
3. **Submission trigger:** LeaseIssuedDomainEvent → InvoiceGeneratedDomainEvent → ZatcaSubmissionCommand (async via Outbox)
4. **Polling model:** Phase 1 synchronous submission; Phase 2: async polling job (Hangfire/Quartz)
5. **Idempotency:** ZatcaSubmissionIdempotencyStore (Redis key = `tenant:{tenantId}:zatca-submit:{invoiceId}`)
6. **ZATCA config:** Sandbox CSID hardcoded in appsettings.json (Phase 2: per-tenant config)

---

## References

- **Spec 02 §4.5:** ZatcaSubmission state machine + lifecycle
- **Spec 03 §8.2:** E-Invoicing clearance flow (KSA regulatory)
- **Spec 04 §11:** Integration adapter recipe
- **ZATCA Fatoorah Sandbox API:** https://zatca.gov.sa/en/E-Invoicing/onboarding/Pages/default.aspx
- **UBL 2.1 Standard:** OASIS Universal Business Language (public spec)

---

## Notes

- **Phase 1 MVP:** Synchronous submission on invoice creation; happy path only (errors logged, not retried)
- **Phase 2 enhancements:** Async polling, HSM signing, per-tenant CSID rotation, credit memo support
- **No email dispatch yet** — invoices stored; ZATCA clearance is the delivery gate (customer portal retrieval in Day-28)
- **Test data:** Sandbox CSID + private key provided by ZATCA (already in hand per dependency checklist)

---

## Completed

- [x] Day-24: Quote PDF generation
- [x] Day-25: Invoice domain + EF persistence + endpoint
- [ ] **Day-26: This workstream** ← START
- [ ] Day-27: Approval tiers + 3-tier routing
