'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { bff, type Invoice } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../../components/ui'
import { CompanyLogo } from '../../../components/company-logo'

// ── Helpers ──────────────────────────────────────────────────────────────────

const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Draft: 'slate', Submitted: 'amber', Cleared: 'green', Finalized: 'blue', SubmissionFailed: 'red', ClearanceFailed: 'red', Voided: 'red',
}

function fmt(n: number) {
  return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function safeDate(s: string) {
  return new Date(s).toLocaleDateString('en-GB', { day: '2-digit', month: 'long', year: 'numeric' })
}


// ── Main Page ────────────────────────────────────────────────────────────────

export default function InvoiceDetailPage() {
  const router = useRouter()
  const params = useParams()
  const id = params?.id as string

  const [invoice, setInvoice] = useState<Invoice | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [markingPaid, setMarkingPaid] = useState(false)
  const [paidInput, setPaidInput] = useState('')

  useEffect(() => {
    if (!id) return
    bff.getInvoiceById(id)
      .then(setInvoice)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [id])

  async function handleMarkPaid() {
    const amt = parseFloat(paidInput)
    if (!amt || !invoice) { alert('Enter valid amount'); return }
    setMarkingPaid(true)
    try {
      const updated = await bff.markInvoicePaid(id, amt, crypto.randomUUID())
      setInvoice(updated)
      setPaidInput('')
    } catch (e) { alert((e as Error).message) }
    finally { setMarkingPaid(false) }
  }

  function handlePrint() { window.print() }

  function downloadCsv() {
    if (!invoice) return
    const rows = [
      ['Field', 'Value'],
      ['Invoice #', invoice.invoiceNumber],
      ['Lease #', invoice.leaseNumber],
      ['Customer', invoice.customerDisplayName],
      ['Vehicle', invoice.vehicleMakeModel],
      ['Plate EN', invoice.vehiclePlate],
      ['Plate AR', invoice.vehiclePlateAr],
      ['Billing Period', `${invoice.billingPeriodStart} to ${invoice.billingPeriodEnd}`],
      ['Issued Date', invoice.issuedDate],
      ['Due Date', invoice.dueDate],
      ['Status', invoice.status],
      ['Quotation #', invoice.quotationNumber ?? ''],
      ['PO #', invoice.poNumber ?? ''],
      ['Sub-Total (SAR)', String(invoice.subTotalSar)],
      ['VAT 15% (SAR)', String(invoice.vatAmountSar)],
      ['Total (SAR)', String(invoice.totalSar)],
      ['Paid (SAR)', String(invoice.paidAmountSar)],
      ['Balance (SAR)', String(invoice.balanceSar)],
    ]
    const csv = rows.map((r) => r.map((c) => `"${c}"`).join(',')).join('\n')
    const a = document.createElement('a')
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `${invoice.invoiceNumber}.csv`
    a.click()
  }

  function handleRetry() {
    setLoading(true)
    setError(null)
    bff.getInvoiceById(id).then(setInvoice).catch((e: Error) => setError(e.message)).finally(() => setLoading(false))
  }

  if (loading) return <Spinner label="Loading invoice..." />
  if (error) return <ErrorBox message={error} onRetry={handleRetry} retryLabel="Retry" />
  if (!invoice) return null

  return (
    <>
      <style jsx global>{`
        @media print {
          @page { size: A4; margin: 15mm; }
          body { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        }
      `}</style>

      <div className="space-y-4">
        {/* ── Screen-only page header ── */}
        <div className="print:hidden">
          <PageHeader
            title={invoice.invoiceNumber}
            subtitle={`${invoice.customerDisplayName} / ${invoice.vehicleMakeModel}`}
          />
        </div>

        {/* ── Printable invoice layout ── */}
        <div className="print:block">

          {/* ── Section 1: Logo + TAX INVOICE + QR ── */}
          <Card className="p-6 print:shadow-none print:border-none print:p-0">
            <div className="flex items-start justify-between gap-4">
              {/* Left: company logo */}
              <CompanyLogo width={180} height={54} />

              {/* Center: title + invoice number + status */}
              <div className="text-center shrink-0">
                <p className="text-2xl font-bold text-slate-900 tracking-wide">TAX INVOICE</p>
                <p className="mt-1 font-mono text-sm text-slate-600">{invoice.invoiceNumber}</p>
                <div className="mt-2 flex justify-center">
                  <Badge tone={STATUS_TONES[invoice.status] ?? 'slate'}>{invoice.status}</Badge>
                </div>
              </div>

              {/* Right: placeholder for future QR */}
              <div className="w-20" />
            </div>

            {/* ── Horizontal rule ── */}
            <hr className="my-5 border-slate-200" />

            {/* ── Section 3: Info grid ── */}
            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 md:grid-cols-3 print:grid-cols-3">

              {/* Supplier */}
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Supplier</p>
                <p className="mt-1 font-semibold text-slate-900">{invoice.supplierName}</p>
                <p className="font-mono text-xs text-slate-500 mt-0.5">CR No: {invoice.supplierCrNo}</p>
                <p className="font-mono text-xs text-slate-500">VAT No: {invoice.supplierVatNo}</p>
              </div>

              {/* Billed To */}
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Billed To</p>
                <p className="mt-1 font-semibold text-slate-900">{invoice.customerDisplayName}</p>
              </div>

              {/* Contract */}
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Contract</p>
                <p className="mt-1 font-mono text-sm text-slate-800">{invoice.leaseNumber}</p>
                <p className="text-xs text-slate-500 mt-0.5">{invoice.vehicleMakeModel}</p>
                <p className="text-xs text-slate-500">
                  Plate EN: <span className="font-semibold">{invoice.vehiclePlate}</span>
                  {' | '}
                  Plate AR: <span className="font-semibold" dir="rtl">{invoice.vehiclePlateAr}</span>
                </p>
              </div>

              {/* Reference */}
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Reference</p>
                <p className="mt-1 text-sm text-slate-800">
                  Quotation: <span className="font-mono">{invoice.quotationNumber ?? 'N/A'}</span>
                </p>
                <p className="text-sm text-slate-800">
                  PO No: <span className="font-mono">{invoice.poNumber ?? 'N/A'}</span>
                </p>
              </div>

              {/* Dates */}
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Dates</p>
                <p className="mt-1 text-sm text-slate-800">Issued: {safeDate(invoice.issuedDate)}</p>
                <p className="text-sm text-slate-800">
                  Due:{' '}
                  <span className={invoice.balanceSar > 0 ? 'font-semibold text-red-700' : ''}>
                    {safeDate(invoice.dueDate)}
                  </span>
                </p>
              </div>

              {/* Billing Period */}
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Billing Period</p>
                <p className="mt-1 text-sm text-slate-800">
                  <span className="font-semibold">{invoice.billingPeriodStart}</span>
                  {' to '}
                  <span className="font-semibold">{invoice.billingPeriodEnd}</span>
                </p>
              </div>
            </div>
          </Card>

          {/* ── Section 4: Line items table ── */}
          <Card className="mt-4 overflow-hidden p-0 print:shadow-none print:border-none print:mt-6">
            <div className="overflow-x-auto">
              <table className="w-full text-sm" style={{ pageBreakInside: 'auto' }}>
                <thead className="border-b border-slate-200 bg-slate-50/80 print:bg-slate-100">
                  <tr>
                    <th className="px-3 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500 w-10">#</th>
                    <th className="px-3 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Plate EN</th>
                    <th className="px-3 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Plate AR</th>
                    <th className="px-3 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Description</th>
                    <th className="px-3 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">Qty</th>
                    <th className="px-3 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">Unit Price</th>
                    <th className="px-3 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">VAT%</th>
                    <th className="px-3 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">VAT Amt</th>
                    <th className="px-3 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {invoice.lines.map((line) => (
                    <tr
                      key={line.id}
                      className="border-t border-slate-100 hover:bg-slate-50/50 print:hover:bg-transparent"
                      style={{ pageBreakInside: 'avoid' }}
                    >
                      <td className="px-3 py-3 text-center font-mono text-xs text-slate-500">{line.lineNumber}</td>
                      <td className="px-3 py-3 font-mono text-xs text-slate-600">{line.plateNumberEn ?? 'N/A'}</td>
                      <td className="px-3 py-3 font-mono text-xs text-slate-600" dir="rtl">{line.plateNumberAr ?? 'N/A'}</td>
                      <td className="px-3 py-3 text-slate-800">{line.description}</td>
                      <td className="px-3 py-3 text-right text-slate-600">{line.quantity}</td>
                      <td className="px-3 py-3 text-right font-mono text-xs">{fmt(line.unitPriceSar)}</td>
                      <td className="px-3 py-3 text-right text-slate-600">{line.vatPercent}%</td>
                      <td className="px-3 py-3 text-right font-mono text-xs">{fmt(line.vatAmountSar)}</td>
                      <td className="px-3 py-3 text-right font-mono text-xs font-semibold">{fmt(line.lineTotalSar)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* ── Section 5: Totals box ── */}
            <div className="border-t border-slate-200 bg-slate-50/60 px-4 py-4 print:bg-slate-50" style={{ pageBreakInside: 'avoid' }}>
              <div className="ms-auto max-w-xs space-y-1.5">
                <div className="flex justify-between text-sm text-slate-600">
                  <span>Sub-Total</span>
                  <span className="font-mono">{fmt(invoice.subTotalSar)}</span>
                </div>
                <div className="flex justify-between text-sm text-slate-600">
                  <span>VAT (15%)</span>
                  <span className="font-mono">{fmt(invoice.vatAmountSar)}</span>
                </div>
                <div className="flex justify-between border-t border-slate-200 pt-1.5 text-base font-bold text-slate-900">
                  <span>Total</span>
                  <span className="font-mono">{fmt(invoice.totalSar)}</span>
                </div>
                {invoice.paidAmountSar > 0 && (
                  <div className="flex justify-between text-sm font-semibold text-green-700">
                    <span>Paid</span>
                    <span className="font-mono">-{fmt(invoice.paidAmountSar)}</span>
                  </div>
                )}
                {invoice.balanceSar > 0 && (
                  <div className="flex justify-between border-t border-slate-200 pt-1.5 font-bold text-red-700">
                    <span>Balance Due</span>
                    <span className="font-mono">{fmt(invoice.balanceSar)}</span>
                  </div>
                )}
              </div>
            </div>
          </Card>

          {/* ── Section 6: Notes ── */}
          {invoice.notes && (
            <Card className="mt-4 p-4 print:shadow-none print:border-none print:mt-6">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Notes</p>
              <p className="mt-1 text-sm text-slate-700 whitespace-pre-line">{invoice.notes}</p>
            </Card>
          )}
        </div>

        {/* ── Section 7: Record Payment (print:hidden) ── */}
        {invoice.balanceSar > 0 && (
          <Card className="p-4 print:hidden">
            <h4 className="mb-3 font-semibold text-slate-800">Record Payment</h4>
            <div className="flex flex-wrap items-center gap-3">
              <input
                type="number"
                className="w-44 rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder={`Max: ${fmt(invoice.balanceSar)}`}
                value={paidInput}
                onChange={(e) => setPaidInput(e.target.value)}
              />
              <button
                type="button"
                onClick={handleMarkPaid}
                disabled={markingPaid || !paidInput}
                className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {markingPaid ? 'Saving...' : 'Mark Paid'}
              </button>
              <p className="text-xs text-slate-400">
                Balance outstanding:{' '}
                <span className="font-semibold text-red-700">{fmt(invoice.balanceSar)}</span>
              </p>
            </div>
          </Card>
        )}

        {/* ── Section 8: Action buttons (print:hidden) ── */}
        <div className="flex flex-wrap gap-2 print:hidden">
          <button
            type="button"
            onClick={handlePrint}
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Print
          </button>
          <button
            type="button"
            onClick={downloadCsv}
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Download CSV
          </button>
          <button
            type="button"
            onClick={() => router.push(`/leases/${invoice.leaseId}`)}
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            View Contract
          </button>
          <button
            type="button"
            onClick={() => router.back()}
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Back
          </button>
        </div>
      </div>
    </>
  )
}
