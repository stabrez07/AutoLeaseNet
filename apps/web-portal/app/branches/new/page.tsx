'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type CreateBranchRequest } from '../../../lib/bff-client'
import { Card, PageHeader, PrimaryButton, SecondaryButton } from '../../../components/ui'

const INPUT = 'w-full rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2 text-sm focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-1 focus:ring-brand-500'
const LABEL = 'mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-500'
const SECTION = 'col-span-full mb-2 mt-5 text-xs font-semibold uppercase tracking-wide text-slate-400'

export default function NewBranchPage() {
  const { t } = useLocale()
  const router = useRouter()
  const f = t.crudBranches.fields
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [code, setCode] = useState('')
  const [nameEn, setNameEn] = useState('')
  const [nameAr, setNameAr] = useState('')
  const [cityEn, setCityEn] = useState('')
  const [cityAr, setCityAr] = useState('')
  const [regionEn, setRegionEn] = useState('')
  const [regionAr, setRegionAr] = useState('')
  const [address, setAddress] = useState('')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [licenseNumber, setLicenseNumber] = useState('')
  const [latitude, setLatitude] = useState('')
  const [longitude, setLongitude] = useState('')
  const [tajeerBranchId, setTajeerBranchId] = useState('')
  const [tajeerOperatorId, setTajeerOperatorId] = useState('')

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault(); setError(null); setSaving(true)
    const key = crypto.randomUUID()
    try {
      const body: CreateBranchRequest = {
        code, nameEn, nameAr,
        cityEn: cityEn || undefined, cityAr: cityAr || undefined,
        regionEn: regionEn || undefined, regionAr: regionAr || undefined,
        address: address || undefined, phoneNumber: phoneNumber || undefined,
        licenseNumber: licenseNumber || undefined,
        latitude: latitude ? Number(latitude) : undefined,
        longitude: longitude ? Number(longitude) : undefined,
        tajeerBranchId: Number(tajeerBranchId),
        tajeerOperatorId: Number(tajeerOperatorId),
      }
      const res = await bff.createBranch(body, key)
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
      router.push(res.branchId ? `/branches/${res.branchId}` : '/branches')
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <PageHeader title={t.crudBranches.newTitle}
        action={<SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>}
      />
      <form onSubmit={handleSubmit}>
        <Card className="p-6">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <p className={SECTION}>{t.crudBranches.sections.identity}</p>
            <div>
              <label className={LABEL}>{f.code}</label>
              <input required value={code} onChange={(e) => setCode(e.target.value)} className={INPUT} placeholder="RUH-01" />
            </div>
            <div>
              <label className={LABEL}>{f.licenseNumber}</label>
              <input value={licenseNumber} onChange={(e) => setLicenseNumber(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.nameEn}</label>
              <input required value={nameEn} onChange={(e) => setNameEn(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.nameAr}</label>
              <input required value={nameAr} onChange={(e) => setNameAr(e.target.value)} className={INPUT} dir="rtl" />
            </div>

            <p className={SECTION}>{t.crudBranches.sections.location}</p>
            <div>
              <label className={LABEL}>{f.cityEn}</label>
              <input value={cityEn} onChange={(e) => setCityEn(e.target.value)} className={INPUT} placeholder="Riyadh" />
            </div>
            <div>
              <label className={LABEL}>{f.cityAr}</label>
              <input value={cityAr} onChange={(e) => setCityAr(e.target.value)} className={INPUT} dir="rtl" placeholder="الرياض" />
            </div>
            <div>
              <label className={LABEL}>{f.regionEn}</label>
              <input value={regionEn} onChange={(e) => setRegionEn(e.target.value)} className={INPUT} placeholder="Riyadh Region" />
            </div>
            <div>
              <label className={LABEL}>{f.regionAr}</label>
              <input value={regionAr} onChange={(e) => setRegionAr(e.target.value)} className={INPUT} dir="rtl" />
            </div>
            <div className="md:col-span-2">
              <label className={LABEL}>{f.address}</label>
              <input value={address} onChange={(e) => setAddress(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.phoneNumber}</label>
              <input type="tel" value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} className={INPUT} />
            </div>
            <div />
            <div>
              <label className={LABEL}>{f.latitude}</label>
              <input type="number" step="any" value={latitude} onChange={(e) => setLatitude(e.target.value)} className={INPUT} placeholder="24.6877" />
            </div>
            <div>
              <label className={LABEL}>{f.longitude}</label>
              <input type="number" step="any" value={longitude} onChange={(e) => setLongitude(e.target.value)} className={INPUT} placeholder="46.7219" />
            </div>

            <p className={SECTION}>{t.crudBranches.sections.tajeer}</p>
            <div>
              <label className={LABEL}>{f.tajeerBranchId}</label>
              <input required type="number" min="1" value={tajeerBranchId} onChange={(e) => setTajeerBranchId(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.tajeerOperatorId}</label>
              <input required type="number" min="1" value={tajeerOperatorId} onChange={(e) => setTajeerOperatorId(e.target.value)} className={INPUT} />
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
