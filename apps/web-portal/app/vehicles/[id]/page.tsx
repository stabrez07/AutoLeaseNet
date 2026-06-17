'use client'

import { useEffect, useState, useCallback } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import {
  bff,
  type VehicleDetail,
  type LeaseSummary,
  type ServiceRecord,
  type VehicleHistoryEvent,
  type VehicleImageDto,
  type CreateServiceRecordRequest,
} from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, PrimaryButton, SecondaryButton, Spinner } from '../../../components/ui'
import { VehicleBodyIcon } from '../../../components/vehicle-body-icon'

// ── HeroImage ─────────────────────────────────────────────────────────────────

function HeroImage({ make, model, year, color }: { make: string; model: string; year: number; color?: string }) {
  const [err, setErr] = useState(false)
  const makeSlug = make.toLowerCase().replace(/ /g, '-')
  const modelSlug = model.toLowerCase().replace(/ /g, '-')
  const COLOR_MAP: Record<string, string> = { White: 'white', Silver: 'silver', Black: 'black', Grey: 'grey', Navy: 'navy', Red: 'red', Bronze: 'bronze' }
  const paintId = COLOR_MAP[color ?? ''] ?? 'white'
  const src = `https://cdn.imagin.studio/getimage?customer=img&make=${makeSlug}&modelFamily=${modelSlug}&modelYear=${year}&paintId=${paintId}&angle=23&width=800`
  if (err) return (
    <div className="flex h-full items-center justify-center">
      <span className="text-8xl opacity-20">🚗</span>
    </div>
  )
  return <img src={src} alt={`${make} ${model}`} className="h-full w-full object-cover" onError={() => setErr(true)} />
}

// ── Shared helpers ─────────────────────────────────────────────────────────────

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

const SECTION_HDR = 'text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3 mt-5 first:mt-0'
const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Available: 'green', Reserved: 'blue', OnRent: 'amber',
  InService: 'slate', Damaged: 'red', Sold: 'slate', Disposed: 'slate',
}
const INPUT = 'w-full rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2 text-sm focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-1 focus:ring-brand-500'
const LABEL = 'mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-500'

type Tab = 'details' | 'history' | 'service' | 'images' | 'contracts'

export default function VehicleDetailPage() {
  const { t } = useLocale()
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const f = t.crudVehicles.fields
  const sh = t.crudVehicles.serviceHistory
  const hi = t.crudVehicles.history
  const im = t.crudVehicles.images

  const [tab, setTab] = useState<Tab>('details')
  const [data, setData] = useState<VehicleDetail | null>(null)
  const [activeLease, setActiveLease] = useState<LeaseSummary | null | undefined>(undefined)
  const [vehicleLeases, setVehicleLeases] = useState<LeaseSummary[]>([])
  const [historyEvents, setHistoryEvents] = useState<VehicleHistoryEvent[]>([])
  const [images, setImages] = useState<VehicleImageDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [deleting, setDeleting] = useState(false)
  const [generatingImg, setGeneratingImg] = useState(false)

  // Service record form
  const [showSvcForm, setShowSvcForm] = useState(false)
  const [svcType, setSvcType] = useState(1)
  const [svcCode, setSvcCode] = useState('')
  const [svcDesc, setSvcDesc] = useState('')
  const [svcDate, setSvcDate] = useState(new Date().toISOString().substring(0, 10))
  const [svcOdo, setSvcOdo] = useState('')
  const [svcCost, setSvcCost] = useState('')
  const [svcBranch, setSvcBranch] = useState('')
  const [svcTech, setSvcTech] = useState('')
  const [svcParts, setSvcParts] = useState('')
  const [svcNextOdo, setSvcNextOdo] = useState('')
  const [svcNextDate, setSvcNextDate] = useState('')
  const [svcNotes, setSvcNotes] = useState('')
  const [svcSaving, setSvcSaving] = useState(false)
  const [svcError, setSvcError] = useState<string | null>(null)

  const load = useCallback(() => {
    setLoading(true); setError(null)
    Promise.all([
      bff.getVehicleById(id),
      bff.getVehicleCurrentLease(id).catch(() => null),
      bff.getVehicleHistory(id).catch(() => [] as VehicleHistoryEvent[]),
      bff.getVehicleImages(id).catch(() => [] as VehicleImageDto[]),
      bff.getVehicleLeases(id).catch(() => [] as LeaseSummary[]),
    ])
      .then(([v, lease, hist, imgs, leases]) => {
        setData(v)
        setActiveLease(lease)
        setHistoryEvents(hist)
        setImages(imgs)
        setVehicleLeases(leases)
      })
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [id])

  useEffect(() => { load() }, [load])

  async function handleDelete() {
    if (!window.confirm(t.crudVehicles.actions.confirmDelete)) return
    setDeleting(true)
    try {
      await bff.deleteVehicle(id, crypto.randomUUID())
      router.push('/vehicles')
    } catch (e) {
      setError((e as Error).message)
      setDeleting(false)
    }
  }

  async function handleGenerateImage() {
    setGeneratingImg(true)
    try {
      await bff.generateVehicleImage(id, crypto.randomUUID())
      const imgs = await bff.getVehicleImages(id)
      setImages(imgs)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setGeneratingImg(false)
    }
  }

  async function handleSvcSubmit(e: React.FormEvent) {
    e.preventDefault(); setSvcError(null); setSvcSaving(true)
    try {
      const body: CreateServiceRecordRequest = {
        type: svcType,
        serviceCode: svcCode,
        description: svcDesc,
        servicedAt: svcDate,
        odometerAtService: Number(svcOdo),
        costSar: Number(svcCost),
        branch: svcBranch,
        technician: svcTech,
        partsReplaced: svcParts ? svcParts.split(',').map((p) => p.trim()).filter(Boolean) : undefined,
        nextServiceOdometer: svcNextOdo ? Number(svcNextOdo) : undefined,
        nextServiceDate: svcNextDate || undefined,
        notes: svcNotes || undefined,
      }
      const res = await bff.createServiceRecord(id, body, crypto.randomUUID())
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
      const updated = await bff.getVehicleById(id)
      setData(updated)
      setShowSvcForm(false)
      // reset
      setSvcCode(''); setSvcDesc(''); setSvcOdo(''); setSvcCost(''); setSvcBranch(''); setSvcTech(''); setSvcParts(''); setSvcNextOdo(''); setSvcNextDate(''); setSvcNotes('')
    } catch (err) {
      setSvcError((err as Error).message)
    } finally {
      setSvcSaving(false)
    }
  }

  if (loading) return <Spinner label={t.common.loading} />
  if (error) return <ErrorBox message={error} retryLabel={t.common.retry} onRetry={load} />
  if (!data) return <p className="text-sm text-slate-500">{t.common.notFound}</p>

  const statusTone = STATUS_TONES[data.status] ?? 'slate'
  const statusLabel = (t.crudVehicles.statuses as Record<string, string>)[data.status] ?? data.status

  const tabs: { key: Tab; label: string }[] = [
    { key: 'details', label: t.crudVehicles.tabs.details },
    { key: 'history', label: t.crudVehicles.tabs.history },
    { key: 'service', label: `${t.crudVehicles.tabs.service} (${data.serviceHistory?.length ?? 0})` },
    { key: 'images', label: `${t.crudVehicles.tabs.images} (${images.length})` },
    { key: 'contracts', label: `Contracts (${vehicleLeases.length})` },
  ]

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <PageHeader
        title={`${data.make} ${data.model} (${data.modelYear})`}
        subtitle={`${data.plateLetters} ${data.plateNumber} · VIN: ${data.vin}`}
        action={
          <div className="flex gap-2">
            <SecondaryButton onClick={() => router.push(`/vehicles/${id}/edit`)}>
              {t.crudVehicles.actions.edit}
            </SecondaryButton>
            <SecondaryButton
              onClick={handleDelete}
              className="text-red-600 hover:bg-red-50 hover:border-red-300"
              disabled={deleting}
            >
              {deleting ? '…' : t.crudVehicles.actions.delete}
            </SecondaryButton>
            <SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>
          </div>
        }
      />

      {/* Hero photo */}
      {data.make && data.model && (
        <div className="relative h-56 w-full overflow-hidden rounded-xl bg-slate-100 md:h-72">
          <HeroImage make={data.make} model={data.model} year={data.modelYear} {...(data.color != null ? { color: data.color } : {})} />
        </div>
      )}

      {/* Hero card */}
      <Card className="p-4">
        <div className="flex items-center gap-6">
          <VehicleBodyIcon bodyType={data.bodyType} className="h-16 w-32 text-slate-500 shrink-0" />
          <div className="space-y-2">
            <div className="flex flex-wrap gap-2">
              <Badge tone={statusTone}>{statusLabel}</Badge>
              <Badge tone="slate">{(t.crudVehicles.bodyTypes as Record<string, string>)[data.bodyType] ?? data.bodyType}</Badge>
              <Badge tone="slate">{(t.crudVehicles.fuelTypes as Record<string, string>)[data.fuelType] ?? data.fuelType}</Badge>
              <Badge tone="slate">{(t.crudVehicles.transmissionTypes as Record<string, string>)[data.transmissionType] ?? data.transmissionType}</Badge>
            </div>
            <p className="text-sm text-slate-600">
              {data.color} · {data.seats} seats · {data.currentKm.toLocaleString()} km
            </p>
          </div>
        </div>
      </Card>

      {/* Active Lease Banner */}
      {activeLease && (
        <Card className="border-l-4 border-amber-400 bg-amber-50 p-4">
          <div className="flex items-start justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-amber-700 mb-2">{t.leases.currentLease}</p>
              <div className="grid grid-cols-2 gap-x-8 gap-y-2 text-sm md:grid-cols-4">
                <div>
                  <div className="text-xs text-amber-600">Lease #</div>
                  <div className="font-mono font-semibold text-amber-900">{activeLease.leaseNumber}</div>
                </div>
                <div>
                  <div className="text-xs text-amber-600">{t.leases.detail.fields.customer}</div>
                  <button type="button" className="font-medium text-brand-700 hover:underline" onClick={() => router.push(`/customers/${activeLease.customerId}`)}>
                    {activeLease.customerDisplayName}
                  </button>
                </div>
                <div>
                  <div className="text-xs text-amber-600">{t.leases.detail.fields.driver}</div>
                  <div className="font-medium text-amber-900">{activeLease.primaryDriverName ?? '—'}</div>
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

      {/* Tab bar */}
      <div className="flex border-b border-slate-200">
        {tabs.map(({ key, label }) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key)}
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              tab === key
                ? 'border-b-2 border-brand-500 text-brand-700'
                : 'text-slate-500 hover:text-slate-700'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {/* ── DETAILS TAB ─────────────────────────────────────────────────────── */}
      {tab === 'details' && (
        <Card className="divide-y divide-slate-100 p-6 space-y-4">
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
              <Field label={f.ownerBranch} value={data.ownerBranchId} />
              <Field label={f.currentBranch} value={data.currentBranchId} />
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
              <Field label={t.common.updatedAt} value={data.updatedAtUtc?.substring(0, 10)} />
              <Field label={t.common.id} value={data.id} />
            </div>
          </div>
          {data.notes && (
            <div className="pt-4">
              <p className={SECTION_HDR}>{f.notes}</p>
              <p className="text-sm text-slate-700 whitespace-pre-wrap">{data.notes}</p>
            </div>
          )}
        </Card>
      )}

      {/* ── HISTORY TAB ─────────────────────────────────────────────────────── */}
      {tab === 'history' && (
        <Card className="overflow-hidden">
          <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-4 py-3">
            <h3 className="text-sm font-semibold text-slate-700">{hi.title}</h3>
            <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs font-semibold text-slate-600">
              {historyEvents.length}
            </span>
          </div>
          {historyEvents.length === 0 ? (
            <p className="px-4 py-6 text-sm text-slate-500">{hi.empty}</p>
          ) : (
            <div className="divide-y divide-slate-100">
              {historyEvents.map((ev) => (
                <div key={ev.id} className="flex gap-3 px-4 py-3">
                  <div className="mt-1 h-2 w-2 shrink-0 rounded-full bg-brand-400" />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge tone="blue">
                        {(hi.eventTypes as Record<string, string>)[ev.eventType] ?? ev.eventType}
                      </Badge>
                      <span className="text-xs text-slate-400">{ev.occurredAtUtc.substring(0, 10)}</span>
                      <span className="text-xs text-slate-400">· {ev.performedByName}</span>
                    </div>
                    <p className="mt-1 text-sm text-slate-700">{ev.description}</p>
                    {(ev.previousValue ?? ev.newValue) && (
                      <p className="mt-0.5 text-xs text-slate-500">
                        {ev.previousValue && <span className="line-through mr-2">{ev.previousValue}</span>}
                        {ev.newValue && <span className="text-green-700">{ev.newValue}</span>}
                      </p>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>
      )}

      {/* ── SERVICE TAB ─────────────────────────────────────────────────────── */}
      {tab === 'service' && (
        <div className="space-y-4">
          {/* Add record form */}
          <Card className="overflow-hidden">
            <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-4 py-3">
              <h3 className="text-sm font-semibold text-slate-700">{sh.title}</h3>
              <PrimaryButton
                type="button"
                onClick={() => setShowSvcForm((v) => !v)}
                className="text-xs px-3 py-1.5"
              >
                {showSvcForm ? t.common.cancel : t.crudVehicles.actions.addServiceRecord}
              </PrimaryButton>
            </div>
            {showSvcForm && (
              <form onSubmit={handleSvcSubmit} className="p-4 grid grid-cols-2 gap-3 md:grid-cols-4">
                <div className="col-span-full">
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-2">{sh.form.title}</p>
                </div>
                <div>
                  <label className={LABEL}>{sh.form.type}</label>
                  <select className={INPUT} value={svcType} onChange={(e) => setSvcType(Number(e.target.value))} required>
                    <option value={1}>PMS — {sh.types.PMS}</option>
                    <option value={2}>CMS — {sh.types.CMS}</option>
                  </select>
                </div>
                <div>
                  <label className={LABEL}>{sh.form.serviceCode}</label>
                  <input className={INPUT} value={svcCode} onChange={(e) => setSvcCode(e.target.value)} required placeholder="e.g. PMS-OIL" />
                </div>
                <div className="col-span-2">
                  <label className={LABEL}>{sh.form.description}</label>
                  <input className={INPUT} value={svcDesc} onChange={(e) => setSvcDesc(e.target.value)} required />
                </div>
                <div>
                  <label className={LABEL}>{sh.form.servicedAt}</label>
                  <input type="date" className={INPUT} value={svcDate} onChange={(e) => setSvcDate(e.target.value)} required />
                </div>
                <div>
                  <label className={LABEL}>{sh.form.odometer}</label>
                  <input type="number" className={INPUT} value={svcOdo} onChange={(e) => setSvcOdo(e.target.value)} required min={0} />
                </div>
                <div>
                  <label className={LABEL}>{sh.form.cost}</label>
                  <input type="number" className={INPUT} value={svcCost} onChange={(e) => setSvcCost(e.target.value)} required min={0} step="0.01" />
                </div>
                <div>
                  <label className={LABEL}>{sh.form.branch}</label>
                  <input className={INPUT} value={svcBranch} onChange={(e) => setSvcBranch(e.target.value)} required />
                </div>
                <div>
                  <label className={LABEL}>{sh.form.technician}</label>
                  <input className={INPUT} value={svcTech} onChange={(e) => setSvcTech(e.target.value)} required />
                </div>
                <div className="col-span-2">
                  <label className={LABEL}>{sh.form.parts}</label>
                  <input className={INPUT} value={svcParts} onChange={(e) => setSvcParts(e.target.value)} placeholder="Part A, Part B" />
                </div>
                <div>
                  <label className={LABEL}>{sh.form.nextOdometer}</label>
                  <input type="number" className={INPUT} value={svcNextOdo} onChange={(e) => setSvcNextOdo(e.target.value)} min={0} />
                </div>
                <div>
                  <label className={LABEL}>{sh.form.nextDate}</label>
                  <input type="date" className={INPUT} value={svcNextDate} onChange={(e) => setSvcNextDate(e.target.value)} />
                </div>
                <div className="col-span-full">
                  <label className={LABEL}>{sh.form.notes}</label>
                  <input className={INPUT} value={svcNotes} onChange={(e) => setSvcNotes(e.target.value)} />
                </div>
                {svcError && <p className="col-span-full text-sm text-red-600">{svcError}</p>}
                <div className="col-span-full flex justify-end gap-2">
                  <SecondaryButton type="button" onClick={() => setShowSvcForm(false)}>{t.common.cancel}</SecondaryButton>
                  <PrimaryButton type="submit" disabled={svcSaving}>
                    {svcSaving ? '…' : sh.form.submit}
                  </PrimaryButton>
                </div>
              </form>
            )}
          </Card>

          {/* Service records table */}
          <Card className="overflow-hidden">
            {!data.serviceHistory || data.serviceHistory.length === 0 ? (
              <p className="px-4 py-6 text-sm text-slate-500">{sh.empty}</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead className="border-b border-slate-200 bg-white text-left">
                    <tr>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{sh.columns.type}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{sh.columns.code}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{sh.columns.description}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{sh.columns.date}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500 text-end">{sh.columns.odometer}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500 text-end">{sh.columns.cost}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{sh.columns.branch}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{sh.columns.technician}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{sh.columns.parts}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{sh.columns.nextDue}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(data.serviceHistory as ServiceRecord[]).map((rec) => (
                      <tr key={rec.id} className="border-t border-slate-100 hover:bg-slate-50">
                        <td className="px-3 py-2">
                          <Badge tone={rec.type === 'PMS' ? 'blue' : 'amber'}>
                            {(sh.types as Record<string, string>)[rec.type] ?? rec.type}
                          </Badge>
                        </td>
                        <td className="px-3 py-2 font-mono text-slate-700">{rec.serviceCode}</td>
                        <td className="px-3 py-2 max-w-[160px] truncate text-slate-700" title={rec.description}>{rec.description}</td>
                        <td className="px-3 py-2 text-slate-600">{rec.servicedAt}</td>
                        <td className="px-3 py-2 text-end font-mono text-slate-600">{rec.odometerAtService.toLocaleString()}</td>
                        <td className="px-3 py-2 text-end font-mono text-slate-600">{rec.costSar.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 0 })}</td>
                        <td className="px-3 py-2 text-slate-600">{rec.branch}</td>
                        <td className="px-3 py-2 text-slate-600">{rec.technician}</td>
                        <td className="px-3 py-2 max-w-[180px]">
                          {rec.partsReplaced.length > 0 ? (
                            <span className="text-slate-600" title={rec.partsReplaced.join(', ')}>
                              {rec.partsReplaced.slice(0, 2).join(', ')}{rec.partsReplaced.length > 2 ? ` +${rec.partsReplaced.length - 2}` : ''}
                            </span>
                          ) : <span className="text-slate-400">—</span>}
                        </td>
                        <td className="px-3 py-2 text-slate-600">
                          {rec.nextServiceDate ?? (rec.nextServiceOdometer ? `${rec.nextServiceOdometer.toLocaleString()} km` : '—')}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </div>
      )}

      {/* ── IMAGES TAB ──────────────────────────────────────────────────────── */}
      {tab === 'images' && (
        <Card className="overflow-hidden">
          <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-4 py-3">
            <h3 className="text-sm font-semibold text-slate-700">{im.title}</h3>
            <PrimaryButton
              type="button"
              onClick={handleGenerateImage}
              disabled={generatingImg}
              className="text-xs px-3 py-1.5"
            >
              {generatingImg ? im.generating : t.crudVehicles.actions.generateImage}
            </PrimaryButton>
          </div>
          {images.length === 0 ? (
            <p className="px-4 py-6 text-sm text-slate-500">{im.empty}</p>
          ) : (
            <div className="grid grid-cols-2 gap-3 p-4 md:grid-cols-3">
              {images.map((img) => (
                <div key={img.id} className="relative overflow-hidden rounded-lg border border-slate-200 bg-slate-50">
                  <img
                    src={img.thumbnailUrl ?? img.imageUrl}
                    alt={img.altText ?? `${data.make} ${data.model}`}
                    className="h-40 w-full object-cover"
                  />
                  {img.isAiGenerated && (
                    <span className="absolute top-1 end-1 rounded bg-brand-600 px-1.5 py-0.5 text-xs font-semibold text-white">
                      {im.aiGenerated}
                    </span>
                  )}
                </div>
              ))}
            </div>
          )}
        </Card>
      )}

      {/* ── CONTRACTS TAB ─────────────────────────────────────────────────────── */}
      {tab === 'contracts' && (
        <Card className="overflow-hidden">
          <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-4 py-3">
            <h3 className="text-sm font-semibold text-slate-700">Lease History</h3>
            <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs font-semibold text-slate-600">{vehicleLeases.length}</span>
          </div>
          {vehicleLeases.length === 0 ? (
            <p className="px-4 py-6 text-sm text-slate-500">No lease history for this vehicle.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-xs">
                <thead className="border-b border-slate-200 bg-white text-left">
                  <tr>
                    <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">Lease #</th>
                    <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">Customer</th>
                    <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">Driver</th>
                    <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">Status</th>
                    <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">Type</th>
                    <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">Start</th>
                    <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">End</th>
                    <th className="px-3 py-2 text-right font-medium uppercase tracking-wide text-slate-500">Rent (SAR)</th>
                    <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {vehicleLeases.map((l) => {
                    const LEASE_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
                      Active: 'green', Extended: 'blue', PendingIssuance: 'amber', Suspended: 'amber', Draft: 'slate', Closed: 'slate', Cancelled: 'red',
                    }
                    return (
                      <tr key={l.id} className="border-t border-slate-100 hover:bg-slate-50">
                        <td className="px-3 py-2 font-mono font-semibold text-slate-900">{l.leaseNumber}</td>
                        <td className="px-3 py-2 max-w-[140px] truncate text-slate-700">{l.customerDisplayName}</td>
                        <td className="px-3 py-2 text-slate-600">{l.primaryDriverName ?? '—'}</td>
                        <td className="px-3 py-2"><Badge tone={LEASE_TONES[l.status] ?? 'slate'}>{l.status}</Badge></td>
                        <td className="px-3 py-2 text-slate-600">{l.contractTypeCode}</td>
                        <td className="px-3 py-2 text-slate-600">{l.contractStartUtc.substring(0, 10)}</td>
                        <td className="px-3 py-2 text-slate-600">{l.contractEndUtc.substring(0, 10)}</td>
                        <td className="px-3 py-2 text-right font-mono text-slate-600">{l.rentAmountSar.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
                        <td className="px-3 py-2">
                          <SecondaryButton onClick={() => router.push(`/leases/${l.id}`)} className="px-2 py-1 text-xs">View</SecondaryButton>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}
    </div>
  )
}
