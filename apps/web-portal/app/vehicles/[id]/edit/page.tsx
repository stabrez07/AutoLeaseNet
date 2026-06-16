'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { useLocale } from '../../../../lib/locale-provider'
import { bff, type BranchDto, type UpdateVehicleRequest } from '../../../../lib/bff-client'
import { Card, ErrorBox, PageHeader, PrimaryButton, SecondaryButton, Spinner } from '../../../../components/ui'

const INPUT = 'w-full rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2 text-sm focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-1 focus:ring-brand-500'
const LABEL = 'mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-500'
const SECTION = 'col-span-full mb-2 mt-5 text-xs font-semibold uppercase tracking-wide text-slate-400'

export default function EditVehiclePage() {
  const { t } = useLocale()
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const f = t.crudVehicles.fields

  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [branches, setBranches] = useState<BranchDto[]>([])
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  // Editable fields (all mutable — plate/VIN are read-only after creation)
  const [make, setMake] = useState('')
  const [model, setModel] = useState('')
  const [modelYear, setModelYear] = useState<number>(new Date().getFullYear())
  const [color, setColor] = useState('')
  const [seats, setSeats] = useState(5)
  const [currentKm, setCurrentKm] = useState(0)
  const [currentBranchId, setCurrentBranchId] = useState('')
  const [licenseExpiry, setLicenseExpiry] = useState('')
  const [insuranceExpiry, setInsuranceExpiry] = useState('')
  const [inspectionExpiry, setInspectionExpiry] = useState('')
  const [insuranceCompany, setInsuranceCompany] = useState('')
  const [insurancePolicyNumber, setInsurancePolicyNumber] = useState('')
  const [purchasePrice, setPurchasePrice] = useState('')
  const [purchaseDate, setPurchaseDate] = useState('')
  const [notes, setNotes] = useState('')

  // Read-only display
  const [plateDisplay, setPlateDisplay] = useState('')
  const [vinDisplay, setVinDisplay] = useState('')

  useEffect(() => {
    setLoading(true); setLoadError(null)
    Promise.all([bff.getVehicleById(id), bff.getBranches()])
      .then(([v, bs]) => {
        setBranches(bs)
        setMake(v.make)
        setModel(v.model)
        setModelYear(v.modelYear)
        setColor(v.color ?? '')
        setSeats(v.seats)
        setCurrentKm(v.currentKm)
        setCurrentBranchId(v.currentBranchId)
        setLicenseExpiry(v.licenseExpiryDate ?? '')
        setInsuranceExpiry(v.insuranceExpiryDate ?? '')
        setInspectionExpiry(v.inspectionExpiryDate ?? '')
        setInsuranceCompany(v.insuranceCompany ?? '')
        setInsurancePolicyNumber(v.insurancePolicyNumber ?? '')
        setPurchasePrice(v.purchasePrice != null ? String(v.purchasePrice) : '')
        setPurchaseDate(v.purchaseDate ?? '')
        setNotes(v.notes ?? '')
        setPlateDisplay(`${v.plateLetters} ${v.plateNumber}`)
        setVinDisplay(v.vin)
      })
      .catch((e: Error) => setLoadError(e.message))
      .finally(() => setLoading(false))
  }, [id])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault(); setSaveError(null); setSaving(true)
    try {
      const body: UpdateVehicleRequest = {
        make: make || undefined,
        model: model || undefined,
        modelYear: modelYear || undefined,
        color: color || undefined,
        seats: seats || undefined,
        currentKm: currentKm,
        currentBranchId: currentBranchId || undefined,
        licenseExpiryDate: licenseExpiry || undefined,
        insuranceExpiryDate: insuranceExpiry || undefined,
        inspectionExpiryDate: inspectionExpiry || undefined,
        insuranceCompany: insuranceCompany || undefined,
        insurancePolicyNumber: insurancePolicyNumber || undefined,
        purchasePrice: purchasePrice ? Number(purchasePrice) : undefined,
        purchaseDate: purchaseDate || undefined,
        notes: notes || undefined,
      }
      const res = await bff.updateVehicle(id, body, crypto.randomUUID())
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
      router.push(`/vehicles/${id}`)
    } catch (err) {
      setSaveError((err as Error).message)
      setSaving(false)
    }
  }

  if (loading) return <Spinner label={t.common.loading} />
  if (loadError) return <ErrorBox message={loadError} retryLabel={t.common.retry} />

  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        title={t.crudVehicles.editTitle}
        subtitle={`${plateDisplay} · ${vinDisplay}`}
        action={<SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>}
      />
      <form onSubmit={handleSubmit}>
        <Card className="p-6">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">

            <p className={SECTION}>{t.crudVehicles.sections.specs}</p>

            <div>
              <label className={LABEL}>{f.make}</label>
              <input className={INPUT} value={make} onChange={(e) => setMake(e.target.value)} required />
            </div>
            <div>
              <label className={LABEL}>{f.model}</label>
              <input className={INPUT} value={model} onChange={(e) => setModel(e.target.value)} required />
            </div>
            <div>
              <label className={LABEL}>{f.modelYear}</label>
              <input type="number" className={INPUT} value={modelYear} onChange={(e) => setModelYear(Number(e.target.value))} required min={1990} max={2040} />
            </div>
            <div>
              <label className={LABEL}>{f.color}</label>
              <input className={INPUT} value={color} onChange={(e) => setColor(e.target.value)} />
            </div>
            <div>
              <label className={LABEL}>{f.seats}</label>
              <input type="number" className={INPUT} value={seats} onChange={(e) => setSeats(Number(e.target.value))} required min={1} max={60} />
            </div>
            <div>
              <label className={LABEL}>{f.currentKm}</label>
              <input type="number" className={INPUT} value={currentKm} onChange={(e) => setCurrentKm(Number(e.target.value))} required min={0} />
            </div>
            <div>
              <label className={LABEL}>{f.currentBranch}</label>
              <select className={INPUT} value={currentBranchId} onChange={(e) => setCurrentBranchId(e.target.value)}>
                <option value="">—</option>
                {branches.map((b) => <option key={b.id} value={b.id}>{b.code} — {b.nameEn}</option>)}
              </select>
            </div>

            <p className={SECTION}>{t.crudVehicles.sections.regulatory}</p>

            <div>
              <label className={LABEL}>{f.licenseExpiry}</label>
              <input type="date" className={INPUT} value={licenseExpiry} onChange={(e) => setLicenseExpiry(e.target.value)} />
            </div>
            <div>
              <label className={LABEL}>{f.insuranceExpiry}</label>
              <input type="date" className={INPUT} value={insuranceExpiry} onChange={(e) => setInsuranceExpiry(e.target.value)} />
            </div>
            <div>
              <label className={LABEL}>{f.inspectionExpiry}</label>
              <input type="date" className={INPUT} value={inspectionExpiry} onChange={(e) => setInspectionExpiry(e.target.value)} />
            </div>
            <div>
              <label className={LABEL}>{f.insuranceCompany}</label>
              <input className={INPUT} value={insuranceCompany} onChange={(e) => setInsuranceCompany(e.target.value)} />
            </div>
            <div>
              <label className={LABEL}>{f.insurancePolicyNumber}</label>
              <input className={INPUT} value={insurancePolicyNumber} onChange={(e) => setInsurancePolicyNumber(e.target.value)} />
            </div>

            <p className={SECTION}>{t.crudVehicles.sections.financial}</p>

            <div>
              <label className={LABEL}>{f.purchasePrice}</label>
              <input type="number" className={INPUT} value={purchasePrice} onChange={(e) => setPurchasePrice(e.target.value)} min={0} step="0.01" />
            </div>
            <div>
              <label className={LABEL}>{f.purchaseDate}</label>
              <input type="date" className={INPUT} value={purchaseDate} onChange={(e) => setPurchaseDate(e.target.value)} />
            </div>

            <div className="col-span-full">
              <label className={LABEL}>{f.notes}</label>
              <textarea
                className={`${INPUT} min-h-[80px] resize-y`}
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
              />
            </div>

            {saveError && <p className="col-span-full text-sm text-red-600">{saveError}</p>}

            <div className="col-span-full flex justify-end gap-2 pt-2">
              <SecondaryButton type="button" onClick={() => router.back()}>{t.common.cancel}</SecondaryButton>
              <PrimaryButton type="submit" disabled={saving}>
                {saving ? '…' : t.crudVehicles.actions.save}
              </PrimaryButton>
            </div>
          </div>
        </Card>
      </form>
    </div>
  )
}
