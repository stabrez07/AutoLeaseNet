'use client'

import { useParams, useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type QuotationDetail } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../../components/ui'
import { CompanyLogo } from '../../../components/company-logo'

const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Draft: 'slate',
  PendingApproval: 'amber',
  Approved: 'blue',
  SentToCustomer: 'blue',
  Accepted: 'green',
  Rejected: 'red',
  Expired: 'red',
  Withdrawn: 'slate',
}

const APPROVAL_TONES: Record<string, 'green' | 'amber' | 'slate' | 'red'> = {
  Pending: 'amber',
  Approved: 'green',
  Rejected: 'red',
  Recalled: 'slate',
}

const printStyles = `
@media print {
  nav, header, aside, .no-print, button, [class*="actions"], [class*="Quick nav"] { display: none !important; }
  body { font-size: 9pt !important; color: #000 !important; -webkit-print-color-adjust: exact; }
  .print-only { display: block !important; }
  @page { size: A4; margin: 10mm 12mm; }
  * { box-shadow: none !important; border-radius: 0 !important; }
  .space-y-5 > * { margin-top: 4px !important; }
  table { font-size: 8pt !important; }
  td, th { padding: 2px 4px !important; }
  .p-5, .p-4 { padding: 6px !important; }
  .mb-3, .mb-4 { margin-bottom: 4px !important; }
  dl { gap: 2px !important; }
  .grid.lg\\:grid-cols-3 { display: block !important; }
  .lg\\:col-span-2 { width: 100% !important; }
}
`

export default function QuotationDetailPage() {
  const params = useParams()
  const id = params?.id as string
  const { t } = useLocale()
  const router = useRouter()
  const d = t.quotations.detail
  const q = t.quotations

  const [quote, setQuote] = useState<QuotationDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [actionBusy, setActionBusy] = useState(false)
  const [actionMsg, setActionMsg] = useState<{ ok: boolean; text: string } | null>(null)

  // Approval decision state
  const [decisionComment, setDecisionComment] = useState('')
  const [signature, setSignature] = useState('')
  const [pdfEmail, setPdfEmail] = useState('')

  async function reload() {
    setLoading(true)
    setError(null)
    try {
      setQuote(await bff.getQuotation(id))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { reload() }, [id]) // eslint-disable-line react-hooks/exhaustive-deps

  async function act(fn: () => Promise<unknown>, successMsg: string) {
    setActionBusy(true)
    setActionMsg(null)
    try {
      await fn()
      setActionMsg({ ok: true, text: successMsg })
      await reload()
    } catch (e) {
      setActionMsg({ ok: false, text: `${d.errorMsg}: ${(e as Error).message}` })
    } finally {
      setActionBusy(false)
    }
  }

  const lbl = 'text-xs font-medium text-slate-700'
  const inp = 'mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500'

  if (loading) return <Spinner label={t.common.loading} />
  if (error) return <ErrorBox message={error} onRetry={reload} retryLabel={t.common.retry} />
  if (!quote) return null

  const status = quote.status
  const canSubmitApproval = status === 'Draft' && quote.lines.length > 0
  const canRevise = status !== 'Accepted' && status !== 'Expired' && status !== 'Withdrawn'
  const canDecide = status === 'PendingApproval'
  const nextPendingTier = quote.approvals.find(a => a.status === 'Pending')
  const canPrint = status !== 'Draft'
  const canSendPdf = status === 'Approved' || status === 'SentToCustomer'
  const canAccept = status === 'SentToCustomer'
  const canCreateContract = status === 'Approved' || status === 'Accepted'

  return (
    <div className="space-y-5">
      <style dangerouslySetInnerHTML={{ __html: printStyles }} />
      {/* Print-only letterhead */}
      <div className="print-only hidden border-b-2 border-slate-800 pb-3 mb-4">
        <div className="flex items-center justify-between">
          <CompanyLogo width={180} height={54} />
          <div className="text-right">
            <h1 className="text-xl font-bold">Vehicle Lease Quotation</h1>
            <p className="text-sm text-slate-600">{quote.quoteNumber} | {quote.quoteDate} | {quote.contractType}</p>
          </div>
        </div>
      </div>
      <PageHeader
        title={`${d.title} — ${quote.quoteNumber}`}
        subtitle={quote.contractType}
        action={
          <Badge tone={STATUS_TONES[status] ?? 'slate'}>
            {q.statuses[status as keyof typeof q.statuses] ?? status}
          </Badge>
        }
      />

      {/* ── Action feedback ── */}
      {actionMsg && (
        <div className={`rounded-md border p-3 text-sm ${actionMsg.ok ? 'border-green-200 bg-green-50 text-green-800' : 'border-red-200 bg-red-50 text-red-800'}`}>
          {actionMsg.text}
        </div>
      )}

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-3">

        {/* ── Left column: header summary ── */}
        <div className="space-y-5 lg:col-span-2">

          {/* Summary card */}
          <Card className="p-5">
            <h2 className="mb-3 text-sm font-semibold text-slate-800">Quote Details</h2>
            <dl className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-3">
              {[
                ['Quote Number', quote.quoteNumber],
                ['Status', q.statuses[status as keyof typeof q.statuses] ?? status],
                ['Contract Type', q.contractTypes[quote.contractType as keyof typeof q.contractTypes] ?? quote.contractType],
                ['Duration', `${quote.estimatedDurationMonths} months`],
                ['Quote Date', quote.quoteDate],
                ['Valid Until', quote.validUntilDate],
                ['Discount', `${quote.discountPercent}%`],
                ['Submitted', quote.submittedAtUtc ? new Date(quote.submittedAtUtc).toLocaleDateString() : '—'],
                ['Approved', quote.approvedAtUtc ? new Date(quote.approvedAtUtc).toLocaleDateString() : '—'],
              ].map(([label, value]) => (
                <div key={label} className="rounded-md bg-slate-50 p-2">
                  <dt className="text-slate-500">{label}</dt>
                  <dd className="mt-0.5 font-medium text-slate-900">{value}</dd>
                </div>
              ))}
            </dl>
          </Card>

          {/* Line items */}
          {quote.lines.length === 0 && status === 'Draft' ? (
            <Card className="p-8 text-center">
              <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-amber-50">
                <svg className="h-7 w-7 text-amber-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
                </svg>
              </div>
              <h3 className="text-sm font-semibold text-slate-800">No line items yet</h3>
              <p className="mx-auto mt-1 max-w-sm text-xs text-slate-500">
                This quotation has no line items. Add vehicle lease items to build your quote — select Make, Model, Year, Quantity, and Unit Price for each line.
              </p>
              <button
                onClick={() => router.push(`/quotations/new?edit=${id}`)}
                className="mt-4 inline-flex items-center gap-1.5 rounded-md bg-brand-700 px-4 py-2 text-sm font-medium text-white hover:bg-brand-800"
              >
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
                </svg>
                Add Line Items
              </button>
            </Card>
          ) : (
            <Card className="overflow-hidden">
              <div className="border-b border-slate-200 bg-slate-50 px-4 py-3">
                <h2 className="text-sm font-semibold text-slate-800">{d.lines}</h2>
              </div>
              <table className="w-full text-xs">
                <thead className="bg-slate-100 text-slate-600">
                  <tr>
                    <th className="px-3 py-2 text-start font-medium">#</th>
                    <th className="px-3 py-2 text-start font-medium">Type</th>
                    <th className="px-3 py-2 text-start font-medium">Description</th>
                    <th className="px-3 py-2 text-end font-medium">Qty</th>
                    <th className="px-3 py-2 text-end font-medium">Unit Price</th>
                    <th className="px-3 py-2 text-end font-medium">Disc %</th>
                    <th className="px-3 py-2 text-end font-medium">Line Total</th>
                  </tr>
                </thead>
                <tbody>
                  {quote.lines.length === 0 && (
                    <tr><td colSpan={7} className="px-3 py-4 text-center text-slate-400">No lines yet.</td></tr>
                  )}
                  {quote.lines.map(line => (
                    <tr key={line.id} className="border-t border-slate-100">
                      <td className="px-3 py-2 text-slate-500">{line.lineNumber}</td>
                      <td className="px-3 py-2">
                        <Badge tone="slate">{line.itemType}</Badge>
                      </td>
                      <td className="px-3 py-2">
                        <div>{line.description}</div>
                        {line.vehicleSpecRef && <div className="text-slate-400">{line.vehicleSpecRef}</div>}
                      </td>
                      <td className="px-3 py-2 text-end">{line.quantity}</td>
                      <td className="px-3 py-2 text-end font-mono">{line.unitPriceSar.toLocaleString('en-SA', { minimumFractionDigits: 2 })}</td>
                      <td className="px-3 py-2 text-end">{line.discountPercent}%</td>
                      <td className="px-3 py-2 text-end font-mono font-medium">{line.lineTotalSar.toLocaleString('en-SA', { minimumFractionDigits: 2 })}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Card>
          )}

          {/* Pricing */}
          <Card className="p-5">
            <h2 className="mb-3 text-sm font-semibold text-slate-800">Pricing</h2>
            <dl className="space-y-1 text-sm">
              <div className="flex justify-between text-slate-600">
                <dt>Subtotal</dt>
                <dd className="font-mono">{quote.subTotalSar.toLocaleString('en-SA', { minimumFractionDigits: 2 })} SAR</dd>
              </div>
              <div className="flex justify-between text-slate-600">
                <dt>VAT (15%)</dt>
                <dd className="font-mono">{quote.vatSar.toLocaleString('en-SA', { minimumFractionDigits: 2 })} SAR</dd>
              </div>
              <div className="flex justify-between border-t border-slate-200 pt-1 text-base font-semibold text-slate-900">
                <dt>Total</dt>
                <dd className="font-mono">{quote.totalSar.toLocaleString('en-SA', { minimumFractionDigits: 2 })} SAR</dd>
              </div>
            </dl>
          </Card>

          {/* Approval chain */}
          {quote.approvals.length > 0 && (
            <Card className="overflow-hidden">
              <div className="border-b border-slate-200 bg-slate-50 px-4 py-3">
                <h2 className="text-sm font-semibold text-slate-800">{d.approvals}</h2>
              </div>
              <table className="w-full text-xs">
                <thead className="bg-slate-100 text-slate-600">
                  <tr>
                    <th className="px-3 py-2 text-start font-medium">{d.tierLabel}</th>
                    <th className="px-3 py-2 text-start font-medium">{d.requiredRole}</th>
                    <th className="px-3 py-2 text-start font-medium">{d.approvalStatus}</th>
                    <th className="px-3 py-2 text-start font-medium">{d.comment}</th>
                    <th className="px-3 py-2 text-start font-medium">Decided</th>
                  </tr>
                </thead>
                <tbody>
                  {quote.approvals.map(a => (
                    <tr key={a.tierLevel} className="border-t border-slate-100">
                      <td className="px-3 py-2 font-medium">{a.tierLevel}</td>
                      <td className="px-3 py-2 font-mono">{a.requiredRoleCode}</td>
                      <td className="px-3 py-2">
                        <Badge tone={APPROVAL_TONES[a.status] ?? 'slate'}>{a.status}</Badge>
                      </td>
                      <td className="px-3 py-2 text-slate-500">{a.comment ?? '—'}</td>
                      <td className="px-3 py-2 text-slate-500">
                        {a.decidedAtUtc ? new Date(a.decidedAtUtc).toLocaleDateString() : '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Card>
          )}
        </div>

        {/* ── Right column: actions ── */}
        <div className="space-y-4">
          <Card className="p-4">
            <h2 className="mb-3 text-sm font-semibold text-slate-800">{d.actions}</h2>
            <div className="space-y-3">

              {/* Edit / Add Lines (Draft only) */}
              {status === 'Draft' && (
                <button
                  onClick={() => router.push(`/quotations/new?edit=${id}`)}
                  className={`w-full rounded-md px-3 py-2 text-sm font-medium ${
                    quote.lines.length === 0
                      ? 'bg-brand-700 text-white hover:bg-brand-800'
                      : 'border border-brand-600 text-brand-700 hover:bg-brand-50'
                  }`}
                >
                  {quote.lines.length === 0 ? 'Add Line Items' : 'Edit Quotation / Add Lines'}
                </button>
              )}

              {/* Submit for Approval */}
              {canSubmitApproval && (
                <button
                  disabled={actionBusy}
                  onClick={() => act(
                    () => bff.submitQuotationForApproval(id, crypto.randomUUID()),
                    d.successMsg
                  )}
                  className="w-full rounded-md bg-amber-500 px-3 py-2 text-sm font-medium text-white hover:bg-amber-600 disabled:opacity-50"
                >
                  {actionBusy ? d.submittingApproval : d.submitApproval}
                </button>
              )}

              {/* Approval Decision */}
              {canDecide && nextPendingTier && (
                <div className="rounded-md border border-amber-200 bg-amber-50 p-3 space-y-2">
                  <p className="text-xs font-medium text-amber-800">
                    {d.tierLabel} {nextPendingTier.tierLevel} — {nextPendingTier.requiredRoleCode}
                  </p>
                  <div>
                    <label className={lbl}>{d.comment}</label>
                    <input className={inp} value={decisionComment}
                      onChange={e => setDecisionComment(e.target.value)} />
                  </div>
                  <div className="flex gap-2">
                    <button
                      disabled={actionBusy}
                      onClick={() => act(
                        () => bff.recordApprovalDecision(id, nextPendingTier.tierLevel, true, decisionComment || undefined, crypto.randomUUID()),
                        d.successMsg
                      )}
                      className="flex-1 rounded-md bg-green-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-green-700 disabled:opacity-50"
                    >
                      {d.approveBtn}
                    </button>
                    <button
                      disabled={actionBusy}
                      onClick={() => act(
                        () => bff.recordApprovalDecision(id, nextPendingTier.tierLevel, false, decisionComment || undefined, crypto.randomUUID()),
                        d.successMsg
                      )}
                      className="flex-1 rounded-md bg-red-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-700 disabled:opacity-50"
                    >
                      {d.rejectBtn}
                    </button>
                  </div>
                </div>
              )}

              {/* Print Quotation */}
              {canPrint && (
                <button
                  onClick={() => window.print()}
                  className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
                >
                  Print Quotation
                </button>
              )}

              {/* Create Contract */}
              {canCreateContract && (
                <button
                  disabled={actionBusy}
                  onClick={() => {
                    const params = new URLSearchParams({
                      fromQuote: id,
                      customerId: quote.customerId,
                      duration: String(quote.estimatedDurationMonths),
                    })
                    router.push(`/leases/new?${params.toString()}`)
                  }}
                  className="w-full rounded-md bg-green-700 px-3 py-2 text-sm font-medium text-white hover:bg-green-800 disabled:opacity-50"
                >
                  Create Contract
                </button>
              )}

              {/* Send PDF to Customer */}
              {canSendPdf && (
                <div className="space-y-2 rounded-md border border-brand-200 bg-brand-50 p-3">
                  <div>
                    <label className={lbl}>Customer Email</label>
                    <input className={inp} value={pdfEmail} placeholder="customer@company.com"
                      onChange={e => setPdfEmail(e.target.value)} />
                  </div>
                  <button
                    disabled={actionBusy || !pdfEmail.includes('@')}
                    onClick={() => act(
                      () => bff.sendQuotePdf(id, pdfEmail, crypto.randomUUID()),
                      'Quotation PDF sent to ' + pdfEmail
                    )}
                    className="w-full rounded-md bg-brand-700 px-3 py-2 text-sm font-medium text-white hover:bg-brand-800 disabled:opacity-50"
                  >
                    {actionBusy ? d.sendingPdf : d.sendPdf}
                  </button>
                </div>
              )}

              {/* Accept */}
              {canAccept && (
                <div className="space-y-2">
                  <div>
                    <label className={lbl}>{d.signatureLabel}</label>
                    <input className={inp} value={signature}
                      placeholder="e.g. Ahmad Al-Harbi"
                      onChange={e => setSignature(e.target.value)} />
                  </div>
                  <button
                    disabled={actionBusy}
                    onClick={() => act(
                      () => bff.acceptQuotation(id, signature || undefined, crypto.randomUUID()),
                      d.successMsg
                    )}
                    className="w-full rounded-md bg-green-700 px-3 py-2 text-sm font-medium text-white hover:bg-green-800 disabled:opacity-50"
                  >
                    {actionBusy ? d.accepting : d.acceptQuote}
                  </button>
                </div>
              )}

              {/* Revise — send back to Draft for editing */}
              {canRevise && status !== 'Draft' && (
                <button
                  onClick={() => router.push(`/quotations/new?revise=${id}`)}
                  className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
                >
                  Revise Quotation
                </button>
              )}
            </div>
          </Card>

          {/* Quick nav */}
          <Card className="p-4">
            <button
              onClick={() => router.push('/quotations')}
              className="text-xs text-brand-700 hover:underline"
            >
              ← Back to Quotations
            </button>
          </Card>
        </div>
      </div>
    </div>
  )
}
