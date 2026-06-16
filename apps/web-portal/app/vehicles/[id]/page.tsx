'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type VehicleDetail } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../../components/ui'

function Field({ label, value }: { label: string; value: string | number | boolean | null | undefined }) {
  return (
    <div>
      <div className="text-xs text-slate-500">{label}</div>
      <div className="mt-0.5 text-sm font-medium text-slate-900">
        {value === null || value === undefined || value === '' ? '—' : String(value)}
      </div>
    </div>
  )
}

const SECTION_HDR = 'text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3 mt-4'

const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Available: 'green', Reserved: 'blue', OnRent: 'amber',
  InService: 'slate', Damaged: 'red', Sold: 'slate', Disposed: 'slate',
}

function ExpiryField({ label, value }: { label: string; value: string | null | undefined }) {
  const soon = value ? new Date(value) < new Date(Date.now() + 30 * 86400000) : false
  return (
    <div>
      <div className="text-xs text-slate-500">{label}</div>
      <div className={`mt-0.5 text-sm font-medium ${soon && value ? 'text-red-600' : 'text-slate-900'}`}>
        {value ?? '—'} {soon && value ? '⚠' : ''}
      </div>
    </div>
  )
}

export default function VehicleDetailPage() {
  const { t } = useLocale()
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const f = t.crudVehicles.fields
  const [data, setData] = useState<VehicleDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setLoading(true); setError(null)
    bff.getVehicleById(id)
      .then(setData)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [id])

  if (loading) return <Spinner label={t.common.loading} />
  if (error) return <ErrorBox message={error} retryLabel={t.common.retry} />
  if (!data) return <p className="text-sm text-slate-500">{t.common.notFound}</p>

  const statusTone = STATUS_TONES[data.status] ?? 'slate'
  const statusLabel = (t.crudVehicles.statuses as Record<string, string>)[data.status] ?? data.status

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <PageHeader
        title={`${data.make} ${data.model} (${data.modelYear})`}
        subtitle={`${data.plateLetters} ${data.plateNumber} · VIN: ${data.vin}`}
        action={<button type="button" onClick={() => router.back()}
          className="text-sm text-slate-500 hover:text-slate-700">{t.common.back}</button>}
      />
      <div className="flex gap-2">
        <Badge tone={statusTone}>{statusLabel}</Badge>
        <Badge tone="slate">{(t.crudVehicles.fuelTypes as Record<string, string>)[data.fuelType] ?? data.fuelType}</Badge>
        <Badge tone="slate">{(t.crudVehicles.transmissionTypes as Record<string, string>)[data.transmissionType] ?? data.transmissionType}</Badge>
      </div>

      <Card className="divide-y divide-slate-100 p-5 space-y-4">
        <div>
          <p className={SECTION_HDR}>{t.crudVehicles.sections.plate}</p>
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-4">
            <Field label={f.plateNumber} value={data.plateNumber} />
            <Field label={f.plateLetters} value={data.plateLetters} />
            <Field label={f.plateTypeCode} value={data.plateTypeCode} />
            <Field label={f.vin} value={data.vin} />
            <Field label={f.engineNumber} value={data.engineNumber} />
          </div>
        </div>
        <div className="pt-4">
          <p className={SECTION_HDR}>{t.crudVehicles.sections.specs}</p>
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-4">
            <Field label={f.make} value={data.make} />
            <Field label={f.model} value={data.model} />
            <Field label={f.modelYear} value={data.modelYear} />
            <Field label={f.color} value={data.color} />
            <Field label={f.fuelType} value={(t.crudVehicles.fuelTypes as Record<string, string>)[data.fuelType]} />
            <Field label={f.transmissionType} value={(t.crudVehicles.transmissionTypes as Record<string, string>)[data.transmissionType]} />
            <Field label={f.bodyType} value={(t.crudVehicles.bodyTypes as Record<string, string>)[data.bodyType]} />
            <Field label={f.seats} value={data.seats} />
            <Field label={f.currentKm} value={data.currentKm.toLocaleString()} />
          </div>
        </div>
        <div className="pt-4">
          <p className={SECTION_HDR}>{t.crudVehicles.sections.regulatory}</p>
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
            <ExpiryField label={f.licenseExpiry} value={data.licenseExpiryDate} />
            <ExpiryField label={f.insuranceExpiry} value={data.insuranceExpiryDate} />
            <ExpiryField label={f.inspectionExpiry} value={data.inspectionExpiryDate} />
            <Field label={f.insuranceCompany} value={data.insuranceCompany} />
            <Field label={f.insurancePolicyNumber} value={data.insurancePolicyNumber} />
          </div>
        </div>
        <div className="pt-4">
          <p className={SECTION_HDR}>{t.crudVehicles.sections.financial}</p>
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
            <Field label={f.purchasePrice} value={data.purchasePrice != null ? `SAR ${data.purchasePrice.toLocaleString()}` : undefined} />
            <Field label={f.purchaseDate} value={data.purchaseDate} />
            <Field label={t.common.createdAt} value={data.createdAtUtc?.substring(0, 10)} />
          </div>
        </div>
      </Card>
    </div>
  )
}
