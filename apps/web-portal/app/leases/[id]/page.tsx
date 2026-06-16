'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type LeaseDetail } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, SecondaryButton, Spinner } from '../../../components/ui'

const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Active: 'green', Extended: 'blue', PendingIssuance: 'amber',
  Suspended: 'amber', Draft: 'slate', Closed: 'slate', Cancelled: 'red',
}

function Field({ label, value, mono }: { label: string; value: string | number | null | undefined; mono?: boolean }) {
  return (
    <div>
      <div className="text-xs text-slate-500">{label}</div>
      <div className={`mt-0.5 text-sm font-medium text-slate-900 ${mono ? 'font-mono text-xs' : ''}`}>
        {value == null || value === '' ? '—' : String(value)}
      </div>
    </div>
  )
}

function SectionHdr({ children }: { children: React.ReactNode }) {
  return <h3 className="mb-3 mt-5 text-xs font-semibold uppercase tracking-wide text-slate-400 first:mt-0">{children}</h3>
}

function SkipButton({ label, onSkip, skipped }: { label: string; onSkip: () => void; skipped: boolean }) {
  if (skipped) return <span className="rounded bg-amber-50 px-2 py-1 text-xs text-amber-700">Skipped (mock)</span>
  return (
    <button
      type="button"
      onClick={onSkip}
      className="rounded border border-amber-300 bg-amber-50 px-3 py-1.5 text-xs font-medium text-amber-700 transition hover:bg-amber-100"
    >
      {label}
    </button>
  )
}

function sar(n: number) { return `SAR ${n.toLocaleString(undefined, { minimumFractionDigits: 2 })}` }
function fmt(s: string | null | undefined) { return s ? s.substring(0, 10) : '—' }
function fmtDt(s: string | null | undefined) { return s ? s.substring(0, 19).replace('T', ' ') : '—' }

export default function LeaseDetailPage() {
  const { t } = useLocale()
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const tl = t.leases.detail
  const tf = tl.fields

  const [data, setData] = useState<LeaseDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tajeerSkipped, setTajeerSkipped] = useState(false)
  const [zatcaSkipped, setZatcaSkipped] = useState(false)

  useEffect(() => {
    setLoading(true); setError(null)
    bff.getLeaseById(id)
      .then(setData)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [id])

  if (loading) return <Spinner label={t.common.loading} />
  if (error) return <ErrorBox message={error} onRetry={() => router.refresh()} retryLabel={t.common.retry} />
  if (!data) return <p className="text-sm text-slate-500">{t.common.notFound}</p>

  const statusLabel = (t.leases.statuses as Record<string, string>)[data.status] ?? data.status
  const tone = STATUS_TONES[data.status] ?? 'slate'
  const contractTypeLabel = (t.leases.contractTypes as Record<string, string>)[data.contractTypeCode] ?? data.contractTypeCode

  return (
    <div className="mx-auto max-w-5xl space-y-4">
      <PageHeader
        title={`${tl.title} ${data.leaseNumber}`}
        subtitle={`${data.customerDisplayName} · ${data.vehicleMakeModel}`}
        action={<SecondaryButton onClick={() => router.push('/leases')}>{t.common.back}</SecondaryButton>}
      />

      <div className="flex flex-wrap gap-2">
        <Badge tone={tone}>{statusLabel}</Badge>
        <Badge tone="slate">{contractTypeLabel}</Badge>
        {data.tajeerContractNumber && (
          <Badge tone="blue">Tajeer #{data.tajeerContractNumber}</Badge>
        )}
      </div>

      {/* Contract Details */}
      <Card className="p-6 space-y-4">
        <SectionHdr>{tl.contract}</SectionHdr>
        <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-4">
          <Field label={tf.leaseNumber} value={data.leaseNumber} mono />
          <Field label={tf.contractType} value={contractTypeLabel} />
          <Field label={tf.start} value={fmt(data.contractStartUtc)} />
          <Field label={tf.end} value={fmt(data.contractEndUtc)} />
          <Field label={tf.branch} value={`${data.workingBranchCode} — ${data.workingBranchName}`} />
          <Field label={tf.kmLimit} value={`${data.allowedKmPerDay} km / day`} />
          <Field label={tf.paymentMethod} value={data.paymentMethodCode} />
          {data.issuedAtUtc && <Field label={tf.issuedAt} value={fmtDt(data.issuedAtUtc)} />}
          {data.closedAtUtc && <Field label={tf.closedAt} value={fmtDt(data.closedAtUtc)} />}
        </div>

        <SectionHdr>{t.common.details} — {t.customers.title}</SectionHdr>
        <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
          <div>
            <div className="text-xs text-slate-500">{tf.customer}</div>
            <button type="button" className="mt-0.5 text-sm font-medium text-brand-700 hover:underline" onClick={() => router.push(`/customers/${data.customerId}`)}>
              {data.customerDisplayName}
            </button>
          </div>
          <div>
            <div className="text-xs text-slate-500">{tf.vehicle}</div>
            <button type="button" className="mt-0.5 text-sm font-medium text-brand-700 hover:underline" onClick={() => router.push(`/vehicles/${data.vehicleId}`)}>
              {data.vehicleMakeModel} — {data.vehiclePlate}
            </button>
          </div>
          <div>
            <div className="text-xs text-slate-500">{tf.driver}</div>
            <button type="button" className="mt-0.5 text-sm font-medium text-brand-700 hover:underline" onClick={() => router.push(`/drivers/${data.primaryDriverId ?? ''}`)}>
              {data.primaryDriverName ?? '—'}
            </button>
          </div>
        </div>
      </Card>

      {/* Financials */}
      <Card className="p-6">
        <SectionHdr>{tl.financial}</SectionHdr>
        <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-5">
          <Field label={tf.rent} value={sar(data.rentAmountSar)} />
          <Field label={tf.vat} value={sar(data.vatAmountSar)} />
          <Field label={tf.total} value={sar(data.totalAmountSar)} />
          <Field label={tf.paid} value={sar(data.paidAmountSar)} />
          <Field label={tf.remaining} value={sar(data.remainingAmountSar)} />
        </div>
      </Card>

      {/* Tajeer Integration */}
      <Card className="p-6">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400">{tl.tajeer}</h3>
          <SkipButton label={tl.skipTajeer} onSkip={() => setTajeerSkipped(true)} skipped={tajeerSkipped} />
        </div>
        {tajeerSkipped && (
          <div className="mb-3 rounded bg-amber-50 px-3 py-2 text-xs text-amber-700">{tl.skippedMsg}</div>
        )}
        <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
          <div>
            <div className="text-xs text-slate-500">{tf.tajeerStatus}</div>
            <div className="mt-0.5">
              {data.tajeerStatus
                ? <Badge tone={data.tajeerStatus === 'Confirmed' ? 'green' : 'amber'}>{data.tajeerStatus}</Badge>
                : <span className="text-sm text-slate-400">—</span>}
            </div>
          </div>
          <Field label={tf.tajeerRef} value={data.tajeerContractNumber} mono />
          <div>
            <div className="text-xs text-slate-500">{tf.tajeerUrl}</div>
            <div className="mt-0.5 text-xs">
              {data.tajeerIssuanceUrl
                ? <span className="font-mono text-brand-700 break-all">{data.tajeerIssuanceUrl}</span>
                : <span className="text-slate-400">—</span>}
            </div>
          </div>
        </div>
      </Card>

      {/* ZATCA */}
      <Card className="p-6">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400">{tl.zatca}</h3>
          <SkipButton label={tl.skipZatca} onSkip={() => setZatcaSkipped(true)} skipped={zatcaSkipped} />
        </div>
        {zatcaSkipped && (
          <div className="mb-3 rounded bg-amber-50 px-3 py-2 text-xs text-amber-700">{tl.skippedMsg}</div>
        )}
        <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
          <div>
            <div className="text-xs text-slate-500">{tf.zatcaStatus}</div>
            <div className="mt-0.5">
              {data.zatcaSubmissionStatus
                ? <Badge tone={data.zatcaSubmissionStatus === 'Cleared' ? 'green' : 'amber'}>{data.zatcaSubmissionStatus}</Badge>
                : <span className="text-sm text-slate-400">—</span>}
            </div>
          </div>
          <Field label={tf.zatcaInvoice} value={data.zatcaInvoiceNumber} mono />
        </div>
      </Card>

      {/* Inspections */}
      <Card className="p-6">
        <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">{tl.inspections}</h3>
        {data.inspections.length === 0 ? (
          <p className="text-sm text-slate-500">{tl.noInspections}</p>
        ) : (
          <table className="w-full text-xs">
            <thead className="border-b border-slate-200">
              <tr>
                {Object.values(tl.inspectionColumns).map((h) => (
                  <th key={h} className="px-2 py-2 text-left font-medium text-slate-500 uppercase tracking-wide">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {data.inspections.map((ins) => (
                <tr key={ins.id} className="border-t border-slate-100">
                  <td className="px-2 py-2"><Badge tone="slate">{ins.type}</Badge></td>
                  <td className="px-2 py-2 text-slate-700">{ins.inspectedAtUtc.substring(0, 10)}</td>
                  <td className="px-2 py-2 font-mono">{ins.odometer.toLocaleString()} km</td>
                  <td className="px-2 py-2">
                    <Badge tone={ins.conditionCode === 'Good' ? 'green' : ins.conditionCode === 'Fair' ? 'amber' : 'red'}>
                      {ins.conditionCode}
                    </Badge>
                  </td>
                  <td className="px-2 py-2 text-slate-600">{ins.notes ?? '—'}</td>
                  <td className="px-2 py-2 text-slate-600">{ins.branch}</td>
                  <td className="px-2 py-2 text-slate-600">{ins.inspector}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      {/* Incidents */}
      <Card className="p-6">
        <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">{tl.incidents}</h3>
        {data.incidents.length === 0 ? (
          <p className="text-sm text-slate-500">{tl.noIncidents}</p>
        ) : (
          <table className="w-full text-xs">
            <thead className="border-b border-slate-200">
              <tr>
                {Object.values(tl.incidentColumns).map((h) => (
                  <th key={h} className="px-2 py-2 text-left font-medium text-slate-500 uppercase tracking-wide">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {data.incidents.map((inc) => (
                <tr key={inc.id} className="border-t border-slate-100">
                  <td className="px-2 py-2"><Badge tone="amber">{inc.type}</Badge></td>
                  <td className="px-2 py-2 text-slate-700">{inc.occurredAtUtc.substring(0, 10)}</td>
                  <td className="px-2 py-2 text-slate-600 max-w-[200px] truncate">{inc.description}</td>
                  <td className="px-2 py-2 font-mono">{inc.estimatedCostSar != null ? sar(inc.estimatedCostSar) : '—'}</td>
                  <td className="px-2 py-2 font-mono">{inc.claimNumber ?? '—'}</td>
                  <td className="px-2 py-2">
                    <Badge tone={inc.resolved ? 'green' : 'amber'}>{inc.resolved ? t.common.yes : t.common.no}</Badge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  )
}
