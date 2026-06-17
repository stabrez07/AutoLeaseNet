'use client'

import { useEffect, useRef, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { bff, type Invoice, type InvoiceStatus } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, PrimaryButton, SecondaryButton, Spinner } from '../../../components/ui'

const STATUS_TONES: Record<InvoiceStatus, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Paid: 'green', PartiallyPaid: 'amber', Issued: 'blue', Draft: 'slate', Overdue: 'red', Cancelled: 'slate',
}

function fmt(n: number) {
  return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function safeDate(s: string) {
  return new Date(s).toLocaleDateString('en-GB', { day: '2-digit', month: 'long', year: 'numeric' })
}

export default function InvoiceDetailPage() {
  const router = useRouter()
  const params = useParams()
  const id = params?.id as string

  const [invoice, setInvoice] = useState<Invoice | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [markingPaid, setMarkingPaid] = useState(false)
  const [paidInput, setPaidInput] = useState('')
  const printRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!id) return
    bff.getInvoiceById(id).then(setInvoice).catch((e: Error) => setError(e.message)).finally(() => setLoading(false))
  }, [id])

  async function handleMarkPaid() {
    const amt = parseFloat(paidInput)
    if (!amt || !invoice) { alert('Enter valid amount'); return }
    setMarkingPaid(true)
    try {
      const updated = await bff.markInvoicePaid(id, amt, crypto.randomUUID())
      setInvoice(updated); setPaidInput('')
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
      ['Plate', invoice.vehiclePlate],
      ['Billing Period', `${invoice.billingPeriodStart} – ${invoice.billingPeriodEnd}`],
      ['Issued Date', invoice.issuedDate],
      ['Due Date', invoice.dueDate],
      ['Status', invoice.status],
      ['Sub-Total (SAR)', String(invoice.subTotalSar)],
      ['VAT 15% (SAR)', String(invoice.vatAmountSar)],
      ['Total (SAR)', String(invoice.totalSar)],
      ['Paid (SAR)', String(invoice.paidAmountSar)],
      ['Balance (SAR)', String(invoice.balanceSar)],
      ...(invoice.zatcaInvoiceNumber ? [['ZATCA Invoice #', invoice.zatcaInvoiceNumber]] : []),
    ]
    const csv = rows.map((r) => r.join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `${invoice.invoiceNumber}.csv`; a.click()
  }

  if (loading) return <Spinner label="Loading invoice…" />
  if (error) return <ErrorBox message={error} onRetry={() => { setLoading(true); setError(null); bff.getInvoiceById(id).then(setInvoice).catch((e: Error) => setError(e.message)).finally(() => setLoading(false)) }} retryLabel="Retry" />
  if (!invoice) return null

  return (
    <div className="space-y-4">
      <PageHeader
        title={invoice.invoiceNumber}
        subtitle={`${invoice.customerDisplayName} · ${invoice.vehicleMakeModel}`}
        action={
          <div className="flex gap-2">
            <SecondaryButton onClick={downloadCsv} className="px-3 py-1.5 text-xs">⬇ CSV</SecondaryButton>
            <SecondaryButton onClick={handlePrint} className="px-3 py-1.5 text-xs">🖨 Print</SecondaryButton>
            <SecondaryButton onClick={() => router.push(`/leases/${invoice.leaseId}`)} className="px-3 py-1.5 text-xs">View Contract</SecondaryButton>
            <SecondaryButton onClick={() => router.back()} className="px-3 py-1.5 text-xs">← Back</SecondaryButton>
          </div>
        }
      />

      {/* ── Print-ready layout ── This entire div is styled for @media print ── */}
      <div ref={printRef} className="print:block">

        {/* Invoice header */}
        <Card className="p-6 print:shadow-none print:border-none">
          <div className="flex items-start justify-between">
            <div>
              <p className="text-2xl font-bold text-slate-900">TAX INVOICE</p>
              <p className="mt-1 font-mono text-sm text-slate-500">{invoice.invoiceNumber}</p>
            </div>
            <div className="text-right">
              <Badge tone={STATUS_TONES[invoice.status]}>{invoice.status}</Badge>
              {invoice.zatcaInvoiceNumber && (
                <p className="mt-1 font-mono text-xs text-slate-500">ZATCA: {invoice.zatcaInvoiceNumber}</p>
              )}
            </div>
          </div>

          <div className="mt-6 grid grid-cols-2 gap-8 md:grid-cols-3">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Billed To</p>
              <p className="mt-1 font-semibold text-slate-900">{invoice.customerDisplayName}</p>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Contract</p>
              <p className="mt-1 font-mono text-sm text-slate-800">{invoice.leaseNumber}</p>
              <p className="text-xs text-slate-500">{invoice.vehiclePlate} · {invoice.vehicleMakeModel}</p>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Dates</p>
              <p className="mt-1 text-sm text-slate-800">Issued: {safeDate(invoice.issuedDate)}</p>
              <p className="text-sm text-slate-800">Due: <span className={invoice.balanceSar > 0 ? 'font-semibold text-red-700' : ''}>{safeDate(invoice.dueDate)}</span></p>
            </div>
          </div>

          <div className="mt-4 rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-600">
            Billing Period: <span className="font-semibold">{invoice.billingPeriodStart}</span> to <span className="font-semibold">{invoice.billingPeriodEnd}</span>
          </div>
        </Card>

        {/* Line items */}
        <Card className="mt-4 overflow-hidden p-0 print:shadow-none print:border-none">
          <table className="w-full text-sm">
            <thead className="border-b border-slate-200 bg-slate-50/80">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Description</th>
                <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">Qty</th>
                <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">Unit Price</th>
                <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">VAT %</th>
                <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">VAT Amount</th>
                <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">Line Total</th>
              </tr>
            </thead>
            <tbody>
              {invoice.lines.map((line) => (
                <tr key={line.id} className="border-t border-slate-100">
                  <td className="px-4 py-3 text-slate-800">{line.description}</td>
                  <td className="px-4 py-3 text-right text-slate-600">{line.quantity}</td>
                  <td className="px-4 py-3 text-right font-mono text-xs">{fmt(line.unitPriceSar)}</td>
                  <td className="px-4 py-3 text-right text-slate-600">{line.vatPercent}%</td>
                  <td className="px-4 py-3 text-right font-mono text-xs">{fmt(line.vatAmountSar)}</td>
                  <td className="px-4 py-3 text-right font-mono text-xs font-semibold">{fmt(line.lineTotalSar)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Totals */}
          <div className="border-t border-slate-200 bg-slate-50/60 px-4 py-3">
            <div className="ms-auto max-w-xs space-y-1">
              <div className="flex justify-between text-sm text-slate-600">
                <span>Sub-Total</span>
                <span className="font-mono">{fmt(invoice.subTotalSar)}</span>
              </div>
              <div className="flex justify-between text-sm text-slate-600">
                <span>VAT (15%)</span>
                <span className="font-mono">{fmt(invoice.vatAmountSar)}</span>
              </div>
              <div className="flex justify-between border-t border-slate-200 pt-1 text-base font-bold text-slate-900">
                <span>Total</span>
                <span className="font-mono">{fmt(invoice.totalSar)}</span>
              </div>
              {invoice.paidAmountSar > 0 && (
                <div className="flex justify-between text-sm text-green-700">
                  <span>Paid</span>
                  <span className="font-mono">-{fmt(invoice.paidAmountSar)}</span>
                </div>
              )}
              {invoice.balanceSar > 0 && (
                <div className="flex justify-between border-t border-slate-200 pt-1 font-bold text-red-700">
                  <span>Balance Due</span>
                  <span className="font-mono">{fmt(invoice.balanceSar)}</span>
                </div>
              )}
            </div>
          </div>
        </Card>

        {/* Notes */}
        {invoice.notes && (
          <Card className="mt-4 p-4 print:shadow-none">
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Notes</p>
            <p className="mt-1 text-sm text-slate-700">{invoice.notes}</p>
          </Card>
        )}
      </div>

      {/* Mark as paid — hidden on print */}
      {invoice.balanceSar > 0 && (
        <Card className="p-4 print:hidden">
          <h4 className="mb-3 font-semibold text-slate-800">Record Payment</h4>
          <div className="flex items-center gap-3">
            <input
              type="number"
              className="w-44 rounded-lg border border-slate-300 px-3 py-2 text-sm"
              placeholder={`Max: ${fmt(invoice.balanceSar)}`}
              value={paidInput}
              onChange={(e) => setPaidInput(e.target.value)}
            />
            <PrimaryButton onClick={handleMarkPaid} disabled={markingPaid || !paidInput} className="px-4 py-2 text-sm">
              {markingPaid ? 'Saving…' : 'Mark Paid'}
            </PrimaryButton>
            <p className="text-xs text-slate-400">Balance outstanding: <span className="font-semibold text-red-700">{fmt(invoice.balanceSar)}</span></p>
          </div>
        </Card>
      )}
    </div>
  )
}
