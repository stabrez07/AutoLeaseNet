'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type DriverDetail, type LeaseSummary, type CustomerDetail } from '../../../lib/bff-client'
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

function SkipButton({ label, onSkip, skipped }: { label: string; onSkip: () => void; skipped: boolean }) {
  if (skipped) return <span className="rounded bg-amber-50 px-2 py-1 text-xs text-amber-700">Skipped (mock)</span>
  return (
    <button type="button" onClick={onSkip}
      className="rounded border border-amber-300 bg-amber-50 px-3 py-1.5 text-xs font-medium text-amber-700 transition hover:bg-amber-100">
      {label}
    </button>
  )
}

export default function DriverDetailPage() {
  const { t } = useLocale()
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const f = t.crudDrivers.fields

  const [data, setData] = useState<DriverDetail | null>(null)
  const [customer, setCustomer] = useState<CustomerDetail | null>(null)
  const [activeLease, setActiveLease] = useState<LeaseSummary | null | undefined>(undefined)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tammSkipped, setTammSkipped] = useState(false)

  useEffect(() => {
    setLoading(true); setError(null)
    bff.getDriverById(id)
      .then(async (driver) => {
        setData(driver)
        const [cust, lease] = await Promise.all([
          driver.customerId ? bff.getCustomerById(driver.customerId).catch(() => null) : Promise.resolve(null),
          bff.getDriverCurrentLease(id).catch(() => null),
        ])
        setCustomer(cust)
        setActiveLease(lease)
      })
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
      <div className="flex items-center gap-2 flex-wrap">
        <Badge tone={statusTone}>{data.status}</Badge>
        {licenseExpiring && <Badge tone="red">License expiring soon ⚠</Badge>}
        <Badge tone="slate">
          TAMM: {(t.crudDrivers.tammStatuses as Record<string, string>)[data.tammAuthorizationStatus] ?? data.tammAuthorizationStatus}
        </Badge>
        {data.nationalityCode && <Badge tone="slate">{data.nationalityCode}</Badge>}
      </div>

      {/* Active Lease Banner */}
      {activeLease && (
        <Card className="border-l-4 border-amber-400 bg-amber-50 p-4">
          <div className="flex items-start justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-amber-700 mb-2">{t.leases.currentLease}</p>
              <div className="grid grid-cols-2 gap-x-8 gap-y-2 text-sm md:grid-cols-3">
                <div>
                  <div className="text-xs text-amber-600">Lease #</div>
                  <div className="font-mono font-semibold text-amber-900">{activeLease.leaseNumber}</div>
                </div>
                <div>
                  <div className="text-xs text-amber-600">Vehicle</div>
                  <div className="font-medium text-amber-900">{activeLease.vehicleMakeModel}</div>
                </div>
                <div>
                  <div className="text-xs text-amber-600">Period</div>
                  <div className="font-medium text-amber-900">
                    {activeLease.contractStartUtc.substring(0, 10)} → {activeLease.contractEndUtc.substring(0, 10)}
                  </div>
                </div>
              </div>
            </div>
            <SecondaryButton onClick={() => router.push(`/leases/${activeLease.id}`)} className="px-2 py-1 text-xs shrink-0 ms-4">
              View Lease
            </SecondaryButton>
          </div>
        </Card>
      )}

      {/* Linked customer */}
      {customer && (
        <Card className="p-4 flex items-center justify-between">
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Linked Customer</p>
            <p className="text-sm font-semibold text-slate-900">{customer.displayName}</p>
            <p className="text-xs text-slate-500">{customer.type} · {customer.status} · {customer.mobile ?? customer.email ?? ''}</p>
          </div>
          <SecondaryButton onClick={() => router.push(`/customers/${customer.id}`)} className="px-2 py-1 text-xs">
            View Customer
          </SecondaryButton>
        </Card>
      )}

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

        {/* TAMM Integration */}
        <div className="pt-4">
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400">TAMM Authorization</h3>
            <SkipButton label="Skip TAMM Check (Demo Mode)" onSkip={() => setTammSkipped(true)} skipped={tammSkipped} />
          </div>
          {tammSkipped && (
            <div className="mb-3 rounded bg-amber-50 px-3 py-2 text-xs text-amber-700">
              TAMM check skipped — running in mock mode.
            </div>
          )}
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
            <div>
              <div className="text-xs text-slate-500">TAMM Status</div>
              <div className="mt-0.5">
                <Badge tone={
                  data.tammAuthorizationStatus === 'Authorized' ? 'green' :
                  data.tammAuthorizationStatus === 'Pending' ? 'amber' :
                  data.tammAuthorizationStatus === 'Rejected' ? 'red' : 'slate'
                }>
                  {(t.crudDrivers.tammStatuses as Record<string, string>)[data.tammAuthorizationStatus] ?? data.tammAuthorizationStatus}
                </Badge>
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
