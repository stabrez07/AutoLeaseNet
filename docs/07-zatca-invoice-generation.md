# 07 — ZATCA Invoice Generation

**Status**: ⏳ Placeholder — to be expanded just-in-time before Week 4 of Phase 1 (when invoice work begins)
**Phase**: Foundation
**Owner**: Architecture
**Depends on**: [02](./02-state-machines-and-sagas.md), [04](./04-integration-architecture.md)
**Last updated**: 2026-05-17

---

## Status: placeholder

This doc is intentionally minimal until the ZATCA workstream starts. The high-level design and saga are already covered in:

- [Doc 02 §4.5](./02-state-machines-and-sagas.md#45-zatcasubmission) — `ZatcaSubmission` state machine
- [Doc 02 §6.6](./02-state-machines-and-sagas.md#66-zatca-invoice-submission-saga) — clearance vs reporting saga, PIH chain integrity
- [Doc 04 §10](./04-integration-architecture.md#10-the-integration-catalog) — ZATCA listed as Pattern B Phase 1 adapter
- [Doc 06 §5.11](./06-bff-api-surface.md#511-invoices--payments) — invoice + ZATCA submission API endpoints

## What this doc will cover when expanded

1. **ZATCA Phase 2 overview** — Tax (B2B clearance) vs Simplified (B2C reporting); 24h reporting window; UBL 2.1 invoice schema
2. **EGS onboarding lifecycle** — Compliance CSID → Production CSID via Fatoorah portal; per-tenant per-organization MOI registration; annual renewal
3. **UBL 2.1 invoice structure** — Invoice header, seller/buyer parties, invoice lines, tax totals, document references (for credit notes), `xades:SignedProperties`
4. **Cryptographic stamping** — ECDSA P-256 with private key from CSID; canonical XML (XML-DSig C14N); invoice hash (SHA-256); xAdES-BES signature
5. **TLV QR code** — Tagged data (seller name, VAT, timestamp, total, VAT amount, hash, signature, public key); base64-encoded; embedded in PDF
6. **PIH chain** — Per-tenant `ZatcaChainState`; first invoice uses `0`-base64; failed submission does NOT advance chain; chain-break detection + halt
7. **Library choice** — Build minimal in-house OR use community ZATCA .NET SDK; decision recorded as ADR
8. **Adapter design** — `IZatcaClient` interface per [doc 04 Pattern B](./04-integration-architecture.md#32-pattern-b--specific-vendor-system-with-one-api); error mapping (warnings vs rejections); retry only on network errors
9. **Sandbox vs production** — same XML format; different endpoint + CSID; switched via `ZatcaOptions.Environment`
10. **Edge cases** — Credit notes referencing UUID; void via credit note; chain reconciliation; time zone (UTC in XML, KSA local in display)
11. **Testing** — Offline XSD validation; sandbox round-trip; snapshot tests for cleared XML

## Inputs already locked

- ZATCA Sandbox CSID is issued (per [project decisions](../../memory/project_decisions.md))
- Production CSID via Fatoorah portal — out of Phase 1 critical path
- Implementation: Pattern B adapter at `packages/adapters/AutoLeaseNet.Adapters.Zatca/`

## When this doc gets expanded

Trigger: Start of Week 4 in Phase 1, OR earlier if invoice scope changes.
