# Day 24 — Quote PDF Generation (QuestPDF)

**Date**: 2026-06-15  
**Duration**: 1 day (5–7 hrs)  
**Goal**: Generate professional quote PDFs and wire send-to-customer flow

---

## Scope

1. **QuestPDF template** for quotations (minimal design):
   - Company header (logo, contact)
   - Quote number, date, valid-until
   - Customer info (name, ID, email)
   - Line items (vehicle, rent policy, rates, total)
   - Terms & conditions footer
   - Bank details for payment

2. **QuoteGeneratedPdf command** (`IMediator`):
   - Input: QuotationId, TenantId, locale (AR/EN)
   - Output: byte[], filename
   - Idempotent (cache via Redis 24h)

3. **SendQuoteToPdfCommand + handler**:
   - Input: QuotationId, RecipientEmail
   - Call IDocumentGenerator.GeneratePdfAsync(...)
   - Call IEmailClient.SendAsync(...) [InMemory in dev]
   - Persist SendQuoteAttempt row (audit)

4. **BFF endpoint** `POST /api/v1/quotations/{id}/send-pdf`:
   - Requires Quotation status ∈ [Submitted, Approved]
   - Returns 202 (async dispatch)
   - Webhook: QuoteSentToPdfDomainEvent → update Quotation.PdfSentAtUtc

5. **Domain events**:
   - QuotePdfGeneratedDomainEvent (fired on success)
   - QuoteSentToPdfDomainEvent (fired on send)

6. **Tests**:
   - Unit: PDF generation with mock data
   - Integration: Full flow (generate + send via InMemory email)

---

## Tasks

- [ ] Add IDocumentGenerator port + InMemory adapter (returns dummy PDF)
- [ ] Implement QuestPDF builder (C# minimal template)
- [ ] Add QuoteGeneratedPdf command/handler + idempotency
- [ ] Add SendQuotePdf command/handler (email dispatch)
- [ ] Wire domain events → update Quotation.PdfSentAtUtc
- [ ] Create BFF endpoint POST /quotations/{id}/send-pdf
- [ ] Unit tests for PDF generation
- [ ] Integration test: Generate + send flow
- [ ] Build + verify tests pass
- [ ] Commit

---

## References

- Spec 02 §6.1 Quote Approval Workflow Saga
- CLAUDE.md §2 TDD discipline, §3 Plans of tasks
- `packages/adapters/AutoLeaseNet.Adapters.Email.InMemory/` (pattern)

## Notes

- Use file-scoped namespaces (CLAUDE.md §3)
- Idempotency key: `tenant:{tenantId:N}:quote-pdf:{quoteId:N}`
- Email adapter is stubbed; real SendGrid wired Phase 2
- Locale-aware: AR vs EN template variant
