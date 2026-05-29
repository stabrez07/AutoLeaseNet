'use client'

import Link from 'next/link'
import { useCallback, useEffect, useState } from 'react'
import { useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type MyLeaseDetail } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner, statusTone } from '../../../components/ui'

function formatDate(iso: string | null): string {
  return iso ? iso.slice(0, 10) : '—'
}

function formatTimestamp(iso: string | null): string {
  if (!iso) return '—'
  // yyyy-MM-dd HH:mm — clearer than the full ISO on a timeline.
  return iso.slice(0, 16).replace('T', ' ')
}

function formatMoney(n: number): string {
  return n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

type StatusKey = keyof ReturnType<typeof useLocale>['t']['leases']['statuses']

export default function LeaseDetailPage() {
  const { t } = useLocale()
  const params = useParams<{ id: string }>()
  const id = params?.id
  const [detail, setDetail] = useState<MyLeaseDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notFound, setNotFound] = useState(false)

  const load = useCallback(async () => {
    if (!id) return
    setError(null)
    setNotFound(false)
    try {
      setDetail(await bff.getMyLeaseDetail(id))
    } catch (e: unknown) {
      const status = (e as { status?: number })?.status
      if (status === 404) {
        setNotFound(true)
        return
      }
      setError(e instanceof Error ? e.message : t.common.error)
    }
  }, [id, t.common.error])

  useEffect(() => {
    void load()
  }, [load])

  const backLink = (
    <Link
      href="/leases"
      className="text-brand-700 hover:text-brand-900 text-sm font-medium underline-offset-4 hover:underline"
    >
      ← {t.leaseDetail.backToList}
    </Link>
  )

  if (notFound) {
    return (
      <div>
        <PageHeader title={t.leases.title} action={backLink} />
        <Card className="p-8 text-center text-sm text-slate-500">{t.leaseDetail.notFound}</Card>
      </div>
    )
  }

  if (error) {
    return (
      <div>
        <PageHeader title={t.leases.title} action={backLink} />
        <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />
      </div>
    )
  }

  if (!detail) {
    return (
      <div>
        <PageHeader title={t.leases.title} action={backLink} />
        <Spinner label={t.common.loading} />
      </div>
    )
  }

  const statusLabel =
    (t.leases.statuses as Record<number, string>)[detail.status as StatusKey] ?? `#${detail.status}`

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${t.leases.columns.contractNumber} ${detail.tajeerContractNumber ?? '—'}`}
        action={backLink}
      />

      <Card className="p-5">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
            {t.leaseDetail.sections.contract}
          </h2>
          <Badge tone={statusTone(detail.status)}>{statusLabel}</Badge>
        </div>
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 text-sm md:grid-cols-2">
          <Row label={t.leaseDetail.contract.start} value={formatDate(detail.contractStartUtc)} />
          <Row label={t.leaseDetail.contract.end} value={formatDate(detail.contractEndUtc)} />
          <Row
            label={t.leaseDetail.contract.actualReturn}
            value={formatDate(detail.actualReturnUtc)}
          />
          <Row label={t.leaseDetail.contract.typeCode} value={detail.contractTypeCode} />
          <Row label={t.leaseDetail.contract.allowedKmPerDay} value={detail.allowedKmPerDay} />
          <Row label={t.leaseDetail.contract.allowedKmPerHour} value={detail.allowedKmPerHour} />
          <Row
            label={t.leaseDetail.contract.unlimitedKm}
            value={detail.unlimitedKm ? t.leaseDetail.yes : t.leaseDetail.no}
          />
          <Row label={t.leaseDetail.contract.allowedLateHours} value={detail.allowedLateHours} />
          <Row label={t.leaseDetail.contract.extensionCount} value={detail.extensionCount} />
        </dl>
      </Card>

      <Card className="p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
          {t.leaseDetail.sections.vehicle}
        </h2>
        {detail.vehicle ? (
          <dl className="grid grid-cols-1 gap-x-6 gap-y-2 text-sm md:grid-cols-2">
            <Row
              label={t.vehicles.columns.plate}
              value={
                <span dir="rtl" className="font-mono">
                  {detail.vehicle.plateLetters}&nbsp;&nbsp;{detail.vehicle.plateNumber}
                </span>
              }
            />
            <Row
              label={t.vehicles.columns.makeModel}
              value={`${detail.vehicle.make} ${detail.vehicle.model}`}
            />
            <Row label={t.vehicles.columns.year} value={detail.vehicle.modelYear} />
            <Row label={t.vehicles.columns.color} value={detail.vehicle.color ?? '—'} />
          </dl>
        ) : (
          <p className="text-sm text-slate-500">{t.leaseDetail.vehicle.unassigned}</p>
        )}
      </Card>

      <Card className="p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
          {t.leaseDetail.sections.payment}
        </h2>
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 text-sm md:grid-cols-2">
          <Row label={t.leaseDetail.payment.rent} value={formatMoney(detail.rentAmount)} />
          <Row label={t.leaseDetail.payment.paid} value={formatMoney(detail.paidAmount)} />
          <Row label={t.leaseDetail.payment.remaining} value={formatMoney(detail.remainingAmount)} />
          <Row label={t.leaseDetail.payment.vat} value={formatMoney(detail.vatAmount)} />
          <Row label={t.leaseDetail.payment.total} value={formatMoney(detail.totalAmount)} />
          <Row label={t.leaseDetail.payment.methodCode} value={detail.paymentMethodCode} />
          {detail.discountType !== null && (
            <Row label={t.leaseDetail.payment.discountType} value={detail.discountType} />
          )}
          {detail.discountValue !== null && (
            <Row
              label={t.leaseDetail.payment.discountValue}
              value={formatMoney(detail.discountValue)}
            />
          )}
        </dl>
      </Card>

      <Card className="p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
          {t.leaseDetail.sections.timeline}
        </h2>
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 text-sm md:grid-cols-2">
          <Row label={t.leaseDetail.timeline.saved} value={formatTimestamp(detail.savedAtUtc)} />
          <Row label={t.leaseDetail.timeline.issued} value={formatTimestamp(detail.issuedAtUtc)} />
          <Row
            label={t.leaseDetail.timeline.suspended}
            value={formatTimestamp(detail.suspendedAtUtc)}
          />
          <Row label={t.leaseDetail.timeline.resumed} value={formatTimestamp(detail.resumedAtUtc)} />
          <Row label={t.leaseDetail.timeline.closed} value={formatTimestamp(detail.closedAtUtc)} />
          <Row
            label={t.leaseDetail.timeline.cancelled}
            value={formatTimestamp(detail.cancelledAtUtc)}
          />
          <Row label={t.leaseDetail.timeline.expired} value={formatTimestamp(detail.expiredAtUtc)} />
        </dl>
      </Card>
    </div>
  )
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-3 border-b border-slate-100 py-1.5 last:border-b-0">
      <dt className="text-xs uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className="text-end font-medium text-slate-800">{value}</dd>
    </div>
  )
}
