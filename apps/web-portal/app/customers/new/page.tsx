'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type CreateCustomerB2BRequest, type CreateCustomerB2CRequest } from '../../../lib/bff-client'
import { Card, PageHeader, PrimaryButton, SecondaryButton } from '../../../components/ui'

type CustomerType = 'b2b' | 'b2c'

const INPUT = 'w-full rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2 text-sm focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-1 focus:ring-brand-500'
const LABEL = 'mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-500'
const SECTION = 'mb-2 mt-5 text-xs font-semibold uppercase tracking-wide text-slate-400'

export default function NewCustomerPage() {
  const { t } = useLocale()
  const router = useRouter()
  const f = t.crudCustomers.fields
  const [type, setType] = useState<CustomerType>('b2b')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // B2B fields
  const [legalName, setLegalName] = useState('')
  const [legalNameAr, setLegalNameAr] = useState('')
  const [commercialReg, setCommercialReg] = useState('')
  const [vatNumber, setVatNumber] = useState('')
  const [creditLimit, setCreditLimit] = useState('')
  const [creditCurrency, setCreditCurrency] = useState('SAR')

  // B2C fields
  const [personNameEn, setPersonNameEn] = useState('')
  const [personNameAr, setPersonNameAr] = useState('')
  const [idTypeCode, setIdTypeCode] = useState(1)
  const [personIdNumber, setPersonIdNumber] = useState('')
  const [dateOfBirth, setDateOfBirth] = useState('')
  const [nationalityCode, setNationalityCode] = useState('')

  // Shared
  const [email, setEmail] = useState('')
  const [mobile, setMobile] = useState('')
  const [nationalAddress, setNationalAddress] = useState('')
  const [billingAddress, setBillingAddress] = useState('')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setSaving(true)
    const key = crypto.randomUUID()
    try {
      if (type === 'b2b') {
        const body: CreateCustomerB2BRequest = {
          legalName, legalNameAr: legalNameAr || undefined,
          commercialRegistration: commercialReg, vatNumber: vatNumber || undefined,
          email: email || undefined, mobile: mobile || undefined,
          nationalAddress: nationalAddress || undefined, billingAddress: billingAddress || undefined,
          creditLimit: creditLimit ? Number(creditLimit) : undefined,
          creditCurrency: creditCurrency || undefined,
        }
        const res = await bff.createCustomerB2B(body, key)
        if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
        router.push(res.customerId ? `/customers/${res.customerId}` : '/customers')
      } else {
        const body: CreateCustomerB2CRequest = {
          personNameEn, personNameAr: personNameAr || undefined,
          idTypeCode, personIdNumber,
          dateOfBirth: dateOfBirth || undefined,
          nationalityCode: nationalityCode || undefined,
          email: email || undefined, mobile: mobile || undefined,
          nationalAddress: nationalAddress || undefined,
        }
        const res = await bff.createCustomerB2C(body, key)
        if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
        router.push(res.customerId ? `/customers/${res.customerId}` : '/customers')
      }
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <PageHeader
        title={t.crudCustomers.newTitle}
        action={
          <SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>
        }
      />

      {/* Type selector */}
      <Card className="flex gap-0 overflow-hidden p-0 shadow-sm">
        {(['b2b', 'b2c'] as CustomerType[]).map((tp) => (
          <button key={tp} type="button" onClick={() => setType(tp)}
            className={`flex-1 py-2.5 text-sm font-medium transition-colors ${
              type === tp ? 'bg-brand-700 text-white' : 'bg-white text-slate-600 hover:bg-brand-50'
            }`}>
            {tp === 'b2b' ? t.crudCustomers.typeB2B : t.crudCustomers.typeB2C}
          </button>
        ))}
      </Card>

      <form onSubmit={handleSubmit}>
        <Card className="space-y-4 p-6">
          {type === 'b2b' ? (
            <>
              <p className={SECTION}>{t.crudCustomers.sections.identity}</p>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div>
                  <label className={LABEL}>{f.legalName}</label>
                  <input required value={legalName} onChange={(e) => setLegalName(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>{f.legalNameAr}</label>
                  <input value={legalNameAr} onChange={(e) => setLegalNameAr(e.target.value)} className={INPUT} dir="rtl" />
                </div>
                <div>
                  <label className={LABEL}>{f.commercialReg}</label>
                  <input required value={commercialReg} onChange={(e) => setCommercialReg(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>{f.vatNumber}</label>
                  <input value={vatNumber} onChange={(e) => setVatNumber(e.target.value)} className={INPUT} />
                </div>
              </div>
              <p className={SECTION}>{t.crudCustomers.sections.financial}</p>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div>
                  <label className={LABEL}>{f.creditLimit}</label>
                  <input type="number" min="0" step="0.01" value={creditLimit} onChange={(e) => setCreditLimit(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>{f.creditCurrency}</label>
                  <input value={creditCurrency} onChange={(e) => setCreditCurrency(e.target.value)} className={INPUT} />
                </div>
              </div>
            </>
          ) : (
            <>
              <p className={SECTION}>{t.crudCustomers.sections.identity}</p>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div>
                  <label className={LABEL}>{f.personNameEn}</label>
                  <input required value={personNameEn} onChange={(e) => setPersonNameEn(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>{f.personNameAr}</label>
                  <input value={personNameAr} onChange={(e) => setPersonNameAr(e.target.value)} className={INPUT} dir="rtl" />
                </div>
                <div>
                  <label className={LABEL}>{f.idTypeCode}</label>
                  <select required value={idTypeCode} onChange={(e) => setIdTypeCode(Number(e.target.value))} className={INPUT}>
                    {Object.entries(t.crudCustomers.idTypes).map(([k, v]) => (
                      <option key={k} value={k}>{v}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className={LABEL}>{f.personIdNumber}</label>
                  <input required value={personIdNumber} onChange={(e) => setPersonIdNumber(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>{f.dateOfBirth}</label>
                  <input type="date" value={dateOfBirth} onChange={(e) => setDateOfBirth(e.target.value)} className={INPUT} />
                </div>
                <div>
                  <label className={LABEL}>{f.nationalityCode}</label>
                  <input maxLength={3} value={nationalityCode} onChange={(e) => setNationalityCode(e.target.value)} className={INPUT} placeholder="SAU" />
                </div>
              </div>
            </>
          )}

          <p className={SECTION}>{t.crudCustomers.sections.contact}</p>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className={LABEL}>{f.email}</label>
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.mobile}</label>
              <input type="tel" value={mobile} onChange={(e) => setMobile(e.target.value)} className={INPUT} placeholder="+966 5x xxx xxxx" />
            </div>
            <div className="md:col-span-2">
              <label className={LABEL}>{f.nationalAddress}</label>
              <input value={nationalAddress} onChange={(e) => setNationalAddress(e.target.value)} className={INPUT} />
            </div>
            {type === 'b2b' && (
              <div className="md:col-span-2">
                <label className={LABEL}>{f.billingAddress}</label>
                <input value={billingAddress} onChange={(e) => setBillingAddress(e.target.value)} className={INPUT} />
              </div>
            )}
          </div>

          {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

          <div className="flex justify-end gap-3 border-t border-slate-100 pt-4">
            <SecondaryButton type="button" onClick={() => router.back()}>
              {t.common.cancel}
            </SecondaryButton>
            <PrimaryButton type="submit" disabled={saving} className="px-6">
              {saving ? t.common.creating : t.common.create}
            </PrimaryButton>
          </div>
        </Card>
      </form>
    </div>
  )
}
