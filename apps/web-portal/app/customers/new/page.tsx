'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { bff, type CreateCustomerB2BRequest } from '../../../lib/bff-client'
import { Card, PageHeader, PrimaryButton, SecondaryButton } from '../../../components/ui'

const INPUT = 'w-full rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2 text-sm focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-1 focus:ring-brand-500'
const LABEL = 'mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-500'
const SECTION = 'mb-2 mt-5 text-xs font-semibold uppercase tracking-wide text-slate-400'

const INDUSTRY_OPTIONS = [
  'Construction', 'Transportation', 'Oil & Gas', 'Real Estate', 'Retail',
  'Healthcare', 'Education', 'Government', 'Technology', 'Manufacturing',
  'Hospitality', 'Agriculture', 'Financial Services', 'Telecommunications', 'Other',
]

const CURRENCY_OPTIONS = ['SAR', 'USD', 'AED', 'EUR']

export default function NewCustomerPage() {
  const router = useRouter()
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [legalName, setLegalName] = useState('')
  const [legalNameAr, setLegalNameAr] = useState('')
  const [commercialReg, setCommercialReg] = useState('')
  const [vatNumber, setVatNumber] = useState('')
  const [creditLimit, setCreditLimit] = useState('')
  const [creditCurrency, setCreditCurrency] = useState('SAR')
  const [industry, setIndustry] = useState('')
  const [paymentTermsDays, setPaymentTermsDays] = useState('30')

  const [email, setEmail] = useState('')
  const [mobile, setMobile] = useState('')
  const [nationalAddress, setNationalAddress] = useState('')
  const [billingAddress, setBillingAddress] = useState('')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSaving(true)
    try {
      const body: CreateCustomerB2BRequest = {
        legalName,
        ...(legalNameAr ? { legalNameAr } : {}),
        commercialRegistration: commercialReg,
        ...(vatNumber ? { vatNumber } : {}),
        ...(email ? { email } : {}),
        ...(mobile ? { mobile } : {}),
        ...(nationalAddress ? { nationalAddress } : {}),
        ...(billingAddress ? { billingAddress } : {}),
        ...(creditLimit ? { creditLimit: Number(creditLimit) } : {}),
        ...(creditCurrency ? { creditCurrency } : {}),
      }
      const res = await bff.createCustomerB2B(body, crypto.randomUUID())
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
      router.push(res.customerId ? `/customers/${res.customerId}` : '/customers')
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <PageHeader
        title="New Customer (B2B)"
        subtitle="Register a new corporate fleet account."
        action={
          <SecondaryButton onClick={() => router.back()}>Back</SecondaryButton>
        }
      />

      <form onSubmit={handleSubmit}>
        <Card className="space-y-4 p-6">
          <p className={SECTION}>Company Identity</p>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className={LABEL}>Legal Name (EN) *</label>
              <input required value={legalName} onChange={(e) => setLegalName(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>Legal Name (AR)</label>
              <input value={legalNameAr} onChange={(e) => setLegalNameAr(e.target.value)} className={INPUT} dir="rtl" />
            </div>
            <div>
              <label className={LABEL}>Commercial Registration *</label>
              <input required value={commercialReg} onChange={(e) => setCommercialReg(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>VAT Number</label>
              <input value={vatNumber} onChange={(e) => setVatNumber(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>Industry</label>
              <select value={industry} onChange={(e) => setIndustry(e.target.value)} className={INPUT}>
                <option value="">Select industry...</option>
                {INDUSTRY_OPTIONS.map((ind) => (
                  <option key={ind} value={ind}>{ind}</option>
                ))}
              </select>
            </div>
          </div>

          <p className={SECTION}>Financial</p>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <div>
              <label className={LABEL}>Credit Limit</label>
              <input type="number" min="0" step="0.01" value={creditLimit} onChange={(e) => setCreditLimit(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>Currency</label>
              <select value={creditCurrency} onChange={(e) => setCreditCurrency(e.target.value)} className={INPUT}>
                {CURRENCY_OPTIONS.map((c) => (
                  <option key={c} value={c}>{c}</option>
                ))}
              </select>
            </div>
            <div>
              <label className={LABEL}>Payment Terms (Days)</label>
              <select value={paymentTermsDays} onChange={(e) => setPaymentTermsDays(e.target.value)} className={INPUT}>
                {[15, 30, 45, 60, 90].map((d) => (
                  <option key={d} value={d}>{d} days</option>
                ))}
              </select>
            </div>
          </div>

          <p className={SECTION}>Contact</p>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className={LABEL}>Email</label>
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>Mobile</label>
              <input type="tel" value={mobile} onChange={(e) => setMobile(e.target.value)} className={INPUT} placeholder="+966 5x xxx xxxx" />
            </div>
            <div className="md:col-span-2">
              <label className={LABEL}>National Address</label>
              <input value={nationalAddress} onChange={(e) => setNationalAddress(e.target.value)} className={INPUT} />
            </div>
            <div className="md:col-span-2">
              <label className={LABEL}>Billing Address</label>
              <input value={billingAddress} onChange={(e) => setBillingAddress(e.target.value)} className={INPUT} />
            </div>
          </div>

          {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

          <div className="flex justify-end gap-3 border-t border-slate-100 pt-4">
            <SecondaryButton type="button" onClick={() => router.back()}>Cancel</SecondaryButton>
            <PrimaryButton type="submit" disabled={saving} className="px-6">
              {saving ? 'Creating...' : 'Create Customer'}
            </PrimaryButton>
          </div>
        </Card>
      </form>
    </div>
  )
}
