'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type CreateDriverRequest } from '../../../lib/bff-client'
import { Card, PageHeader, PrimaryButton, SecondaryButton } from '../../../components/ui'

const INPUT = 'w-full rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2 text-sm focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-1 focus:ring-brand-500'
const LABEL = 'mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-500'
const SECTION = 'col-span-full mb-2 mt-5 text-xs font-semibold uppercase tracking-wide text-slate-400'

export default function NewDriverPage() {
  const { t } = useLocale()
  const router = useRouter()
  const f = t.crudDrivers.fields
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [personNameEn, setPersonNameEn] = useState('')
  const [personNameAr, setPersonNameAr] = useState('')
  const [idTypeCode, setIdTypeCode] = useState(1)
  const [personIdNumber, setPersonIdNumber] = useState('')
  const [dateOfBirth, setDateOfBirth] = useState('')
  const [nationalityCode, setNationalityCode] = useState('')
  const [licenseNumber, setLicenseNumber] = useState('')
  const [licenseClass, setLicenseClass] = useState(2)
  const [licenseExpiry, setLicenseExpiry] = useState('')
  const [mobile, setMobile] = useState('')
  const [email, setEmail] = useState('')
  const [nationalAddress, setNationalAddress] = useState('')
  const [customerId, setCustomerId] = useState('')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault(); setError(null); setSaving(true)
    const key = crypto.randomUUID()
    try {
      const body: CreateDriverRequest = {
        personNameEn, personNameAr: personNameAr || undefined,
        idTypeCode, personIdNumber,
        dateOfBirth: dateOfBirth || undefined,
        nationalityCode: nationalityCode || undefined,
        driverLicenseNumber: licenseNumber, licenseClass, licenseExpiryDate: licenseExpiry,
        mobile: mobile || undefined, email: email || undefined,
        nationalAddress: nationalAddress || undefined,
        customerId: customerId || undefined,
      }
      const res = await bff.createDriver(body, key)
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
      router.push(res.driverId ? `/drivers/${res.driverId}` : '/drivers')
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <PageHeader title={t.crudDrivers.newTitle}
        action={<SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>}
      />
      <form onSubmit={handleSubmit}>
        <Card className="p-6">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <p className={SECTION}>{t.crudDrivers.sections.identity}</p>
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
                {Object.entries(t.crudDrivers.idTypes).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
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

            <p className={SECTION}>{t.crudDrivers.sections.license}</p>
            <div>
              <label className={LABEL}>{f.licenseNumber}</label>
              <input required value={licenseNumber} onChange={(e) => setLicenseNumber(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.licenseClass}</label>
              <select required value={licenseClass} onChange={(e) => setLicenseClass(Number(e.target.value))} className={INPUT}>
                {Object.entries(t.crudDrivers.licenseClasses).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
            </div>
            <div>
              <label className={LABEL}>{f.licenseExpiry}</label>
              <input required type="date" value={licenseExpiry} onChange={(e) => setLicenseExpiry(e.target.value)} className={INPUT} />
            </div>

            <p className={SECTION}>{t.crudDrivers.sections.contact}</p>
            <div>
              <label className={LABEL}>{f.mobile}</label>
              <input type="tel" value={mobile} onChange={(e) => setMobile(e.target.value)} className={INPUT} placeholder="+966 5x xxx xxxx" />
            </div>
            <div>
              <label className={LABEL}>{f.email}</label>
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} className={INPUT} />
            </div>
            <div className="md:col-span-2">
              <label className={LABEL}>{f.nationalAddress}</label>
              <input value={nationalAddress} onChange={(e) => setNationalAddress(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.customerId}</label>
              <input value={customerId} onChange={(e) => setCustomerId(e.target.value)} className={INPUT} placeholder="UUID (optional)" />
            </div>
          </div>

          {error && <p className="mt-4 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

          <div className="mt-6 flex justify-end gap-3 border-t border-slate-100 pt-4">
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
