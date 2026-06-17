# Workstream: Contract Lifecycle, Invoicing & Fleet UI

**Date:** 2026-06-17  
**Branch:** `feat/mock-ui-seed-mode`  
**Scope:** Mock UI only (no .NET backend changes)  
**Owner:** Shams Tabrez

---

## Goals

1. **Vehicle card grid** — Replace table with photo card grid; real car thumbnails via Imagin Studio CDN
2. **Contract Management lifecycle** — Full quotation → contract → operations flow with damages & violations
3. **Monthly Invoice Generation** — Fixed flat rent, VAT 15%, per-contract, download PDF
4. **Advance Payments + FIFO** — Customer account, record payments, auto-allocate FIFO
5. **Statement of Account** — Per customer, date range, running balance, download
6. **Bulk download everywhere** — CSV export on every list page; Excel-compatible

---

## New Interfaces (bff-client.ts)

### Vehicle
```
VehicleSummary += color, bodyType, fuelType, transmissionType, seats, thumbnailUrl
```

### Damage Recording
- `DamageRecord` — type (8 types), location, severity (4), fault, estimatedCostSar, repairStatus
- `CreateDamageRecordRequest`
- `DamageType`: Accident | ScratchDent | Glass | TyreWheel | Mechanical | Flood | TheftVandalism | Fire | Other
- `DamageSeverity`: Minor | Moderate | Major | TotalLoss
- `DamageFault`: Customer | ThirdParty | Unknown | ActOfGod
- `RepairStatus`: Pending | InProgress | Completed | Waived

### Traffic Violations
- `TrafficViolation` — type (9 types), authority, fineAmountSar, responsibleParty, paymentStatus
- `CreateTrafficViolationRequest`
- `ViolationType`: Speeding | Parking | RedLight | WrongWay | MobilePhone | ExpiredRegistration | Seatbelt | RecklessDriving | Other
- `ViolationAuthority`: Muroor | Municipality | MOT | Other

### Invoices
- `Invoice` — fixed monthly rent, VAT 15%, per billing period
- `InvoiceLine`
- `GenerateInvoiceRequest`
- `BulkGenerateInvoicesRequest`
- `InvoiceStatus`: Draft | Issued | PartiallyPaid | Paid | Overdue | Cancelled

### Advance Payments & FIFO
- `AdvancePayment` — amount, paymentMethod, receivedDate, allocations
- `PaymentAllocation` — invoice reference, amount allocated
- `RecordAdvancePaymentRequest`
- `PaymentMethod`: Cash | CreditCard | BankTransfer | Cheque | OnlineTransfer

### Statement of Account
- `StatementOfAccount` — customerId, period, openingBalance, transactions[], closingBalance
- `SoaTransaction` — date, type, reference, debit, credit, runningBalance

---

## New Pages

| Page | Route | Description |
|---|---|---|
| Vehicle card grid | `/vehicles` | 3-col card grid, Imagin Studio photos, full spec pills |
| Contract detail | `/leases/[id]` | 5 tabs: Overview, Damages, Violations, Invoices, History |
| Quotation detail | `/quotations/[id]` | Status flow + convert to contract |
| Invoice list | `/invoices` | Filter by status, customer, period; bulk generate; download CSV |
| Invoice detail | `/invoices/[id]` | Full detail + print-ready layout |
| Bulk generate | `/invoices/generate` | Select month/year, preview, generate all active contracts |
| Customer account | `/customers/[id]/account` | Advance payments list, SOA, FIFO allocation view |

---

## Mock Data Seeding

| Entity | Count | Strategy |
|---|---|---|
| DamageRecords | ~400 | 1-3 per lease with incident flag |
| TrafficViolations | ~600 | 0-3 per active lease |
| Invoices | ~2800 | 1 per contract per month for duration |
| AdvancePayments | ~300 | 2-4 per B2B customer |

---

## Tasks

- [x] Write plan file
- [ ] Update VehicleSummary + mock getVehicles
- [ ] Add all new interfaces (Damage, Violation, Invoice, Payment, SOA)
- [ ] Seed mock data for all new entities
- [ ] Add all BffClient + MockBffClient method signatures
- [ ] Rewrite vehicles/page.tsx as card grid
- [ ] Build leases/[id]/page.tsx (5-tab contract detail)
- [ ] Build invoices/page.tsx
- [ ] Build invoices/[id]/page.tsx (detail + print)
- [ ] Build invoices/generate/page.tsx
- [ ] Build customers/[id]/account/page.tsx
- [ ] Add CSV download to vehicles, leases, customers, drivers, invoices pages
- [ ] Update app-shell navigation (add Invoices)
- [ ] Add i18n keys for all new modules
- [ ] Commit

---

## Verification Checklist

- [ ] `pnpm build` passes (0 errors)
- [ ] Vehicle card grid loads with images in browser
- [ ] Can navigate contract → damages tab → add damage record
- [ ] Can generate invoice for a contract
- [ ] Can record advance payment, see FIFO allocation
- [ ] Can download SOA as CSV
- [ ] Invoice print layout renders correctly (`window.print()`)
