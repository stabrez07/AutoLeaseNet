'use client'

import { useEffect, useState, useCallback } from 'react'
import { useRouter, useParams, useSearchParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type ContractSummary, type CustomerDetail, type CustomerInvoiceSummary, type CustomerPaymentSummary, type LeaseSummary, type VehicleSummary, type DriverSummary, type AuditEvent, type UpdateCustomerB2BRequest } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, PrimaryButton, SecondaryButton, Spinner } from '../../../components/ui'

/* ── styling constants ──────────────────────────────────────────────────────── */

const INPUT = 'w-full rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2 text-sm focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-1 focus:ring-brand-500'
const LABEL = 'mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-500'

/* ── helpers ────────────────────────────────────────────────────────────────── */

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

function EditField({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (v: string) => void; type?: string }) {
  return (
    <div>
      <label className={LABEL}>{label}</label>
      <input type={type} className={INPUT} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  )
}

/* ── tone maps ──────────────────────────────────────────────────────────────── */

const LEASE_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Active: 'green', Extended: 'blue', PendingIssuance: 'amber',
  Suspended: 'amber', Draft: 'slate', Closed: 'slate', Cancelled: 'red',
}
const VEHICLE_TONES: Record<number, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  1: 'green', 2: 'blue', 3: 'amber', 4: 'slate', 5: 'slate',
}
const DRIVER_TONES: Record<number, 'green' | 'amber' | 'slate'> = { 1: 'green', 2: 'amber', 3: 'slate' }
const AUDIT_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Created: 'green', Updated: 'blue', StatusChanged: 'amber', Deleted: 'red', Viewed: 'slate', Exported: 'slate',
}

/* ── edit-mode form shape ───────────────────────────────────────────────────── */

interface B2BFormState {
  legalName: string
  legalNameAr: string
  commercialRegistration: string
  vatNumber: string
  email: string
  mobile: string
  nationalAddress: string
  billingAddress: string
  creditLimit: string
  creditCurrency: string
}

function toFormState(d: CustomerDetail): B2BFormState {
  return {
    legalName: d.legalName ?? '',
    legalNameAr: d.legalNameAr ?? '',
    commercialRegistration: d.commercialRegistration ?? '',
    vatNumber: d.vatNumber ?? '',
    email: d.email ?? '',
    mobile: d.mobile ?? '',
    nationalAddress: d.nationalAddress ?? '',
    billingAddress: d.billingAddress ?? '',
    creditLimit: d.creditLimit != null ? String(d.creditLimit) : '',
    creditCurrency: d.creditCurrency ?? 'SAR',
  }
}

/* ── main component ─────────────────────────────────────────────────────────── */

type Tab = 'details' | 'contracts' | 'leases' | 'vehicles' | 'drivers' | 'invoices' | 'payments' | 'audit'

export default function CustomerDetailPage() {
  const { t } = useLocale()
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const searchParams = useSearchParams()
  const c = t.crudCustomers

  /* ── data state ─────────────────────────────────────────────────────────── */
  const [data, setData] = useState<CustomerDetail | null>(null)
  const [contracts, setContracts] = useState<ContractSummary[] | null>(null)
  const [leases, setLeases] = useState<LeaseSummary[] | null>(null)
  const [vehicles, setVehicles] = useState<VehicleSummary[] | null>(null)
  const [drivers, setDrivers] = useState<DriverSummary[] | null>(null)
  const [invoices, setInvoices] = useState<CustomerInvoiceSummary[] | null>(null)
  const [payments, setPayments] = useState<CustomerPaymentSummary[] | null>(null)
  const [auditEvents, setAuditEvents] = useState<AuditEvent[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [actionBusy, setActionBusy] = useState(false)
  const [actionMsg, setActionMsg] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('details')
  const [selectedLease, setSelectedLease] = useState<LeaseSummary | null>(null)

  /* ── edit mode state ────────────────────────────────────────────────────── */
  const [editing, setEditing] = useState(false)
  const [form, setForm] = useState<B2BFormState>({
    legalName: '', legalNameAr: '', commercialRegistration: '', vatNumber: '',
    email: '', mobile: '', nationalAddress: '', billingAddress: '',
    creditLimit: '', creditCurrency: 'SAR',
  })
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  /* ── delete state ───────────────────────────────────────────────────────── */
  const [deleteConfirm, setDeleteConfirm] = useState(false)
  const [deleting, setDeleting] = useState(false)

  /* ── data loading ───────────────────────────────────────────────────────── */
  const load = useCallback(async () => {
    setLoading(true); setError(null)
    try {
      const [cust, cnts, ls, vs, ds, inv, pay, events] = await Promise.all([
        bff.getCustomerById(id),
        bff.getCustomerContracts(id).catch(() => [] as ContractSummary[]),
        bff.getCustomerLeases(id),
        bff.getCustomerVehicles(id),
        bff.getCustomerDrivers(id),
        bff.getCustomerInvoices(id).catch(() => [] as CustomerInvoiceSummary[]),
        bff.getCustomerPayments(id).catch(() => [] as CustomerPaymentSummary[]),
        bff.getAuditEvents('Customer', id).catch(() => [] as AuditEvent[]),
      ])
      setData(cust)
      setContracts(cnts)
      setLeases(ls)
      setVehicles(vs)
      setDrivers(ds)
      setInvoices(inv)
      setPayments(pay)
      setAuditEvents(events)
      setForm(toFormState(cust))
    } catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }, [id])

  useEffect(() => { void load() }, [load])

  /* auto-enter edit mode from URL query param */
  useEffect(() => {
    if (searchParams.get('edit') === 'true' && data) {
      setEditing(true)
      setTab('details')
    }
  }, [searchParams, data])

  /* ── status action ──────────────────────────────────────────────────────── */
  async function handleStatusAction(action: string) {
    const comment = window.prompt(`Enter reason/comment for "${action}" action (required for audit):`)
    if (comment === null) return
    if (!comment.trim()) { setActionMsg('Comment is required for audit trail.'); return }
    setActionBusy(true); setActionMsg(null)
    try {
      const res = await bff.updateCustomerStatus(id, action, crypto.randomUUID())
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
      setActionMsg(t.common.successCreated)
      await load()
    } catch (e) { setActionMsg((e as Error).message) }
    finally { setActionBusy(false) }
  }

  /* ── save edit ──────────────────────────────────────────────────────────── */
  async function handleSave() {
    setSaving(true); setSaveError(null)
    try {
      const body: UpdateCustomerB2BRequest = {
        ...(form.legalName ? { legalName: form.legalName } : {}),
        ...(form.legalNameAr ? { legalNameAr: form.legalNameAr } : {}),
        ...(form.commercialRegistration ? { commercialRegistration: form.commercialRegistration } : {}),
        ...(form.vatNumber ? { vatNumber: form.vatNumber } : {}),
        ...(form.email ? { email: form.email } : {}),
        ...(form.mobile ? { mobile: form.mobile } : {}),
        ...(form.nationalAddress ? { nationalAddress: form.nationalAddress } : {}),
        ...(form.billingAddress ? { billingAddress: form.billingAddress } : {}),
        ...(form.creditLimit ? { creditLimit: Number(form.creditLimit) } : {}),
        ...(form.creditCurrency ? { creditCurrency: form.creditCurrency } : {}),
      }
      const res = await bff.updateCustomerB2B(id, body, crypto.randomUUID())
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Update failed')
      setEditing(false)
      await load()
    } catch (e) { setSaveError((e as Error).message) }
    finally { setSaving(false) }
  }

  /* ── delete ─────────────────────────────────────────────────────────────── */
  async function handleDelete() {
    setDeleting(true)
    try {
      const res = await bff.deleteCustomer(id, crypto.randomUUID())
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Delete failed')
      router.push('/customers')
    } catch (e) { setActionMsg((e as Error).message); setDeleteConfirm(false) }
    finally { setDeleting(false) }
  }

  /* ── form field updater ─────────────────────────────────────────────────── */
  function setField(key: keyof B2BFormState) {
    return (val: string) => setForm((prev) => ({ ...prev, [key]: val }))
  }

  /* ── guards ─────────────────────────────────────────────────────────────── */
  if (loading) return <Spinner label={t.common.loading} />
  if (error) return <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />
  if (!data) return <p className="text-sm text-slate-500">{t.common.notFound}</p>

  const statusTone = data.status === 'Active' ? 'green' : data.status === 'Suspended' ? 'amber' : 'slate'

  const tabs: { key: Tab; label: string; count?: number }[] = [
    { key: 'details', label: t.common.details },
    { key: 'contracts', label: 'Contracts', ...(contracts != null ? { count: contracts.length } : {}) },
    { key: 'leases', label: 'Lease Agreements', ...(leases != null ? { count: leases.length } : {}) },
    { key: 'invoices', label: 'Invoices', ...(invoices != null ? { count: invoices.length } : {}) },
    { key: 'vehicles', label: t.common.vehicles, ...(vehicles != null ? { count: vehicles.length } : {}) },
    { key: 'drivers', label: t.common.drivers, ...(drivers != null ? { count: drivers.length } : {}) },
    { key: 'payments', label: 'Payments', ...(payments != null ? { count: payments.length } : {}) },
    { key: 'audit', label: 'Audit Log' },
  ]

  /* ══════════════════════════════════════════════════════════════════════════ */
  /*  RENDER                                                                   */
  /* ══════════════════════════════════════════════════════════════════════════ */

  return (
    <div className="mx-auto max-w-5xl space-y-4">
      <PageHeader
        title={data.displayName}
        subtitle={`B2B · ${data.id}`}
        action={
          <div className="flex flex-wrap gap-2">
            <SecondaryButton onClick={() => router.push(`/accounts?customerId=${data.id}`)} className="px-3 py-1.5 text-xs">View Accounts</SecondaryButton>
            <SecondaryButton onClick={() => router.push(`/accounts/new?customerId=${data.id}`)} className="px-3 py-1.5 text-xs">Create Account</SecondaryButton>
            <SecondaryButton onClick={() => router.push(`/customers/${data.id}/account`)} className="px-3 py-1.5 text-xs">Account &amp; SOA</SecondaryButton>
            <SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>
          </div>
        }
      />

      {/* ── B2B header card ─────────────────────────────────────────────────── */}
      <Card className="p-4">
        <div className="flex items-start justify-between">
          <div>
            <div className="flex items-center gap-3">
              <Badge tone={statusTone}>{c.statuses[data.status as keyof typeof c.statuses] ?? data.status}</Badge>
              <Badge tone="blue">{t.customers.type.b2b}</Badge>
              {data.kycVerified && <Badge tone="green">{c.kycBadge}</Badge>}
            </div>
            <div className="mt-3 grid grid-cols-2 gap-x-8 gap-y-1 text-sm md:grid-cols-4">
              <div><span className="text-xs text-slate-500">CR No</span><p className="font-mono text-xs font-semibold text-slate-900">{data.commercialRegistration ?? '—'}</p></div>
              <div><span className="text-xs text-slate-500">VAT No</span><p className="font-mono text-xs font-semibold text-slate-900">{data.vatNumber ?? '—'}</p></div>
              <div><span className="text-xs text-slate-500">Credit Limit</span><p className="text-xs font-medium text-slate-900">{data.creditLimit ? `SAR ${data.creditLimit.toLocaleString()}` : '—'}</p></div>
              <div><span className="text-xs text-slate-500">City</span><p className="text-xs font-medium text-slate-900">{data.nationalAddress?.split(',')[0] ?? '—'}</p></div>
            </div>
          </div>
          {data.contactPersonNameEn && (
            <div className="ms-6 shrink-0 rounded-lg border border-slate-200 bg-slate-50 px-4 py-2.5 text-right">
              <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Contact Person</p>
              <p className="text-sm font-semibold text-slate-900">{data.contactPersonNameEn}</p>
              <p className="text-xs text-slate-500">{data.contactPersonPosition ?? ''}</p>
              {data.contactPersonMobile && <p className="mt-0.5 font-mono text-xs text-brand-700">{data.contactPersonMobile}</p>}
              {data.contactPersonEmail && <p className="text-xs text-slate-500">{data.contactPersonEmail}</p>}
            </div>
          )}
        </div>
        <div className="mt-2 flex flex-wrap gap-3 text-xs text-slate-500">
          {data.email && <span>{data.email}</span>}
          {data.mobile && <span>{data.mobile}</span>}
        </div>
      </Card>

      {/* ── Tab bar ─────────────────────────────────────────────────────────── */}
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

      {/* ════════════════════════════════════════════════════════════════════── */}
      {/*  DETAILS TAB                                                          */}
      {/* ════════════════════════════════════════════════════════════════════── */}
      {tab === 'details' && (
        <>
          <Card className="divide-y divide-slate-100 p-6">
            {/* edit / cancel toggle */}
            <div className="mb-4 flex items-center justify-between">
              <h3 className="text-sm font-semibold text-slate-700">B2B Customer Details</h3>
              <div className="flex gap-2">
                {!editing ? (
                  <SecondaryButton onClick={() => { setForm(toFormState(data)); setEditing(true); setSaveError(null) }} className="px-3 py-1.5 text-xs">Edit</SecondaryButton>
                ) : (
                  <>
                    <PrimaryButton onClick={handleSave} disabled={saving} className="px-3 py-1.5 text-xs">{saving ? 'Saving...' : 'Save'}</PrimaryButton>
                    <SecondaryButton onClick={() => { setEditing(false); setSaveError(null); setForm(toFormState(data)) }} disabled={saving} className="px-3 py-1.5 text-xs">Cancel</SecondaryButton>
                  </>
                )}
              </div>
            </div>

            {saveError && (
              <p className="mb-3 rounded bg-red-50 px-3 py-1.5 text-sm text-red-700">{saveError}</p>
            )}

            {/* ── identity section ────────────────────────────────────────────── */}
            {editing ? (
              <div className="pb-4">
                <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">{c.sections.identity}</h3>
                <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
                  <EditField label={c.fields.legalName} value={form.legalName} onChange={setField('legalName')} />
                  <EditField label={c.fields.legalNameAr} value={form.legalNameAr} onChange={setField('legalNameAr')} />
                  <EditField label={c.fields.commercialReg} value={form.commercialRegistration} onChange={setField('commercialRegistration')} />
                  <EditField label={c.fields.vatNumber} value={form.vatNumber} onChange={setField('vatNumber')} />
                  <EditField label={c.fields.creditLimit} value={form.creditLimit} onChange={setField('creditLimit')} type="number" />
                  <EditField label="Credit Currency" value={form.creditCurrency} onChange={setField('creditCurrency')} />
                </div>
              </div>
            ) : (
              <Section title={c.sections.identity}>
                <Field label={c.fields.legalName} value={data.legalName} />
                <Field label={c.fields.legalNameAr} value={data.legalNameAr} />
                <Field label={c.fields.commercialReg} value={data.commercialRegistration} />
                <Field label={c.fields.vatNumber} value={data.vatNumber} />
                <Field label={c.fields.creditLimit} value={data.creditLimit != null ? `${data.creditLimit} ${data.creditCurrency ?? ''}` : undefined} />
                <Field label={c.fields.billingAddress} value={data.billingAddress} />
              </Section>
            )}

            {/* ── contact person (read-only, not editable B2B field) ──────────── */}
            {data.contactPersonNameEn && (
              <div className="pt-4">
                <Section title="Contact Person">
                  <Field label="Name (EN)" value={data.contactPersonNameEn} />
                  <Field label="Name (AR)" value={data.contactPersonNameAr} />
                  <Field label="Position" value={data.contactPersonPosition} />
                  <Field label="Mobile" value={data.contactPersonMobile} />
                  <Field label="Email" value={data.contactPersonEmail} />
                </Section>
              </div>
            )}

            {/* ── contact / address section ───────────────────────────────────── */}
            {editing ? (
              <div className="pt-4">
                <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">{c.sections.contact}</h3>
                <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
                  <EditField label={c.fields.email} value={form.email} onChange={setField('email')} type="email" />
                  <EditField label={c.fields.mobile} value={form.mobile} onChange={setField('mobile')} />
                  <EditField label={c.fields.nationalAddress} value={form.nationalAddress} onChange={setField('nationalAddress')} />
                  <EditField label={c.fields.billingAddress} value={form.billingAddress} onChange={setField('billingAddress')} />
                </div>
              </div>
            ) : (
              <div className="pt-4">
                <Section title={c.sections.contact}>
                  <Field label={c.fields.email} value={data.email} />
                  <Field label={c.fields.mobile} value={data.mobile} />
                  <Field label={c.fields.nationalAddress} value={data.nationalAddress} />
                </Section>
              </div>
            )}

            {/* ── system details (always read-only) ───────────────────────────── */}
            <div className="pt-4">
              <Section title={t.common.details}>
                <Field label={t.common.id} value={data.id} />
                <Field label="Preferred language" value={data.preferredLanguage} />
                <Field label="KYC verified" value={data.kycVerified ? `Yes -- ${data.kycVerifiedAtUtc?.substring(0, 10) ?? ''}` : 'No'} />
                <Field label={t.common.createdAt} value={data.createdAtUtc?.substring(0, 10)} />
                <Field label={t.common.updatedAt} value={data.updatedAtUtc?.substring(0, 10)} />
              </Section>
            </div>
          </Card>

          {/* ── actions card ──────────────────────────────────────────────────── */}
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

              {/* delete customer */}
              {!deleteConfirm ? (
                <SecondaryButton onClick={() => setDeleteConfirm(true)} disabled={actionBusy} className="border-red-200 text-red-600 hover:bg-red-50">Delete Customer</SecondaryButton>
              ) : (
                <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-1.5">
                  <span className="text-sm text-red-700">Are you sure? This cannot be undone.</span>
                  <PrimaryButton onClick={handleDelete} disabled={deleting} className="bg-red-600 px-3 py-1 text-xs hover:bg-red-700">{deleting ? 'Deleting...' : 'Confirm Delete'}</PrimaryButton>
                  <SecondaryButton onClick={() => setDeleteConfirm(false)} disabled={deleting} className="px-3 py-1 text-xs">Cancel</SecondaryButton>
                </div>
              )}
            </div>
          </Card>
        </>
      )}

      {/* ════════════════════════════════════════════════════════════════════── */}
      {/*  CONTRACTS TAB                                                        */}
      {/* ════════════════════════════════════════════════════════════════════── */}
      {tab === 'contracts' && (
        <Card className="overflow-hidden">
          {!contracts || contracts.length === 0 ? (
            <p className="p-6 text-sm text-slate-500">{t.common.noRecords}</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50 text-left">
                <tr>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Contract #</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Status</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Vehicles</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Monthly Rent</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Duration</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Total Value</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Lease Agmts</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Period</th>
                </tr>
              </thead>
              <tbody>
                {contracts.map((cnt) => {
                  const cntTone = cnt.status === 'Active' ? 'green' : cnt.status === 'Draft' ? 'slate' : cnt.status === 'Suspended' ? 'amber' : 'slate'
                  return (
                    <tr key={cnt.id} className="cursor-pointer border-t border-slate-100 hover:bg-brand-50/40" onClick={() => router.push(`/contracts/${cnt.id}`)}>
                      <td className="px-3 py-2 font-mono text-xs font-semibold text-brand-700">{cnt.contractNumber}</td>
                      <td className="px-3 py-2"><Badge tone={cntTone as 'green' | 'amber' | 'slate'}>{cnt.status}</Badge></td>
                      <td className="px-3 py-2 text-end text-xs font-medium text-slate-900">{cnt.totalVehicles}</td>
                      <td className="px-3 py-2 text-end font-mono text-xs">SAR {cnt.monthlyRentSar.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                      <td className="px-3 py-2 text-xs text-slate-600">{cnt.durationMonths} months</td>
                      <td className="px-3 py-2 text-end font-mono text-xs font-semibold">SAR {cnt.totalContractValueSar.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                      <td className="px-3 py-2 text-end text-xs font-medium text-brand-700">{cnt.leaseAgreementCount}</td>
                      <td className="px-3 py-2 text-xs text-slate-600">{cnt.startDate?.substring(0, 10)} {'→'} {cnt.endDate?.substring(0, 10)}</td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          )}
        </Card>
      )}

      {/* ════════════════════════════════════════════════════════════════════── */}
      {/*  LEASES TAB                                                           */}
      {/* ════════════════════════════════════════════════════════════════════── */}
      {tab === 'leases' && (
        <Card className="overflow-hidden">
          {!leases || leases.length === 0 ? (
            <p className="p-6 text-sm text-slate-500">{t.common.noRecords}</p>
          ) : (
            <>
              <table className="w-full text-sm">
                <thead className="border-b border-slate-200 bg-slate-50 text-left">
                  <tr>
                    <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Lease #</th>
                    <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Vehicle</th>
                    <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Plate</th>
                    <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Driver</th>
                    <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Status</th>
                    <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Type</th>
                    <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Period</th>
                    <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Rent (SAR)</th>
                  </tr>
                </thead>
                <tbody>
                  {leases.map((l) => (
                    <tr key={l.id}
                      className={`cursor-pointer border-t border-slate-100 transition ${selectedLease?.id === l.id ? 'bg-brand-50 ring-1 ring-inset ring-brand-300' : 'hover:bg-brand-50/40'}`}
                      onClick={() => setSelectedLease(selectedLease?.id === l.id ? null : l)}
                    >
                      <td className="px-3 py-2 font-mono text-xs font-semibold text-brand-700">{l.leaseNumber}</td>
                      <td className="px-3 py-2 text-xs text-slate-700">{l.vehicleMakeModel}</td>
                      <td className="px-3 py-2 font-mono text-xs text-slate-600">{l.vehiclePlate}</td>
                      <td className="px-3 py-2 text-xs text-slate-600">{l.primaryDriverName ?? '—'}</td>
                      <td className="px-3 py-2"><Badge tone={LEASE_TONES[l.status] ?? 'slate'}>{(t.leases.statuses as Record<string, string>)[l.status] ?? l.status}</Badge></td>
                      <td className="px-3 py-2 text-xs text-slate-600">{l.contractTypeCode}</td>
                      <td className="px-3 py-2 text-xs text-slate-600">{l.contractStartUtc.substring(0,10)} {'→'} {l.contractEndUtc.substring(0,10)}</td>
                      <td className="px-3 py-2 text-end font-mono text-xs">{l.rentAmountSar.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {selectedLease && (
                <div className="border-t border-slate-200 bg-slate-50/80 p-4">
                  <div className="flex items-start justify-between">
                    <div>
                      <p className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Selected Lease</p>
                      <p className="mt-1 font-mono text-sm font-bold text-brand-700">{selectedLease.leaseNumber}</p>
                    </div>
                    <div className="flex gap-2">
                      <SecondaryButton onClick={() => router.push(`/leases/${selectedLease.id}`)} className="px-2 py-1 text-xs">Open Full Details</SecondaryButton>
                      <SecondaryButton onClick={() => setSelectedLease(null)} className="px-2 py-1 text-xs">Close</SecondaryButton>
                    </div>
                  </div>
                  <div className="mt-3 grid grid-cols-2 gap-x-6 gap-y-2 text-sm md:grid-cols-4">
                    <div><span className="text-xs text-slate-500">Customer</span><p className="text-xs font-medium text-slate-900">{selectedLease.customerDisplayName}</p></div>
                    <div><span className="text-xs text-slate-500">Vehicle</span><p className="text-xs font-medium text-slate-900">{selectedLease.vehicleMakeModel}</p></div>
                    <div><span className="text-xs text-slate-500">Plate</span><p className="font-mono text-xs text-slate-900">{selectedLease.vehiclePlate}</p></div>
                    <div><span className="text-xs text-slate-500">Driver</span><p className="text-xs font-medium text-slate-900">{selectedLease.primaryDriverName ?? '—'}</p></div>
                    <div><span className="text-xs text-slate-500">Status</span><p className="text-xs"><Badge tone={LEASE_TONES[selectedLease.status] ?? 'slate'}>{selectedLease.status}</Badge></p></div>
                    <div><span className="text-xs text-slate-500">Type</span><p className="text-xs text-slate-900">{selectedLease.contractTypeCode}</p></div>
                    <div><span className="text-xs text-slate-500">Period</span><p className="text-xs text-slate-900">{selectedLease.contractStartUtc.substring(0,10)} {'→'} {selectedLease.contractEndUtc.substring(0,10)}</p></div>
                    <div><span className="text-xs text-slate-500">Rent</span><p className="font-mono text-xs text-slate-900">SAR {selectedLease.rentAmountSar.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p></div>
                    <div><span className="text-xs text-slate-500">Branch</span><p className="text-xs text-slate-900">{selectedLease.workingBranchName}</p></div>
                    <div><span className="text-xs text-slate-500">Contract #</span><p className="font-mono text-xs text-slate-900">{selectedLease.leaseNumber}</p></div>
                  </div>
                </div>
              )}
            </>
          )}
        </Card>
      )}

      {/* ════════════════════════════════════════════════════════════════════── */}
      {/*  VEHICLES TAB                                                         */}
      {/* ════════════════════════════════════════════════════════════════════── */}
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

      {/* ════════════════════════════════════════════════════════════════════── */}
      {/*  DRIVERS TAB                                                          */}
      {/* ════════════════════════════════════════════════════════════════════── */}
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
                        {d.licenseExpiryDate} {expiring ? '(!)' : ''}
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

      {/* ════════════════════════════════════════════════════════════════════── */}
      {/*  INVOICES TAB                                                         */}
      {/* ════════════════════════════════════════════════════════════════════── */}
      {tab === 'invoices' && (
        <Card className="overflow-hidden">
          {!invoices || invoices.length === 0 ? (
            <p className="p-6 text-sm text-slate-500">{t.common.noRecords}</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50 text-left">
                <tr>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Invoice #</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Contract</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Status</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Issue Date</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Due Date</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Total (SAR)</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Paid (SAR)</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Balance</th>
                </tr>
              </thead>
              <tbody>
                {invoices.map((inv) => {
                  const balance = inv.totalAmountSar - inv.paidAmountSar
                  const invTone = inv.status === 'Cleared' || inv.status === 'Finalized' ? 'green' : inv.status === 'Draft' ? 'slate' : inv.status === 'Submitted' ? 'blue' : 'amber'
                  return (
                    <tr key={inv.id} className="cursor-pointer border-t border-slate-100 hover:bg-brand-50/40" onClick={() => router.push(`/invoices/${inv.id}`)}>
                      <td className="px-3 py-2 font-mono text-xs font-semibold text-brand-700">{inv.invoiceNumber}</td>
                      <td className="px-3 py-2 font-mono text-xs text-slate-600">{inv.leaseNumber}</td>
                      <td className="px-3 py-2"><Badge tone={invTone as 'green' | 'amber' | 'blue' | 'slate'}>{inv.status}</Badge></td>
                      <td className="px-3 py-2 text-xs text-slate-600">{inv.issueDateUtc?.substring(0, 10)}</td>
                      <td className="px-3 py-2 text-xs text-slate-600">{inv.dueDateUtc?.substring(0, 10)}</td>
                      <td className="px-3 py-2 text-end font-mono text-xs">{inv.totalAmountSar.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                      <td className="px-3 py-2 text-end font-mono text-xs text-green-700">{inv.paidAmountSar.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                      <td className={`px-3 py-2 text-end font-mono text-xs font-semibold ${balance > 0 ? 'text-red-600' : 'text-green-700'}`}>
                        {balance.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          )}
        </Card>
      )}

      {/* ════════════════════════════════════════════════════════════════════── */}
      {/*  PAYMENTS TAB                                                         */}
      {/* ════════════════════════════════════════════════════════════════════── */}
      {tab === 'payments' && (
        <Card className="overflow-hidden">
          {!payments || payments.length === 0 ? (
            <p className="p-6 text-sm text-slate-500">{t.common.noRecords}</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50 text-left">
                <tr>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">ID</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Date</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Method</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Reference</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Amount (SAR)</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase text-end">Remaining</th>
                  <th className="px-3 py-2 text-xs font-medium text-slate-500 uppercase">Allocated To</th>
                </tr>
              </thead>
              <tbody>
                {payments.map((p) => (
                  <tr key={p.id} className="cursor-pointer border-t border-slate-100 hover:bg-brand-50/40" onClick={() => router.push(`/payments/${p.id}`)}>
                    <td className="px-3 py-2 font-mono text-xs font-semibold text-brand-700">PAY-{p.displayId}</td>
                    <td className="px-3 py-2 text-xs text-slate-600">{p.receivedDate?.substring(0, 10)}</td>
                    <td className="px-3 py-2 text-xs text-slate-700">{p.paymentMethod}</td>
                    <td className="px-3 py-2 font-mono text-xs text-slate-600">{p.referenceNumber ?? '—'}</td>
                    <td className="px-3 py-2 text-end font-mono text-xs font-semibold">{p.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    <td className={`px-3 py-2 text-end font-mono text-xs ${p.remainingBalance > 0 ? 'text-amber-600' : 'text-green-700'}`}>
                      {p.remainingBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                    </td>
                    <td className="px-3 py-2 text-xs text-slate-500">
                      {p.allocations.length > 0
                        ? p.allocations.map((a) => a.invoiceNumber).join(', ')
                        : <span className="text-slate-400">Unallocated</span>
                      }
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
      )}

      {/* ════════════════════════════════════════════════════════════════════── */}
      {/*  AUDIT TAB                                                            */}
      {/* ════════════════════════════════════════════════════════════════════── */}
      {tab === 'audit' && (
        <Card className="overflow-hidden">
          <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-4 py-3">
            <h3 className="text-sm font-semibold text-slate-700">Audit Trail</h3>
            <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs font-semibold text-slate-600">{auditEvents.length}</span>
          </div>
          {auditEvents.length === 0 ? (
            <p className="px-4 py-6 text-sm text-slate-500">No audit events recorded.</p>
          ) : (
            <div className="divide-y divide-slate-100">
              {auditEvents.map((ev) => (
                <div key={ev.id} className="flex items-start gap-3 px-4 py-3">
                  <div className="mt-0.5 h-2 w-2 shrink-0 rounded-full bg-brand-400" />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge tone={AUDIT_TONES[ev.action] ?? 'slate'}>{ev.action}</Badge>
                      <span className="text-xs font-medium text-slate-700">{ev.performedBy}</span>
                      <span className="text-xs text-slate-400">{new Date(ev.performedAtUtc).toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })}</span>
                    </div>
                    {(ev.previousValue || ev.newValue) && (
                      <p className="mt-0.5 text-xs text-slate-500">
                        {ev.previousValue && <span className="line-through me-2">{ev.previousValue}</span>}
                        {ev.newValue && <span className="text-green-700">{ev.newValue}</span>}
                      </p>
                    )}
                    {ev.comment && <p className="mt-0.5 text-xs italic text-slate-500">&ldquo;{ev.comment}&rdquo;</p>}
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>
      )}
    </div>
  )
}
