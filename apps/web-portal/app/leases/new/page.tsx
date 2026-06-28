'use client'

import { Suspense, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import {
  bff,
  type BranchDto,
  type CustomerSummary,
  type DriverSummary,
  type QuotationDetail,
  type RentPolicyDto,
  type SaveContractRequest,
  type SaveContractResponse,
  type VehicleSummary,
} from '../../../lib/bff-client'

const CONTRACT_TYPES: { code: number; label: string }[] = [
  { code: 1, label: 'Long Term Lease' },
  { code: 2, label: 'Short Term Rental' },
  { code: 3, label: 'Daily Rental' },
]

const PAYMENT_METHODS: { code: number; label: string }[] = [
  { code: 1, label: 'Cash' },
  { code: 2, label: 'Bank Transfer' },
  { code: 3, label: 'Credit Card' },
  { code: 4, label: 'Cheque' },
]
import { Card, ErrorBox, PageHeader, Spinner } from '../../../components/ui'

function toLocalDatetime(d: Date): string {
  // <input type="datetime-local"> expects YYYY-MM-DDTHH:mm without timezone.
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export default function NewLeasePageWrapper() {
  return (
    <Suspense fallback={<div className="p-6 text-sm text-slate-500">Loading...</div>}>
      <NewLeasePage />
    </Suspense>
  )
}

function NewLeasePage() {
  const { t, locale } = useLocale()
  const searchParams = useSearchParams()
  const fromQuoteId = searchParams?.get('fromQuote') ?? null
  const fromQuoteCustomerId = searchParams?.get('customerId') ?? null
  const fromQuoteDuration = Number(searchParams?.get('duration') ?? 0)
  const [customers, setCustomers] = useState<CustomerSummary[]>([])
  const [vehicles, setVehicles] = useState<VehicleSummary[]>([])
  const [drivers, setDrivers] = useState<DriverSummary[]>([])
  const [policies, setPolicies] = useState<RentPolicyDto[]>([])
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [bootError, setBootError] = useState<string | null>(null)
  const [booting, setBooting] = useState(true)
  const [sourceQuote, setSourceQuote] = useState<QuotationDetail | null>(null)
  const [rentFromQuote, setRentFromQuote] = useState(false)

  const now = useMemo(() => new Date(), [])
  const twoDays = useMemo(() => {
    const d = new Date()
    d.setDate(d.getDate() + 2)
    return d
  }, [])

  const [form, setForm] = useState({
    customerId: '',
    vehicleId: '',
    primaryDriverId: '',
    rentPolicyId: '',
    workingBranchId: '',
    receiveBranchId: '',
    returnBranchId: '',
    contractStartLocal: toLocalDatetime(now),
    contractEndLocal: toLocalDatetime(twoDays),
    contractTypeCode: 1,
    allowedKmPerDay: 300,
    rentAmount: 200,
    paidAmount: 50,
    paymentMethodCode: 1,
  })

  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [result, setResult] = useState<SaveContractResponse | null>(null)

  useEffect(() => {
    ;(async () => {
      try {
        const [c, v, d, p, b] = await Promise.all([
          bff.getCustomers(1, 50),
          bff.getVehicles(1, 50, undefined, 1),
          bff.getDrivers(1, 50),
          bff.getRentPolicies(),
          bff.getBranches(),
        ])
        setCustomers(c.items)
        setVehicles(v.items)
        setDrivers(d.items)
        setPolicies(p)
        setBranches(b)

        const preCustomer = fromQuoteCustomerId || c.items[0]?.id || ''
        const preDriver = d.items[0]?.id || ''
        let endLocal = toLocalDatetime(twoDays)
        let computedRent = 200
        let isFromQuote = false

        if (fromQuoteId) {
          try {
            const quote = await bff.getQuotation(fromQuoteId)
            setSourceQuote(quote)
            if (quote.estimatedDurationMonths > 0) {
              computedRent = Math.round((quote.totalSar / quote.estimatedDurationMonths) * 100) / 100
            }
            if (quote.estimatedDurationMonths > 0) {
              const end = new Date()
              end.setMonth(end.getMonth() + quote.estimatedDurationMonths)
              endLocal = toLocalDatetime(end)
            }
            isFromQuote = true
            setRentFromQuote(true)
          } catch { /* quote fetch failed — continue with manual entry */ }
        } else if (fromQuoteDuration > 0) {
          const end = new Date()
          end.setMonth(end.getMonth() + fromQuoteDuration)
          endLocal = toLocalDatetime(end)
        }

        setForm((prev) => ({
          ...prev,
          customerId: preCustomer,
          vehicleId: prev.vehicleId || v.items[0]?.id || '',
          primaryDriverId: preDriver,
          rentPolicyId: prev.rentPolicyId || p[0]?.id || '',
          workingBranchId: prev.workingBranchId || b[0]?.id || '',
          receiveBranchId: prev.receiveBranchId || b[0]?.id || '',
          returnBranchId: prev.returnBranchId || b[0]?.id || '',
          contractEndLocal: endLocal,
          ...(isFromQuote ? { rentAmount: computedRent, contractTypeCode: 1 } : {}),
        }))
      } catch (e) {
        setBootError((e as Error).message)
      } finally {
        setBooting(false)
      }
    })()
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true)
    setSubmitError(null)
    setResult(null)
    try {
      const idempotencyKey = crypto.randomUUID()
      const body: SaveContractRequest = {
        customerId: form.customerId,
        vehicleId: form.vehicleId,
        primaryDriverId: form.primaryDriverId,
        rentPolicyId: form.rentPolicyId,
        workingBranchId: form.workingBranchId,
        receiveBranchId: form.receiveBranchId,
        returnBranchId: form.returnBranchId,
        contractStartUtc: new Date(form.contractStartLocal).toISOString(),
        contractEndUtc: new Date(form.contractEndLocal).toISOString(),
        contractTypeCode: Number(form.contractTypeCode),
        allowedKmPerDay: Number(form.allowedKmPerDay),
        rentAmount: Number(form.rentAmount),
        paidAmount: Number(form.paidAmount),
        paymentMethodCode: Number(form.paymentMethodCode),
      }
      const res = await bff.saveContract(body, idempotencyKey)
      setResult(res)
    } catch (e) {
      setSubmitError((e as Error).message)
    } finally {
      setSubmitting(false)
    }
  }

  const labelClass = 'text-xs font-medium text-slate-700'
  const inputClass =
    'mt-1 w-full rounded-lg border border-slate-300 px-2.5 py-1.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500'

  return (
    <div className="space-y-4">
      <PageHeader title={t.newLease.title} subtitle={t.newLease.subtitle} />

      {sourceQuote && (
        <Card className="border-green-200 bg-green-50 p-3">
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-green-800">Creating contract from Quotation {sourceQuote.quoteNumber}</span>
            <span className="rounded-full bg-green-200 px-2 py-0.5 text-[10px] font-medium text-green-800">
              {sourceQuote.estimatedDurationMonths} months &middot; SAR {sourceQuote.totalSar.toLocaleString(undefined, { minimumFractionDigits: 2 })}
            </span>
          </div>
          <p className="mt-1 text-xs text-green-700">
            Rent amount and duration are pre-filled from the quotation. Select vehicle, driver, and branch to complete the contract.
          </p>
        </Card>
      )}
      {!sourceQuote && (
        <Card className="border-brand-200 bg-brand-50 p-3">
          <p className="text-xs text-brand-900">{t.newLease.devHint}</p>
        </Card>
      )}

      {bootError && <ErrorBox message={bootError} />}
      {booting && <Spinner label={t.common.loading} />}

      {!booting && !bootError && (
        <form onSubmit={submit} className="space-y-4">
          <Card className="p-4">
            <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
              <div>
                <label className={labelClass}>{t.newLease.fields.customer}</label>
                <select
                  className={inputClass}
                  value={form.customerId}
                  onChange={(e) => setForm({ ...form, customerId: e.target.value })}
                  required
                >
                  {customers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.displayName}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.vehicle}</label>
                <select
                  className={inputClass}
                  value={form.vehicleId}
                  onChange={(e) => setForm({ ...form, vehicleId: e.target.value })}
                  required
                >
                  {vehicles.map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.plateNumber} — {v.make} {v.model}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.primaryDriver}</label>
                <select
                  className={inputClass}
                  value={form.primaryDriverId}
                  onChange={(e) => setForm({ ...form, primaryDriverId: e.target.value })}
                  required
                >
                  {drivers.map((d) => (
                    <option key={d.id} value={d.id}>
                      {(locale === 'ar' ? d.personNameAr : d.personNameEn) ?? d.driverLicenseNumber}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.rentPolicy}</label>
                <select
                  className={inputClass}
                  value={form.rentPolicyId}
                  onChange={(e) => setForm({ ...form, rentPolicyId: e.target.value })}
                  required
                >
                  {policies.map((p) => (
                    <option key={p.id} value={p.id}>
                      {locale === 'ar' ? p.nameAr : p.nameEn}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.workingBranch}</label>
                <select
                  className={inputClass}
                  value={form.workingBranchId}
                  onChange={(e) => setForm({ ...form, workingBranchId: e.target.value })}
                  required
                >
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {locale === 'ar' ? b.nameAr : b.nameEn}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.receiveBranch}</label>
                <select
                  className={inputClass}
                  value={form.receiveBranchId}
                  onChange={(e) => setForm({ ...form, receiveBranchId: e.target.value })}
                  required
                >
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {locale === 'ar' ? b.nameAr : b.nameEn}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.returnBranch}</label>
                <select
                  className={inputClass}
                  value={form.returnBranchId}
                  onChange={(e) => setForm({ ...form, returnBranchId: e.target.value })}
                  required
                >
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {locale === 'ar' ? b.nameAr : b.nameEn}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.contractStart}</label>
                <input
                  type="datetime-local"
                  className={inputClass}
                  value={form.contractStartLocal}
                  onChange={(e) => setForm({ ...form, contractStartLocal: e.target.value })}
                  required
                />
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.contractEnd}</label>
                <input
                  type="datetime-local"
                  className={inputClass}
                  value={form.contractEndLocal}
                  onChange={(e) => setForm({ ...form, contractEndLocal: e.target.value })}
                  required
                />
              </div>
              <div>
                <label className={labelClass}>Contract Type</label>
                <select
                  className={inputClass}
                  value={form.contractTypeCode}
                  onChange={(e) => setForm({ ...form, contractTypeCode: Number(e.target.value) })}
                >
                  {CONTRACT_TYPES.map((ct) => (
                    <option key={ct.code} value={ct.code}>{ct.label}</option>
                  ))}
                </select>
              </div>
              {form.contractTypeCode !== 1 && (
                <div>
                  <label className={labelClass}>Allowed km / day</label>
                  <input
                    type="number"
                    min={0}
                    className={inputClass}
                    value={form.allowedKmPerDay}
                    onChange={(e) => setForm({ ...form, allowedKmPerDay: Number(e.target.value) })}
                  />
                </div>
              )}
              <div>
                <label className={labelClass}>Payment Method</label>
                <select
                  className={inputClass}
                  value={form.paymentMethodCode}
                  onChange={(e) => setForm({ ...form, paymentMethodCode: Number(e.target.value) })}
                >
                  {PAYMENT_METHODS.map((pm) => (
                    <option key={pm.code} value={pm.code}>{pm.label}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className={labelClass}>Monthly Rent (SAR)</label>
                {rentFromQuote ? (
                  <div className="relative">
                    <input
                      type="number"
                      className={`${inputClass} bg-slate-50 text-slate-700`}
                      value={form.rentAmount}
                      readOnly
                    />
                    <span className="mt-1 block text-[10px] text-brand-600">
                      Calculated from Quotation {sourceQuote?.quoteNumber ?? ''}
                    </span>
                  </div>
                ) : (
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    className={inputClass}
                    value={form.rentAmount}
                    onChange={(e) => setForm({ ...form, rentAmount: Number(e.target.value) })}
                  />
                )}
              </div>
              {form.contractTypeCode !== 1 && (
                <div>
                  <label className={labelClass}>Paid Amount (SAR)</label>
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    className={inputClass}
                    value={form.paidAmount}
                    onChange={(e) => setForm({ ...form, paidAmount: Number(e.target.value) })}
                  />
                </div>
              )}
              {form.contractTypeCode === 1 && (
                <div className="col-span-full">
                  <div className="rounded-md border border-blue-200 bg-blue-50 px-3 py-2 text-xs text-blue-800">
                    Long-term lease — monthly billing at end of each month per contract terms. No upfront payment required.
                  </div>
                </div>
              )}
            </div>
          </Card>

          {submitError && <ErrorBox message={`${t.newLease.error}: ${submitError}`} />}

          {result && (
            <Card className="border-green-200 bg-green-50 p-4">
              <h3 className="font-semibold text-green-900">{t.newLease.successTitle}</h3>
              <p className="mt-1 text-sm text-green-800">Contract <span className="font-mono font-semibold">{result.leaseNumber}</span> has been created successfully.</p>
              <div className="mt-3 flex gap-2">
                <a href="/leases" className="rounded-md bg-green-700 px-4 py-1.5 text-xs font-medium text-white hover:bg-green-800">
                  View All Contracts
                </a>
                <a href={`/leases/${result.leaseId}`} className="rounded-md border border-green-600 px-4 py-1.5 text-xs font-medium text-green-800 hover:bg-green-100">
                  Open Contract
                </a>
              </div>
            </Card>
          )}

          <div className="flex gap-3">
            <button
              type="submit"
              disabled={submitting}
              className="bg-brand-600 hover:bg-brand-700 inline-flex items-center rounded-md px-4 py-2 text-sm font-medium text-white shadow-sm disabled:opacity-60"
            >
              {submitting ? t.newLease.submitting : t.newLease.submit}
            </button>
            <button
              type="button"
              onClick={() => {
                const from = searchParams?.get('fromQuote')
                window.location.href = from ? `/quotations/${from}` : '/leases'
              }}
              className="inline-flex items-center rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 shadow-sm hover:bg-slate-50"
            >
              Cancel
            </button>
          </div>
        </form>
      )}
    </div>
  )
}
