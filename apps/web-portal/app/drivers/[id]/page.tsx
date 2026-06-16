'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type DriverDetail } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, SecondaryButton, Spinner } from '../../../components/ui'

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

export default function DriverDetailPage() {
  const { t } = useLocale()
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const f = t.crudDrivers.fields
  const [data, setData] = useState<DriverDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setLoading(true); setError(null)
    bff.getDriverById(id)
      .then(setData)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [id])

  if (loading) return <Spinner label={t.common.loading} />
  if (error) return <ErrorBox message={error} retryLabel={t.common.retry} />
  if (!data) return <p className="text-sm text-slate-500">{t.common.notFound}</p>

  const statusTone = data.status === 'Active' ? 'green' : data.status === 'Suspended' ? 'amber' : 'slate'
  const licenseExpiring = new Date(data.licenseExpiryDate) < new Date(Date.now() + 30 * 86400000)

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <PageHeader
        title={data.personNameEn}
        subtitle={`${data.driverLicenseNumber} · ${data.id}`}
        action={<SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>}
      />
      <div className="flex items-center gap-2">
        <Badge tone={statusTone}>{data.status}</Badge>
        {licenseExpiring && <Badge tone="red">License expiring</Badge>}
        <Badge tone="slate">
          TAMM: {(t.crudDrivers.tammStatuses as Record<string, string>)[data.tammAuthorizationStatus] ?? data.tammAuthorizationStatus}
        </Badge>
      </div>

      <Card className="divide-y divide-slate-100 p-6 space-y-4">
        <div>
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">{t.crudDrivers.sections.identity}</h3>
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
            <Field label={f.personNameEn} value={data.personNameEn} />
            <Field label={f.personNameAr} value={data.personNameAr} />
            <Field label={f.idTypeCode} value={(t.crudDrivers.idTypes as Record<number, string>)[data.idTypeCode]} />
            <Field label={f.personIdNumber} value={data.personIdNumber} />
            <Field label={f.dateOfBirth} value={data.dateOfBirth} />
            <Field label={f.nationalityCode} value={data.nationalityCode} />
          </div>
        </div>
        <div className="pt-4">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">{t.crudDrivers.sections.license}</h3>
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
            <Field label={f.licenseNumber} value={data.driverLicenseNumber} />
            <Field label={f.licenseClass} value={(t.crudDrivers.licenseClasses as Record<number, string>)[data.licenseClass]} />
            <div>
              <div className="text-xs text-slate-500">{f.licenseExpiry}</div>
              <div className={`mt-0.5 text-sm font-medium ${licenseExpiring ? 'text-red-600' : 'text-slate-900'}`}>
                {data.licenseExpiryDate} {licenseExpiring ? '⚠' : ''}
              </div>
            </div>
          </div>
        </div>
        <div className="pt-4">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">{t.crudDrivers.sections.contact}</h3>
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
            <Field label={f.mobile} value={data.mobile} />
            <Field label={f.email} value={data.email} />
            <Field label={f.nationalAddress} value={data.nationalAddress} />
            <Field label={f.customerId} value={data.customerId} />
          </div>
        </div>
        <div className="pt-4">
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
            <Field label={t.common.id} value={data.id} />
            <Field label={t.common.createdAt} value={data.createdAtUtc?.substring(0, 10)} />
            <Field label={t.common.updatedAt} value={data.updatedAtUtc?.substring(0, 10)} />
          </div>
        </div>
      </Card>
    </div>
  )
}
