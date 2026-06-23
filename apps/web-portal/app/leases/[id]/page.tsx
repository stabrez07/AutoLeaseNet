'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import {
  bff,
  type LeaseDetail, type DamageRecord, type TrafficViolation,
  type Invoice, type CreateDamageRecordRequest, type CreateTrafficViolationRequest,
  type VehicleSummary, type SwitchVehicleRequest, type AdvancePayment,
} from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, PrimaryButton, SecondaryButton, Spinner } from '../../../components/ui'

const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Active: 'green', Extended: 'blue', PendingIssuance: 'amber',
  Suspended: 'amber', Draft: 'slate', Closed: 'slate', Cancelled: 'red',
}
const INV_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Draft: 'slate', Submitted: 'amber', Cleared: 'green', Finalized: 'blue', SubmissionFailed: 'red', ClearanceFailed: 'red', Voided: 'red',
}
const DMG_TONES: Record<string, 'red' | 'amber' | 'slate'> = {
  TotalLoss: 'red', Major: 'red', Moderate: 'amber', Minor: 'slate',
}

type Tab = 'overview' | 'damages' | 'violations' | 'invoices' | 'payments' | 'history'

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
          <SecondaryButton onClick={downloadCsv} className="px-3 py-1.5 text-xs">Export CSV</SecondaryButton>
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
          <SecondaryButton onClick={downloadCsv} className="px-3 py-1.5 text-xs">Export CSV</SecondaryButton>
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

// ─── Payments Tab ────────────────────────────────────────────────────────────

function PaymentsTab({ leaseId }: { leaseId: string }) {
  const router = useRouter()
  const [payments, setPayments] = useState<AdvancePayment[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    bff.getLeasePayments(leaseId).then(setPayments).catch(() => {}).finally(() => setLoading(false))
  }, [leaseId])

  if (loading) return <Spinner label="Loading payments..." />

  return (
    <Card className="p-4">
      <SectionHdr>Payments ({payments.length})</SectionHdr>
      {payments.length === 0 && <p className="text-sm text-slate-500">No payments recorded for this contract.</p>}
      {payments.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-slate-200 bg-slate-50 text-left text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                <th className="px-3 py-2">Receipt #</th>
                <th className="px-3 py-2">Customer</th>
                <th className="px-3 py-2">Method</th>
                <th className="px-3 py-2">Date</th>
                <th className="px-3 py-2 text-right">Amount</th>
                <th className="px-3 py-2 text-right">Remaining</th>
                <th className="px-3 py-2 text-center">Allocations</th>
                <th className="px-3 py-2"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {payments.map((p) => (
                <tr key={p.id} className="hover:bg-slate-50">
                  <td className="px-3 py-2 font-mono font-semibold">P-{p.displayId}</td>
                  <td className="px-3 py-2">{p.customerDisplayName}</td>
                  <td className="px-3 py-2"><Badge tone={p.paymentMethod === 'Cash' ? 'green' : p.paymentMethod === 'CreditCard' ? 'blue' : 'slate'}>{p.paymentMethod}</Badge></td>
                  <td className="px-3 py-2">{safeDate(p.receivedDate)}</td>
                  <td className="px-3 py-2 text-right font-mono tabular-nums">{fmt(p.amount)}</td>
                  <td className="px-3 py-2 text-right font-mono tabular-nums">{p.remainingBalance > 0 ? <span className="text-amber-700">{fmt(p.remainingBalance)}</span> : '—'}</td>
                  <td className="px-3 py-2 text-center">{p.allocations.length > 0 ? <span className="font-semibold text-green-700">{p.allocations.length}</span> : '—'}</td>
                  <td className="px-3 py-2"><SecondaryButton onClick={() => router.push(`/payments/${p.id}`)} className="px-2 py-1 text-xs">Receipt</SecondaryButton></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  )
}

// ─── Invoices Tab ─────────────────────────────────────────────────────────────

function InvoicesTab({ leaseId, lease }: { leaseId: string; lease: LeaseDetail }) {
  const router = useRouter()
  const [invoices, setInvoices] = useState<Invoice[]>([])
  const [loading, setLoading] = useState(true)
  const [generating, setGenerating] = useState(false)
  const [showGenerate, setShowGenerate] = useState(false)
  const [selectedMonths, setSelectedMonths] = useState<string[]>([])

  useEffect(() => {
    bff.getInvoices(1, 50, leaseId).then((res) => setInvoices(res.items)).finally(() => setLoading(false))
  }, [leaseId])

  // Build available months from contract start to end
  const availableMonths: { value: string; label: string; start: string; end: string }[] = []
  if (lease.contractStartUtc && lease.contractEndUtc) {
    const s = new Date(lease.contractStartUtc)
    const e = new Date(lease.contractEndUtc)
    const cur = new Date(s.getFullYear(), s.getMonth(), 1)
    while (cur <= e && availableMonths.length < 60) {
      const y = cur.getFullYear()
      const m = cur.getMonth()
      const mStr = `${y}-${String(m + 1).padStart(2, '0')}`
      const lastDay = new Date(y, m + 1, 0).getDate()
      const alreadyGenerated = invoices.some((inv) => inv.billingPeriodStart?.startsWith(mStr))
      if (!alreadyGenerated) {
        availableMonths.push({
          value: mStr,
          label: cur.toLocaleDateString('en-GB', { month: 'short', year: 'numeric' }),
          start: `${mStr}-01`,
          end: `${mStr}-${String(lastDay).padStart(2, '0')}`,
        })
      }
      cur.setMonth(cur.getMonth() + 1)
    }
  }

  async function handleGenerateSelected() {
    if (selectedMonths.length === 0) return
    setGenerating(true)
    try {
      for (const mStr of selectedMonths) {
        const mo = availableMonths.find((m) => m.value === mStr)
        if (!mo) continue
        const inv = await bff.generateInvoice({ leaseId, billingPeriodStart: mo.start, billingPeriodEnd: mo.end }, crypto.randomUUID())
        setInvoices((prev) => [inv, ...prev])
      }
      setSelectedMonths([])
      setShowGenerate(false)
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

  function toggleMonth(m: string) {
    setSelectedMonths((prev) => prev.includes(m) ? prev.filter((x) => x !== m) : [...prev, m])
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
          <SecondaryButton onClick={downloadCsv} className="px-3 py-1.5 text-xs">Export CSV</SecondaryButton>
          <PrimaryButton onClick={() => setShowGenerate(!showGenerate)} className="px-3 py-1.5 text-xs">
            {showGenerate ? 'Cancel' : '+ Generate Invoice'}
          </PrimaryButton>
        </div>
      </div>

      {/* Month selection for invoice generation */}
      {showGenerate && (
        <Card className="border-brand-200 bg-brand-50/30 p-4">
          <p className="mb-2 text-xs font-semibold text-brand-800">Select months to generate invoices</p>
          <p className="mb-3 text-[10px] text-brand-600">
            Contract period: {safeDate(lease.contractStartUtc)} – {safeDate(lease.contractEndUtc)}. Already-generated months are excluded.
          </p>
          {availableMonths.length === 0 ? (
            <p className="text-xs text-slate-500">All months in the contract period already have invoices generated.</p>
          ) : (
            <>
              <div className="flex flex-wrap gap-2">
                {availableMonths.map((m) => (
                  <button key={m.value} type="button" onClick={() => toggleMonth(m.value)}
                    className={`rounded-md border px-3 py-1.5 text-xs font-medium transition ${selectedMonths.includes(m.value) ? 'border-brand-600 bg-brand-700 text-white' : 'border-slate-300 bg-white text-slate-700 hover:border-brand-400 hover:bg-brand-50'}`}>
                    {m.label}
                  </button>
                ))}
              </div>
              <div className="mt-3 flex items-center gap-3">
                <button type="button" onClick={() => setSelectedMonths(availableMonths.map((m) => m.value))} className="text-xs text-brand-700 hover:underline">Select All</button>
                <button type="button" onClick={() => setSelectedMonths([])} className="text-xs text-slate-500 hover:underline">Clear</button>
                <div className="flex-1" />
                <PrimaryButton onClick={handleGenerateSelected} disabled={generating || selectedMonths.length === 0} className="px-4 py-2 text-sm">
                  {generating ? 'Generating...' : `Generate ${selectedMonths.length} Invoice${selectedMonths.length !== 1 ? 's' : ''}`}
                </PrimaryButton>
              </div>
            </>
          )}
        </Card>
      )}

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
  const [switchForm, setSwitchForm] = useState({ newVehicleId: '', switchType: 'PermanentToTemporary', reasonCode: 'Maintenance', reasonOther: '', odometer: 0 })
  const [switchBusy, setSwitchBusy] = useState(false)
  const [switchError, setSwitchError] = useState<string | null>(null)

  useEffect(() => {
    if (showSwitch) {
      bff.getVehicles(1, 50, undefined, 1).then((r) => setAvailableVehicles(r.items)).catch(() => {})
    }
  }, [showSwitch])

  async function handleSwitchVehicle() {
    const reason = switchForm.reasonCode === 'Other' ? switchForm.reasonOther : switchForm.reasonCode
    if (!switchForm.newVehicleId) { setSwitchError('Please select a replacement vehicle.'); return }
    if (!reason.trim()) { setSwitchError('Please provide a reason for the switch.'); return }
    setSwitchBusy(true); setSwitchError(null)
    try {
      const body: SwitchVehicleRequest = {
        newVehicleId: switchForm.newVehicleId,
        reason: `[${switchForm.switchType}] ${reason}`,
        odometer: switchForm.odometer,
        notes: `Switch type: ${switchForm.switchType}. Reason: ${reason}`,
      }
      const res = await bff.switchLeaseVehicle(id, body, crypto.randomUUID())
      if (!res.success) throw new Error(res.errorMessage ?? 'Switch failed')
      const updated = await bff.getLeaseById(id)
      setLease(updated)
      setShowSwitch(false)
      setSwitchForm({ newVehicleId: '', switchType: 'PermanentToTemporary', reasonCode: 'Maintenance', reasonOther: '', odometer: 0 })
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
      if (op === 'activate') {
        await bff.activateLease(id, crypto.randomUUID())
      }
      const updated = await bff.getLeaseById(id)
      setLease(updated)
    } catch (e) { alert(`${op} failed: ${(e as Error).message}`) }
    finally { setOperating(false) }
  }

  function downloadCsv() {
    const rows = [['Field', 'Value'], ['Contract #', lease!.leaseNumber], ['Customer', lease!.customerDisplayName], ['Vehicle', lease!.vehicleMakeModel], ['Plate', lease!.vehiclePlate], ['Driver', lease!.primaryDriverName ?? '—'], ['Status', lease!.status], ['Start', lease!.contractStartUtc], ['End', lease!.contractEndUtc], ['Rent/mo', String(lease!.rentAmountSar)]]
    const csv = rows.map((r) => r.join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `contract-${lease!.leaseNumber}.csv`; a.click()
  }

  const TABS: { key: Tab; label: string }[] = [
    { key: 'overview', label: 'Overview' },
    { key: 'damages', label: 'Damages' },
    { key: 'violations', label: 'Violations' },
    { key: 'invoices', label: 'Invoices' },
    { key: 'payments', label: 'Payments' },
    { key: 'history', label: 'History' },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title={`Lease Agreement ${lease.leaseNumber}`}
        subtitle={`${lease.customerDisplayName} · ${lease.vehiclePlate} · ${lease.vehicleMakeModel}`}
        action={
          <div className="flex flex-wrap gap-2">
            <SecondaryButton onClick={downloadCsv} className="px-3 py-1.5 text-xs">Export</SecondaryButton>
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
        <span className="text-sm text-slate-600">{
          lease.contractTypeCode === '1' ? 'Long Term Lease' : lease.contractTypeCode === '2' ? 'Short Term Rental' : lease.contractTypeCode === '3' ? 'Daily Rental' : lease.contractTypeCode
        }</span>
        <span className="text-sm text-slate-400">·</span>
        <span className="text-sm text-slate-600">{safeDate(lease.contractStartUtc)} → {safeDate(lease.contractEndUtc)}</span>
        <span className="text-sm text-slate-500">{lease.leaseNumber}</span>
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

          {/* Vehicle Card */}
          <Card className="p-4">
            <div className="flex items-center justify-between">
              <SectionHdr>Vehicle</SectionHdr>
              {(lease.status === 'Active' || lease.status === 'Extended' || lease.status === 'PendingIssuance') && (
                <SecondaryButton onClick={() => setShowSwitch((v) => !v)} className="px-2 py-1 text-xs">
                  {showSwitch ? 'Cancel' : 'Switch Vehicle'}
                </SecondaryButton>
              )}
            </div>
            <div className="rounded-lg border border-slate-200 bg-slate-50 p-4">
              <div className="flex items-start justify-between">
                <div>
                  <p className="font-mono text-lg font-bold text-slate-900">{lease.vehiclePlate}</p>
                  <p className="text-sm font-medium text-slate-700">{lease.vehicleMakeModel}</p>
                </div>
                <Badge tone="blue">Permanent</Badge>
              </div>
              <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
                <div><span className="text-slate-500">Checkout Date</span><p className="font-medium text-slate-900">{safeDate(lease.contractStartUtc)}</p></div>
                <div><span className="text-slate-500">Expected Check-in</span><p className="font-medium text-slate-900">{safeDate(lease.contractEndUtc)}</p></div>
                <div><span className="text-slate-500">Branch</span><p className="font-medium text-slate-900">{lease.workingBranchName}</p></div>
                <div><span className="text-slate-500">Payment Method</span><p className="font-medium text-slate-900">{
                  lease.paymentMethodCode === '1' ? 'Cash' : lease.paymentMethodCode === '2' ? 'Bank Transfer' : lease.paymentMethodCode === '3' ? 'Credit Card' : lease.paymentMethodCode === '4' ? 'Cheque' : lease.paymentMethodCode
                }</p></div>
              </div>
            </div>
              {showSwitch && (
                <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-4">
                  <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-amber-700">Vehicle Switch</p>
                  <div className="space-y-3">
                    <div className="grid grid-cols-2 gap-3">
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-600">Replacement Vehicle *</label>
                        <select className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={switchForm.newVehicleId}
                          onChange={(e) => {
                            const vid = e.target.value
                            const veh = availableVehicles.find((v) => v.id === vid)
                            setSwitchForm((f) => ({ ...f, newVehicleId: vid, odometer: veh?.currentKm ?? 0 }))
                          }}>
                          <option value="">— Select available vehicle —</option>
                          {availableVehicles.map((v) => <option key={v.id} value={v.id}>{v.plateNumber} — {v.make} {v.model} ({v.modelYear}) — {v.currentKm.toLocaleString()} km</option>)}
                        </select>
                      </div>
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-600">Switch Type *</label>
                        <select className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={switchForm.switchType} onChange={(e) => setSwitchForm((f) => ({ ...f, switchType: e.target.value }))}>
                          <option value="PermanentToTemporary">Permanent → Temporary</option>
                          <option value="TemporaryToTemporary">Temporary → Temporary</option>
                          <option value="TemporaryToPermanent">Temporary → Permanent</option>
                        </select>
                      </div>
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-600">Reason *</label>
                        <select className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={switchForm.reasonCode} onChange={(e) => setSwitchForm((f) => ({ ...f, reasonCode: e.target.value }))}>
                          <option value="Maintenance">Scheduled Maintenance / PMS</option>
                          <option value="Accident">Accident / Damage Repair</option>
                          <option value="Breakdown">Mechanical Breakdown</option>
                          <option value="CustomerRequest">Customer Request</option>
                          <option value="Insurance">Insurance Claim</option>
                          <option value="Upgrade">Vehicle Upgrade</option>
                          <option value="Downgrade">Vehicle Downgrade</option>
                          <option value="Recall">Manufacturer Recall</option>
                          <option value="Other">Other (specify below)</option>
                        </select>
                      </div>
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-600">Odometer (km)</label>
                        <input type="number" readOnly className="w-full rounded-lg border border-slate-200 bg-slate-100 px-3 py-2 text-sm text-slate-600" value={switchForm.odometer} />
                        <p className="mt-0.5 text-[10px] text-slate-400">Auto-filled from selected vehicle</p>
                      </div>
                    </div>
                    {switchForm.reasonCode === 'Other' && (
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-600">Specify Reason *</label>
                        <input className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={switchForm.reasonOther} onChange={(e) => setSwitchForm((f) => ({ ...f, reasonOther: e.target.value }))} placeholder="Enter reason details..." />
                      </div>
                    )}
                  </div>
                  {switchError && <p className="mt-2 rounded-md border border-red-200 bg-red-50 px-3 py-1.5 text-sm text-red-600">{switchError}</p>}
                  <div className="mt-3 flex gap-2">
                    <PrimaryButton onClick={handleSwitchVehicle} disabled={switchBusy || !switchForm.newVehicleId} className="px-4 py-2 text-sm">
                      {switchBusy ? 'Switching...' : 'Confirm Switch'}
                    </PrimaryButton>
                    <SecondaryButton onClick={() => { setShowSwitch(false); setSwitchError(null) }} className="px-3 py-2 text-sm">Cancel</SecondaryButton>
                  </div>
                </div>
              )}
          </Card>

          {/* Driver Card */}
          <div className="space-y-4">
            <Card className="p-4">
              <SectionHdr>Driver</SectionHdr>
              <div className="rounded-lg border border-slate-200 bg-slate-50 p-4">
                <p className="text-sm font-semibold text-slate-900">{lease.primaryDriverName ?? '—'}</p>
                <div className="mt-2 grid grid-cols-2 gap-2 text-xs">
                  <div><span className="text-slate-500">Customer</span><p className="font-medium text-slate-900">{lease.customerDisplayName}</p></div>
                  <div><span className="text-slate-500">Status</span><p><Badge tone={STATUS_TONES[lease.status] ?? 'slate'}>{lease.status}</Badge></p></div>
                </div>
              </div>
              {lease.status === 'PendingIssuance' && (
                <div className="mt-3 rounded-md border border-amber-200 bg-amber-50 p-3">
                  <p className="text-xs font-semibold text-amber-800">Status: Pending Issuance</p>
                  <p className="mt-0.5 text-[10px] text-amber-700">This lease agreement is waiting to be activated. Activate it to start the vehicle checkout.</p>
                  <PrimaryButton onClick={() => doOperation('activate', 'Activate this lease agreement? Vehicle will be checked out to the driver.')} disabled={operating} className="mt-2 px-3 py-1.5 text-xs">
                    {operating ? 'Activating...' : 'Activate (Checkout)'}
                  </PrimaryButton>
                </div>
              )}
            </Card>

            {/* Financials */}
            <Card className="p-4">
              <SectionHdr>Financials</SectionHdr>
              <div className="space-y-2 text-xs">
                <div className="flex justify-between"><span className="text-slate-500">Monthly Rent</span><span className="font-mono font-medium text-slate-900">{fmt(lease.rentAmountSar)}</span></div>
                <div className="flex justify-between"><span className="text-slate-500">VAT (15%)</span><span className="font-mono text-slate-700">{fmt(lease.vatAmountSar)}</span></div>
                <div className="flex justify-between border-t border-slate-200 pt-1"><span className="font-semibold text-slate-700">Total</span><span className="font-mono font-bold text-slate-900">{fmt(lease.totalAmountSar)}</span></div>
                <div className="flex justify-between"><span className="text-slate-500">Paid</span><span className="font-mono text-green-700">{fmt(lease.paidAmountSar)}</span></div>
                <div className="flex justify-between"><span className="text-slate-500">Remaining</span><span className={`font-mono font-semibold ${lease.remainingAmountSar > 0 ? 'text-red-600' : 'text-green-700'}`}>{fmt(lease.remainingAmountSar)}</span></div>
              </div>
            </Card>

            {/* LA Info + Navigation */}
            <Card className="p-4">
              <SectionHdr>Lease Agreement Info</SectionHdr>
              <div className="grid grid-cols-2 gap-x-6 gap-y-2 text-xs">
                <Field label="LA Number" value={lease.leaseNumber} mono />
                <Field label="Type" value={
                  lease.contractTypeCode === '1' ? 'Long Term Lease' : lease.contractTypeCode === '2' ? 'Short Term Rental' : lease.contractTypeCode === '3' ? 'Daily Rental' : lease.contractTypeCode
                } />
              </div>
              <div className="mt-3 flex flex-col gap-1.5 border-t border-slate-200 pt-3">
                {lease.contractId && lease.contractId !== '00000000-0000-0000-0000-000000000000' && (
                  <Link href={`/contracts/${lease.contractId}`} className="flex items-center gap-1 rounded-md border border-brand-200 bg-brand-50 px-3 py-1.5 text-xs font-medium text-brand-700 hover:bg-brand-100">
                    View this Contract →
                  </Link>
                )}
                {lease.quotationId && lease.quotationId !== '00000000-0000-0000-0000-000000000000' && (
                  <Link href={`/quotations/${lease.quotationId}`} className="flex items-center gap-1 rounded-md border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50">
                    View this Quotation →
                  </Link>
                )}
                <Link href="/leases" className="mt-1 text-xs text-slate-500 hover:underline">← Back to Lease Agreements</Link>
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
      {tab === 'payments' && <PaymentsTab leaseId={id} />}

      {tab === 'history' && (
        <div className="space-y-4">
          {/* Checkout / Check-in Summary */}
          <Card className="overflow-hidden">
            <div className="border-b border-slate-200 bg-slate-50 px-4 py-3">
              <h3 className="text-sm font-semibold text-slate-800">Checkout &amp; Check-in</h3>
            </div>
            <table className="w-full text-xs">
              <thead className="bg-slate-100 text-slate-600">
                <tr>
                  <th className="px-3 py-2 text-start font-medium">Event</th>
                  <th className="px-3 py-2 text-start font-medium">Date</th>
                  <th className="px-3 py-2 text-start font-medium">Vehicle</th>
                  <th className="px-3 py-2 text-start font-medium">Driver</th>
                  <th className="px-3 py-2 text-start font-medium">Status</th>
                  <th className="px-3 py-2 text-start font-medium">Reason / Notes</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-t border-slate-100">
                  <td className="px-3 py-2 font-medium text-green-700">Checkout</td>
                  <td className="px-3 py-2">{safeDate(lease.issuedAtUtc ?? lease.contractStartUtc)}</td>
                  <td className="px-3 py-2 font-mono">{lease.vehiclePlate}</td>
                  <td className="px-3 py-2">{lease.primaryDriverName ?? '—'}</td>
                  <td className="px-3 py-2"><Badge tone="green">Active</Badge></td>
                  <td className="px-3 py-2 text-slate-500">Initial vehicle checkout</td>
                </tr>
                {lease.closedAtUtc && (
                  <tr className="border-t border-slate-100">
                    <td className="px-3 py-2 font-medium text-red-700">Check-in</td>
                    <td className="px-3 py-2">{safeDate(lease.closedAtUtc)}</td>
                    <td className="px-3 py-2 font-mono">{lease.vehiclePlate}</td>
                    <td className="px-3 py-2">{lease.primaryDriverName ?? '—'}</td>
                    <td className="px-3 py-2"><Badge tone="slate">Closed</Badge></td>
                    <td className="px-3 py-2 text-slate-500">Vehicle returned</td>
                  </tr>
                )}
                {!lease.closedAtUtc && lease.status !== 'Draft' && lease.status !== 'PendingIssuance' && (
                  <tr className="border-t border-slate-100 bg-amber-50/40">
                    <td className="px-3 py-2 font-medium text-amber-700">Check-in (pending)</td>
                    <td className="px-3 py-2 text-slate-400">{safeDate(lease.contractEndUtc)}</td>
                    <td className="px-3 py-2 font-mono text-slate-400">{lease.vehiclePlate}</td>
                    <td className="px-3 py-2 text-slate-400">{lease.primaryDriverName ?? '—'}</td>
                    <td className="px-3 py-2"><Badge tone="amber">Expected</Badge></td>
                    <td className="px-3 py-2 text-slate-400">Scheduled return date</td>
                  </tr>
                )}
              </tbody>
            </table>
          </Card>

          {/* Full Activity Log */}
          <Card className="p-4">
            <SectionHdr>Activity Log</SectionHdr>
            <div className="overflow-hidden rounded-lg border border-slate-200">
              <table className="w-full text-xs">
                <thead className="bg-slate-100 text-slate-600">
                  <tr>
                    <th className="px-3 py-2 text-start font-medium">Timestamp</th>
                    <th className="px-3 py-2 text-start font-medium">Event</th>
                    <th className="px-3 py-2 text-start font-medium">Status</th>
                    <th className="px-3 py-2 text-start font-medium">Details</th>
                  </tr>
                </thead>
                <tbody>
                  {[
                    { ts: lease.createdAtUtc, event: 'Lease Agreement Created', status: 'Draft', detail: `LA ${lease.leaseNumber} created for ${lease.customerDisplayName}` },
                    ...(lease.issuedAtUtc ? [{ ts: lease.issuedAtUtc, event: 'Vehicle Checkout (Activated)', status: 'Active', detail: `${lease.vehiclePlate} checked out to ${lease.primaryDriverName ?? 'driver'}` }] : []),
                    ...(lease.suspendedAtUtc ? [{ ts: lease.suspendedAtUtc, event: 'Lease Suspended', status: 'Suspended', detail: 'Operations suspended by operator' }] : []),
                    ...(lease.resumedAtUtc ? [{ ts: lease.resumedAtUtc, event: 'Lease Resumed', status: 'Active', detail: 'Operations resumed' }] : []),
                    ...(lease.closedAtUtc ? [{ ts: lease.closedAtUtc, event: 'Vehicle Check-in (Closed)', status: 'Closed', detail: `${lease.vehiclePlate} returned by ${lease.primaryDriverName ?? 'driver'}` }] : []),
                    ...(lease.cancelledAtUtc ? [{ ts: lease.cancelledAtUtc, event: 'Lease Cancelled', status: 'Cancelled', detail: 'Lease agreement cancelled' }] : []),
                  ].map((ev, i) => (
                    <tr key={i} className="border-t border-slate-100">
                      <td className="px-3 py-2 font-mono text-slate-500">{new Date(ev.ts).toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })}</td>
                      <td className="px-3 py-2 font-medium text-slate-800">{ev.event}</td>
                      <td className="px-3 py-2"><Badge tone={STATUS_TONES[ev.status] ?? 'slate'}>{ev.status}</Badge></td>
                      <td className="px-3 py-2 text-slate-600">{ev.detail}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      )}
    </div>
  )
}
