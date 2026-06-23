'use client'

import { Suspense, useEffect, useState } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import { bff, type CreateAccountRequest, type CustomerSummary } from '../../../lib/bff-client'
import { Card, PageHeader, PrimaryButton, SecondaryButton } from '../../../components/ui'

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

export default function NewAccountPageWrapper() {
  return (
    <Suspense fallback={<div className="p-6 text-sm text-slate-500">Loading...</div>}>
      <NewAccountPage />
    </Suspense>
  )
}

function NewAccountPage() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const preselectedCustomerId = searchParams.get('customerId') ?? ''

  const [customers, setCustomers] = useState<CustomerSummary[]>([])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [customerId, setCustomerId] = useState(preselectedCustomerId)
  const [natureOfBusiness, setNatureOfBusiness] = useState('')

  // Customer contact
  const [customerContactNameEn, setCustomerContactNameEn] = useState('')
  const [customerContactNameAr, setCustomerContactNameAr] = useState('')
  const [customerContactPosition, setCustomerContactPosition] = useState('')
  const [customerContactMobile, setCustomerContactMobile] = useState('')
  const [customerContactEmail, setCustomerContactEmail] = useState('')

  // Our account holder
  const [accountHolderNameEn, setAccountHolderNameEn] = useState('')
  const [accountHolderNameAr, setAccountHolderNameAr] = useState('')
  const [accountHolderPosition, setAccountHolderPosition] = useState('')
  const [accountHolderMobile, setAccountHolderMobile] = useState('')
  const [accountHolderEmail, setAccountHolderEmail] = useState('')

  // Address
  const [street, setStreet] = useState('')
  const [city, setCity] = useState('')
  const [region, setRegion] = useState('')
  const [postalCode, setPostalCode] = useState('')
  const [country, setCountry] = useState('Saudi Arabia')

  useEffect(() => {
    bff.getCustomers(1, 200).then((r) => setCustomers(r.items.filter((c) => c.type === 1))).catch(() => {})
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    if (!customerId) { setError('Please select a customer.'); return }
    setSaving(true)
    try {
      const body: CreateAccountRequest = {
        customerId,
        ...(natureOfBusiness ? { natureOfBusiness } : {}),
        customerContactNameEn,
        ...(customerContactNameAr ? { customerContactNameAr } : {}),
        ...(customerContactPosition ? { customerContactPosition } : {}),
        ...(customerContactMobile ? { customerContactMobile } : {}),
        ...(customerContactEmail ? { customerContactEmail } : {}),
        accountHolderNameEn,
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
      const res = await bff.createAccount(body, crypto.randomUUID())
      router.push(`/accounts/${res.accountId}`)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <PageHeader
        title="New Account"
        subtitle="Register a new business relationship account."
        action={<SecondaryButton onClick={() => router.back()}>Back</SecondaryButton>}
      />

      <form onSubmit={handleSubmit}>
        <Card className="space-y-4 p-6">
          <p className={SECTION}>Customer</p>
          <div>
            <label className={LABEL}>Customer (B2B) *</label>
            <select required value={customerId} onChange={(e) => setCustomerId(e.target.value)} className={INPUT}>
              <option value="">Select customer...</option>
              {customers.map((c) => (
                <option key={c.id} value={c.id}>{c.displayName} ({c.commercialRegistration ?? 'N/A'})</option>
              ))}
            </select>
          </div>
          <div>
            <label className={LABEL}>Nature of Business</label>
            <select value={natureOfBusiness} onChange={(e) => setNatureOfBusiness(e.target.value)} className={INPUT}>
              <option value="">Select...</option>
              {BUSINESS_TYPES.map((b) => (
                <option key={b} value={b}>{b}</option>
              ))}
            </select>
          </div>

          <p className={SECTION}>Customer Company Contact</p>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className={LABEL}>Contact Name (EN) *</label>
              <input required value={customerContactNameEn} onChange={(e) => setCustomerContactNameEn(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>Contact Name (AR)</label>
              <input value={customerContactNameAr} onChange={(e) => setCustomerContactNameAr(e.target.value)} className={INPUT} dir="rtl" />
            </div>
            <div>
              <label className={LABEL}>Position</label>
              <input value={customerContactPosition} onChange={(e) => setCustomerContactPosition(e.target.value)} className={INPUT} placeholder="e.g. Fleet Manager" />
            </div>
            <div>
              <label className={LABEL}>Mobile</label>
              <input type="tel" value={customerContactMobile} onChange={(e) => setCustomerContactMobile(e.target.value)} className={INPUT} placeholder="+966 5x xxx xxxx" />
            </div>
            <div className="md:col-span-2">
              <label className={LABEL}>Email</label>
              <input type="email" value={customerContactEmail} onChange={(e) => setCustomerContactEmail(e.target.value)} className={INPUT} />
            </div>
          </div>

          <p className={SECTION}>Our Company Account Holder</p>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className={LABEL}>Account Holder Name (EN) *</label>
              <input required value={accountHolderNameEn} onChange={(e) => setAccountHolderNameEn(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>Account Holder Name (AR)</label>
              <input value={accountHolderNameAr} onChange={(e) => setAccountHolderNameAr(e.target.value)} className={INPUT} dir="rtl" />
            </div>
            <div>
              <label className={LABEL}>Position</label>
              <input value={accountHolderPosition} onChange={(e) => setAccountHolderPosition(e.target.value)} className={INPUT} placeholder="e.g. Account Manager" />
            </div>
            <div>
              <label className={LABEL}>Mobile</label>
              <input type="tel" value={accountHolderMobile} onChange={(e) => setAccountHolderMobile(e.target.value)} className={INPUT} placeholder="+966 5x xxx xxxx" />
            </div>
            <div className="md:col-span-2">
              <label className={LABEL}>Email</label>
              <input type="email" value={accountHolderEmail} onChange={(e) => setAccountHolderEmail(e.target.value)} className={INPUT} />
            </div>
          </div>

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
              <input value={postalCode} onChange={(e) => setPostalCode(e.target.value)} className={INPUT} maxLength={10} />
            </div>
            <div>
              <label className={LABEL}>Country</label>
              <input value={country} onChange={(e) => setCountry(e.target.value)} className={INPUT} />
            </div>
          </div>

          {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

          <div className="flex justify-end gap-3 border-t border-slate-100 pt-4">
            <SecondaryButton type="button" onClick={() => router.back()}>Cancel</SecondaryButton>
            <PrimaryButton type="submit" disabled={saving} className="px-6">
              {saving ? 'Creating...' : 'Create Account'}
            </PrimaryButton>
          </div>
        </Card>
      </form>
    </div>
  )
}
