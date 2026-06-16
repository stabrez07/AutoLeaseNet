'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import {
  bff,
  type AddQuotationLineRequest,
  type CreateQuotationRequest,
  type CustomerSummary,
  type QuotationDetail,
} from '../../../lib/bff-client'
import { Card, ErrorBox, PageHeader, Spinner } from '../../../components/ui'

const CONTRACT_TYPES = ['ShortTermRental', 'LongTermLease', 'FinanceLease', 'OperatingLease']
const ITEM_TYPES = ['VehicleRental', 'InsuranceCoverage', 'MaintenancePackage', 'DeliveryCharge', 'FuelAllowance', 'CustomService']

interface LineInput {
  itemType: string
  description: string
  vehicleSpecRef: string
  quantity: number
  unitPriceSar: number
  discountPercent: number
}

const emptyLine = (): LineInput => ({
  itemType: 'VehicleRental',
  description: '',
  vehicleSpecRef: '',
  quantity: 1,
  unitPriceSar: 0,
  discountPercent: 0,
})

const VAT = 0.15

function computePricing(lines: LineInput[], discountPercent: number) {
  const subTotal = lines.reduce((s, l) => {
    const lineTotal = l.quantity * l.unitPriceSar * (1 - l.discountPercent / 100)
    return s + lineTotal
  }, 0)
  const taxable = subTotal * (1 - discountPercent / 100)
  const vat = Math.round(taxable * VAT * 100) / 100
  const total = Math.round((taxable + vat) * 100) / 100
  return { subTotal: Math.round(subTotal * 100) / 100, taxable: Math.round(taxable * 100) / 100, vat, total }
}

export default function NewQuotationPage() {
  const { t } = useLocale()
  const router = useRouter()
  const f = t.quotations.newForm

  const [customers, setCustomers] = useState<CustomerSummary[]>([])
  const [booting, setBooting] = useState(true)
  const [bootError, setBootError] = useState<string | null>(null)

  const today = new Date().toISOString().slice(0, 10)
  const nextMonth = new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10)

  const [form, setForm] = useState({
    customerId: '',
    accountManagerId: '',
    quoteDate: today,
    validUntilDate: nextMonth,
    contractType: 'LongTermLease',
    estimatedDurationMonths: 12,
    discountPercent: 0,
    termsAndConditionsMd: '',
  })

  const [lines, setLines] = useState<LineInput[]>([emptyLine()])
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  useEffect(() => {
    bff.getCustomers(1, 100).then(res => {
      setCustomers(res.items)
      if (res.items[0]) setForm(prev => ({ ...prev, customerId: res.items[0].id }))
    }).catch(e => setBootError((e as Error).message)).finally(() => setBooting(false))
  }, [])

  const pricing = computePricing(lines, form.discountPercent)

  function updateLine(i: number, patch: Partial<LineInput>) {
    setLines(ls => ls.map((l, idx) => idx === i ? { ...l, ...patch } : l))
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true)
    setSubmitError(null)
    try {
      const idemKey = crypto.randomUUID()
      const body: CreateQuotationRequest = {
        customerId: form.customerId,
        accountManagerId: form.accountManagerId || crypto.randomUUID(),
        quoteDate: form.quoteDate,
        validUntilDate: form.validUntilDate,
        contractType: form.contractType,
        estimatedDurationMonths: Number(form.estimatedDurationMonths),
        discountPercent: Number(form.discountPercent),
        termsAndConditionsMd: form.termsAndConditionsMd || undefined,
      }
      const created: QuotationDetail = await bff.createQuotation(body, idemKey)

      // Add lines sequentially
      for (const line of lines) {
        if (!line.description.trim() || line.unitPriceSar <= 0) continue
        const lineBody: AddQuotationLineRequest = {
          itemType: line.itemType,
          description: line.description,
          vehicleSpecRef: line.vehicleSpecRef || undefined,
          quantity: Number(line.quantity),
          unitPriceSar: Number(line.unitPriceSar),
          discountPercent: Number(line.discountPercent),
        }
        await bff.addQuotationLine(created.id, lineBody, crypto.randomUUID())
      }

      router.push(`/quotations/${created.id}`)
    } catch (e) {
      setSubmitError((e as Error).message)
      setSubmitting(false)
    }
  }

  const lbl = 'text-xs font-medium text-slate-700'
  const inp = 'mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500'
  const sel = inp + ' bg-white'

  return (
    <div className="space-y-5">
      <PageHeader title={f.title} subtitle={f.subtitle} />

      {bootError && <ErrorBox message={bootError} />}
      {booting && <Spinner label={t.common.loading} />}

      {!booting && !bootError && (
        <form onSubmit={handleSubmit} className="space-y-5">

          {/* ── Header fields ── */}
          <Card className="p-5">
            <h2 className="mb-4 text-sm font-semibold text-slate-800">Contract Details</h2>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <div>
                <label className={lbl}>{f.fields.customer} *</label>
                <select className={sel} required value={form.customerId}
                  onChange={e => setForm({ ...form, customerId: e.target.value })}>
                  {customers.map(c => (
                    <option key={c.id} value={c.id}>{c.displayName}</option>
                  ))}
                </select>
              </div>

              <div>
                <label className={lbl}>{f.fields.contractType} *</label>
                <select className={sel} value={form.contractType}
                  onChange={e => setForm({ ...form, contractType: e.target.value })}>
                  {CONTRACT_TYPES.map(ct => (
                    <option key={ct} value={ct}>
                      {t.quotations.contractTypes[ct as keyof typeof t.quotations.contractTypes] ?? ct}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className={lbl}>{f.fields.durationMonths}</label>
                <input type="number" min={1} max={60} className={inp} value={form.estimatedDurationMonths}
                  onChange={e => setForm({ ...form, estimatedDurationMonths: Number(e.target.value) })} />
              </div>

              <div>
                <label className={lbl}>{f.fields.quoteDate} *</label>
                <input type="date" className={inp} required value={form.quoteDate}
                  onChange={e => setForm({ ...form, quoteDate: e.target.value })} />
              </div>

              <div>
                <label className={lbl}>{f.fields.validUntilDate} *</label>
                <input type="date" className={inp} required value={form.validUntilDate}
                  onChange={e => setForm({ ...form, validUntilDate: e.target.value })} />
              </div>

              <div>
                <label className={lbl}>{f.fields.discountPercent}</label>
                <input type="number" min={0} max={100} step="0.01" className={inp}
                  value={form.discountPercent}
                  onChange={e => setForm({ ...form, discountPercent: Number(e.target.value) })} />
              </div>

              <div className="sm:col-span-2 lg:col-span-3">
                <label className={lbl}>{f.fields.termsNotes}</label>
                <textarea rows={3} className={inp + ' font-mono text-xs'} value={form.termsAndConditionsMd}
                  onChange={e => setForm({ ...form, termsAndConditionsMd: e.target.value })} />
              </div>
            </div>
          </Card>

          {/* ── Line items ── */}
          <Card className="p-5">
            <div className="mb-3 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-slate-800">{f.addLine}</h2>
              <button type="button"
                onClick={() => setLines(ls => [...ls, emptyLine()])}
                className="rounded-md border border-dashed border-slate-300 px-3 py-1 text-xs text-slate-600 hover:border-blue-400 hover:text-blue-600">
                + {f.addLine}
              </button>
            </div>

            <div className="space-y-3">
              {lines.map((line, i) => (
                <div key={i} className="grid grid-cols-2 gap-2 rounded-lg border border-slate-200 bg-slate-50 p-3 sm:grid-cols-3 lg:grid-cols-6">
                  <div>
                    <label className={lbl}>{f.lineFields.itemType}</label>
                    <select className={sel + ' text-xs'} value={line.itemType}
                      onChange={e => updateLine(i, { itemType: e.target.value })}>
                      {ITEM_TYPES.map(it => (
                        <option key={it} value={it}>
                          {f.itemTypes[it as keyof typeof f.itemTypes] ?? it}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="col-span-1 sm:col-span-2">
                    <label className={lbl}>{f.lineFields.description} *</label>
                    <input className={inp} required value={line.description}
                      placeholder="e.g. Toyota Camry 2025 — 12 months"
                      onChange={e => updateLine(i, { description: e.target.value })} />
                  </div>

                  <div>
                    <label className={lbl}>{f.lineFields.vehicleSpec}</label>
                    <input className={inp} value={line.vehicleSpecRef}
                      onChange={e => updateLine(i, { vehicleSpecRef: e.target.value })} />
                  </div>

                  <div>
                    <label className={lbl}>{f.lineFields.quantity}</label>
                    <input type="number" min={1} className={inp} value={line.quantity}
                      onChange={e => updateLine(i, { quantity: Number(e.target.value) })} />
                  </div>

                  <div>
                    <label className={lbl}>{f.lineFields.unitPrice} *</label>
                    <input type="number" min={0} step="0.01" className={inp} required value={line.unitPriceSar}
                      onChange={e => updateLine(i, { unitPriceSar: Number(e.target.value) })} />
                  </div>

                  <div className="flex items-end gap-2">
                    <div className="flex-1">
                      <label className={lbl}>{f.lineFields.lineDiscount}</label>
                      <input type="number" min={0} max={100} step="0.01" className={inp}
                        value={line.discountPercent}
                        onChange={e => updateLine(i, { discountPercent: Number(e.target.value) })} />
                    </div>
                    {lines.length > 1 && (
                      <button type="button" onClick={() => setLines(ls => ls.filter((_, idx) => idx !== i))}
                        className="mb-1 rounded p-1 text-red-400 hover:text-red-600" title="Remove line">
                        ✕
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </Card>

          {/* ── Pricing summary ── */}
          <Card className="p-5">
            <h2 className="mb-3 text-sm font-semibold text-slate-800">{f.pricingSummary}</h2>
            <dl className="space-y-1 text-sm">
              <div className="flex justify-between text-slate-600">
                <dt>{f.subTotal}</dt>
                <dd className="font-mono">{pricing.subTotal.toLocaleString('en-SA', { minimumFractionDigits: 2 })} SAR</dd>
              </div>
              {form.discountPercent > 0 && (
                <div className="flex justify-between text-slate-600">
                  <dt>{f.discount} ({form.discountPercent}%)</dt>
                  <dd className="font-mono text-red-600">
                    − {(pricing.subTotal - pricing.taxable).toLocaleString('en-SA', { minimumFractionDigits: 2 })} SAR
                  </dd>
                </div>
              )}
              <div className="flex justify-between text-slate-600">
                <dt>{f.vat}</dt>
                <dd className="font-mono">{pricing.vat.toLocaleString('en-SA', { minimumFractionDigits: 2 })} SAR</dd>
              </div>
              <div className="flex justify-between border-t border-slate-200 pt-1 text-base font-semibold text-slate-900">
                <dt>{f.total}</dt>
                <dd className="font-mono">{pricing.total.toLocaleString('en-SA', { minimumFractionDigits: 2 })} SAR</dd>
              </div>
            </dl>
          </Card>

          {submitError && <ErrorBox message={`${f.error}: ${submitError}`} />}

          <div>
            <button type="submit" disabled={submitting}
              className="bg-brand-600 hover:bg-brand-700 inline-flex items-center rounded-md px-5 py-2.5 text-sm font-medium text-white shadow-sm disabled:opacity-60">
              {submitting ? f.submitting : f.submit}
            </button>
          </div>
        </form>
      )}
    </div>
  )
}
