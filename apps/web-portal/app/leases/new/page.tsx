'use client'

import { useEffect, useMemo, useState } from 'react'
import { useLocale } from '../../../lib/locale-provider'
import {
  bff,
  type BranchDto,
  type CustomerSummary,
  type DriverSummary,
  type RentPolicyDto,
  type SaveContractRequest,
  type SaveContractResponse,
  type VehicleSummary,
} from '../../../lib/bff-client'
import { Card, ErrorBox, PageHeader, Spinner } from '../../../components/ui'

function toLocalDatetime(d: Date): string {
  // <input type="datetime-local"> expects YYYY-MM-DDTHH:mm without timezone.
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export default function NewLeasePage() {
  const { t, locale } = useLocale()
  const [customers, setCustomers] = useState<CustomerSummary[]>([])
  const [vehicles, setVehicles] = useState<VehicleSummary[]>([])
  const [drivers, setDrivers] = useState<DriverSummary[]>([])
  const [policies, setPolicies] = useState<RentPolicyDto[]>([])
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [bootError, setBootError] = useState<string | null>(null)
  const [booting, setBooting] = useState(true)

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
        // pre-pick first values for instant happy-path submit
        setForm((f) => ({
          ...f,
          customerId: f.customerId || c.items[0]?.id || '',
          vehicleId: f.vehicleId || v.items[0]?.id || '',
          primaryDriverId: f.primaryDriverId || d.items[0]?.id || '',
          rentPolicyId: f.rentPolicyId || p[0]?.id || '',
          workingBranchId: f.workingBranchId || b[0]?.id || '',
          receiveBranchId: f.receiveBranchId || b[0]?.id || '',
          returnBranchId: f.returnBranchId || b[0]?.id || '',
        }))
      } catch (e) {
        setBootError((e as Error).message)
      } finally {
        setBooting(false)
      }
    })()
  }, [])

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
    'mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500'

  return (
    <div className="space-y-4">
      <PageHeader title={t.newLease.title} subtitle={t.newLease.subtitle} />

      <Card className="border-blue-200 bg-blue-50 p-3">
        <p className="text-xs text-blue-900">{t.newLease.devHint}</p>
      </Card>

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
                <label className={labelClass}>{t.newLease.fields.contractType}</label>
                <input
                  type="number"
                  min={1}
                  className={inputClass}
                  value={form.contractTypeCode}
                  onChange={(e) => setForm({ ...form, contractTypeCode: Number(e.target.value) })}
                />
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.allowedKmPerDay}</label>
                <input
                  type="number"
                  min={0}
                  className={inputClass}
                  value={form.allowedKmPerDay}
                  onChange={(e) => setForm({ ...form, allowedKmPerDay: Number(e.target.value) })}
                />
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.paymentMethod}</label>
                <input
                  type="number"
                  min={1}
                  className={inputClass}
                  value={form.paymentMethodCode}
                  onChange={(e) => setForm({ ...form, paymentMethodCode: Number(e.target.value) })}
                />
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.rentAmount}</label>
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  className={inputClass}
                  value={form.rentAmount}
                  onChange={(e) => setForm({ ...form, rentAmount: Number(e.target.value) })}
                />
              </div>
              <div>
                <label className={labelClass}>{t.newLease.fields.paidAmount}</label>
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  className={inputClass}
                  value={form.paidAmount}
                  onChange={(e) => setForm({ ...form, paidAmount: Number(e.target.value) })}
                />
              </div>
            </div>
          </Card>

          {submitError && <ErrorBox message={`${t.newLease.error}: ${submitError}`} />}

          {result && (
            <Card className="border-green-200 bg-green-50 p-4">
              <h3 className="font-semibold text-green-900">{t.newLease.successTitle}</h3>
              <dl className="mt-2 space-y-1 text-sm text-green-900">
                <div className="flex gap-2">
                  <dt className="font-medium">{t.newLease.successLeaseId}:</dt>
                  <dd className="break-all font-mono">{result.leaseId}</dd>
                </div>
                <div className="flex gap-2">
                  <dt className="font-medium">{t.newLease.successContractNumber}:</dt>
                  <dd className="font-mono">{result.tajeerContractNumber}</dd>
                </div>
                <div className="flex gap-2">
                  <dt className="font-medium">{t.newLease.successIssuanceUrl}:</dt>
                  <dd className="break-all font-mono">
                    <a
                      href={result.issuanceUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="underline"
                    >
                      {result.issuanceUrl}
                    </a>
                  </dd>
                </div>
              </dl>
            </Card>
          )}

          <div>
            <button
              type="submit"
              disabled={submitting}
              className="bg-brand-600 hover:bg-brand-700 inline-flex items-center rounded-md px-4 py-2 text-sm font-medium text-white shadow-sm disabled:opacity-60"
            >
              {submitting ? t.newLease.submitting : t.newLease.submit}
            </button>
          </div>
        </form>
      )}
    </div>
  )
}
