'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { bff, type AdvancePayment } from '../../../lib/bff-client'
import { CompanyLogo } from '../../../components/company-logo'
import { Card, ErrorBox, Spinner } from '../../../components/ui'

// ── Helpers ──────────────────────────────────────────────────────────────────

function fmt(n: number) {
  return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function safeDate(s: string) {
  return new Date(s).toLocaleDateString('en-GB', { day: '2-digit', month: 'long', year: 'numeric' })
}

// ── Main Page ────────────────────────────────────────────────────────────────

export default function PaymentReceiptPage() {
  const router = useRouter()
  const params = useParams()
  const id = params?.id as string

  const [payment, setPayment] = useState<AdvancePayment | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    bff.getPaymentById(id)
      .then(setPayment)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [id])

  function handlePrint() { window.print() }

  function handleRetry() {
    setLoading(true)
    setError(null)
    bff.getPaymentById(id)
      .then(setPayment)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }

  if (loading) return <Spinner label="Loading payment..." />
  if (error) return <ErrorBox message={error} onRetry={handleRetry} retryLabel="Retry" />
  if (!payment) return null

  const receiptNumber = payment.referenceNumber ?? `P-${payment.displayId}`

  return (
    <>
      <style jsx global>{`
        @media print {
          @page { size: A4; margin: 20mm; }
          body { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        }
      `}</style>

      <div className="mx-auto max-w-3xl space-y-4 py-6">

        {/* ── Printable receipt layout ── */}
        <Card className="p-8 print:shadow-none print:border-none print:p-0">

          {/* ── Header: Logo + Title ── */}
          <div className="flex items-start justify-between gap-4">
            <CompanyLogo width={180} height={54} />
            <div className="text-right shrink-0">
              <p className="text-2xl font-bold text-slate-900 tracking-wide">PAYMENT RECEIPT</p>
              <p className="mt-1 font-mono text-sm text-slate-600">{receiptNumber}</p>
            </div>
          </div>

          <hr className="my-6 border-slate-200" />

          {/* ── Receipt details grid ── */}
          <div className="grid grid-cols-2 gap-x-8 gap-y-4 print:grid-cols-2">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Receipt Number</p>
              <p className="mt-1 font-mono text-sm font-semibold text-slate-900">{receiptNumber}</p>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Date Received</p>
              <p className="mt-1 text-sm text-slate-900">{safeDate(payment.receivedDate)}</p>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Customer</p>
              <p className="mt-1 text-sm font-semibold text-slate-900">{payment.customerDisplayName}</p>
            </div>
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Payment Method</p>
              <p className="mt-1 text-sm text-slate-900">{payment.paymentMethod}</p>
            </div>
            {payment.referenceNumber && (
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Reference Number</p>
                <p className="mt-1 font-mono text-sm text-slate-900">{payment.referenceNumber}</p>
              </div>
            )}
            {payment.notes && (
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Notes</p>
                <p className="mt-1 text-sm text-slate-700">{payment.notes}</p>
              </div>
            )}
          </div>

          <hr className="my-6 border-slate-200" />

          {/* ── Amount (prominent) ── */}
          <div className="rounded-lg bg-slate-50 px-6 py-5 text-center print:bg-slate-50">
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Amount Received</p>
            <p className="mt-2 font-mono text-3xl font-bold text-slate-900">{fmt(payment.amount)}</p>
          </div>

          {/* ── Allocations table ── */}
          {payment.allocations.length > 0 && (
            <div className="mt-6">
              <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">
                Allocations ({payment.allocations.length})
              </p>
              <div className="overflow-x-auto">
                <table className="w-full text-sm" style={{ pageBreakInside: 'auto' }}>
                  <thead className="border-b border-slate-200 bg-slate-50/80 print:bg-slate-100">
                    <tr>
                      <th className="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Invoice #</th>
                      <th className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">Amount (SAR)</th>
                      <th className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">Date</th>
                    </tr>
                  </thead>
                  <tbody>
                    {payment.allocations.map((a) => (
                      <tr
                        key={a.id}
                        className="border-t border-slate-100"
                        style={{ pageBreakInside: 'avoid' }}
                      >
                        <td className="px-3 py-2.5 font-mono text-sm text-slate-800">{a.invoiceNumber}</td>
                        <td className="px-3 py-2.5 text-right font-mono text-sm font-semibold text-slate-900">{fmt(a.allocatedAmountSar)}</td>
                        <td className="px-3 py-2.5 text-right text-sm text-slate-600">{safeDate(a.allocatedAtUtc)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* ── Unallocated balance ── */}
          {payment.remainingBalance > 0 && (
            <div className="mt-4 flex items-center justify-between rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 print:bg-amber-50">
              <span className="text-sm font-medium text-amber-800">Unallocated Balance</span>
              <span className="font-mono text-sm font-bold text-amber-900">{fmt(payment.remainingBalance)}</span>
            </div>
          )}

          <hr className="my-6 border-slate-200" />

          {/* ── Signature lines ── */}
          <div className="grid grid-cols-2 gap-8 pt-2" style={{ pageBreakInside: 'avoid' }}>
            <div>
              <p className="text-sm text-slate-600">Received by: ________________________________________</p>
            </div>
            <div>
              <p className="text-sm text-slate-600">Signature: ________________________________________</p>
            </div>
          </div>
        </Card>

        {/* ── Action buttons (hidden in print) ── */}
        <div className="flex flex-wrap gap-2 print:hidden">
          <button
            type="button"
            onClick={handlePrint}
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Print Receipt
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
