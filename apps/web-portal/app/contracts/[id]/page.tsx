'use client'

import Link from 'next/link'
import { useParams, useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import { bff, type ContractDetail, type DriverSummary, type VehicleSummary } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, PrimaryButton, SecondaryButton, Spinner } from '../../../components/ui'

const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Draft: 'slate', Active: 'green', Suspended: 'amber', Closed: 'slate', Cancelled: 'red',
}
const LA_STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Draft: 'slate', PendingIssuance: 'amber', Active: 'green', Extended: 'blue',
  Suspended: 'amber', Closed: 'slate', Cancelled: 'red',
}
const CONTRACT_TYPES: Record<string, string> = {
  '1': 'Long Term Lease', '2': 'Short Term Rental', '3': 'Daily Rental',
  LongTermLease: 'Long Term Lease', ShortTermRental: 'Short Term Rental', Daily: 'Daily Rental',
  OperatingLease: 'Operating Lease', FinanceLease: 'Finance Lease',
}

function safeDate(s: string | null | undefined) {
  if (!s) return '—'
  return new Date(s).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}
function fmtMoney(n: number | string | null | undefined) {
  if (n == null || n === '') return '—'
  const num = typeof n === 'string' ? parseFloat(n) : n
  if (isNaN(num)) return '—'
  return num.toLocaleString('en-SA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

type Tab = 'overview' | 'quotation' | 'leases'

/* ---------------------------------------------------------------------------
 * Lease Agreements Tab — allocate vehicles + create LA + view grid
 * -------------------------------------------------------------------------*/

function LeaseAgreementsTab({ contract, onReload }: { contract: ContractDetail; onReload: () => void }) {
  const router = useRouter()
  const [showAllocate, setShowAllocate] = useState(false)
  const [showCreateLA, setShowCreateLA] = useState(false)
  const [availableVehicles, setAvailableVehicles] = useState<VehicleSummary[]>([])
  const [allocatedVehicles, setAllocatedVehicles] = useState<{ id: string; plateNumber: string; make: string; model: string; modelYear: number; status: string }[]>([])
  const [drivers, setDrivers] = useState<DriverSummary[]>([])
  const [allocVehicleId, setAllocVehicleId] = useState('')
  const [laForm, setLaForm] = useState({ vehicleId: '', driverId: '', checkoutDate: new Date().toISOString().substring(0, 10) })
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)
  const [showNewDriver, setShowNewDriver] = useState(false)
  const [newDriver, setNewDriver] = useState({ personNameEn: '', driverLicenseNumber: '', licenseExpiryDate: '', idTypeCode: 1, personIdNumber: '', licenseClass: 2 })
  const [driverBusy, setDriverBusy] = useState(false)

  useEffect(() => {
    bff.getContractAllocatedVehicles(contract.id).then(setAllocatedVehicles).catch(() => {})
  }, [contract.id])

  useEffect(() => {
    if (showAllocate) bff.getVehicles(1, 100, undefined, 1).then(r => setAvailableVehicles(r.items)).catch(() => {})
  }, [showAllocate])

  function loadCustomerDrivers() {
    bff.getCustomerDrivers(contract.customerId).then(setDrivers).catch(() => setDrivers([]))
  }

  useEffect(() => {
    if (showCreateLA) loadCustomerDrivers()
  }, [showCreateLA]) // eslint-disable-line react-hooks/exhaustive-deps

  async function handleAllocate() {
    if (!allocVehicleId) return
    setBusy(true); setMsg(null)
    try {
      await bff.allocateVehicleToContract(contract.id, allocVehicleId, crypto.randomUUID())
      setMsg({ ok: true, text: 'Vehicle allocated to this contract.' })
      setAllocVehicleId('')
      setShowAllocate(false)
      const updated = await bff.getContractAllocatedVehicles(contract.id)
      setAllocatedVehicles(updated)
    } catch (e) { setMsg({ ok: false, text: (e as Error).message }) }
    finally { setBusy(false) }
  }

  async function handleCreateLA() {
    if (!laForm.vehicleId || !laForm.driverId) { setMsg({ ok: false, text: 'Select vehicle and driver.' }); return }
    setBusy(true); setMsg(null)
    try {
      const res = await bff.createLeaseAgreement(contract.id, laForm, crypto.randomUUID())
      setMsg({ ok: true, text: `Lease Agreement ${res.leaseNumber} created.` })
      setShowCreateLA(false)
      setLaForm({ vehicleId: '', driverId: '', checkoutDate: new Date().toISOString().substring(0, 10) })
      onReload()
    } catch (e) { setMsg({ ok: false, text: (e as Error).message }) }
    finally { setBusy(false) }
  }

  const availableSlots = contract.totalVehicles - (contract.checkedOutVehicles ?? 0)

  return (
    <div className="space-y-4">
      {/* Status bar */}
      <div className="flex flex-wrap items-center gap-4 rounded-lg border border-slate-200 bg-white px-4 py-3">
        <div className="text-xs"><span className="text-slate-500">Total Vehicles:</span> <span className="font-semibold">{contract.totalVehicles}</span></div>
        <div className="text-xs"><span className="text-slate-500">Allocated:</span> <span className="font-semibold">{allocatedVehicles.length}</span></div>
        <div className="text-xs"><span className="text-slate-500">Checked Out:</span> <span className="font-semibold">{contract.checkedOutVehicles ?? 0}</span></div>
        <div className="text-xs"><span className="text-slate-500">Available Slots:</span> <span className={`font-semibold ${availableSlots > 0 ? 'text-green-700' : 'text-red-600'}`}>{availableSlots}</span></div>
        <div className="flex-1" />
        <SecondaryButton onClick={() => setShowAllocate(!showAllocate)} className="px-3 py-1.5 text-xs">
          {showAllocate ? 'Cancel' : '+ Allocate Vehicle'}
        </SecondaryButton>
        {allocatedVehicles.length > 0 && availableSlots > 0 && (
          <PrimaryButton onClick={() => setShowCreateLA(!showCreateLA)} className="px-3 py-1.5 text-xs">
            {showCreateLA ? 'Cancel' : '+ Create Lease Agreement'}
          </PrimaryButton>
        )}
      </div>

      {/* Feedback */}
      {msg && <div className={`rounded-md border p-3 text-sm ${msg.ok ? 'border-green-200 bg-green-50 text-green-800' : 'border-red-200 bg-red-50 text-red-800'}`}>{msg.text}</div>}

      {/* Allocate Vehicle Form */}
      {showAllocate && (
        <Card className="border-blue-200 bg-blue-50/30 p-4">
          <h4 className="mb-2 text-xs font-semibold text-blue-800">Allocate Vehicle to Contract</h4>
          <p className="mb-3 text-[10px] text-blue-600">Select an available vehicle to assign to this contract and customer. Only allocated vehicles can be checked out.</p>
          <div className="flex items-end gap-3">
            <div className="flex-1">
              <label className="mb-1 block text-xs font-medium text-slate-600">Vehicle</label>
              <select className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" value={allocVehicleId} onChange={e => setAllocVehicleId(e.target.value)}>
                <option value="">— Select available vehicle —</option>
                {availableVehicles.filter(v => !allocatedVehicles.some(av => av.id === v.id)).map(v => (
                  <option key={v.id} value={v.id}>{v.plateNumber} — {v.make} {v.model} ({v.modelYear})</option>
                ))}
              </select>
            </div>
            <PrimaryButton onClick={handleAllocate} disabled={busy || !allocVehicleId} className="px-4 py-2 text-sm">
              {busy ? 'Allocating...' : 'Allocate'}
            </PrimaryButton>
          </div>
        </Card>
      )}

      {/* Allocated Vehicles */}
      {allocatedVehicles.length > 0 && (
        <Card className="overflow-hidden">
          <div className="border-b border-slate-200 bg-slate-50 px-4 py-2">
            <h4 className="text-xs font-semibold text-slate-700">Allocated Vehicles ({allocatedVehicles.length})</h4>
          </div>
          <table className="w-full text-xs">
            <thead className="bg-slate-100 text-slate-600"><tr>
              <th className="px-3 py-1.5 text-start font-medium">Plate</th>
              <th className="px-3 py-1.5 text-start font-medium">Make / Model</th>
              <th className="px-3 py-1.5 text-start font-medium">Year</th>
              <th className="px-3 py-1.5 text-start font-medium">Status</th>
            </tr></thead>
            <tbody>{allocatedVehicles.map(v => (
              <tr key={v.id} className="border-t border-slate-100">
                <td className="px-3 py-1.5 font-mono font-semibold">{v.plateNumber}</td>
                <td className="px-3 py-1.5">{v.make} {v.model}</td>
                <td className="px-3 py-1.5">{v.modelYear}</td>
                <td className="px-3 py-1.5"><Badge tone={v.status === 'OnRent' ? 'amber' : 'green'}>{v.status}</Badge></td>
              </tr>
            ))}</tbody>
          </table>
        </Card>
      )}

      {/* Create LA Form */}
      {showCreateLA && (
        <Card className="border-green-200 bg-green-50/30 p-4">
          <h4 className="mb-2 text-xs font-semibold text-green-800">Create Lease Agreement (Vehicle Checkout)</h4>
          <p className="mb-3 text-[10px] text-green-600">
            Customer: <span className="font-semibold">{contract.customerDisplayName}</span> &middot; Contract: <span className="font-semibold">{contract.contractNumber}</span>
          </p>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Allocated Vehicle *</label>
              <select className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" value={laForm.vehicleId} onChange={e => setLaForm(f => ({ ...f, vehicleId: e.target.value }))}>
                <option value="">— Select vehicle —</option>
                {allocatedVehicles.filter(v => v.status !== 'OnRent').map(v => (
                  <option key={v.id} value={v.id}>{v.plateNumber} — {v.make} {v.model} ({v.modelYear})</option>
                ))}
              </select>
              {allocatedVehicles.filter(v => v.status !== 'OnRent').length === 0 && (
                <p className="mt-1 text-[10px] text-red-600">No available vehicles. Allocate a vehicle first.</p>
              )}
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Customer Driver *</label>
              <select className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" value={laForm.driverId} onChange={e => setLaForm(f => ({ ...f, driverId: e.target.value }))}>
                <option value="">— Select driver for {contract.customerDisplayName} —</option>
                {drivers.map(d => (
                  <option key={d.id} value={d.id}>{d.personNameEn} — Lic: {d.driverLicenseNumber}</option>
                ))}
              </select>
              {drivers.length === 0 && !showNewDriver && (
                <div className="mt-1 rounded border border-amber-200 bg-amber-50 px-2 py-1.5">
                  <p className="text-[10px] text-amber-800">No drivers found for this customer.</p>
                  <button type="button" onClick={() => setShowNewDriver(true)} className="mt-1 text-[10px] font-semibold text-brand-700 hover:underline">
                    + Create New Driver
                  </button>
                </div>
              )}
              {drivers.length > 0 && (
                <button type="button" onClick={() => setShowNewDriver(!showNewDriver)} className="mt-1 text-[10px] text-brand-600 hover:underline">
                  {showNewDriver ? 'Cancel' : '+ Add new driver'}
                </button>
              )}
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Checkout Date</label>
              <input type="date" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" value={laForm.checkoutDate} onChange={e => setLaForm(f => ({ ...f, checkoutDate: e.target.value }))} />
            </div>
          </div>

          {/* Inline New Driver Form */}
          {showNewDriver && (
            <div className="mt-3 rounded-lg border border-blue-200 bg-blue-50/50 p-3">
              <div className="flex items-center justify-between mb-2">
                <h5 className="text-xs font-semibold text-blue-800">New Driver for {contract.customerDisplayName}</h5>
                <span className="rounded bg-slate-200 px-2 py-0.5 text-[10px] font-mono text-slate-600">{contract.customerDisplayName}</span>
              </div>
              <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                <div>
                  <label className="mb-0.5 block text-[10px] font-medium text-slate-600">Full Name (EN) *</label>
                  <input className="w-full rounded border border-slate-300 px-2 py-1.5 text-xs" value={newDriver.personNameEn} onChange={e => setNewDriver(d => ({ ...d, personNameEn: e.target.value }))} placeholder="Driver full name" />
                </div>
                <div>
                  <label className="mb-0.5 block text-[10px] font-medium text-slate-600">ID Type *</label>
                  <select className="w-full rounded border border-slate-300 px-2 py-1.5 text-xs" value={newDriver.idTypeCode} onChange={e => setNewDriver(d => ({ ...d, idTypeCode: Number(e.target.value) }))}>
                    <option value={1}>National ID</option>
                    <option value={2}>Iqama</option>
                    <option value={3}>Passport</option>
                    <option value={4}>GCC ID</option>
                  </select>
                </div>
                <div>
                  <label className="mb-0.5 block text-[10px] font-medium text-slate-600">ID Number *</label>
                  <input className="w-full rounded border border-slate-300 px-2 py-1.5 text-xs" value={newDriver.personIdNumber} onChange={e => setNewDriver(d => ({ ...d, personIdNumber: e.target.value }))} placeholder="1234567890" />
                </div>
                <div>
                  <label className="mb-0.5 block text-[10px] font-medium text-slate-600">License Number *</label>
                  <input className="w-full rounded border border-slate-300 px-2 py-1.5 text-xs" value={newDriver.driverLicenseNumber} onChange={e => setNewDriver(d => ({ ...d, driverLicenseNumber: e.target.value }))} placeholder="DL1234567890" />
                </div>
                <div>
                  <label className="mb-0.5 block text-[10px] font-medium text-slate-600">License Class *</label>
                  <select className="w-full rounded border border-slate-300 px-2 py-1.5 text-xs" value={newDriver.licenseClass} onChange={e => setNewDriver(d => ({ ...d, licenseClass: Number(e.target.value) }))}>
                    <option value={1}>Class 1 — Motorcycle</option>
                    <option value={2}>Class 2 — Light Vehicle</option>
                    <option value={3}>Class 3 — Heavy Vehicle</option>
                    <option value={4}>Class 4 — Bus</option>
                  </select>
                </div>
                <div>
                  <label className="mb-0.5 block text-[10px] font-medium text-slate-600">License Expiry *</label>
                  <input type="date" className="w-full rounded border border-slate-300 px-2 py-1.5 text-xs" value={newDriver.licenseExpiryDate} onChange={e => setNewDriver(d => ({ ...d, licenseExpiryDate: e.target.value }))} />
                </div>
              </div>
              <PrimaryButton
                disabled={driverBusy || !newDriver.personNameEn || !newDriver.driverLicenseNumber || !newDriver.personIdNumber || !newDriver.licenseExpiryDate}
                onClick={async () => {
                  setDriverBusy(true); setMsg(null)
                  try {
                    await bff.createDriver({
                      personNameEn: newDriver.personNameEn,
                      idTypeCode: newDriver.idTypeCode,
                      personIdNumber: newDriver.personIdNumber,
                      nationalityCode: 'SA',
                      driverLicenseNumber: newDriver.driverLicenseNumber,
                      licenseClass: newDriver.licenseClass,
                      licenseExpiryDate: newDriver.licenseExpiryDate,
                      customerId: contract.customerId,
                    }, crypto.randomUUID())
                    setMsg({ ok: true, text: `Driver ${newDriver.personNameEn} created.` })
                    setShowNewDriver(false)
                    setNewDriver({ personNameEn: '', driverLicenseNumber: '', licenseExpiryDate: '', idTypeCode: 1, personIdNumber: '', licenseClass: 2 })
                    loadCustomerDrivers()
                  } catch (e) { setMsg({ ok: false, text: (e as Error).message }) }
                  finally { setDriverBusy(false) }
                }}
                className="mt-2 px-3 py-1.5 text-xs"
              >
                {driverBusy ? 'Creating...' : 'Create Driver'}
              </PrimaryButton>
            </div>
          )}

          <div className="mt-2 rounded-md bg-slate-100 px-3 py-2 text-xs text-slate-600">
            Monthly Rent: <span className="font-mono font-bold text-brand-700">SAR {fmtMoney(contract.monthlyRentSar)}</span> (from contract — non-editable)
          </div>
          <div className="mt-3">
            <PrimaryButton onClick={handleCreateLA} disabled={busy || !laForm.vehicleId || !laForm.driverId} className="px-4 py-2 text-sm">
              {busy ? 'Creating...' : 'Create & Checkout'}
            </PrimaryButton>
          </div>
        </Card>
      )}

      {/* LA Grid */}
      <Card className="overflow-hidden">
        <div className="border-b border-slate-200 bg-slate-50 px-4 py-3">
          <h3 className="text-sm font-semibold text-slate-800">Lease Agreements ({contract.leaseAgreements.length})</h3>
        </div>
        {contract.leaseAgreements.length === 0 ? (
          <div className="px-4 py-8 text-center text-sm text-slate-400">
            No lease agreements yet. Allocate vehicles first, then create lease agreements.
          </div>
        ) : (
          <table className="w-full text-xs">
            <thead className="bg-slate-100 text-slate-600"><tr>
              <th className="px-3 py-2 text-start font-medium">LA #</th>
              <th className="px-3 py-2 text-start font-medium">Vehicle</th>
              <th className="px-3 py-2 text-start font-medium">Plate</th>
              <th className="px-3 py-2 text-start font-medium">Driver</th>
              <th className="px-3 py-2 text-start font-medium">Status</th>
              <th className="px-3 py-2 text-start font-medium">Checkout</th>
              <th className="px-3 py-2 text-start font-medium">Return</th>
              <th className="px-3 py-2 text-end font-medium">Rent / mo</th>
              <th className="px-3 py-2 text-start font-medium">Actions</th>
            </tr></thead>
            <tbody>{contract.leaseAgreements.map(la => (
              <tr key={la.id} className="border-t border-slate-100 hover:bg-brand-50/40">
                <td className="px-3 py-2 font-mono font-semibold text-brand-700">{la.leaseNumber}</td>
                <td className="px-3 py-2">{la.vehicleMakeModel}</td>
                <td className="px-3 py-2 font-mono text-slate-600">{la.vehiclePlate}</td>
                <td className="px-3 py-2 text-slate-600">{la.primaryDriverName || '—'}</td>
                <td className="px-3 py-2"><Badge tone={LA_STATUS_TONES[la.status] ?? 'slate'}>{la.status}</Badge></td>
                <td className="px-3 py-2 text-slate-600">{safeDate(la.contractStartUtc)}</td>
                <td className="px-3 py-2 text-slate-600">{safeDate(la.contractEndUtc)}</td>
                <td className="px-3 py-2 text-end font-mono">SAR {fmtMoney(la.rentAmountSar)}</td>
                <td className="px-3 py-2">
                  <button onClick={() => router.push(`/leases/${la.id}`)} className="rounded border border-brand-300 px-2 py-0.5 text-[10px] font-medium text-brand-700 hover:bg-brand-50">
                    View / Edit
                  </button>
                </td>
              </tr>
            ))}</tbody>
          </table>
        )}
      </Card>
    </div>
  )
}

export default function ContractDetailPage() {
  const params = useParams()
  const id = params?.id as string
  const router = useRouter()
  const [contract, setContract] = useState<ContractDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('overview')

  async function reload() {
    setLoading(true); setError(null)
    try { setContract(await bff.getContractById(id)) }
    catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }
  useEffect(() => { reload() }, [id]) // eslint-disable-line react-hooks/exhaustive-deps

  if (loading) return <Spinner label="Loading contract..." />
  if (error) return <ErrorBox message={error} onRetry={reload} retryLabel="Retry" />
  if (!contract) return null

  const status = contract.status
  const lineTotalSum = contract.lines.reduce((s, l) => s + l.lineTotalSar, 0)
  const hasQuote = !!contract.quoteNumber
  const tabs: { key: Tab; label: string }[] = [
    { key: 'overview', label: 'Contract Details' },
    ...(hasQuote ? [{ key: 'quotation' as Tab, label: 'Quotation & T&C' }] : []),
    { key: 'leases', label: `Lease Agreements (${contract.leaseAgreements.length})` },
  ]

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <PageHeader
        title={`Contract ${contract.contractNumber}`}
        subtitle={contract.customerDisplayName}
        action={<Badge tone={STATUS_TONES[status] ?? 'slate'}>{status}</Badge>}
      />

      {/* Key metrics bar */}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-6">
        {[
          { label: 'Vehicles', value: `${contract.checkedOutVehicles ?? 0} / ${contract.totalVehicles}`, accent: false },
          { label: 'Base Amount', value: `SAR ${fmtMoney(contract.baseAmountSar)}`, accent: false },
          { label: 'Discount', value: `${contract.discountPercent ?? 0}%`, accent: false },
          { label: 'VAT (15%)', value: `SAR ${fmtMoney(contract.vatAmountSar)}`, accent: false },
          { label: 'Total Amount', value: `SAR ${fmtMoney(contract.totalAmountSar)}`, accent: true },
          { label: 'Monthly Rent', value: `SAR ${fmtMoney(contract.monthlyRentSar)}`, accent: true },
        ].map((m) => (
          <Card key={m.label} className={`p-3 text-center ${m.accent ? 'border-brand-200 bg-brand-50' : ''}`}>
            <div className="text-[10px] font-semibold uppercase tracking-wide text-slate-500">{m.label}</div>
            <div className={`mt-1 text-sm font-bold ${m.accent ? 'text-brand-700' : 'text-slate-900'}`}>{m.value}</div>
          </Card>
        ))}
      </div>

      {/* Tab navigation */}
      <div className="flex gap-1 border-b border-slate-200">
        {tabs.map(({ key, label }) => (
          <button key={key} type="button" onClick={() => setTab(key)}
            className={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px ${tab === key ? 'border-brand-600 text-brand-700' : 'border-transparent text-slate-500 hover:text-slate-700'}`}>
            {label}
          </button>
        ))}
      </div>

      {/* ─── Overview Tab ─── */}
      {tab === 'overview' && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <div className="space-y-4 lg:col-span-2">
            {/* Contract summary */}
            <Card className="p-5">
              <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Contract Information</h3>
              <dl className="grid grid-cols-2 gap-3 text-xs sm:grid-cols-3">
                {([
                  ['Contract #', contract.contractNumber],
                  ['Customer', contract.customerDisplayName],
                  ['Status', status],
                  ['Type', CONTRACT_TYPES[contract.contractType ?? contract.contractTypeCode] ?? contract.contractTypeCode],
                  ['Start Date', safeDate(contract.startDate)],
                  ['End Date', safeDate(contract.endDate)],
                  ['Duration', `${contract.durationMonths} months`],
                  ['Payment Terms', `Net ${contract.paymentTermsDays} days`],
                  ['Source Quote', contract.quoteNumber ?? '—'],
                ] as [string, string][]).map(([label, value]) => (
                  <div key={label} className="rounded-md bg-slate-50 p-2.5">
                    <dt className="text-slate-500">{label}</dt>
                    <dd className="mt-0.5 font-medium text-slate-900">
                      {label === 'Source Quote' && contract.quotationId && contract.quoteNumber ? (
                        <Link href={`/quotations/${contract.quotationId}`} className="text-brand-700 hover:underline">{value}</Link>
                      ) : value}
                    </dd>
                  </div>
                ))}
              </dl>
            </Card>

            {/* Vehicle Lines (pricing by make/model) */}
            <Card className="overflow-hidden">
              <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-4 py-3">
                <h3 className="text-sm font-semibold text-slate-800">Vehicle Pricing Breakdown</h3>
                <span className="rounded-full bg-slate-200 px-2 py-0.5 text-[10px] font-bold text-slate-600">{contract.totalVehicles} vehicles</span>
              </div>
              <table className="w-full text-xs">
                <thead className="bg-slate-100 text-slate-600">
                  <tr>
                    <th className="px-3 py-2 text-start font-medium">#</th>
                    <th className="px-3 py-2 text-start font-medium">Make</th>
                    <th className="px-3 py-2 text-start font-medium">Model</th>
                    <th className="px-3 py-2 text-end font-medium">Year</th>
                    <th className="px-3 py-2 text-end font-medium">Qty</th>
                    <th className="px-3 py-2 text-end font-medium">Unit Price / mo</th>
                    <th className="px-3 py-2 text-end font-medium">Line Total / mo</th>
                  </tr>
                </thead>
                <tbody>
                  {contract.lines.length === 0 && (
                    <tr><td colSpan={7} className="px-3 py-4 text-center text-slate-400">No vehicle lines.</td></tr>
                  )}
                  {contract.lines.map((line, idx) => (
                    <tr key={line.id} className="border-t border-slate-100">
                      <td className="px-3 py-2 text-slate-500">{idx + 1}</td>
                      <td className="px-3 py-2 font-medium">{line.make}</td>
                      <td className="px-3 py-2">{line.model}</td>
                      <td className="px-3 py-2 text-end">{line.year}</td>
                      <td className="px-3 py-2 text-end font-semibold">{line.quantity}</td>
                      <td className="px-3 py-2 text-end font-mono">{fmtMoney(line.unitPriceSar)}</td>
                      <td className="px-3 py-2 text-end font-mono font-bold">{fmtMoney(line.lineTotalSar)}</td>
                    </tr>
                  ))}
                </tbody>
                {contract.lines.length > 0 && (
                  <tfoot>
                    <tr className="border-t-2 border-slate-200 bg-slate-50">
                      <td colSpan={6} className="px-3 py-2 text-end text-xs font-semibold text-slate-700">Monthly Total</td>
                      <td className="px-3 py-2 text-end font-mono text-xs font-bold text-slate-900">SAR {fmtMoney(lineTotalSum)}</td>
                    </tr>
                  </tfoot>
                )}
              </table>
            </Card>
          </div>

          {/* Right column */}
          <div className="space-y-4">
            {/* Pricing Breakdown */}
            <Card className="p-4">
              <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Pricing Summary</h3>
              <div className="space-y-2 text-xs">
                <div className="flex justify-between"><span className="text-slate-500">Base Amount (lines)</span><span className="font-mono font-medium">SAR {fmtMoney(contract.baseAmountSar)}</span></div>
                <div className="flex justify-between"><span className="text-slate-500">Discount ({contract.discountPercent ?? 0}%)</span><span className="font-mono text-red-600">- SAR {fmtMoney(contract.discountAmountSar)}</span></div>
                <div className="flex justify-between"><span className="text-slate-500">Net Amount</span><span className="font-mono">SAR {fmtMoney(contract.netAmountSar)}</span></div>
                <div className="flex justify-between"><span className="text-slate-500">VAT ({contract.vatPercent ?? 15}%)</span><span className="font-mono">SAR {fmtMoney(contract.vatAmountSar)}</span></div>
                <div className="flex justify-between border-t border-slate-200 pt-2"><span className="font-semibold text-slate-700">Total Contract Value ({contract.durationMonths} mo)</span><span className="font-mono font-bold text-brand-700">SAR {fmtMoney(contract.totalAmountSar)}</span></div>
                <div className="flex justify-between bg-brand-50 rounded-md px-2 py-1.5 -mx-1"><span className="font-semibold text-brand-700">Monthly Rent</span><span className="font-mono font-bold text-brand-700">SAR {fmtMoney(contract.monthlyRentSar)}</span></div>
              </div>
            </Card>

            <Card className="p-4">
              <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Actions</h3>
              <div className="space-y-2">
                <button onClick={() => setTab('leases')} className="w-full rounded-md bg-brand-700 px-3 py-2 text-sm font-medium text-white hover:bg-brand-800">
                  View Lease Agreements
                </button>
                {hasQuote && (
                  <Link href={`/quotations/${contract.quotationId}`} className="block w-full rounded-md border border-brand-300 px-3 py-2 text-center text-sm font-medium text-brand-700 hover:bg-brand-50">
                    View Source Quotation
                  </Link>
                )}
              </div>
            </Card>

            {contract.notes && (
              <Card className="p-4">
                <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">Notes</h3>
                <p className="text-xs text-slate-600 whitespace-pre-wrap">{contract.notes}</p>
              </Card>
            )}

            <Card className="p-4">
              <button onClick={() => router.push('/contracts')} className="text-xs text-brand-700 hover:underline">
                ← Back to Contracts
              </button>
            </Card>
          </div>
        </div>
      )}

      {/* ─── Quotation & T&C Tab ─── */}
      {tab === 'quotation' && hasQuote && (
        <div className="space-y-4">
          {/* Quote summary */}
          <Card className="p-5">
            <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Linked Quotation — {contract.quoteNumber}</h3>
            <dl className="grid grid-cols-2 gap-3 text-xs sm:grid-cols-4">
              {([
                ['Quote #', contract.quoteNumber ?? '—'],
                ['Quote Date', safeDate(contract.quoteDate)],
                ['Valid Until', safeDate(contract.quoteValidUntil)],
                ['Status', contract.quoteStatus ?? '—'],
                ['Type', CONTRACT_TYPES[contract.contractType ?? ''] ?? contract.contractType ?? '—'],
                ['Duration', contract.estimatedDurationMonths ? `${contract.estimatedDurationMonths} months` : '—'],
                ['Discount', contract.quoteDiscountPercent ? `${contract.quoteDiscountPercent}%` : '0%'],
                ['Subtotal', contract.quoteSubTotalSar ? `SAR ${fmtMoney(contract.quoteSubTotalSar)}` : '—'],
                ['VAT (15%)', contract.quoteVatSar ? `SAR ${fmtMoney(contract.quoteVatSar)}` : '—'],
                ['Total', contract.quoteTotalSar ? `SAR ${fmtMoney(contract.quoteTotalSar)}` : '—'],
              ] as [string, string][]).map(([label, value]) => (
                <div key={label} className="rounded-md bg-slate-50 p-2.5">
                  <dt className="text-slate-500">{label}</dt>
                  <dd className="mt-0.5 font-medium text-slate-900">{value}</dd>
                </div>
              ))}
            </dl>
          </Card>

          {/* Extras / Quote line items (includes insurance, maintenance, GPS, etc.) */}
          {contract.quoteLines && contract.quoteLines.length > 0 && (
            <Card className="overflow-hidden">
              <div className="border-b border-slate-200 bg-slate-50 px-4 py-3">
                <h3 className="text-sm font-semibold text-slate-800">Quotation Line Items & Extras</h3>
                <p className="text-[10px] text-slate-500">Includes vehicle rental, insurance, maintenance, and other agreed services.</p>
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
                    <th className="px-3 py-2 text-end font-medium">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {contract.quoteLines.map((ql) => (
                    <tr key={ql.lineNumber} className="border-t border-slate-100">
                      <td className="px-3 py-2 text-slate-500">{ql.lineNumber}</td>
                      <td className="px-3 py-2"><Badge tone={ql.itemType === 'VehicleRental' ? 'blue' : 'slate'}>{ql.itemType}</Badge></td>
                      <td className="px-3 py-2">
                        <div>{ql.description}</div>
                        {ql.vehicleSpecRef && <div className="text-[10px] text-slate-400">{ql.vehicleSpecRef}</div>}
                      </td>
                      <td className="px-3 py-2 text-end">{ql.quantity}</td>
                      <td className="px-3 py-2 text-end font-mono">{fmtMoney(ql.unitPriceSar)}</td>
                      <td className="px-3 py-2 text-end">{ql.discountPercent}%</td>
                      <td className="px-3 py-2 text-end font-mono font-bold">{fmtMoney(ql.lineTotalSar)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Card>
          )}

          {/* Terms & Conditions */}
          <Card className="p-5">
            <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Terms & Conditions</h3>
            {contract.termsAndConditions ? (
              <div className="prose prose-sm max-w-none text-xs text-slate-700 whitespace-pre-wrap">
                {contract.termsAndConditions}
              </div>
            ) : (
              <div className="rounded-md border border-slate-200 bg-slate-50 px-4 py-6 text-center">
                <p className="text-sm text-slate-400">No terms & conditions attached to this quotation.</p>
                <p className="mt-1 text-[10px] text-slate-400">Terms can be added when creating or editing the quotation.</p>
              </div>
            )}
          </Card>
        </div>
      )}

      {/* ─── Lease Agreements Tab ─── */}
      {tab === 'leases' && (
        <LeaseAgreementsTab contract={contract} onReload={reload} />
      )}
    </div>
  )
}
