'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type CustomerDetail, type LeaseSummary, type VehicleSummary, type DriverSummary } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, PrimaryButton, SecondaryButton, Spinner } from '../../../components/ui'

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
function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">{title}</h3>
      <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">{children}</div>
    </div>
  )
}

const LEASE_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Active: 'green', Extended: 'blue', PendingIssuance: 'amber',
  Suspended: 'amber', Draft: 'slate', Closed: 'slate', Cancelled: 'red',
}
const VEHICLE_TONES: Record<number, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  1: 'green', 2: 'blue', 3: 'amber', 4: 'slate', 5: 'slate',
}
const DRIVER_TONES: Record<number, 'green' | 'amber' | 'slate'> = { 1: 'green', 2: 'amber', 3: 'slate' }

type Tab = 'details' | 'leases' | 'vehicles' | 'drivers'

export default function CustomerDetailPage() {
  const { t } = useLocale()
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const c = t.crudCustomers

  const [data, setData] = useState<CustomerDetail | null>(null)
  const [leases, setLeases] = useState<LeaseSummary[] | null>(null)
  const [vehicles, setVehicles] = useState<VehicleSummary[] | null>(null)
  const [drivers, setDrivers] = useState<DriverSummary[] | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [actionBusy, setActionBusy] = useState(false)
  const [actionMsg, setActionMsg] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('details')

  async function load() {
    setLoading(true); setError(null)
    try {
      const [cust, ls, vs, ds] = await Promise.all([
        bff.getCustomerById(id),
        bff.getCustomerLeases(id),
        bff.getCustomerVehicles(id),
        bff.getCustomerDrivers(id),
      ])
      setData(cust); setLeases(ls); setVehicles(vs); setDrivers(ds)
    } catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => { void load() }, [id])

  async function handleStatusAction(action: string) {
    setActionBusy(true); setActionMsg(null)
    try {
      const res = await bff.updateCustomerStatus(id, action, crypto.randomUUID())
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
      setActionMsg(t.common.successCreated)
      await load()
    } catch (e) { setActionMsg((e as Error).message) }
    finally { setActionBusy(false) }
  }

  if (loading) return <Spinner label={t.common.loading} />
  if (error) return <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />
  if (!data) return <p className="text-sm text-slate-500">{t.common.notFound}</p>

  const isB2B = data.type === 'B2B'
  const statusTone = data.status === 'Active' ? 'green' : data.status === 'Suspended' ? 'amber' : 'slate'

  const tabs: { key: Tab; label: string; count?: number }[] = [
    { key: 'details', label: t.common.details },
    { key: 'leases', label: t.common.leases, ...(leases != null ? { count: leases.length } : {}) },
    { key: 'vehicles', label: t.common.vehicles, ...(vehicles != null ? { count: vehicles.length } : {}) },
    { key: 'drivers', label: t.common.drivers, ...(drivers != null ? { count: drivers.length } : {}) },
  ]

  return (
    <div className="mx-auto max-w-5xl space-y-4">
      <PageHeader
        title={data.displayName}
        subtitle={`${data.type} · ${data.id}`}
        action={<SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>}
      />

      <div className="flex items-center gap-3">
        <Badge tone={statusTone}>{c.statuses[data.status as keyof typeof c.statuses] ?? data.status}</Badge>
        <Badge tone={isB2B ? 'blue' : 'slate'}>{isB2B ? t.customers.type.b2b : t.customers.type.b2c}</Badge>
        {data.kycVerified && <Badge tone="green">{c.kycBadge}</Badge>}
        {data.email && <span className="text-xs text-slate-500">{data.email}</span>}
        {data.mobile && <span className="text-xs text-slate-500">{data.mobile}</span>}
      </div>

      {/* Tab bar */}
      <div className="flex border-b border-slate-200">
        {tabs.map(({ key, label, count }) => (
          <button key={key} type="button"
            onClick={() => setTab(key)}
            className={[
              'px-4 py-2 text-sm font-medium transition border-b-2 -mb-px',
              tab === key ? 'border-brand-600 text-brand-700' : 'border-transparent text-slate-500 hover:text-slate-700',
            ].join(' ')}
          >
            {label}
            {count != null && <span className="ms-1.5 rounded-full bg-slate-100 px-1.5 py-0.5 text-xs font-semibold text-slate-600">{count}</span>}
          </button>
        ))}
      </div>

      {/* Details tab */}
      {tab === 'details' && (
        <>
          <Card className="divide-y divide-slate-100 p-6">
            {isB2B ? (
              <Section title={c.sections.identity}>
                <Field label={c.fields.legalName} value={data.legalName} />
                <Field label={c.fields.legalNameAr} value={data.legalNameAr} />
                <Field label={c.fields.commercialReg} value={data.commercialRegistration} />
                <Field label={c.fields.vatNumber} value={data.vatNumber} />
                <Field label={c.fields.creditLimit} value={data.creditLimit != null ? `${data.creditLimit} ${data.creditCurrency ?? ''}` : undefined} />
                <Field label={c.fields.billingAddress} value={data.billingAddress} />
              </Section>
            ) : (
              <Section title={c.sections.identity}>
                <Field label={c.fields.personNameEn} value={data.personNameEn} />
                <Field label={c.fields.personNameAr} value={data.personNameAr} />
                <Field label={c.fields.idTypeCode} value={c.idTypes[data.idTypeCode as keyof typeof c.idTypes] ?? data.idTypeCode} />
                <Field label={c.fields.personIdNumber} value={data.personIdNumber} />
                <Field label={c.fields.dateOfBirth} value={data.dateOfBirth} />
                <Field label={c.fields.nationalityCode} value={data.nationalityCode} />
              </Section>
            )}
            <div className="pt-4">
              <Section title={c.sections.contact}>
                <Field label={c.fields.email} value={data.email} />
                <Field label={c.fields.mobile} value={data.mobile} />
                <Field label={c.fields.nationalAddress} value={data.nationalAddress} />
              </Section>
            </div>
            <div className="pt-4">
              <Section title={t.common.details}>
                <Field label={t.common.id} value={data.id} />
                <Field label="Preferred language" value={data.preferredLanguage} />
                <Field label="KYC verified" value={data.kycVerified ? `Yes — ${data.kycVerifiedAtUtc?.substring(0, 10) ?? ''}` : 'No'} />
                <Field label={t.common.createdAt} value={data.createdAtUtc?.substring(0, 10)} />
                <Field label={t.common.updatedAt} value={data.updatedAtUtc?.substring(0, 10)} />
              </Section>
            </div>
          </Card>

          <Card className="p-4">
            <h3 className="mb-3 text-sm font-semibold text-slate-700">{t.common.actions}</h3>
            {actionMsg && (
              <p className={`mb-3 rounded px-3 py-1.5 text-sm ${actionMsg.includes('success') || actionMsg.includes('بنجاح') ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
                {actionMsg}
              </p>
            )}
            <div className="flex flex-wrap gap-2">
              {data.status === 'Active' && (
                <SecondaryButton onClick={() => handleStatusAction('suspend')} disabled={actionBusy}>{c.actions.suspend}</SecondaryButton>
              )}
              {data.status === 'Suspended' && (
                <PrimaryButton onClick={() => handleStatusAction('reactivate')} disabled={actionBusy}>{c.actions.reactivate}</PrimaryButton>
              )}
              {data.status !== 'Closed' && (
                <SecondaryButton onClick={() => handleStatusAction('close')} disabled={actionBusy}>{c.actions.close}</SecondaryButton>
              )}
            </div>
          </Card>
        </>
      )}

      {/* Leases tab */}
      {tab === 'leases' && (
        <Card className="overflow-hidden">
          {!leases || leases.length === 0 ? (
            <p className="p-6 text-sm text-slate-500">{t.common.noRecords}</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50 text-left">
                <tr>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Lease #</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Vehicle</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Driver</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Status</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Period</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Rent (SAR)</th>
                </tr>
              </thead>
              <tbody>
                {leases.map((l) => (
                  <tr key={l.id} className="cursor-pointer border-t border-slate-100 hover:bg-brand-50/40" onClick={() => router.push(`/leases/${l.id}`)}>
                    <td className="px-3 py-2 font-mono text-xs font-semibold text-brand-700">{l.leaseNumber}</td>
                    <td className="px-3 py-2 text-xs text-slate-700">{l.vehicleMakeModel}</td>
                    <td className="px-3 py-2 text-xs text-slate-600">{l.primaryDriverName ?? '—'}</td>
                    <td className="px-3 py-2"><Badge tone={LEASE_TONES[l.status] ?? 'slate'}>{(t.leases.statuses as Record<string, string>)[l.status] ?? l.status}</Badge></td>
                    <td className="px-3 py-2 text-xs text-slate-600">{l.contractStartUtc.substring(0,10)} → {l.contractEndUtc.substring(0,10)}</td>
                    <td className="px-3 py-2 text-end font-mono text-xs">{l.rentAmountSar.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
      )}

      {/* Vehicles tab */}
      {tab === 'vehicles' && (
        <Card className="overflow-hidden">
          {!vehicles || vehicles.length === 0 ? (
            <p className="p-6 text-sm text-slate-500">{t.common.noRecords}</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50 text-left">
                <tr>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Plate</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Make / Model</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Year</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Status</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Odometer</th>
                </tr>
              </thead>
              <tbody>
                {vehicles.map((v) => (
                  <tr key={v.id} className="cursor-pointer border-t border-slate-100 hover:bg-brand-50/40" onClick={() => router.push(`/vehicles/${v.id}`)}>
                    <td className="px-3 py-2 font-mono text-xs text-brand-700">{v.plateNumber}</td>
                    <td className="px-3 py-2 text-xs text-slate-700">{v.make} {v.model}</td>
                    <td className="px-3 py-2 text-xs text-slate-600">{v.modelYear}</td>
                    <td className="px-3 py-2"><Badge tone={VEHICLE_TONES[v.status] ?? 'slate'}>{(t.vehicles.statuses as Record<number, string>)[v.status] ?? v.status}</Badge></td>
                    <td className="px-3 py-2 text-end font-mono text-xs">{v.currentKm.toLocaleString()} km</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
      )}

      {/* Drivers tab */}
      {tab === 'drivers' && (
        <Card className="overflow-hidden">
          {!drivers || drivers.length === 0 ? (
            <p className="p-6 text-sm text-slate-500">{t.common.noRecords}</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50 text-left">
                <tr>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Name</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">License #</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Expiry</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Status</th>
                </tr>
              </thead>
              <tbody>
                {drivers.map((d) => {
                  const expiring = new Date(d.licenseExpiryDate) < new Date(Date.now() + 30 * 86400000)
                  return (
                    <tr key={d.id} className="cursor-pointer border-t border-slate-100 hover:bg-brand-50/40" onClick={() => router.push(`/drivers/${d.id}`)}>
                      <td className="px-3 py-2 text-xs font-medium text-brand-700">{d.personNameEn}</td>
                      <td className="px-3 py-2 font-mono text-xs text-slate-700">{d.driverLicenseNumber}</td>
                      <td className={`px-3 py-2 text-xs ${expiring ? 'text-red-600 font-semibold' : 'text-slate-600'}`}>
                        {d.licenseExpiryDate} {expiring ? '⚠' : ''}
                      </td>
                      <td className="px-3 py-2"><Badge tone={DRIVER_TONES[d.status] ?? 'slate'}>{(t.drivers.statuses as Record<number, string>)[d.status] ?? d.status}</Badge></td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          )}
        </Card>
      )}
    </div>
  )
}
