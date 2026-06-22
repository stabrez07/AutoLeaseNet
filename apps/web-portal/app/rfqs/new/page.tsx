'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { bff, type CustomerSummary } from '../../../lib/bff-client'
import { Card, PageHeader, PrimaryButton, SecondaryButton } from '../../../components/ui'

/* ---------------------------------------------------------------------------
 * Constants
 * -------------------------------------------------------------------------*/

const SOURCE_OPTIONS = ['Direct', 'CRM Sync', 'Website', 'Referral'] as const
const TENURE_OPTIONS = [12, 24, 36, 48, 60] as const
const CATEGORY_OPTIONS = ['Sedan', 'SUV', 'Pickup', 'Van', 'Bus'] as const
const SERVICE_OPTIONS = ['Maintenance', 'Insurance', 'Replacement Vehicle', 'GPS Tracking'] as const

/* ---------------------------------------------------------------------------
 * Form state type
 * -------------------------------------------------------------------------*/

interface FormState {
  customerId: string
  source: string
  vehicleQty: string
  tenureMonths: string
  categories: string[]
  annualMileageCapKm: string
  services: string[]
  expectedCloseDate: string
  notes: string
}

interface FormErrors {
  customerId?: string
  source?: string
  vehicleQty?: string
  tenureMonths?: string
}

/* ---------------------------------------------------------------------------
 * Page component
 * -------------------------------------------------------------------------*/

export default function NewRfqPage() {
  const router = useRouter()

  // Customers for dropdown
  const [customers, setCustomers] = useState<CustomerSummary[]>([])
  const [customersLoading, setCustomersLoading] = useState(true)

  // Form state
  const [form, setForm] = useState<FormState>({
    customerId: '',
    source: '',
    vehicleQty: '',
    tenureMonths: '',
    categories: [],
    annualMileageCapKm: '',
    services: [],
    expectedCloseDate: '',
    notes: '',
  })
  const [errors, setErrors] = useState<FormErrors>({})
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  /* ─── Load customers ──────────────────────────────────────────────────────*/
  useEffect(() => {
    let cancelled = false
    async function load() {
      try {
        const result = await bff.getCustomers(1, 200)
        if (!cancelled) setCustomers(result.items)
      } catch {
        // Non-critical — user can retry
      } finally {
        if (!cancelled) setCustomersLoading(false)
      }
    }
    load()
    return () => {
      cancelled = true
    }
  }, [])

  /* ─── Field updaters ──────────────────────────────────────────────────────*/
  function set<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }))
    if (key in errors) {
      setErrors((prev) => {
        const next = { ...prev }
        delete next[key as keyof FormErrors]
        return next
      })
    }
  }

  function toggleCategory(cat: string) {
    setForm((prev) => ({
      ...prev,
      categories: prev.categories.includes(cat)
        ? prev.categories.filter((c) => c !== cat)
        : [...prev.categories, cat],
    }))
  }

  function toggleService(svc: string) {
    setForm((prev) => ({
      ...prev,
      services: prev.services.includes(svc)
        ? prev.services.filter((s) => s !== svc)
        : [...prev.services, svc],
    }))
  }

  /* ─── Validation ──────────────────────────────────────────────────────────*/
  function validate(): FormErrors {
    const errs: FormErrors = {}
    if (!form.customerId) errs.customerId = 'Please select a customer.'
    if (!form.source) errs.source = 'Please select a source.'
    const qty = parseInt(form.vehicleQty, 10)
    if (!form.vehicleQty || isNaN(qty) || qty < 1 || qty > 500) {
      errs.vehicleQty = 'Vehicle quantity must be between 1 and 500.'
    }
    if (!form.tenureMonths) errs.tenureMonths = 'Please select a lease tenure.'
    return errs
  }

  /* ─── Submit ──────────────────────────────────────────────────────────────*/
  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const errs = validate()
    if (Object.keys(errs).length > 0) {
      setErrors(errs)
      return
    }
    setSubmitting(true)
    setSubmitError(null)
    try {
      const mileage = form.annualMileageCapKm
        ? parseInt(form.annualMileageCapKm, 10)
        : undefined
      const result = await bff.createRfq(
        {
          customerId: form.customerId,
          source: form.source,
          vehicleQty: parseInt(form.vehicleQty, 10),
          tenureMonths: parseInt(form.tenureMonths, 10),
          ...(form.categories.length > 0 ? { vehicleCategories: form.categories.join(', ') } : {}),
          ...(form.services.length > 0 ? { services: form.services.join(', ') } : {}),
          ...(mileage && !isNaN(mileage) ? { annualMileageCapKm: mileage } : {}),
          ...(form.expectedCloseDate ? { expectedCloseDate: form.expectedCloseDate } : {}),
          ...(form.notes.trim() ? { notes: form.notes.trim() } : {}),
        },
        `new-rfq-${Date.now()}`,
      )
      if (result.rfqId) {
        router.push(`/rfqs/${result.rfqId}`)
      } else {
        router.push('/rfqs')
      }
    } catch (e) {
      setSubmitError((e as Error).message)
    } finally {
      setSubmitting(false)
    }
  }

  /* ─── Render ──────────────────────────────────────────────────────────────*/

  const inputCls =
    'mt-1 block w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400'
  const selectCls =
    'mt-1 block w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400'
  const labelCls = 'block text-xs font-medium text-slate-700'
  const errCls = 'mt-1 text-[11px] text-red-600'

  return (
    <div className="mx-auto max-w-3xl px-4 py-6 sm:px-6 lg:px-8">
      <Link href="/rfqs" className="mb-3 inline-block text-xs text-brand-700 hover:underline">
        &larr; Back to Pipeline
      </Link>

      <PageHeader
        title="New Lead"
        subtitle="Create a new Request for Quotation to track a sales opportunity."
      />

      <Card className="p-6">
        <form onSubmit={handleSubmit} noValidate>
          <div className="grid gap-5 sm:grid-cols-2">
            {/* Customer */}
            <div className="sm:col-span-2">
              <label className={labelCls}>
                Customer <span className="text-red-500">*</span>
              </label>
              <select
                value={form.customerId}
                onChange={(e) => set('customerId', e.target.value)}
                className={selectCls}
                disabled={customersLoading}
              >
                <option value="">
                  {customersLoading ? 'Loading customers...' : 'Select a customer'}
                </option>
                {customers.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.displayName}
                    {c.city ? ` (${c.city})` : ''}
                  </option>
                ))}
              </select>
              {errors.customerId && <p className={errCls}>{errors.customerId}</p>}
            </div>

            {/* Source */}
            <div>
              <label className={labelCls}>
                Source <span className="text-red-500">*</span>
              </label>
              <select
                value={form.source}
                onChange={(e) => set('source', e.target.value)}
                className={selectCls}
              >
                <option value="">Select source</option>
                {SOURCE_OPTIONS.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </select>
              {errors.source && <p className={errCls}>{errors.source}</p>}
            </div>

            {/* Vehicle Qty */}
            <div>
              <label className={labelCls}>
                Vehicle Quantity <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                min={1}
                max={500}
                value={form.vehicleQty}
                onChange={(e) => set('vehicleQty', e.target.value)}
                placeholder="1 - 500"
                className={inputCls}
              />
              {errors.vehicleQty && <p className={errCls}>{errors.vehicleQty}</p>}
            </div>

            {/* Tenure */}
            <div>
              <label className={labelCls}>
                Tenure (months) <span className="text-red-500">*</span>
              </label>
              <select
                value={form.tenureMonths}
                onChange={(e) => set('tenureMonths', e.target.value)}
                className={selectCls}
              >
                <option value="">Select tenure</option>
                {TENURE_OPTIONS.map((t) => (
                  <option key={t} value={String(t)}>
                    {t} months
                  </option>
                ))}
              </select>
              {errors.tenureMonths && <p className={errCls}>{errors.tenureMonths}</p>}
            </div>

            {/* Annual Mileage Cap */}
            <div>
              <label className={labelCls}>Annual Mileage Cap (km)</label>
              <input
                type="number"
                min={0}
                value={form.annualMileageCapKm}
                onChange={(e) => set('annualMileageCapKm', e.target.value)}
                placeholder="e.g. 30000"
                className={inputCls}
              />
            </div>

            {/* Vehicle Categories */}
            <div className="sm:col-span-2">
              <label className={labelCls}>Vehicle Categories</label>
              <p className="mt-0.5 text-[11px] text-slate-400">
                Select one or more vehicle types for this opportunity.
              </p>
              <div className="mt-2 flex flex-wrap gap-2">
                {CATEGORY_OPTIONS.map((cat) => {
                  const active = form.categories.includes(cat)
                  return (
                    <button
                      key={cat}
                      type="button"
                      onClick={() => toggleCategory(cat)}
                      className={`rounded-md border px-3 py-1.5 text-xs font-medium transition ${
                        active
                          ? 'border-brand-300 bg-brand-50 text-brand-700'
                          : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300'
                      }`}
                    >
                      {cat}
                    </button>
                  )
                })}
              </div>
            </div>

            {/* Services */}
            <div className="sm:col-span-2">
              <label className={labelCls}>Services</label>
              <p className="mt-0.5 text-[11px] text-slate-400">
                Select included services for the lease package.
              </p>
              <div className="mt-2 flex flex-wrap gap-3">
                {SERVICE_OPTIONS.map((svc) => {
                  const active = form.services.includes(svc)
                  return (
                    <label
                      key={svc}
                      className="flex cursor-pointer items-center gap-2 text-xs text-slate-700"
                    >
                      <input
                        type="checkbox"
                        checked={active}
                        onChange={() => toggleService(svc)}
                        className="h-3.5 w-3.5 rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                      />
                      {svc}
                    </label>
                  )
                })}
              </div>
            </div>

            {/* Expected Close Date */}
            <div>
              <label className={labelCls}>Expected Close Date</label>
              <input
                type="date"
                value={form.expectedCloseDate}
                onChange={(e) => set('expectedCloseDate', e.target.value)}
                className={inputCls}
              />
            </div>

            {/* Notes */}
            <div className="sm:col-span-2">
              <label className={labelCls}>Notes</label>
              <textarea
                value={form.notes}
                onChange={(e) => set('notes', e.target.value)}
                rows={3}
                placeholder="Any additional details about this opportunity..."
                className={inputCls}
              />
            </div>
          </div>

          {/* Submit error */}
          {submitError && (
            <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-4 py-2.5 text-xs text-red-700">
              {submitError}
            </div>
          )}

          {/* Buttons */}
          <div className="mt-6 flex items-center justify-end gap-3">
            <SecondaryButton onClick={() => router.push('/rfqs')}>Cancel</SecondaryButton>
            <PrimaryButton type="submit" disabled={submitting}>
              {submitting ? 'Creating...' : 'Create Lead'}
            </PrimaryButton>
          </div>
        </form>
      </Card>
    </div>
  )
}
