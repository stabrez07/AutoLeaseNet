'use client'

import Link from 'next/link'
import { useParams, useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import { bff, type RfqDetail } from '../../../lib/bff-client'
import { Card, ErrorBox, PageHeader, Spinner } from '../../../components/ui'
import { type BadgeTone, DateCell, StatusBadge } from '../../../components/data-grid'

/* ---------------------------------------------------------------------------
 * Stage colour mapping
 * -------------------------------------------------------------------------*/

const STAGE_TONES: Record<string, BadgeTone> = {
  Draft: 'slate',
  Qualified: 'blue',
  Proposal: 'amber',
  Negotiation: 'purple',
  Won: 'green',
  Lost: 'red',
}

/* ---------------------------------------------------------------------------
 * Helper: days between two dates
 * -------------------------------------------------------------------------*/
function daysBetween(from: string, to: Date): number {
  return Math.max(0, Math.floor((to.getTime() - new Date(from).getTime()) / 86400000))
}

/* ---------------------------------------------------------------------------
 * Probability bar
 * -------------------------------------------------------------------------*/
function ProbabilityBar({ value }: { value: number }) {
  const color =
    value >= 80
      ? 'bg-emerald-500'
      : value >= 50
        ? 'bg-amber-500'
        : value >= 20
          ? 'bg-blue-500'
          : 'bg-slate-400'
  return (
    <div className="flex items-center gap-2">
      <div className="h-2 flex-1 rounded-full bg-slate-100">
        <div className={`h-2 rounded-full ${color}`} style={{ width: `${Math.min(100, value)}%` }} />
      </div>
      <span className="text-xs font-semibold text-slate-700">{value}%</span>
    </div>
  )
}

/* ---------------------------------------------------------------------------
 * Main component
 * -------------------------------------------------------------------------*/

export default function RfqDetailPage() {
  const params = useParams()
  const id = params?.id as string
  const router = useRouter()

  const [rfq, setRfq] = useState<RfqDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [actionBusy, setActionBusy] = useState(false)
  const [actionMsg, setActionMsg] = useState<{ ok: boolean; text: string } | null>(null)

  // Lost reason input
  const [showLostInput, setShowLostInput] = useState(false)
  const [lostReason, setLostReason] = useState('')

  async function reload() {
    setLoading(true)
    setError(null)
    try {
      setRfq(await bff.getRfqById(id))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    reload()
  }, [id]) // eslint-disable-line react-hooks/exhaustive-deps

  async function act(fn: () => Promise<unknown>, successMsg: string) {
    setActionBusy(true)
    setActionMsg(null)
    try {
      await fn()
      setActionMsg({ ok: true, text: successMsg })
      await reload()
    } catch (e) {
      setActionMsg({ ok: false, text: (e as Error).message })
    } finally {
      setActionBusy(false)
    }
  }

  function idempKey() {
    return `rfq-${id}-${Date.now()}`
  }

  /* ─── Stage transition actions ───────────────────────────────────────────*/

  function handleQualify() {
    if (!window.confirm('Move this RFQ to Qualified stage?')) return
    act(
      () => bff.updateRfqStage(id, 'Qualified', 'Customer requirements verified', idempKey()),
      'RFQ qualified successfully.',
    )
  }

  function handleConvert() {
    if (!window.confirm('Convert this RFQ to a Quotation? A new draft quotation will be created.')) return
    act(async () => {
      const result = await bff.convertRfqToQuotation(id, idempKey())
      if (result.quotationId) {
        router.push(`/quotations/${result.quotationId}`)
      }
    }, 'Quotation created from RFQ.')
  }

  function handleMoveToNegotiation() {
    if (!window.confirm('Move this RFQ to Negotiation stage?')) return
    act(
      () => bff.updateRfqStage(id, 'Negotiation', 'Customer is negotiating terms', idempKey()),
      'Moved to Negotiation.',
    )
  }

  function handleMarkWon() {
    if (!window.confirm('Mark this RFQ as Won?')) return
    act(
      () => bff.updateRfqStage(id, 'Won', 'Deal closed successfully', idempKey()),
      'RFQ marked as Won.',
    )
  }

  function handleMarkLost() {
    if (!lostReason.trim()) return
    act(
      () => bff.updateRfqStage(id, 'Lost', lostReason.trim(), idempKey()),
      'RFQ marked as Lost.',
    )
    setShowLostInput(false)
    setLostReason('')
  }

  function handleReopen() {
    if (!window.confirm('Reopen this RFQ and move it back to Draft?')) return
    act(
      () => bff.updateRfqStage(id, 'Draft', 'Reopened from Lost', idempKey()),
      'RFQ reopened.',
    )
  }

  /* ─── Loading / error states ─────────────────────────────────────────────*/

  if (loading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Spinner />
      </div>
    )
  }

  if (error || !rfq) {
    return (
      <div className="p-8">
        <ErrorBox
          message={error ?? 'RFQ not found.'}
          onRetry={reload}
          retryLabel="Retry"
        />
        <Link href="/rfqs" className="mt-4 inline-block text-xs text-brand-700 hover:underline">
          Back to RFQ Pipeline
        </Link>
      </div>
    )
  }

  const nowDate = new Date()
  const lastStageChange = rfq.stageHistory.length > 0
    ? rfq.stageHistory[rfq.stageHistory.length - 1]!
    : null
  const daysInStage = lastStageChange ? daysBetween(lastStageChange.createdAtUtc, nowDate) : 0
  const totalAge = daysBetween(rfq.createdAtUtc, nowDate)
  const isTerminal = rfq.stage === 'Won' || rfq.stage === 'Lost'
  const categories = rfq.vehicleCategories?.split(',').map((s) => s.trim()).filter(Boolean) ?? []
  const services = rfq.services?.split(',').map((s) => s.trim()).filter(Boolean) ?? []

  return (
    <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
      {/* Back link + header */}
      <Link href="/rfqs" className="mb-3 inline-block text-xs text-brand-700 hover:underline">
        &larr; Back to Pipeline
      </Link>

      <PageHeader
        title={rfq.rfqNumber}
        subtitle="Manage this opportunity -- qualify, convert to quotation, or close."
        action={
          <StatusBadge tone={STAGE_TONES[rfq.stage] ?? 'slate'}>{rfq.stage}</StatusBadge>
        }
      />

      {/* Action result */}
      {actionMsg && (
        <div
          className={`mb-4 rounded-lg border px-4 py-2.5 text-xs ${
            actionMsg.ok
              ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
              : 'border-red-200 bg-red-50 text-red-700'
          }`}
        >
          {actionMsg.text}
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        {/* ─── Left column (2/3) ──────────────────────────────────────────────*/}
        <div className="space-y-5 lg:col-span-2">
          {/* Summary card */}
          <Card className="p-5">
            <h2 className="mb-4 text-sm font-semibold text-slate-900">Summary</h2>
            <div className="mb-3 flex items-center gap-3">
              <span className="font-mono text-lg font-bold text-slate-900">{rfq.rfqNumber}</span>
              <StatusBadge tone={STAGE_TONES[rfq.stage] ?? 'slate'}>{rfq.stage}</StatusBadge>
            </div>
            <p className="mb-4 text-sm text-slate-600">{rfq.customerDisplayName}</p>

            <dl className="grid grid-cols-2 gap-x-6 gap-y-3 text-xs sm:grid-cols-3">
              <div>
                <dt className="font-medium text-slate-500">Source</dt>
                <dd className="mt-0.5 text-slate-900">{rfq.source}</dd>
              </div>
              <div>
                <dt className="font-medium text-slate-500">Vehicle Qty</dt>
                <dd className="mt-0.5 text-slate-900">{rfq.vehicleQty}</dd>
              </div>
              <div>
                <dt className="font-medium text-slate-500">Tenure</dt>
                <dd className="mt-0.5 text-slate-900">{rfq.tenureMonths} months</dd>
              </div>
              <div>
                <dt className="font-medium text-slate-500">Annual Mileage Cap</dt>
                <dd className="mt-0.5 text-slate-900">
                  {rfq.annualMileageCapKm != null
                    ? `${rfq.annualMileageCapKm.toLocaleString()} km`
                    : '—'}
                </dd>
              </div>
              <div>
                <dt className="font-medium text-slate-500">Probability</dt>
                <dd className="mt-0.5 text-slate-900">{rfq.probability}%</dd>
              </div>
              <div>
                <dt className="font-medium text-slate-500">Expected Close</dt>
                <dd className="mt-0.5 text-slate-900">
                  {rfq.expectedCloseDate ? (
                    <DateCell date={rfq.expectedCloseDate} />
                  ) : (
                    '—'
                  )}
                </dd>
              </div>
              {rfq.crmOpportunityId && (
                <div>
                  <dt className="font-medium text-slate-500">CRM Opportunity</dt>
                  <dd className="mt-0.5 font-mono text-slate-900">{rfq.crmOpportunityId}</dd>
                </div>
              )}
            </dl>

            {/* Tags */}
            {(categories.length > 0 || services.length > 0) && (
              <div className="mt-4 space-y-2">
                {categories.length > 0 && (
                  <div>
                    <span className="mr-2 text-[11px] font-medium text-slate-500">Categories:</span>
                    {categories.map((c) => (
                      <span
                        key={c}
                        className="mr-1.5 inline-block rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600"
                      >
                        {c}
                      </span>
                    ))}
                  </div>
                )}
                {services.length > 0 && (
                  <div>
                    <span className="mr-2 text-[11px] font-medium text-slate-500">Services:</span>
                    {services.map((s) => (
                      <span
                        key={s}
                        className="mr-1.5 inline-block rounded-md bg-blue-50 px-2 py-0.5 text-[11px] font-medium text-blue-600"
                      >
                        {s}
                      </span>
                    ))}
                  </div>
                )}
              </div>
            )}

            {/* Notes */}
            {rfq.notes && (
              <div className="mt-4 rounded-md bg-slate-50 p-3">
                <span className="text-[11px] font-medium text-slate-500">Notes</span>
                <p className="mt-0.5 text-xs text-slate-700">{rfq.notes}</p>
              </div>
            )}
          </Card>

          {/* Stage history timeline */}
          <Card className="p-5">
            <h2 className="mb-4 text-sm font-semibold text-slate-900">Stage History</h2>
            {rfq.stageHistory.length === 0 ? (
              <p className="text-xs text-slate-400">No history available.</p>
            ) : (
              <ol className="relative border-l-2 border-slate-200 pl-5">
                {[...rfq.stageHistory].reverse().map((entry) => (
                  <li key={entry.id} className="mb-4 last:mb-0">
                    <div className="absolute -left-[7px] mt-1 h-3 w-3 rounded-full border-2 border-white bg-slate-300" />
                    <div className="flex flex-wrap items-center gap-2 text-xs">
                      {entry.fromStage && (
                        <>
                          <StatusBadge tone={STAGE_TONES[entry.fromStage] ?? 'slate'}>
                            {entry.fromStage}
                          </StatusBadge>
                          <span className="text-slate-400">&rarr;</span>
                        </>
                      )}
                      <StatusBadge tone={STAGE_TONES[entry.toStage] ?? 'slate'}>
                        {entry.toStage}
                      </StatusBadge>
                      <span className="text-[11px] text-slate-400">
                        {new Date(entry.createdAtUtc).toLocaleDateString('en-GB', {
                          day: 'numeric',
                          month: 'short',
                          year: 'numeric',
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                      </span>
                    </div>
                    {entry.comment && (
                      <p className="mt-1 text-[11px] text-slate-500">{entry.comment}</p>
                    )}
                  </li>
                ))}
              </ol>
            )}
          </Card>

          {/* Attachments */}
          <Card className="p-5">
            <h2 className="mb-4 text-sm font-semibold text-slate-900">Attachments</h2>
            {rfq.attachments.length === 0 ? (
              <div className="rounded-md border border-dashed border-slate-200 p-6 text-center">
                <p className="text-xs text-slate-400">No attachments yet.</p>
                <p className="mt-1 text-[11px] text-slate-400">
                  Attachments will appear here once uploaded via the API.
                </p>
              </div>
            ) : (
              <div className="overflow-x-auto rounded border border-slate-200">
                <table className="w-full text-[11px]">
                  <thead>
                    <tr className="border-b border-slate-100 bg-slate-50/80 text-slate-500">
                      <th className="px-3 py-1.5 text-left font-medium">File Name</th>
                      <th className="px-3 py-1.5 text-left font-medium">Uploaded</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {rfq.attachments.map((att) => (
                      <tr key={att.id}>
                        <td className="px-3 py-1.5">
                          <a
                            href={att.fileUrl}
                            className="text-brand-700 hover:underline"
                            target="_blank"
                            rel="noopener noreferrer"
                          >
                            {att.fileName}
                          </a>
                        </td>
                        <td className="px-3 py-1.5 text-slate-500">
                          <DateCell date={att.createdAtUtc} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </div>

        {/* ─── Right column (1/3) ─────────────────────────────────────────────*/}
        <div className="space-y-5">
          {/* Actions card */}
          <Card className="p-5">
            <h2 className="mb-4 text-sm font-semibold text-slate-900">Actions</h2>

            <div className="space-y-2">
              {rfq.stage === 'Draft' && (
                <button
                  onClick={handleQualify}
                  disabled={actionBusy}
                  className="w-full rounded-md bg-emerald-600 px-3 py-2 text-xs font-medium text-white transition hover:bg-emerald-700 disabled:opacity-40"
                >
                  {actionBusy ? 'Processing...' : 'Qualify'}
                </button>
              )}

              {rfq.stage === 'Qualified' && (
                <button
                  onClick={handleConvert}
                  disabled={actionBusy}
                  className="w-full rounded-md bg-brand-700 px-3 py-2 text-xs font-medium text-white transition hover:bg-brand-800 disabled:opacity-40"
                >
                  {actionBusy ? 'Processing...' : 'Create Quotation'}
                </button>
              )}

              {rfq.stage === 'Proposal' && (
                <button
                  onClick={handleMoveToNegotiation}
                  disabled={actionBusy}
                  className="w-full rounded-md bg-purple-600 px-3 py-2 text-xs font-medium text-white transition hover:bg-purple-700 disabled:opacity-40"
                >
                  {actionBusy ? 'Processing...' : 'Move to Negotiation'}
                </button>
              )}

              {rfq.stage === 'Negotiation' && (
                <button
                  onClick={handleMarkWon}
                  disabled={actionBusy}
                  className="w-full rounded-md bg-emerald-600 px-3 py-2 text-xs font-medium text-white transition hover:bg-emerald-700 disabled:opacity-40"
                >
                  {actionBusy ? 'Processing...' : 'Mark as Won'}
                </button>
              )}

              {rfq.stage === 'Won' && rfq.quotationId && (
                <Link
                  href={`/quotations/${rfq.quotationId}`}
                  className="block w-full rounded-md border border-brand-200 bg-brand-50 px-3 py-2 text-center text-xs font-medium text-brand-700 transition hover:bg-brand-100"
                >
                  View Quotation
                </Link>
              )}

              {rfq.stage === 'Lost' && (
                <>
                  {rfq.lostReason && (
                    <div className="rounded-md bg-red-50 p-3">
                      <span className="text-[11px] font-medium text-red-600">Lost Reason</span>
                      <p className="mt-0.5 text-xs text-red-700">{rfq.lostReason}</p>
                    </div>
                  )}
                  <button
                    onClick={handleReopen}
                    disabled={actionBusy}
                    className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-xs font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-40"
                  >
                    {actionBusy ? 'Processing...' : 'Reopen (Back to Draft)'}
                  </button>
                </>
              )}

              {/* Mark as Lost (available for all non-terminal stages) */}
              {!isTerminal && (
                <>
                  {!showLostInput ? (
                    <button
                      onClick={() => setShowLostInput(true)}
                      disabled={actionBusy}
                      className="w-full rounded-md border border-red-300 bg-white px-3 py-2 text-xs font-medium text-red-600 transition hover:bg-red-50 disabled:opacity-40"
                    >
                      Mark as Lost
                    </button>
                  ) : (
                    <div className="rounded-md border border-red-200 bg-red-50/50 p-3">
                      <label className="mb-1 block text-[11px] font-medium text-red-700">
                        Reason for loss *
                      </label>
                      <textarea
                        value={lostReason}
                        onChange={(e) => setLostReason(e.target.value)}
                        className="mb-2 w-full rounded-md border border-red-200 bg-white px-2 py-1.5 text-xs text-slate-700 placeholder:text-slate-400 focus:border-red-300 focus:outline-none focus:ring-1 focus:ring-red-300"
                        placeholder="Why was this opportunity lost?"
                        rows={2}
                      />
                      {!lostReason.trim() && (
                        <p className="mb-2 text-[11px] text-red-500">Please provide a reason.</p>
                      )}
                      <div className="flex gap-2">
                        <button
                          onClick={handleMarkLost}
                          disabled={actionBusy || !lostReason.trim()}
                          className="flex-1 rounded-md bg-red-600 px-2 py-1.5 text-[11px] font-medium text-white transition hover:bg-red-700 disabled:opacity-40"
                        >
                          {actionBusy ? 'Processing...' : 'Confirm Lost'}
                        </button>
                        <button
                          onClick={() => {
                            setShowLostInput(false)
                            setLostReason('')
                          }}
                          className="rounded-md border border-slate-200 px-2 py-1.5 text-[11px] font-medium text-slate-600 transition hover:bg-slate-50"
                        >
                          Cancel
                        </button>
                      </div>
                    </div>
                  )}
                </>
              )}
            </div>
          </Card>

          {/* Quick stats */}
          <Card className="p-5">
            <h2 className="mb-4 text-sm font-semibold text-slate-900">Quick Stats</h2>
            <div className="space-y-3">
              <div>
                <span className="text-[11px] font-medium text-slate-500">
                  Days in current stage
                </span>
                <p className="text-lg font-bold text-slate-900">{daysInStage}</p>
              </div>
              <div>
                <span className="text-[11px] font-medium text-slate-500">
                  Total pipeline age
                </span>
                <p className="text-lg font-bold text-slate-900">{totalAge} days</p>
              </div>
              <div>
                <span className="mb-1 block text-[11px] font-medium text-slate-500">
                  Win probability
                </span>
                <ProbabilityBar value={rfq.probability} />
              </div>
            </div>
          </Card>
        </div>
      </div>
    </div>
  )
}
