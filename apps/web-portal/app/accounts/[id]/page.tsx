'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import Link from 'next/link'
import { bff, type AccountDetail, type UpdateAccountRequest } from '../../../lib/bff-client'
import { Card, PageHeader, PrimaryButton, SecondaryButton, Spinner, ErrorBox } from '../../../components/ui'

const INPUT = 'w-full rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2 text-sm focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-1 focus:ring-brand-500'
const LABEL = 'mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-500'
const SECTION = 'mb-2 mt-5 text-xs font-semibold uppercase tracking-wide text-slate-400'

const BUSINESS_TYPES = [
  'Construction', 'Transportation', 'Oil & Gas', 'Real Estate', 'Retail',
  'Healthcare', 'Education', 'Government', 'Technology', 'Manufacturing',
  'Hospitality', 'Agriculture', 'Financial Services', 'Telecommunications',
  'Logistics', 'Mining', 'Water & Utilities', 'Food & Beverage', 'Other',
]

const REGIONS = [
  'Riyadh', 'Makkah', 'Eastern Province', 'Madinah', 'Qassim',
  'Asir', 'Tabuk', 'Hail', 'Northern Borders', 'Jazan',
  'Najran', 'Al Bahah', 'Al Jawf',
]

function Field({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <div>
      <div className="text-xs text-slate-500">{label}</div>
      <div className="mt-0.5 text-sm font-medium text-slate-900">{value || '—'}</div>
    </div>
  )
}

export default function AccountDetailPage() {
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const [data, setData] = useState<AccountDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [editing, setEditing] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveMsg, setSaveMsg] = useState<string | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)

  // Edit fields
  const [natureOfBusiness, setNatureOfBusiness] = useState('')
  const [customerContactNameEn, setCustomerContactNameEn] = useState('')
  const [customerContactNameAr, setCustomerContactNameAr] = useState('')
  const [customerContactPosition, setCustomerContactPosition] = useState('')
  const [customerContactMobile, setCustomerContactMobile] = useState('')
  const [customerContactEmail, setCustomerContactEmail] = useState('')
  const [accountHolderNameEn, setAccountHolderNameEn] = useState('')
  const [accountHolderNameAr, setAccountHolderNameAr] = useState('')
  const [accountHolderPosition, setAccountHolderPosition] = useState('')
  const [accountHolderMobile, setAccountHolderMobile] = useState('')
  const [accountHolderEmail, setAccountHolderEmail] = useState('')
  const [street, setStreet] = useState('')
  const [city, setCity] = useState('')
  const [region, setRegion] = useState('')
  const [postalCode, setPostalCode] = useState('')
  const [country, setCountry] = useState('')

  function populateFields(d: AccountDetail) {
    setNatureOfBusiness(d.natureOfBusiness ?? '')
    setCustomerContactNameEn(d.customerContactNameEn ?? '')
    setCustomerContactNameAr(d.customerContactNameAr ?? '')
    setCustomerContactPosition(d.customerContactPosition ?? '')
    setCustomerContactMobile(d.customerContactMobile ?? '')
    setCustomerContactEmail(d.customerContactEmail ?? '')
    setAccountHolderNameEn(d.accountHolderNameEn ?? '')
    setAccountHolderNameAr(d.accountHolderNameAr ?? '')
    setAccountHolderPosition(d.accountHolderPosition ?? '')
    setAccountHolderMobile(d.accountHolderMobile ?? '')
    setAccountHolderEmail(d.accountHolderEmail ?? '')
    setStreet(d.street ?? '')
    setCity(d.city ?? '')
    setRegion(d.region ?? '')
    setPostalCode(d.postalCode ?? '')
    setCountry(d.country ?? '')
  }

  async function load() {
    setLoading(true); setError(null)
    try {
      const d = await bff.getAccountById(id)
      setData(d)
      populateFields(d)
    } catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => { void load() }, [id])

  async function handleSave() {
    setSaving(true); setSaveMsg(null)
    try {
      const body: UpdateAccountRequest = {
        ...(natureOfBusiness ? { natureOfBusiness } : {}),
        ...(customerContactNameEn ? { customerContactNameEn } : {}),
        ...(customerContactNameAr ? { customerContactNameAr } : {}),
        ...(customerContactPosition ? { customerContactPosition } : {}),
        ...(customerContactMobile ? { customerContactMobile } : {}),
        ...(customerContactEmail ? { customerContactEmail } : {}),
        ...(accountHolderNameEn ? { accountHolderNameEn } : {}),
        ...(accountHolderNameAr ? { accountHolderNameAr } : {}),
        ...(accountHolderPosition ? { accountHolderPosition } : {}),
        ...(accountHolderMobile ? { accountHolderMobile } : {}),
        ...(accountHolderEmail ? { accountHolderEmail } : {}),
        ...(street ? { street } : {}),
        ...(city ? { city } : {}),
        ...(region ? { region } : {}),
        ...(postalCode ? { postalCode } : {}),
        ...(country ? { country } : {}),
      }
      await bff.updateAccount(id, body, crypto.randomUUID())
      setSaveMsg('Account updated successfully.')
      setEditing(false)
      await load()
    } catch (e) { setSaveMsg((e as Error).message) }
    finally { setSaving(false) }
  }

  async function handleDelete() {
    try {
      await bff.deleteAccount(id, crypto.randomUUID())
      router.push('/accounts')
    } catch (e) {
      alert((e as Error).message)
    }
  }

  if (loading) return <Spinner label="Loading account..." />
  if (error) return <ErrorBox message={error} onRetry={load} retryLabel="Retry" />
  if (!data) return <p className="text-sm text-slate-500">Account not found.</p>

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <PageHeader
        title={`Account — ${data.customerDisplayName}`}
        subtitle={`${data.natureOfBusiness || 'No business type'} | ACC-${data.displayId}`}
        action={
          <div className="flex gap-2">
            <Link href={`/customers/${data.customerId}`} className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
              View Customer
            </Link>
            <SecondaryButton onClick={() => router.back()}>Back</SecondaryButton>
          </div>
        }
      />

      {/* Header card */}
      <Card className="p-4">
        <div className="grid grid-cols-2 gap-4 text-sm md:grid-cols-4">
          <div>
            <span className="text-xs text-slate-500">Customer</span>
            <p className="font-semibold text-slate-900">{data.customerDisplayName}</p>
          </div>
          <div>
            <span className="text-xs text-slate-500">Nature of Business</span>
            <p className="font-medium text-slate-900">{data.natureOfBusiness || '—'}</p>
          </div>
          <div>
            <span className="text-xs text-slate-500">City</span>
            <p className="font-medium text-slate-900">{data.city || '—'}</p>
          </div>
          <div>
            <span className="text-xs text-slate-500">Status</span>
            <p className={`font-semibold ${data.status === 'Active' ? 'text-green-700' : 'text-slate-500'}`}>{data.status}</p>
          </div>
        </div>
      </Card>

      {saveMsg && (
        <p className={`rounded-md px-3 py-2 text-sm ${saveMsg.includes('success') ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
          {saveMsg}
        </p>
      )}

      {/* Detail / Edit */}
      <Card className="p-6">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-slate-700">Account Details</h3>
          {!editing ? (
            <SecondaryButton onClick={() => { populateFields(data); setEditing(true) }} className="text-xs">Edit</SecondaryButton>
          ) : (
            <div className="flex gap-2">
              <SecondaryButton onClick={() => setEditing(false)} className="text-xs">Cancel</SecondaryButton>
              <PrimaryButton onClick={handleSave} disabled={saving} className="text-xs">
                {saving ? 'Saving...' : 'Save Changes'}
              </PrimaryButton>
            </div>
          )}
        </div>

        {!editing ? (
          <div className="space-y-6">
            <div>
              <p className={SECTION}>Customer Company Contact</p>
              <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
                <Field label="Name (EN)" value={data.customerContactNameEn} />
                <Field label="Name (AR)" value={data.customerContactNameAr} />
                <Field label="Position" value={data.customerContactPosition} />
                <Field label="Mobile" value={data.customerContactMobile} />
                <Field label="Email" value={data.customerContactEmail} />
              </div>
            </div>
            <div>
              <p className={SECTION}>Our Company Account Holder</p>
              <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
                <Field label="Name (EN)" value={data.accountHolderNameEn} />
                <Field label="Name (AR)" value={data.accountHolderNameAr} />
                <Field label="Position" value={data.accountHolderPosition} />
                <Field label="Mobile" value={data.accountHolderMobile} />
                <Field label="Email" value={data.accountHolderEmail} />
              </div>
            </div>
            <div>
              <p className={SECTION}>Address</p>
              <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
                <Field label="Street" value={data.street} />
                <Field label="City" value={data.city} />
                <Field label="Region" value={data.region} />
                <Field label="Postal Code" value={data.postalCode} />
                <Field label="Country" value={data.country} />
              </div>
            </div>
            <div>
              <p className={SECTION}>Metadata</p>
              <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
                <Field label="Created" value={data.createdAtUtc.substring(0, 10)} />
                <Field label="Updated" value={data.updatedAtUtc.substring(0, 10)} />
              </div>
            </div>
          </div>
        ) : (
          <div className="space-y-6">
            <div>
              <p className={SECTION}>Nature of Business</p>
              <select value={natureOfBusiness} onChange={(e) => setNatureOfBusiness(e.target.value)} className={INPUT}>
                <option value="">Select...</option>
                {BUSINESS_TYPES.map((b) => (
                  <option key={b} value={b}>{b}</option>
                ))}
              </select>
            </div>

            <div>
              <p className={SECTION}>Customer Company Contact</p>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div>
                  <label className={LABEL}>Name (EN)</label>
                  <input value={customerContactNameEn} onChange={(e) => setCustomerContactNameEn(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>Name (AR)</label>
                  <input value={customerContactNameAr} onChange={(e) => setCustomerContactNameAr(e.target.value)} className={INPUT} dir="rtl" />
                </div>
                <div>
                  <label className={LABEL}>Position</label>
                  <input value={customerContactPosition} onChange={(e) => setCustomerContactPosition(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>Mobile</label>
                  <input type="tel" value={customerContactMobile} onChange={(e) => setCustomerContactMobile(e.target.value)} className={INPUT} />
                </div>
                <div className="md:col-span-2">
                  <label className={LABEL}>Email</label>
                  <input type="email" value={customerContactEmail} onChange={(e) => setCustomerContactEmail(e.target.value)} className={INPUT} />
                </div>
              </div>
            </div>

            <div>
              <p className={SECTION}>Our Company Account Holder</p>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div>
                  <label className={LABEL}>Name (EN)</label>
                  <input value={accountHolderNameEn} onChange={(e) => setAccountHolderNameEn(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>Name (AR)</label>
                  <input value={accountHolderNameAr} onChange={(e) => setAccountHolderNameAr(e.target.value)} className={INPUT} dir="rtl" />
                </div>
                <div>
                  <label className={LABEL}>Position</label>
                  <input value={accountHolderPosition} onChange={(e) => setAccountHolderPosition(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>Mobile</label>
                  <input type="tel" value={accountHolderMobile} onChange={(e) => setAccountHolderMobile(e.target.value)} className={INPUT} />
                </div>
                <div className="md:col-span-2">
                  <label className={LABEL}>Email</label>
                  <input type="email" value={accountHolderEmail} onChange={(e) => setAccountHolderEmail(e.target.value)} className={INPUT} />
                </div>
              </div>
            </div>

            <div>
              <p className={SECTION}>Address</p>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div className="md:col-span-2">
                  <label className={LABEL}>Street</label>
                  <input value={street} onChange={(e) => setStreet(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>City</label>
                  <input value={city} onChange={(e) => setCity(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>Region</label>
                  <select value={region} onChange={(e) => setRegion(e.target.value)} className={INPUT}>
                    <option value="">Select region...</option>
                    {REGIONS.map((r) => (
                      <option key={r} value={r}>{r}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className={LABEL}>Postal Code</label>
                  <input value={postalCode} onChange={(e) => setPostalCode(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>Country</label>
                  <input value={country} onChange={(e) => setCountry(e.target.value)} className={INPUT} />
                </div>
              </div>
            </div>
          </div>
        )}
      </Card>

      {/* Delete section */}
      <Card className="p-4">
        <h3 className="mb-3 text-sm font-semibold text-red-700">Danger Zone</h3>
        {!confirmDelete ? (
          <button type="button" onClick={() => setConfirmDelete(true)}
            className="rounded-md border border-red-300 bg-white px-3 py-1.5 text-xs font-medium text-red-700 hover:bg-red-50">
            Delete Account
          </button>
        ) : (
          <div className="flex items-center gap-3">
            <span className="text-sm text-red-700">Are you sure? This cannot be undone.</span>
            <button type="button" onClick={handleDelete}
              className="rounded-md bg-red-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-700">
              Confirm Delete
            </button>
            <button type="button" onClick={() => setConfirmDelete(false)}
              className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs text-slate-600 hover:bg-slate-50">
              Cancel
            </button>
          </div>
        )}
      </Card>
    </div>
  )
}
