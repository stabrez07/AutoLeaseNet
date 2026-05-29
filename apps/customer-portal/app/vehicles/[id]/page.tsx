'use client'

import Link from 'next/link'
import { useCallback, useEffect, useState } from 'react'
import { useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type MyVehicleDetail } from '../../../lib/bff-client'
import { Card, ErrorBox, PageHeader, Spinner } from '../../../components/ui'

function formatDate(iso: string | null): string {
  return iso ? iso.slice(0, 10) : '—'
}

function formatKm(km: number | null): string {
  return km === null ? '—' : km.toLocaleString()
}

export default function VehicleDetailPage() {
  const { t } = useLocale()
  const params = useParams<{ id: string }>()
  const id = params?.id
  const [detail, setDetail] = useState<MyVehicleDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notFound, setNotFound] = useState(false)

  const load = useCallback(async () => {
    if (!id) return
    setError(null)
    setNotFound(false)
    try {
      setDetail(await bff.getMyVehicleDetail(id))
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
      href="/vehicles"
      className="text-brand-700 hover:text-brand-900 text-sm font-medium underline-offset-4 hover:underline"
    >
      ← {t.vehicleDetail.backToList}
    </Link>
  )

  if (notFound) {
    return (
      <div>
        <PageHeader title={t.vehicles.title} action={backLink} />
        <Card className="p-8 text-center text-sm text-slate-500">{t.vehicleDetail.notFound}</Card>
      </div>
    )
  }

  if (error) {
    return (
      <div>
        <PageHeader title={t.vehicles.title} action={backLink} />
        <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />
      </div>
    )
  }

  if (!detail) {
    return (
      <div>
        <PageHeader title={t.vehicles.title} action={backLink} />
        <Spinner label={t.common.loading} />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${detail.make} ${detail.model} ${detail.modelYear}`}
        subtitle={`${detail.plateLetters}  ${detail.plateNumber}`}
        action={backLink}
      />

      <Card className="p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
          {t.vehicleDetail.sections.identification}
        </h2>
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 text-sm md:grid-cols-2">
          <Row
            label={t.vehicleDetail.identification.plate}
            value={
              <span dir="rtl" className="font-mono">
                {detail.plateLetters}&nbsp;&nbsp;{detail.plateNumber}
              </span>
            }
          />
          <Row
            label={t.vehicleDetail.identification.makeModel}
            value={`${detail.make} ${detail.model}`}
          />
          <Row label={t.vehicleDetail.identification.year} value={detail.modelYear} />
          <Row label={t.vehicleDetail.identification.color} value={detail.color ?? '—'} />
          <Row
            label={t.vehicleDetail.identification.fuelTypeCode}
            value={detail.fuelTypeCode}
          />
          <Row
            label={t.vehicleDetail.identification.transmissionTypeCode}
            value={detail.transmissionTypeCode}
          />
          <Row
            label={t.vehicleDetail.identification.bodyTypeCode}
            value={detail.bodyTypeCode}
          />
          <Row label={t.vehicleDetail.identification.seats} value={detail.seats} />
        </dl>
      </Card>

      <Card className="p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
          {t.vehicleDetail.sections.regulatory}
        </h2>
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 text-sm md:grid-cols-2">
          <Row
            label={t.vehicleDetail.regulatory.licenseExpiry}
            value={formatDate(detail.licenseExpiryDate)}
          />
          <Row
            label={t.vehicleDetail.regulatory.insuranceExpiry}
            value={formatDate(detail.insuranceExpiryDate)}
          />
          <Row
            label={t.vehicleDetail.regulatory.inspectionExpiry}
            value={formatDate(detail.inspectionExpiryDate)}
          />
          <Row
            label={t.vehicleDetail.regulatory.insuranceCompany}
            value={detail.insuranceCompany ?? '—'}
          />
          <Row
            label={t.vehicleDetail.regulatory.insurancePolicyNumber}
            value={detail.insurancePolicyNumber ?? '—'}
          />
        </dl>
      </Card>

      <Card className="p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
          {t.vehicleDetail.sections.service}
        </h2>
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 text-sm md:grid-cols-2">
          <Row
            label={t.vehicleDetail.service.currentKm}
            value={formatKm(detail.currentKm)}
          />
          <Row
            label={t.vehicleDetail.service.nextServiceDueKm}
            value={formatKm(detail.nextServiceDueKm)}
          />
          <Row
            label={t.vehicleDetail.service.nextServiceDueDate}
            value={formatDate(detail.nextServiceDueDate)}
          />
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
