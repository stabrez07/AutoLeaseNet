'use client'

import { useParams, useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type QuotationDetail } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../../components/ui'

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
  const inp = 'mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500'

  if (loading) return <Spinner label={t.common.loading} />
  if (error) return <ErrorBox message={error} onRetry={reload} retryLabel={t.common.retry} />
  if (!quote) return null

  const status = quote.status
  const canSubmitApproval = status === 'Draft' && quote.lines.length > 0
  const canDecide = status === 'PendingApproval'
  const nextPendingTier = quote.approvals.find(a => a.status === 'Pending')
  const canSendPdf = status === 'Approved'
  const canAccept = status === 'SentToCustomer'

  return (
    <div className="space-y-5">
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

          {/* Line items */}
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

              {/* Send PDF */}
              {canSendPdf && (
                <button
                  disabled={actionBusy}
                  onClick={() => act(
                    () => bff.submitQuotationForApproval(id, crypto.randomUUID()), // reuse PDF send when wired
                    d.successMsg
                  )}
                  className="w-full rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                >
                  {actionBusy ? d.sendingPdf : d.sendPdf}
                </button>
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

              {/* Status info when no actions */}
              {!canSubmitApproval && !canDecide && !canSendPdf && !canAccept && (
                <p className="text-xs text-slate-500">No actions available for status <strong>{status}</strong>.</p>
              )}
            </div>
          </Card>

          {/* Quick nav */}
          <Card className="p-4">
            <button
              onClick={() => router.push('/quotations')}
              className="text-xs text-blue-600 hover:underline"
            >
              ← Back to Quotations
            </button>
          </Card>
        </div>
      </div>
    </div>
  )
}
