'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import {
  bff,
  type LeaseDetail, type DamageRecord, type TrafficViolation,
  type Invoice, type CreateDamageRecordRequest, type CreateTrafficViolationRequest,
  type VehicleSummary, type SwitchVehicleRequest,
} from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, PrimaryButton, SecondaryButton, Spinner } from '../../../components/ui'

const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Active: 'green', Extended: 'blue', PendingIssuance: 'amber',
  Suspended: 'amber', Draft: 'slate', Closed: 'slate', Cancelled: 'red',
}
const INV_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Paid: 'green', PartiallyPaid: 'amber', Issued: 'blue', Draft: 'slate', Overdue: 'red', Cancelled: 'slate',
}
const DMG_TONES: Record<string, 'red' | 'amber' | 'slate'> = {
  TotalLoss: 'red', Major: 'red', Moderate: 'amber', Minor: 'slate',
}

type Tab = 'overview' | 'damages' | 'violations' | 'invoices' | 'history'

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

function safeDate(s: string | null | undefined) {
  if (!s) return '—'
  return new Date(s).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

function fmt(n: number | null | undefined) {
  if (n == null) return '—'
  return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

// ─── Damages Tab ─────────────────────────────────────────────────────────────

function DamagesTab({ leaseId, vehicleId }: { leaseId: string; vehicleId: string }) {
  const [damages, setDamages] = useState<DamageRecord[]>([])
  const [loading, setLoading] = useState(true)
  const [showForm, setShowForm] = useState(false)
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState<Partial<CreateDamageRecordRequest>>({
    leaseId, vehicleId, fault: 'Customer', severity: 'Minor', location: 'Front', type: 'ScratchDent', chargeToCustomer: true,
    occurredAt: new Date().toISOString().substring(0, 10),
  })

  useEffect(() => {
    bff.getDamageRecords(leaseId).then(setDamages).finally(() => setLoading(false))
  }, [leaseId])

  async function handleSave() {
    if (!form.type || !form.location || !form.severity || !form.fault || !form.description || !form.occurredAt) {
      alert('Fill in all required fields'); return
    }
    setSaving(true)
    try {
      const rec = await bff.createDamageRecord(form as CreateDamageRecordRequest, crypto.randomUUID())
      setDamages((d) => [rec, ...d])
      setShowForm(false)
    } catch (e) { alert((e as Error).message) }
    finally { setSaving(false) }
  }

  function downloadCsv() {
    const rows = [['ID', 'Type', 'Location', 'Severity', 'Fault', 'Occurred', 'Est. Cost', 'Repair Status', 'Charge Customer', 'Description']]
    damages.forEach((d) => rows.push([d.id, d.type, d.location, d.severity, d.fault, d.occurredAt, String(d.estimatedCostSar ?? ''), d.repairStatus, String(d.chargeToCustomer), d.description]))
    const csv = rows.map((r) => r.join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `damages-${leaseId}.csv`; a.click()
  }

  if (loading) return <Spinner label="Loading damages…" />

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-500">{damages.length} damage record{damages.length !== 1 ? 's' : ''}</p>
        <div className="flex gap-2">
          <SecondaryButton onClick={downloadCsv} className="px-3 py-1.5 text-xs">⬇ Export CSV</SecondaryButton>
          <PrimaryButton onClick={() => setShowForm((f) => !f)} className="px-3 py-1.5 text-xs">
            {showForm ? 'Cancel' : '+ Record Damage'}
          </PrimaryButton>
        </div>
      </div>

      {showForm && (
        <Card className="space-y-4 p-4">
          <h4 className="font-semibold text-slate-800">New Damage Record</h4>
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3">
            {([['type', 'Type', ['Accident', 'ScratchDent', 'Glass', 'TyreWheel', 'Mechanical', 'Flood', 'TheftVandalism', 'Fire', 'Other']],
              ['location', 'Location', ['Front', 'Rear', 'LeftSide', 'RightSide', 'Roof', 'Underbody', 'Interior', 'Multiple']],
              ['severity', 'Severity', ['Minor', 'Moderate', 'Major', 'TotalLoss']],
              ['fault', 'Fault', ['Customer', 'ThirdParty', 'Unknown', 'ActOfGod']],
            ] as [string, string, string[]][]).map(([key, label, opts]) => (
              <div key={key}>
                <label className="mb-1 block text-xs font-medium text-slate-600">{label}</label>
                <select className="w-full rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={(form as Record<string, string>)[key] ?? ''} onChange={(e) => setForm((f) => ({ ...f, [key]: e.target.value }))}>
                  {opts.map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              </div>
            ))}
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Occurred Date</label>
              <input type="date" className="w-full rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={form.occurredAt ?? ''} onChange={(e) => setForm((f) => ({ ...f, occurredAt: e.target.value }))} />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Est. Cost (SAR)</label>
              <input type="number" className="w-full rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={form.estimatedCostSar ?? ''} onChange={(e) => { const v = Number(e.target.value); setForm((f) => ({ ...f, ...(v ? { estimatedCostSar: v } : {}) })) }} placeholder="0.00" />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Insurance Claim #</label>
              <input type="text" className="w-full rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={form.insuranceClaimNumber ?? ''} onChange={(e) => { const v = e.target.value; setForm((f) => ({ ...f, ...(v ? { insuranceClaimNumber: v } : {}) })) }} placeholder="CLM-..." />
            </div>
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">Description *</label>
            <textarea rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={form.description ?? ''} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} placeholder="Describe the damage…" />
          </div>
          <div className="flex items-center gap-2">
            <input type="checkbox" id="chargeCust" checked={form.chargeToCustomer ?? false} onChange={(e) => setForm((f) => ({ ...f, chargeToCustomer: e.target.checked }))} />
            <label htmlFor="chargeCust" className="text-sm text-slate-700">Charge to customer</label>
            {form.chargeToCustomer && (
              <input type="number" className="ml-3 w-32 rounded-lg border border-slate-300 px-2 py-1 text-sm" value={form.chargedAmountSar ?? ''} onChange={(e) => { const v = Number(e.target.value); setForm((f) => ({ ...f, ...(v ? { chargedAmountSar: v } : {}) })) }} placeholder="Amount SAR" />
            )}
          </div>
          <div className="flex gap-2">
            <PrimaryButton onClick={handleSave} disabled={saving} className="px-4 py-2 text-sm">{saving ? 'Saving…' : 'Save Damage Record'}</PrimaryButton>
            <SecondaryButton onClick={() => setShowForm(false)} className="px-4 py-2 text-sm">Cancel</SecondaryButton>
          </div>
        </Card>
      )}

      {damages.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-300 py-10 text-center text-sm text-slate-400">No damage records for this contract.</div>
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="border-b border-slate-200 bg-slate-50/80">
              <tr>
                {['Type', 'Location', 'Severity', 'Fault', 'Occurred', 'Est. Cost', 'Repair Status', 'Charge?'].map((h) => (
                  <th key={h} className="px-3 py-2 text-left text-xs font-semibold text-slate-600">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {damages.map((d) => (
                <tr key={d.id} className="border-t border-slate-100 hover:bg-slate-50/60">
                  <td className="px-3 py-2 font-medium">{d.type}</td>
                  <td className="px-3 py-2 text-slate-600">{d.location}</td>
                  <td className="px-3 py-2"><Badge tone={DMG_TONES[d.severity] ?? 'slate'}>{d.severity}</Badge></td>
                  <td className="px-3 py-2 text-slate-600">{d.fault}</td>
                  <td className="px-3 py-2 text-slate-600">{d.occurredAt}</td>
                  <td className="px-3 py-2 font-mono text-xs">{fmt(d.estimatedCostSar)}</td>
                  <td className="px-3 py-2"><Badge tone={d.repairStatus === 'Completed' ? 'green' : d.repairStatus === 'InProgress' ? 'blue' : d.repairStatus === 'Waived' ? 'slate' : 'amber'}>{d.repairStatus}</Badge></td>
                  <td className="px-3 py-2">{d.chargeToCustomer ? <Badge tone="red">Yes — {fmt(d.chargedAmountSar)}</Badge> : <Badge tone="slate">No</Badge>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

// ─── Violations Tab ───────────────────────────────────────────────────────────

function ViolationsTab({ leaseId, vehicleId, driverId }: { leaseId: string; vehicleId: string; driverId: string | null }) {
  const [violations, setViolations] = useState<TrafficViolation[]>([])
  const [loading, setLoading] = useState(true)
  const [showForm, setShowForm] = useState(false)
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState<Partial<CreateTrafficViolationRequest>>({
    leaseId, vehicleId, ...(driverId ? { driverId } : {}),
    type: 'Speeding', authority: 'Muroor', responsibleParty: 'Customer',
    occurredAt: new Date().toISOString().substring(0, 10), fineAmountSar: 300,
  })

  useEffect(() => {
    bff.getTrafficViolations(leaseId).then(setViolations).finally(() => setLoading(false))
  }, [leaseId])

  async function handleSave() {
    if (!form.violationNumber || !form.type || !form.fineAmountSar) { alert('Fill required fields'); return }
    setSaving(true)
    try {
      const rec = await bff.createTrafficViolation(form as CreateTrafficViolationRequest, crypto.randomUUID())
      setViolations((v) => [rec, ...v])
      setShowForm(false)
    } catch (e) { alert((e as Error).message) }
    finally { setSaving(false) }
  }

  function downloadCsv() {
    const rows = [['Violation #', 'Type', 'Authority', 'Occurred', 'Location', 'Fine (SAR)', 'Responsible', 'Status']]
    violations.forEach((v) => rows.push([v.violationNumber, v.type, v.authority, v.occurredAt, v.location ?? '', String(v.fineAmountSar), v.responsibleParty, v.paymentStatus]))
    const csv = rows.map((r) => r.join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `violations-${leaseId}.csv`; a.click()
  }

  const PAY_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
    PaidByCustomer: 'green', PaidByCompany: 'blue', Waived: 'slate', Contested: 'amber', Unpaid: 'red',
  }

  if (loading) return <Spinner label="Loading violations…" />

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-500">{violations.length} violation{violations.length !== 1 ? 's' : ''}</p>
        <div className="flex gap-2">
          <SecondaryButton onClick={downloadCsv} className="px-3 py-1.5 text-xs">⬇ Export CSV</SecondaryButton>
          <PrimaryButton onClick={() => setShowForm((f) => !f)} className="px-3 py-1.5 text-xs">{showForm ? 'Cancel' : '+ Add Violation'}</PrimaryButton>
        </div>
      </div>

      {showForm && (
        <Card className="space-y-4 p-4">
          <h4 className="font-semibold text-slate-800">New Traffic Violation</h4>
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3">
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Violation Number *</label>
              <input type="text" className="w-full rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={form.violationNumber ?? ''} onChange={(e) => setForm((f) => ({ ...f, violationNumber: e.target.value }))} placeholder="MRR-20240001234" />
            </div>
            {([['type', 'Type', ['Speeding', 'Parking', 'RedLight', 'WrongWay', 'MobilePhone', 'ExpiredRegistration', 'Seatbelt', 'RecklessDriving', 'Other']],
              ['authority', 'Authority', ['Muroor', 'Municipality', 'MOT', 'Other']],
              ['responsibleParty', 'Responsible', ['Customer', 'Company']],
            ] as [string, string, string[]][]).map(([key, label, opts]) => (
              <div key={key}>
                <label className="mb-1 block text-xs font-medium text-slate-600">{label}</label>
                <select className="w-full rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={(form as Record<string, string>)[key] ?? ''} onChange={(e) => setForm((f) => ({ ...f, [key]: e.target.value }))}>
                  {opts.map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              </div>
            ))}
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Occurred Date</label>
              <input type="date" className="w-full rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={form.occurredAt ?? ''} onChange={(e) => setForm((f) => ({ ...f, occurredAt: e.target.value }))} />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Fine Amount (SAR) *</label>
              <input type="number" className="w-full rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={form.fineAmountSar ?? ''} onChange={(e) => setForm((f) => ({ ...f, fineAmountSar: Number(e.target.value) }))} />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Absher Ref #</label>
              <input type="text" className="w-full rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={form.absherRefNumber ?? ''} onChange={(e) => { const v = e.target.value; setForm((f) => ({ ...f, ...(v ? { absherRefNumber: v } : {}) })) }} placeholder="ABS-..." />
            </div>
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">Location</label>
            <input type="text" className="w-full rounded-lg border border-slate-300 px-3 py-1.5 text-sm" value={form.location ?? ''} onChange={(e) => { const v = e.target.value; setForm((f) => ({ ...f, ...(v ? { location: v } : {}) })) }} placeholder="King Fahd Road, Riyadh" />
          </div>
          <div className="flex gap-2">
            <PrimaryButton onClick={handleSave} disabled={saving} className="px-4 py-2 text-sm">{saving ? 'Saving…' : 'Save Violation'}</PrimaryButton>
            <SecondaryButton onClick={() => setShowForm(false)} className="px-4 py-2 text-sm">Cancel</SecondaryButton>
          </div>
        </Card>
      )}

      {violations.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-300 py-10 text-center text-sm text-slate-400">No violations for this contract.</div>
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="border-b border-slate-200 bg-slate-50/80">
              <tr>
                {['Violation #', 'Type', 'Authority', 'Occurred', 'Fine', 'Responsible', 'Status'].map((h) => (
                  <th key={h} className="px-3 py-2 text-left text-xs font-semibold text-slate-600">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {violations.map((v) => (
                <tr key={v.id} className="border-t border-slate-100 hover:bg-slate-50/60">
                  <td className="px-3 py-2 font-mono text-xs font-semibold">{v.violationNumber}</td>
                  <td className="px-3 py-2">{v.type}</td>
                  <td className="px-3 py-2 text-slate-600">{v.authority}</td>
                  <td className="px-3 py-2 text-slate-600">{v.occurredAt}</td>
                  <td className="px-3 py-2 font-mono text-xs font-semibold text-red-700">{fmt(v.fineAmountSar)}</td>
                  <td className="px-3 py-2"><Badge tone={v.responsibleParty === 'Customer' ? 'amber' : 'blue'}>{v.responsibleParty}</Badge></td>
                  <td className="px-3 py-2"><Badge tone={PAY_TONES[v.paymentStatus] ?? 'slate'}>{v.paymentStatus}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

// ─── Invoices Tab ─────────────────────────────────────────────────────────────

function InvoicesTab({ leaseId }: { leaseId: string; lease: LeaseDetail }) {
  const router = useRouter()
  const [invoices, setInvoices] = useState<Invoice[]>([])
  const [loading, setLoading] = useState(true)
  const [generating, setGenerating] = useState(false)
  const [period, setPeriod] = useState({ start: new Date().toISOString().substring(0, 7) + '-01', end: new Date().toISOString().substring(0, 7) + '-30' })

  useEffect(() => {
    bff.getInvoices(1, 50, leaseId).then((res) => setInvoices(res.items)).finally(() => setLoading(false))
  }, [leaseId])

  async function handleGenerate() {
    setGenerating(true)
    try {
      const inv = await bff.generateInvoice({ leaseId, billingPeriodStart: period.start, billingPeriodEnd: period.end }, crypto.randomUUID())
      setInvoices((prev) => [inv, ...prev])
    } catch (e) { alert((e as Error).message) }
    finally { setGenerating(false) }
  }

  function downloadCsv() {
    const rows = [['Invoice #', 'Period', 'Issued', 'Due', 'Status', 'Rent (SAR)', 'VAT', 'Total', 'Paid', 'Balance']]
    invoices.forEach((inv) => rows.push([inv.invoiceNumber, `${inv.billingPeriodStart}–${inv.billingPeriodEnd}`, inv.issuedDate, inv.dueDate, inv.status, String(inv.subTotalSar), String(inv.vatAmountSar), String(inv.totalSar), String(inv.paidAmountSar), String(inv.balanceSar)]))
    const csv = rows.map((r) => r.join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `invoices-${leaseId}.csv`; a.click()
  }

  if (loading) return <Spinner label="Loading invoices…" />

  const totalOutstanding = invoices.filter((i) => i.balanceSar > 0).reduce((s, i) => s + i.balanceSar, 0)

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-3">
          <p className="text-sm text-slate-500">{invoices.length} invoice{invoices.length !== 1 ? 's' : ''}</p>
          {totalOutstanding > 0 && <Badge tone="red">Outstanding: {fmt(totalOutstanding)}</Badge>}
        </div>
        <div className="flex items-center gap-2">
          <SecondaryButton onClick={downloadCsv} className="px-3 py-1.5 text-xs">⬇ Export CSV</SecondaryButton>
          <input type="month" className="rounded-lg border border-slate-300 px-2 py-1.5 text-sm" value={period.start.substring(0, 7)} onChange={(e) => { const m = e.target.value; setPeriod({ start: `${m}-01`, end: `${m}-30` }) }} />
          <PrimaryButton onClick={handleGenerate} disabled={generating} className="px-3 py-1.5 text-xs">
            {generating ? 'Generating…' : '+ Generate Invoice'}
          </PrimaryButton>
        </div>
      </div>

      {invoices.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-300 py-10 text-center text-sm text-slate-400">No invoices yet.</div>
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="border-b border-slate-200 bg-slate-50/80">
              <tr>
                {['Invoice #', 'Billing Period', 'Issued', 'Due', 'Status', 'Total', 'Paid', 'Balance', ''].map((h) => (
                  <th key={h} className="px-3 py-2 text-left text-xs font-semibold text-slate-600">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {invoices.map((inv) => (
                <tr key={inv.id} className="border-t border-slate-100 hover:bg-slate-50/60">
                  <td className="px-3 py-2 font-mono text-xs font-semibold">{inv.invoiceNumber}</td>
                  <td className="px-3 py-2 text-slate-600 text-xs">{inv.billingPeriodStart} – {inv.billingPeriodEnd}</td>
                  <td className="px-3 py-2 text-slate-600">{inv.issuedDate}</td>
                  <td className="px-3 py-2 text-slate-600">{inv.dueDate}</td>
                  <td className="px-3 py-2"><Badge tone={INV_TONES[inv.status] ?? 'slate'}>{inv.status}</Badge></td>
                  <td className="px-3 py-2 font-mono text-xs">{fmt(inv.totalSar)}</td>
                  <td className="px-3 py-2 font-mono text-xs text-green-700">{fmt(inv.paidAmountSar)}</td>
                  <td className="px-3 py-2 font-mono text-xs font-semibold text-red-700">{inv.balanceSar > 0 ? fmt(inv.balanceSar) : '—'}</td>
                  <td className="px-3 py-2">
                    <SecondaryButton onClick={() => router.push(`/invoices/${inv.id}`)} className="px-2 py-1 text-xs">View</SecondaryButton>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

// ─── Main Contract Detail Page ────────────────────────────────────────────────

export default function LeaseDetailPage() {
  const router = useRouter()
  const params = useParams()
  const id = params?.id as string

  const [lease, setLease] = useState<LeaseDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('overview')
  const [operating, setOperating] = useState(false)

  // Vehicle switch state
  const [showSwitch, setShowSwitch] = useState(false)
  const [availableVehicles, setAvailableVehicles] = useState<VehicleSummary[]>([])
  const [switchForm, setSwitchForm] = useState({ newVehicleId: '', reason: 'ServiceVehicle', odometer: 0, notes: '' })
  const [switchBusy, setSwitchBusy] = useState(false)
  const [switchError, setSwitchError] = useState<string | null>(null)

  useEffect(() => {
    if (showSwitch) {
      bff.getVehicles(1, 50, undefined, 1).then((r) => setAvailableVehicles(r.items)).catch(() => {})
    }
  }, [showSwitch])

  async function handleSwitchVehicle() {
    if (!switchForm.newVehicleId || !switchForm.notes.trim()) { setSwitchError('Select a vehicle and provide notes for audit.'); return }
    setSwitchBusy(true); setSwitchError(null)
    try {
      const res = await bff.switchLeaseVehicle(id, switchForm as SwitchVehicleRequest, crypto.randomUUID())
      if (!res.success) throw new Error(res.errorMessage ?? 'Switch failed')
      const updated = await bff.getLeaseById(id)
      setLease(updated)
      setShowSwitch(false)
      setSwitchForm({ newVehicleId: '', reason: 'ServiceVehicle', odometer: 0, notes: '' })
    } catch (e) { setSwitchError((e as Error).message) }
    finally { setSwitchBusy(false) }
  }

  useEffect(() => {
    if (!id) return
    bff.getLeaseById(id).then(setLease).catch((e: Error) => setError(e.message)).finally(() => setLoading(false))
  }, [id])

  if (loading) return <Spinner label="Loading contract…" />
  if (error) return <ErrorBox message={error} onRetry={() => { setLoading(true); setError(null); bff.getLeaseById(id).then(setLease).catch((e: Error) => setError(e.message)).finally(() => setLoading(false)) }} retryLabel="Retry" />
  if (!lease) return null

  const isActive = lease.status === 'Active' || lease.status === 'Extended'
  const canClose = isActive
  const canSuspend = isActive && lease.status !== 'Suspended'

  async function doOperation(op: string, confirmMsg: string) {
    if (!confirm(confirmMsg)) return
    setOperating(true)
    try {
      // mock: just reload to simulate
      await new Promise((r) => setTimeout(r, 500))
      alert(`${op} applied (mock mode)`)
    } finally { setOperating(false) }
  }

  function downloadCsv() {
    const rows = [['Field', 'Value'], ['Contract #', lease!.leaseNumber], ['Customer', lease!.customerDisplayName], ['Vehicle', lease!.vehicleMakeModel], ['Plate', lease!.vehiclePlate], ['Driver', lease!.primaryDriverName ?? '—'], ['Status', lease!.status], ['Start', lease!.contractStartUtc], ['End', lease!.contractEndUtc], ['Rent/mo', String(lease!.rentAmountSar)], ['Tajeer #', String(lease!.tajeerContractNumber ?? '')]]
    const csv = rows.map((r) => r.join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `contract-${lease!.leaseNumber}.csv`; a.click()
  }

  const TABS: { key: Tab; label: string }[] = [
    { key: 'overview', label: 'Overview' },
    { key: 'damages', label: 'Damages' },
    { key: 'violations', label: 'Violations' },
    { key: 'invoices', label: 'Invoices' },
    { key: 'history', label: 'History' },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title={`Contract ${lease.leaseNumber}`}
        subtitle={`${lease.customerDisplayName} · ${lease.vehicleMakeModel}`}
        action={
          <div className="flex flex-wrap gap-2">
            <SecondaryButton onClick={downloadCsv} className="px-3 py-1.5 text-xs">⬇ Export</SecondaryButton>
            {canSuspend && <SecondaryButton onClick={() => doOperation('Suspend', 'Suspend this contract?')} disabled={operating} className="px-3 py-1.5 text-xs text-amber-700 border-amber-300">Suspend</SecondaryButton>}
            {canClose && <SecondaryButton onClick={() => doOperation('Close', 'Close this contract?')} disabled={operating} className="px-3 py-1.5 text-xs text-red-700 border-red-300">Close Contract</SecondaryButton>}
            {isActive && <SecondaryButton onClick={() => doOperation('Extend', 'Extend this contract by 3 months?')} disabled={operating} className="px-3 py-1.5 text-xs">Extend</SecondaryButton>}
            <SecondaryButton onClick={() => router.back()} className="px-3 py-1.5 text-xs">← Back</SecondaryButton>
          </div>
        }
      />

      {/* Status bar */}
      <div className="flex flex-wrap items-center gap-3 rounded-lg border border-slate-200 bg-white px-4 py-2.5">
        <Badge tone={STATUS_TONES[lease.status] ?? 'slate'}>{lease.status}</Badge>
        <span className="text-sm text-slate-600">{lease.contractTypeCode} contract</span>
        <span className="text-sm text-slate-400">·</span>
        <span className="text-sm text-slate-600">{safeDate(lease.contractStartUtc)} → {safeDate(lease.contractEndUtc)}</span>
        {lease.tajeerContractNumber && <span className="text-sm text-slate-500">Tajeer #{lease.tajeerContractNumber}</span>}
      </div>

      {/* Tab navigation */}
      <div className="flex gap-1 border-b border-slate-200">
        {TABS.map(({ key, label }) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key)}
            className={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px
              ${tab === key ? 'border-brand-600 text-brand-700' : 'border-transparent text-slate-500 hover:text-slate-700'}`}
          >
            {label}
          </button>
        ))}
      </div>

      {/* Overview */}
      {tab === 'overview' && (
        <div className="grid gap-4 md:grid-cols-2">
          <Card className="p-4">
            <SectionHdr>Contract Details</SectionHdr>
            <div className="grid grid-cols-2 gap-x-6 gap-y-3">
              <Field label="Contract Number" value={lease.leaseNumber} mono />
              <Field label="Status" value={lease.status} />
              <Field label="Contract Type" value={lease.contractTypeCode} />
              <Field label="Branch" value={lease.workingBranchName} />
              <Field label="Start Date" value={safeDate(lease.contractStartUtc)} />
              <Field label="End Date" value={safeDate(lease.contractEndUtc)} />
              <Field label="Monthly Rent" value={fmt(lease.rentAmountSar)} />
              <Field label="VAT (15%)" value={fmt(lease.vatAmountSar)} />
              <Field label="Total Amount" value={fmt(lease.totalAmountSar)} />
              <Field label="Paid Amount" value={fmt(lease.paidAmountSar)} />
              <Field label="Remaining" value={fmt(lease.remainingAmountSar)} />
              <Field label="Allowed KM/day" value={lease.allowedKmPerDay ? `${lease.allowedKmPerDay} km` : '—'} />
              <Field label="Payment Method" value={lease.paymentMethodCode} />
            </div>
          </Card>

          <div className="space-y-4">
            <Card className="p-4">
              <SectionHdr>Customer</SectionHdr>
              <div className="grid grid-cols-2 gap-x-6 gap-y-3">
                <Field label="Customer" value={lease.customerDisplayName} />
                <Field label="Driver" value={lease.primaryDriverName} />
              </div>
            </Card>

            <Card className="p-4">
              <div className="flex items-center justify-between">
                <SectionHdr>Vehicle</SectionHdr>
                {(lease.status === 'Active' || lease.status === 'Extended') && (
                  <SecondaryButton onClick={() => setShowSwitch((v) => !v)} className="px-2 py-1 text-xs">
                    {showSwitch ? 'Cancel' : 'Switch Vehicle'}
                  </SecondaryButton>
                )}
              </div>
              <div className="grid grid-cols-2 gap-x-6 gap-y-3">
                <Field label="Plate" value={lease.vehiclePlate} mono />
                <Field label="Make / Model" value={lease.vehicleMakeModel} />
              </div>
              {showSwitch && (
                <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-4">
                  <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-amber-700">Switch to Temporary Vehicle</p>
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="mb-1 block text-xs font-semibold text-slate-600">Replacement Vehicle</label>
                      <select className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={switchForm.newVehicleId} onChange={(e) => setSwitchForm((f) => ({ ...f, newVehicleId: e.target.value }))}>
                        <option value="">— Select vehicle —</option>
                        {availableVehicles.map((v) => <option key={v.id} value={v.id}>{v.plateNumber} — {v.make} {v.model} ({v.modelYear})</option>)}
                      </select>
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-semibold text-slate-600">Reason</label>
                      <select className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={switchForm.reason} onChange={(e) => setSwitchForm((f) => ({ ...f, reason: e.target.value }))}>
                        <option value="ServiceVehicle">Service Vehicle (repair/maintenance)</option>
                        <option value="Replacement">Replacement (accident/damage)</option>
                        <option value="PreLease">Pre-Lease (upgrade/downgrade)</option>
                      </select>
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-semibold text-slate-600">Current Odometer (km)</label>
                      <input type="number" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={switchForm.odometer} onChange={(e) => setSwitchForm((f) => ({ ...f, odometer: Number(e.target.value) }))} />
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-semibold text-slate-600">Notes / Comment</label>
                      <input className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={switchForm.notes} onChange={(e) => setSwitchForm((f) => ({ ...f, notes: e.target.value }))} placeholder="Reason details for audit..." />
                    </div>
                  </div>
                  {switchError && <p className="mt-2 text-sm text-red-600">{switchError}</p>}
                  <div className="mt-3 flex gap-2">
                    <PrimaryButton onClick={handleSwitchVehicle} disabled={switchBusy || !switchForm.newVehicleId} className="px-4 py-2 text-sm">
                      {switchBusy ? 'Switching...' : 'Confirm Switch'}
                    </PrimaryButton>
                    <SecondaryButton onClick={() => setShowSwitch(false)} className="px-3 py-2 text-sm">Cancel</SecondaryButton>
                  </div>
                </div>
              )}
            </Card>

            <Card className="p-4">
              <SectionHdr>Integration Status</SectionHdr>
              <div className="grid grid-cols-2 gap-x-6 gap-y-3">
                <Field label="Tajeer Status" value={lease.tajeerStatus} />
                <Field label="Tajeer Contract #" value={lease.tajeerContractNumber} mono />
                <Field label="ZATCA Status" value={lease.zatcaSubmissionStatus} />
                <Field label="ZATCA Invoice #" value={lease.zatcaInvoiceNumber} mono />
              </div>
            </Card>
          </div>

          {/* Inspections */}
          {lease.inspections.length > 0 && (
            <Card className="p-4 md:col-span-2">
              <SectionHdr>Inspections</SectionHdr>
              <div className="overflow-hidden rounded-lg border border-slate-200">
                <table className="w-full text-sm">
                  <thead className="bg-slate-50/80 border-b border-slate-200">
                    <tr>
                      {['Type', 'Date', 'Odometer', 'Condition', 'Veh. Type', 'Sub Type', 'Inspector', 'Notes'].map((h) => (
                        <th key={h} className="px-3 py-2 text-left text-xs font-semibold text-slate-600">{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {lease.inspections.map((ins) => (
                      <tr key={ins.id} className="border-t border-slate-100">
                        <td className="px-3 py-2 font-medium">{ins.type}</td>
                        <td className="px-3 py-2 text-slate-600">{safeDate(ins.inspectedAtUtc)}</td>
                        <td className="px-3 py-2 font-mono text-xs">{ins.odometer.toLocaleString()} km</td>
                        <td className="px-3 py-2"><Badge tone={ins.conditionCode === 'Good' ? 'green' : ins.conditionCode === 'Damaged' ? 'red' : 'amber'}>{ins.conditionCode}</Badge></td>
                        <td className="px-3 py-2"><Badge tone={ins.vehicleAssignmentType === 'Temporary' ? 'amber' : 'green'}>{ins.vehicleAssignmentType}</Badge></td>
                        <td className="px-3 py-2 text-xs text-slate-600">{ins.vehicleSubType ?? '—'}</td>
                        <td className="px-3 py-2 text-slate-600">{ins.inspector}</td>
                        <td className="px-3 py-2 text-slate-500 text-xs">{ins.notes ?? '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>
          )}
        </div>
      )}

      {tab === 'damages' && <DamagesTab leaseId={id} vehicleId={lease.vehicleId} />}
      {tab === 'violations' && <ViolationsTab leaseId={id} vehicleId={lease.vehicleId} driverId={lease.primaryDriverId} />}
      {tab === 'invoices' && <InvoicesTab leaseId={id} lease={lease} />}

      {tab === 'history' && (
        <Card className="p-4">
          <SectionHdr>Contract Timeline</SectionHdr>
          <ol className="relative ms-4 space-y-4 border-s border-slate-200">
            {[
              { date: safeDate(lease.createdAtUtc), event: 'Contract created', status: 'Draft' },
              ...(lease.issuedAtUtc ? [{ date: safeDate(lease.issuedAtUtc), event: 'Contract issued (Active)', status: 'Active' }] : []),
              ...(lease.suspendedAtUtc ? [{ date: safeDate(lease.suspendedAtUtc), event: 'Contract suspended', status: 'Suspended' }] : []),
              ...(lease.resumedAtUtc ? [{ date: safeDate(lease.resumedAtUtc), event: 'Contract resumed', status: 'Active' }] : []),
              ...(lease.closedAtUtc ? [{ date: safeDate(lease.closedAtUtc), event: 'Contract closed', status: 'Closed' }] : []),
              ...(lease.cancelledAtUtc ? [{ date: safeDate(lease.cancelledAtUtc), event: 'Contract cancelled', status: 'Cancelled' }] : []),
            ].map((ev, i) => (
              <li key={i} className="ms-4">
                <div className="absolute -start-1.5 mt-1 h-3 w-3 rounded-full border border-white bg-brand-600" />
                <p className="text-xs text-slate-400">{ev.date}</p>
                <p className="text-sm font-medium text-slate-800">{ev.event}</p>
                <Badge tone={STATUS_TONES[ev.status] ?? 'slate'}>{ev.status}</Badge>
              </li>
            ))}
          </ol>
        </Card>
      )}
    </div>
  )
}
