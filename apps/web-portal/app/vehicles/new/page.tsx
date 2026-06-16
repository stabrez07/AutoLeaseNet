'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type BranchDto, type CreateVehicleRequest } from '../../../lib/bff-client'
import { Card, PageHeader, PrimaryButton, SecondaryButton } from '../../../components/ui'

const INPUT = 'w-full rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2 text-sm focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-1 focus:ring-brand-500'
const LABEL = 'mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-500'
const SECTION = 'col-span-full mb-2 mt-5 text-xs font-semibold uppercase tracking-wide text-slate-400'

export default function NewVehiclePage() {
  const { t } = useLocale()
  const router = useRouter()
  const f = t.crudVehicles.fields
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Plate & ID
  const [plateNumber, setPlateNumber] = useState('')
  const [plateLetters, setPlateLetters] = useState('')
  const [plateTypeCode, setPlateTypeCode] = useState(1)
  const [vin, setVin] = useState('')
  const [engineNumber, setEngineNumber] = useState('')

  // Specs
  const [make, setMake] = useState('')
  const [model, setModel] = useState('')
  const [modelYear, setModelYear] = useState(new Date().getFullYear())
  const [color, setColor] = useState('')
  const [fuelType, setFuelType] = useState(1)
  const [transmissionType, setTransmissionType] = useState(1)
  const [bodyType, setBodyType] = useState(1)
  const [seats, setSeats] = useState(5)

  // Regulatory
  const [licenseExpiry, setLicenseExpiry] = useState('')
  const [insuranceExpiry, setInsuranceExpiry] = useState('')
  const [inspectionExpiry, setInspectionExpiry] = useState('')
  const [insuranceCompany, setInsuranceCompany] = useState('')
  const [insurancePolicyNumber, setInsurancePolicyNumber] = useState('')

  // Assignment
  const [ownerBranchId, setOwnerBranchId] = useState('')
  const [currentKm, setCurrentKm] = useState(0)

  // Financial
  const [purchasePrice, setPurchasePrice] = useState('')
  const [purchaseDate, setPurchaseDate] = useState('')

  useEffect(() => {
    bff.getBranches().then((bs) => {
      setBranches(bs)
      if (bs.length > 0 && !ownerBranchId) setOwnerBranchId(bs[0]!.id)
    }).catch(() => {})
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault(); setError(null); setSaving(true)
    const key = crypto.randomUUID()
    try {
      const body: CreateVehicleRequest = {
        plateNumber, plateLetters, plateTypeCode,
        vin, engineNumber: engineNumber || undefined,
        make, model, modelYear, color: color || undefined,
        fuelType, transmissionType, bodyType, seats,
        licenseExpiryDate: licenseExpiry || undefined,
        insuranceExpiryDate: insuranceExpiry || undefined,
        inspectionExpiryDate: inspectionExpiry || undefined,
        insuranceCompany: insuranceCompany || undefined,
        insurancePolicyNumber: insurancePolicyNumber || undefined,
        ownerBranchId,
        currentKm,
        purchasePrice: purchasePrice ? Number(purchasePrice) : undefined,
        purchaseDate: purchaseDate || undefined,
      }
      const res = await bff.createVehicle(body, key)
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
      router.push(res.vehicleId ? `/vehicles/${res.vehicleId}` : '/vehicles')
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <PageHeader title={t.crudVehicles.newTitle}
        action={<SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>}
      />
      <form onSubmit={handleSubmit}>
        <Card className="p-6">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <p className={SECTION}>{t.crudVehicles.sections.plate}</p>
            <div>
              <label className={LABEL}>{f.plateNumber}</label>
              <input required value={plateNumber} onChange={(e) => setPlateNumber(e.target.value)} className={INPUT} placeholder="1234" />
            </div>
            <div>
              <label className={LABEL}>{f.plateLetters}</label>
              <input required value={plateLetters} onChange={(e) => setPlateLetters(e.target.value)} className={INPUT} placeholder="أ ب ج" />
            </div>
            <div>
              <label className={LABEL}>{f.plateTypeCode}</label>
              <input required type="number" min="1" value={plateTypeCode} onChange={(e) => setPlateTypeCode(Number(e.target.value))} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.vin}</label>
              <input required value={vin} onChange={(e) => setVin(e.target.value)} className={INPUT} maxLength={17} />
            </div>
            <div>
              <label className={LABEL}>{f.engineNumber}</label>
              <input value={engineNumber} onChange={(e) => setEngineNumber(e.target.value)} className={INPUT} />
            </div>

            <p className={SECTION}>{t.crudVehicles.sections.specs}</p>
            <div>
              <label className={LABEL}>{f.make}</label>
              <input required value={make} onChange={(e) => setMake(e.target.value)} className={INPUT} placeholder="Toyota" />
            </div>
            <div>
              <label className={LABEL}>{f.model}</label>
              <input required value={model} onChange={(e) => setModel(e.target.value)} className={INPUT} placeholder="Camry" />
            </div>
            <div>
              <label className={LABEL}>{f.modelYear}</label>
              <input required type="number" min="2000" max="2030" value={modelYear} onChange={(e) => setModelYear(Number(e.target.value))} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.color}</label>
              <input value={color} onChange={(e) => setColor(e.target.value)} className={INPUT} placeholder="White" />
            </div>
            <div>
              <label className={LABEL}>{f.fuelType}</label>
              <select value={fuelType} onChange={(e) => setFuelType(Number(e.target.value))} className={INPUT}>
                {Object.entries(t.crudVehicles.fuelTypes).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
            </div>
            <div>
              <label className={LABEL}>{f.transmissionType}</label>
              <select value={transmissionType} onChange={(e) => setTransmissionType(Number(e.target.value))} className={INPUT}>
                {Object.entries(t.crudVehicles.transmissionTypes).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
            </div>
            <div>
              <label className={LABEL}>{f.bodyType}</label>
              <select value={bodyType} onChange={(e) => setBodyType(Number(e.target.value))} className={INPUT}>
                {Object.entries(t.crudVehicles.bodyTypes).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
            </div>
            <div>
              <label className={LABEL}>{f.seats}</label>
              <input required type="number" min="1" max="60" value={seats} onChange={(e) => setSeats(Number(e.target.value))} className={INPUT} />
            </div>

            <p className={SECTION}>{t.crudVehicles.sections.regulatory}</p>
            <div>
              <label className={LABEL}>{f.licenseExpiry}</label>
              <input type="date" value={licenseExpiry} onChange={(e) => setLicenseExpiry(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.insuranceExpiry}</label>
              <input type="date" value={insuranceExpiry} onChange={(e) => setInsuranceExpiry(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.inspectionExpiry}</label>
              <input type="date" value={inspectionExpiry} onChange={(e) => setInspectionExpiry(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.insuranceCompany}</label>
              <input value={insuranceCompany} onChange={(e) => setInsuranceCompany(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.insurancePolicyNumber}</label>
              <input value={insurancePolicyNumber} onChange={(e) => setInsurancePolicyNumber(e.target.value)} className={INPUT} />
            </div>

            <p className={SECTION}>{t.crudVehicles.sections.financial}</p>
            <div>
              <label className={LABEL}>{f.ownerBranch}</label>
              <select required value={ownerBranchId} onChange={(e) => setOwnerBranchId(e.target.value)} className={INPUT}>
                <option value="">— Select —</option>
                {branches.map((b) => <option key={b.id} value={b.id}>{b.code} — {b.nameEn}</option>)}
              </select>
            </div>
            <div>
              <label className={LABEL}>{f.currentKm}</label>
              <input required type="number" min="0" value={currentKm} onChange={(e) => setCurrentKm(Number(e.target.value))} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.purchasePrice}</label>
              <input type="number" min="0" step="0.01" value={purchasePrice} onChange={(e) => setPurchasePrice(e.target.value)} className={INPUT} />
            </div>
            <div>
              <label className={LABEL}>{f.purchaseDate}</label>
              <input type="date" value={purchaseDate} onChange={(e) => setPurchaseDate(e.target.value)} className={INPUT} />
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
