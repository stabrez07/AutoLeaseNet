'use client'

import { useEffect, useMemo, useState } from 'react'
import {
  buildDummyQuotationPricingSetupData,
  type CalendarPeriodSetupRow,
  type CommissionRateSetupRow,
  type DepreciationSetupRow,
  type DiscountOptionSetupRow,
  type FeeCode,
  type FeeMasterSetupRow,
  type InsuranceSetupRow,
  type InterestRateSetupRow,
  type LeaseTermSetupRow,
  type MaintenanceSetupRow,
  type ProfitMarginSetupRow,
  type QuotationPricingSetupData,
  type QuotationPricingVehicleProfile,
  type ReplacementPolicySetupRow,
  type ResidualValueSetupRow,
  type TrackingChargeSetupRow,
  type VehicleInterestSetupRow,
} from '../../lib/quotation-pricing-catalog'
import {
  loadOrSeedQuotationPricingSetup,
  loadQuotationPricingSetupWithLocalFallback,
  saveQuotationPricingSetupToApi,
} from '../../lib/quotation-pricing-setup-api'
import { Card, PageHeader, PrimaryButton, SecondaryButton } from '../../components/ui'

interface InvoicingRules {
  billingDay: number
  paymentTermsDays: number
  vatRate: number
  latePenaltyRate: number
  autoGenerate: boolean
}

interface CompanyProfile {
  companyName: string
  crNo: string
  vatNo: string
  phone: string
  email: string
}

interface NotificationRules {
  sendInvoiceEmail: boolean
  overdueReminderDays: number
}

const DEFAULT_INVOICING: InvoicingRules = {
  billingDay: 1,
  paymentTermsDays: 10,
  vatRate: 15,
  latePenaltyRate: 2,
  autoGenerate: false,
}

const DEFAULT_COMPANY: CompanyProfile = {
  companyName: 'Auto Lead Company',
  crNo: '1010012345',
  vatNo: '300123456789003',
  phone: '+966 11 234 5678',
  email: 'info@autolead.com.sa',
}

const DEFAULT_NOTIFICATIONS: NotificationRules = {
  sendInvoiceEmail: true,
  overdueReminderDays: 7,
}

type MainTab = 'pricing' | 'invoicing' | 'company' | 'notifications'
type PricingTab =
  | 'vehicles'
  | 'insurance'
  | 'interest'
  | 'depreciation'
  | 'maintenance'
  | 'discount'
  | 'tracking'
  | 'leaseTerms'
  | 'interestRateTable'
  | 'residualValueTable'
  | 'replacementPolicy'
  | 'feeMaster'
  | 'commissionRates'
  | 'profitMargin'
  | 'calendarPeriods'

function parseCsvRows(content: string): string[][] {
  return content
    .split(/\r?\n/)
    .map((x) => x.trim())
    .filter((x) => x.length > 0)
    .map((line) => line.split(',').map((v) => v.trim()))
}

function toNum(v: string | undefined, fallback = 0): number {
  const n = Number(v ?? '')
  return Number.isFinite(n) ? n : fallback
}

function toBool(v: string): boolean {
  const x = v.toLowerCase()
  return x === 'true' || x === '1' || x === 'yes'
}

function parseCsvWithHeader<T>(
  content: string,
  map: (r: Record<string, string>, i: number) => T,
): T[] {
  const rows = parseCsvRows(content)
  if (rows.length < 2) return []
  const headers = rows[0]!
  return rows.slice(1).map((cells, i) => {
    const row: Record<string, string> = {}
    for (let c = 0; c < headers.length; c++) {
      row[headers[c]!.toLowerCase()] = cells[c] ?? ''
    }
    return map(row, i)
  })
}

function CsvUpload({ onUploaded, title }: { onUploaded: (text: string) => void; title: string }) {
  return (
    <label className="hover:border-brand-400 inline-flex cursor-pointer items-center rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs text-slate-700">
      {title}
      <input
        type="file"
        accept=".csv,text/csv"
        className="hidden"
        onChange={async (e) => {
          const file = e.target.files?.[0]
          if (!file) return
          const text = await file.text()
          onUploaded(text)
          e.target.value = ''
        }}
      />
    </label>
  )
}

function TxtInput({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <input
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="w-full rounded border border-slate-300 px-2 py-1 text-xs"
    />
  )
}

function NumInput({
  value,
  onChange,
  min,
  max,
  step,
}: {
  value: number
  onChange: (v: number) => void
  min?: number
  max?: number
  step?: number
}) {
  return (
    <input
      type="number"
      value={value}
      onChange={(e) => onChange(toNum(e.target.value))}
      min={min}
      max={max}
      step={step}
      className="w-full rounded border border-slate-300 px-2 py-1 text-xs"
    />
  )
}

function BoolInput({ value, onChange }: { value: boolean; onChange: (v: boolean) => void }) {
  return (
    <input
      type="checkbox"
      checked={value}
      onChange={(e) => onChange(e.target.checked)}
      className="h-4 w-4"
    />
  )
}

function SelectInput({
  value,
  onChange,
  options,
}: {
  value: string
  onChange: (v: string) => void
  options: { value: string; label: string }[]
}) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="w-full rounded border border-slate-300 px-2 py-1 text-xs"
    >
      {options.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
  )
}

function DateInput({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <input
      type="date"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="w-full rounded border border-slate-300 px-2 py-1 text-xs"
    />
  )
}

export default function SetupPage() {
  const [mainTab, setMainTab] = useState<MainTab>('pricing')
  const [pricingTab, setPricingTab] = useState<PricingTab>('vehicles')

  const [invoicing, setInvoicing] = useState<InvoicingRules>(DEFAULT_INVOICING)
  const [company, setCompany] = useState<CompanyProfile>(DEFAULT_COMPANY)
  const [notifications, setNotifications] = useState<NotificationRules>(DEFAULT_NOTIFICATIONS)

  const [setupData, setSetupData] = useState<QuotationPricingSetupData>({
    vehicles: [],
    insurance: [],
    vehicleInterest: [],
    depreciation: [],
    maintenance: [],
    discountOptions: [],
    trackingCharges: [],
    leaseTerms: [],
    interestRateTable: [],
    residualValueTable: [],
    replacementPolicy: [],
    feeMaster: [],
    commissionRateTable: [],
    profitMarginSetup: [],
    calendarPeriods: [],
  })
  const [ready, setReady] = useState(false)
  const [saved, setSaved] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  const thisYear = useMemo(() => new Date().getFullYear(), [])

  useEffect(() => {
    let cancelled = false

    async function boot() {
      setReady(false)
      try {
        const seeded = await loadOrSeedQuotationPricingSetup(thisYear)
        if (!cancelled) setSetupData(seeded)
      } finally {
        if (!cancelled) setReady(true)
      }
    }

    boot().catch(() => {
      if (!cancelled) setReady(true)
    })

    return () => {
      cancelled = true
    }
  }, [thisYear])

  async function saveAll() {
    setSaveError(null)
    try {
      await saveQuotationPricingSetupToApi(setupData)
      setSaved(true)
      setTimeout(() => setSaved(false), 2000)
    } catch (error) {
      setSaveError((error as Error).message)
    }
  }

  async function seedDemo() {
    setSaveError(null)
    const seeded = buildDummyQuotationPricingSetupData(thisYear)
    setSetupData(seeded)
    try {
      await saveQuotationPricingSetupToApi(seeded)
    } catch (error) {
      setSaveError((error as Error).message)
    }
  }

  async function reloadFromStorage() {
    const data = await loadQuotationPricingSetupWithLocalFallback()
    setSetupData(data)
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Setup"
        subtitle="Easy setup with pricing sub-menu and bulk upload options for each pricing screen."
        action={<PrimaryButton onClick={saveAll}>{saved ? 'Saved' : 'Save Setup'}</PrimaryButton>}
      />
      {saveError && (
        <div className="rounded border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {saveError}
        </div>
      )}

      <div className="flex gap-2 border-b border-slate-200">
        {(['pricing', 'invoicing', 'company', 'notifications'] as MainTab[]).map((tab) => (
          <button
            key={tab}
            type="button"
            onClick={() => setMainTab(tab)}
            className={`px-4 py-2 text-sm ${mainTab === tab ? 'border-brand-600 text-brand-700 border-b-2' : 'text-slate-500'}`}
          >
            {tab === 'pricing' ? 'Quotations & Pricing' : tab[0]!.toUpperCase() + tab.slice(1)}
          </button>
        ))}
      </div>

      {mainTab === 'pricing' && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[220px_1fr]">
          <Card className="p-3">
            <div className="space-y-1">
              {(
                [
                  ['vehicles', 'Vehicles'],
                  ['insurance', 'Insurance'],
                  ['interest', 'Vehicle Interest'],
                  ['depreciation', 'Depreciation'],
                  ['maintenance', 'Maintenance'],
                  ['discount', 'Discount Options'],
                  ['tracking', 'Tracking & Tires'],
                  ['leaseTerms', 'Lease Terms'],
                  ['interestRateTable', 'Interest Rate Table'],
                  ['residualValueTable', 'Residual Value Table'],
                  ['replacementPolicy', 'Replacement Policy'],
                  ['feeMaster', 'Fee Master'],
                  ['commissionRates', 'Commission Rates'],
                  ['profitMargin', 'Profit Margin'],
                  ['calendarPeriods', 'Calendar Periods'],
                ] as [PricingTab, string][]
              ).map(([key, label]) => (
                <button
                  key={key}
                  type="button"
                  onClick={() => setPricingTab(key)}
                  className={`w-full rounded px-3 py-2 text-left text-sm ${pricingTab === key ? 'bg-brand-50 text-brand-700' : 'text-slate-600 hover:bg-slate-50'}`}
                >
                  {label}
                </button>
              ))}
            </div>
            <div className="mt-3 space-y-2">
              <PrimaryButton onClick={seedDemo}>Seed {thisYear} Demo Data</PrimaryButton>
              <SecondaryButton onClick={reloadFromStorage}>Reload</SecondaryButton>
            </div>
          </Card>

          <Card className="space-y-3 p-4">
            {!ready && <div className="text-sm text-slate-500">Loading pricing setup...</div>}

            {ready && pricingTab === 'vehicles' && (
              <>
                <div className="flex items-center justify-between">
                  <h3 className="text-sm font-semibold text-slate-800">Vehicles Catalog</h3>
                  <CsvUpload
                    title="Bulk Upload Vehicles CSV"
                    onUploaded={(text) => {
                      const rows = parseCsvWithHeader<QuotationPricingVehicleProfile>(
                        text,
                        (r, i) => ({
                          id: r.id || `veh-${Date.now()}-${i}`,
                          make: r.make || '',
                          model: r.model || '',
                          vehicleType: r.vehicletype || '',
                          year: toNum(r.year, thisYear),
                          engineSizeCc: toNum(r.enginesizecc),
                          basePriceSar: toNum(r.basepricesar),
                          monthlyLeasePriceSar: toNum(r.monthlyleasepricesar),
                          maintenanceCostSar: toNum(r.maintenancecostsar),
                          insuranceCoverageSar: toNum(r.insurancecoveragesar),
                          interestRatePercent: toNum(r.interestratepercent),
                          defaultDurationMonths: toNum(r.defaultdurationmonths, 12),
                          leaseDurationMonths: toNum(r.leasedurationmonths, 24),
                          otherServicesSar: toNum(r.otherservicessar),
                          adminChargesSar: toNum(r.adminchargessar),
                          operationChargesSar: toNum(r.operationchargessar),
                          fuelAllowanceSar: toNum(r.fuelallowancesar),
                          deliveryChargesSar: toNum(r.deliverychargessar),
                          customerServiceChargesSar: toNum(r.customerservicechargessar),
                        }),
                      )
                      if (rows.length > 0) setSetupData((p) => ({ ...p, vehicles: rows }))
                    }}
                  />
                </div>
                <div className="overflow-x-auto rounded border border-slate-200">
                  <table className="min-w-[1700px] text-xs">
                    <thead className="bg-slate-100">
                      <tr>
                        <th className="px-2 py-2 text-left">Make</th>
                        <th className="px-2 py-2 text-left">Model</th>
                        <th className="px-2 py-2 text-left">Type</th>
                        <th className="px-2 py-2 text-left">Year</th>
                        <th className="px-2 py-2 text-left">Price</th>
                        <th className="px-2 py-2 text-left">Monthly</th>
                        <th className="px-2 py-2 text-left">Maintenance</th>
                        <th className="px-2 py-2 text-left">Insurance</th>
                        <th className="px-2 py-2 text-left">Interest %</th>
                        <th className="px-2 py-2 text-left">Admin</th>
                        <th className="px-2 py-2 text-left">Ops</th>
                      </tr>
                    </thead>
                    <tbody>
                      {setupData.vehicles.map((row) => (
                        <tr key={row.id} className="border-t border-slate-100">
                          <td className="px-2 py-1">
                            <TxtInput
                              value={row.make}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, make: v } : x,
                                  ),
                                }))
                              }
                            />
                          </td>
                          <td className="px-2 py-1">
                            <TxtInput
                              value={row.model}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, model: v } : x,
                                  ),
                                }))
                              }
                            />
                          </td>
                          <td className="px-2 py-1">
                            <TxtInput
                              value={row.vehicleType}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, vehicleType: v } : x,
                                  ),
                                }))
                              }
                            />
                          </td>
                          <td className="px-2 py-1">
                            <NumInput
                              value={row.year}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, year: v } : x,
                                  ),
                                }))
                              }
                            />
                          </td>
                          <td className="px-2 py-1">
                            <NumInput
                              value={row.basePriceSar}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, basePriceSar: v } : x,
                                  ),
                                }))
                              }
                            />
                          </td>
                          <td className="px-2 py-1">
                            <NumInput
                              value={row.monthlyLeasePriceSar}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, monthlyLeasePriceSar: v } : x,
                                  ),
                                }))
                              }
                            />
                          </td>
                          <td className="px-2 py-1">
                            <NumInput
                              value={row.maintenanceCostSar}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, maintenanceCostSar: v } : x,
                                  ),
                                }))
                              }
                            />
                          </td>
                          <td className="px-2 py-1">
                            <NumInput
                              value={row.insuranceCoverageSar}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, insuranceCoverageSar: v } : x,
                                  ),
                                }))
                              }
                            />
                          </td>
                          <td className="px-2 py-1">
                            <NumInput
                              value={row.interestRatePercent}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, interestRatePercent: v } : x,
                                  ),
                                }))
                              }
                              min={0}
                              max={25}
                              step={0.1}
                            />
                          </td>
                          <td className="px-2 py-1">
                            <NumInput
                              value={row.adminChargesSar}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, adminChargesSar: v } : x,
                                  ),
                                }))
                              }
                            />
                          </td>
                          <td className="px-2 py-1">
                            <NumInput
                              value={row.operationChargesSar}
                              onChange={(v) =>
                                setSetupData((p) => ({
                                  ...p,
                                  vehicles: p.vehicles.map((x) =>
                                    x.id === row.id ? { ...x, operationChargesSar: v } : x,
                                  ),
                                }))
                              }
                            />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            )}

            {ready && pricingTab === 'insurance' && (
              <PricingTableInsurance setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'interest' && (
              <PricingTableInterest setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'depreciation' && (
              <PricingTableDepreciation setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'maintenance' && (
              <PricingTableMaintenance setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'discount' && (
              <PricingTableDiscount setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'tracking' && (
              <PricingTableTracking setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'leaseTerms' && (
              <PricingTableLeaseTerms setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'interestRateTable' && (
              <PricingTableInterestRateTable setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'residualValueTable' && (
              <PricingTableResidualValue setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'replacementPolicy' && (
              <PricingTableReplacementPolicy setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'feeMaster' && (
              <PricingTableFeeMaster setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'commissionRates' && (
              <PricingTableCommissionRates setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'profitMargin' && (
              <PricingTableProfitMargin setupData={setupData} setSetupData={setSetupData} />
            )}

            {ready && pricingTab === 'calendarPeriods' && (
              <PricingTableCalendarPeriods
                setupData={setupData}
                setSetupData={setSetupData}
                thisYear={thisYear}
              />
            )}
          </Card>
        </div>
      )}

      {mainTab === 'invoicing' && (
        <Card className="max-w-2xl space-y-3 p-4">
          <h3 className="text-sm font-semibold">Invoicing</h3>
          <div className="grid grid-cols-2 gap-3 text-sm">
            <label>
              Billing Day{' '}
              <NumInput
                value={invoicing.billingDay}
                onChange={(v) => setInvoicing((x) => ({ ...x, billingDay: v }))}
                min={1}
                max={28}
              />
            </label>
            <label>
              Payment Terms{' '}
              <NumInput
                value={invoicing.paymentTermsDays}
                onChange={(v) => setInvoicing((x) => ({ ...x, paymentTermsDays: v }))}
                min={1}
                max={90}
              />
            </label>
            <label>
              VAT %{' '}
              <NumInput
                value={invoicing.vatRate}
                onChange={(v) => setInvoicing((x) => ({ ...x, vatRate: v }))}
                min={0}
                max={100}
              />
            </label>
            <label>
              Late Penalty %{' '}
              <NumInput
                value={invoicing.latePenaltyRate}
                onChange={(v) => setInvoicing((x) => ({ ...x, latePenaltyRate: v }))}
                min={0}
                max={20}
              />
            </label>
          </div>
        </Card>
      )}

      {mainTab === 'company' && (
        <Card className="max-w-2xl space-y-3 p-4">
          <h3 className="text-sm font-semibold">Company</h3>
          <div className="grid grid-cols-2 gap-3">
            <label className="text-sm">
              Name{' '}
              <TxtInput
                value={company.companyName}
                onChange={(v) => setCompany((x) => ({ ...x, companyName: v }))}
              />
            </label>
            <label className="text-sm">
              CR{' '}
              <TxtInput
                value={company.crNo}
                onChange={(v) => setCompany((x) => ({ ...x, crNo: v }))}
              />
            </label>
            <label className="text-sm">
              VAT{' '}
              <TxtInput
                value={company.vatNo}
                onChange={(v) => setCompany((x) => ({ ...x, vatNo: v }))}
              />
            </label>
            <label className="text-sm">
              Phone{' '}
              <TxtInput
                value={company.phone}
                onChange={(v) => setCompany((x) => ({ ...x, phone: v }))}
              />
            </label>
            <label className="col-span-2 text-sm">
              Email{' '}
              <TxtInput
                value={company.email}
                onChange={(v) => setCompany((x) => ({ ...x, email: v }))}
              />
            </label>
          </div>
        </Card>
      )}

      {mainTab === 'notifications' && (
        <Card className="max-w-2xl space-y-3 p-4">
          <h3 className="text-sm font-semibold">Notifications</h3>
          <div className="flex items-center gap-2 text-sm">
            <BoolInput
              value={notifications.sendInvoiceEmail}
              onChange={(v) => setNotifications((x) => ({ ...x, sendInvoiceEmail: v }))}
            />
            Send Invoice Emails
          </div>
          <label className="text-sm">
            Overdue Reminder Days{' '}
            <NumInput
              value={notifications.overdueReminderDays}
              onChange={(v) => setNotifications((x) => ({ ...x, overdueReminderDays: v }))}
              min={1}
              max={90}
            />
          </label>
        </Card>
      )}
    </div>
  )
}

function PricingTableInsurance({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Insurance Setup (Rate 0% to 3%)</h3>
        <CsvUpload
          title="Bulk Upload Insurance CSV"
          onUploaded={(text) => {
            const rows = parseCsvWithHeader<InsuranceSetupRow>(text, (r, i) => ({
              id: r.id || `ins-${Date.now()}-${i}`,
              make: r.make || '',
              model: r.model || '',
              vehicleType: r.vehicletype || '',
              minVehicleValueSar: toNum(r.minvehiclevaluesar),
              maxVehicleValueSar: toNum(r.maxvehiclevaluesar),
              ratePercent: Math.min(3, Math.max(0, toNum(r.ratepercent))),
              minPremiumSar: toNum(r.minpremiumsar),
            }))
            if (rows.length > 0) setSetupData((p) => ({ ...p, insurance: rows }))
          }}
        />
      </div>
      <SimpleInsuranceTable rows={setupData.insurance} setSetupData={setSetupData} />
    </>
  )
}

function SimpleInsuranceTable({
  rows,
  setSetupData,
}: {
  rows: InsuranceSetupRow[]
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  return (
    <div className="overflow-x-auto rounded border border-slate-200">
      <table className="min-w-[900px] text-xs">
        <thead className="bg-slate-100">
          <tr>
            <th className="px-2 py-2 text-left">Make</th>
            <th className="px-2 py-2 text-left">Model</th>
            <th className="px-2 py-2 text-left">Type</th>
            <th className="px-2 py-2 text-left">Min Value</th>
            <th className="px-2 py-2 text-left">Max Value</th>
            <th className="px-2 py-2 text-left">Rate %</th>
            <th className="px-2 py-2 text-left">Min Premium</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.id} className="border-t border-slate-100">
              <td className="px-2 py-1">
                <TxtInput
                  value={row.make}
                  onChange={(v) =>
                    setSetupData((p) => ({
                      ...p,
                      insurance: p.insurance.map((x) => (x.id === row.id ? { ...x, make: v } : x)),
                    }))
                  }
                />
              </td>
              <td className="px-2 py-1">
                <TxtInput
                  value={row.model}
                  onChange={(v) =>
                    setSetupData((p) => ({
                      ...p,
                      insurance: p.insurance.map((x) => (x.id === row.id ? { ...x, model: v } : x)),
                    }))
                  }
                />
              </td>
              <td className="px-2 py-1">
                <TxtInput
                  value={row.vehicleType}
                  onChange={(v) =>
                    setSetupData((p) => ({
                      ...p,
                      insurance: p.insurance.map((x) =>
                        x.id === row.id ? { ...x, vehicleType: v } : x,
                      ),
                    }))
                  }
                />
              </td>
              <td className="px-2 py-1">
                <NumInput
                  value={row.minVehicleValueSar}
                  onChange={(v) =>
                    setSetupData((p) => ({
                      ...p,
                      insurance: p.insurance.map((x) =>
                        x.id === row.id ? { ...x, minVehicleValueSar: v } : x,
                      ),
                    }))
                  }
                />
              </td>
              <td className="px-2 py-1">
                <NumInput
                  value={row.maxVehicleValueSar}
                  onChange={(v) =>
                    setSetupData((p) => ({
                      ...p,
                      insurance: p.insurance.map((x) =>
                        x.id === row.id ? { ...x, maxVehicleValueSar: v } : x,
                      ),
                    }))
                  }
                />
              </td>
              <td className="px-2 py-1">
                <NumInput
                  value={row.ratePercent}
                  onChange={(v) =>
                    setSetupData((p) => ({
                      ...p,
                      insurance: p.insurance.map((x) =>
                        x.id === row.id ? { ...x, ratePercent: Math.min(3, Math.max(0, v)) } : x,
                      ),
                    }))
                  }
                  min={0}
                  max={3}
                  step={0.1}
                />
              </td>
              <td className="px-2 py-1">
                <NumInput
                  value={row.minPremiumSar}
                  onChange={(v) =>
                    setSetupData((p) => ({
                      ...p,
                      insurance: p.insurance.map((x) =>
                        x.id === row.id ? { ...x, minPremiumSar: v } : x,
                      ),
                    }))
                  }
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function PricingTableInterest({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">
          Vehicle Interest, Admin Fee & Replacement Charges
        </h3>
        <CsvUpload
          title="Bulk Upload Interest CSV"
          onUploaded={(text) => {
            const rows = parseCsvWithHeader<VehicleInterestSetupRow>(text, (r, i) => ({
              id: r.id || `int-${Date.now()}-${i}`,
              make: r.make || '',
              model: r.model || '',
              vehicleType: r.vehicletype || '',
              interestRatePercent: Math.min(25, Math.max(0, toNum(r.interestratepercent))),
              adminFeeSar: toNum(r.adminfeesar),
              replacementType: r.replacementtype || '',
              replacementChargesPercent: toNum(r.replacementchargespercent),
            }))
            if (rows.length > 0) setSetupData((p) => ({ ...p, vehicleInterest: rows }))
          }}
        />
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[950px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Make</th>
              <th className="px-2 py-2 text-left">Model</th>
              <th className="px-2 py-2 text-left">Type</th>
              <th className="px-2 py-2 text-left">Interest %</th>
              <th className="px-2 py-2 text-left">Admin Fee</th>
              <th className="px-2 py-2 text-left">Replacement Type</th>
              <th className="px-2 py-2 text-left">Replacement %</th>
            </tr>
          </thead>
          <tbody>
            {setupData.vehicleInterest.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.make}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        vehicleInterest: p.vehicleInterest.map((x) =>
                          x.id === row.id ? { ...x, make: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.model}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        vehicleInterest: p.vehicleInterest.map((x) =>
                          x.id === row.id ? { ...x, model: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.vehicleType}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        vehicleInterest: p.vehicleInterest.map((x) =>
                          x.id === row.id ? { ...x, vehicleType: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.interestRatePercent}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        vehicleInterest: p.vehicleInterest.map((x) =>
                          x.id === row.id
                            ? { ...x, interestRatePercent: Math.min(25, Math.max(0, v)) }
                            : x,
                        ),
                      }))
                    }
                    min={0}
                    max={25}
                    step={0.1}
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.adminFeeSar}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        vehicleInterest: p.vehicleInterest.map((x) =>
                          x.id === row.id ? { ...x, adminFeeSar: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.replacementType}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        vehicleInterest: p.vehicleInterest.map((x) =>
                          x.id === row.id ? { ...x, replacementType: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.replacementChargesPercent}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        vehicleInterest: p.vehicleInterest.map((x) =>
                          x.id === row.id ? { ...x, replacementChargesPercent: v } : x,
                        ),
                      }))
                    }
                    min={0}
                    max={100}
                    step={0.1}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function PricingTableDepreciation({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Depreciation Setup</h3>
        <CsvUpload
          title="Bulk Upload Depreciation CSV"
          onUploaded={(text) => {
            const rows = parseCsvWithHeader<DepreciationSetupRow>(text, (r, i) => ({
              id: r.id || `dep-${Date.now()}-${i}`,
              make: r.make || '',
              model: r.model || '',
              vehicleType: r.vehicletype || '',
              annualDepRatePercent: toNum(r.annualdepratepercent),
            }))
            if (rows.length > 0) setSetupData((p) => ({ ...p, depreciation: rows }))
          }}
        />
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[700px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Make</th>
              <th className="px-2 py-2 text-left">Model</th>
              <th className="px-2 py-2 text-left">Type</th>
              <th className="px-2 py-2 text-left">Annual Dep %</th>
            </tr>
          </thead>
          <tbody>
            {setupData.depreciation.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.make}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        depreciation: p.depreciation.map((x) =>
                          x.id === row.id ? { ...x, make: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.model}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        depreciation: p.depreciation.map((x) =>
                          x.id === row.id ? { ...x, model: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.vehicleType}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        depreciation: p.depreciation.map((x) =>
                          x.id === row.id ? { ...x, vehicleType: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.annualDepRatePercent}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        depreciation: p.depreciation.map((x) =>
                          x.id === row.id ? { ...x, annualDepRatePercent: v } : x,
                        ),
                      }))
                    }
                    min={0}
                    max={100}
                    step={0.1}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function PricingTableMaintenance({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">
          Maintenance by Manufacturer, Mileage & Vehicle Type
        </h3>
        <CsvUpload
          title="Bulk Upload Maintenance CSV"
          onUploaded={(text) => {
            const rows = parseCsvWithHeader<MaintenanceSetupRow>(text, (r, i) => ({
              id: r.id || `mtc-${Date.now()}-${i}`,
              manufacturer: r.manufacturer || '',
              vehicleType: r.vehicletype || '',
              minMileageKm: toNum(r.minmileagekm),
              maxMileageKm: toNum(r.maxmileagekm),
              mtcRateSar: toNum(r.mtcratesar),
            }))
            if (rows.length > 0) setSetupData((p) => ({ ...p, maintenance: rows }))
          }}
        />
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[760px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Manufacturer</th>
              <th className="px-2 py-2 text-left">Type</th>
              <th className="px-2 py-2 text-left">Min KM</th>
              <th className="px-2 py-2 text-left">Max KM</th>
              <th className="px-2 py-2 text-left">MTC Rate</th>
            </tr>
          </thead>
          <tbody>
            {setupData.maintenance.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.manufacturer}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        maintenance: p.maintenance.map((x) =>
                          x.id === row.id ? { ...x, manufacturer: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.vehicleType}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        maintenance: p.maintenance.map((x) =>
                          x.id === row.id ? { ...x, vehicleType: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.minMileageKm}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        maintenance: p.maintenance.map((x) =>
                          x.id === row.id ? { ...x, minMileageKm: v } : x,
                        ),
                      }))
                    }
                    min={0}
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.maxMileageKm}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        maintenance: p.maintenance.map((x) =>
                          x.id === row.id ? { ...x, maxMileageKm: v } : x,
                        ),
                      }))
                    }
                    min={0}
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.mtcRateSar}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        maintenance: p.maintenance.map((x) =>
                          x.id === row.id ? { ...x, mtcRateSar: v } : x,
                        ),
                      }))
                    }
                    step={0.01}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function PricingTableDiscount({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Discount Options (from Setup)</h3>
        <CsvUpload
          title="Bulk Upload Discount CSV"
          onUploaded={(text) => {
            const rows = parseCsvWithHeader<DiscountOptionSetupRow>(text, (r, i) => ({
              id: r.id || `disc-${Date.now()}-${i}`,
              optionName: r.optionname || '',
              discountPercent: toNum(r.discountpercent),
              requiresWorkflowApproval: toBool(r.requiresworkflowapproval || 'false'),
            }))
            if (rows.length > 0) setSetupData((p) => ({ ...p, discountOptions: rows }))
          }}
        />
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[650px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Option</th>
              <th className="px-2 py-2 text-left">Discount %</th>
              <th className="px-2 py-2 text-left">Needs Approval</th>
            </tr>
          </thead>
          <tbody>
            {setupData.discountOptions.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.optionName}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        discountOptions: p.discountOptions.map((x) =>
                          x.id === row.id ? { ...x, optionName: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.discountPercent}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        discountOptions: p.discountOptions.map((x) =>
                          x.id === row.id ? { ...x, discountPercent: v } : x,
                        ),
                      }))
                    }
                    min={0}
                    max={100}
                    step={0.1}
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <BoolInput
                    value={row.requiresWorkflowApproval}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        discountOptions: p.discountOptions.map((x) =>
                          x.id === row.id ? { ...x, requiresWorkflowApproval: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

function PricingTableTracking({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">
          Tracking Charges & Tire Changes During Lease
        </h3>
        <CsvUpload
          title="Bulk Upload Tracking CSV"
          onUploaded={(text) => {
            const rows = parseCsvWithHeader<TrackingChargeSetupRow>(text, (r, i) => ({
              id: r.id || `trk-${Date.now()}-${i}`,
              vehicleType: r.vehicletype || '',
              trackingChargesSar: toNum(r.trackingchargessar),
              tireCountIncluded: toNum(r.tirecountincluded, 4),
              tireChangeChargesSar: toNum(r.tirechangechargessar),
            }))
            if (rows.length > 0) setSetupData((p) => ({ ...p, trackingCharges: rows }))
          }}
        />
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[680px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Vehicle Type</th>
              <th className="px-2 py-2 text-left">Tracking Charges</th>
              <th className="px-2 py-2 text-left">Tires Included</th>
              <th className="px-2 py-2 text-left">Tire Change Charges</th>
            </tr>
          </thead>
          <tbody>
            {setupData.trackingCharges.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.vehicleType}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        trackingCharges: p.trackingCharges.map((x) =>
                          x.id === row.id ? { ...x, vehicleType: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.trackingChargesSar}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        trackingCharges: p.trackingCharges.map((x) =>
                          x.id === row.id ? { ...x, trackingChargesSar: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.tireCountIncluded}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        trackingCharges: p.trackingCharges.map((x) =>
                          x.id === row.id ? { ...x, tireCountIncluded: v } : x,
                        ),
                      }))
                    }
                    min={0}
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.tireChangeChargesSar}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        trackingCharges: p.trackingCharges.map((x) =>
                          x.id === row.id ? { ...x, tireChangeChargesSar: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

// ─── D1: Lease Terms ─────────────────────────────────────────────────────────

function PricingTableLeaseTerms({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  function addRow() {
    const row: LeaseTermSetupRow = {
      id: `term-${Date.now()}`,
      termMonths: 12,
      description: '',
    }
    setSetupData((p) => ({ ...p, leaseTerms: [...p.leaseTerms, row] }))
  }

  function removeRow(id: string) {
    setSetupData((p) => ({ ...p, leaseTerms: p.leaseTerms.filter((x) => x.id !== id) }))
  }

  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Lease Terms</h3>
        <div className="flex gap-2">
          <CsvUpload
            title="Bulk Upload CSV"
            onUploaded={(text) => {
              const rows = parseCsvWithHeader<LeaseTermSetupRow>(text, (r, i) => ({
                id: r.id || `term-${Date.now()}-${i}`,
                termMonths: toNum(r.termmonths, 12),
                description: r.description || '',
              }))
              if (rows.length > 0) setSetupData((p) => ({ ...p, leaseTerms: rows }))
            }}
          />
          <SecondaryButton onClick={addRow}>+ Add</SecondaryButton>
        </div>
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[400px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Term (Months)</th>
              <th className="px-2 py-2 text-left">Description</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {setupData.leaseTerms.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <NumInput
                    value={row.termMonths}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        leaseTerms: p.leaseTerms.map((x) =>
                          x.id === row.id ? { ...x, termMonths: v } : x,
                        ),
                      }))
                    }
                    min={1}
                  />
                </td>
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.description}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        leaseTerms: p.leaseTerms.map((x) =>
                          x.id === row.id ? { ...x, description: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <button
                    type="button"
                    onClick={() => removeRow(row.id)}
                    className="text-red-400 hover:text-red-600"
                  >
                    x
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

// ─── D2: Interest Rate Table ─────────────────────────────────────────────────

function PricingTableInterestRateTable({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  function addRow() {
    const today = new Date().toISOString().slice(0, 10)
    const row: InterestRateSetupRow = {
      id: `ir-${Date.now()}`,
      termMonths: 24,
      strategy: 'A',
      annualRatePercent: 5,
      effectiveFrom: today,
      isActive: true,
    }
    setSetupData((p) => ({ ...p, interestRateTable: [...p.interestRateTable, row] }))
  }

  function removeRow(id: string) {
    setSetupData((p) => ({
      ...p,
      interestRateTable: p.interestRateTable.filter((x) => x.id !== id),
    }))
  }

  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Interest Rate Table</h3>
        <div className="flex gap-2">
          <CsvUpload
            title="Bulk Upload CSV"
            onUploaded={(text) => {
              const rows = parseCsvWithHeader<InterestRateSetupRow>(text, (r, i) => ({
                id: r.id || `ir-${Date.now()}-${i}`,
                termMonths: toNum(r.termmonths, 24),
                strategy: (r.strategy === 'B' ? 'B' : 'A') as 'A' | 'B',
                annualRatePercent: toNum(r.annualratepercent),
                effectiveFrom: r.effectivefrom || new Date().toISOString().slice(0, 10),
                ...(r.effectiveto ? { effectiveTo: r.effectiveto } : {}),
                isActive: r.isactive ? toBool(r.isactive) : true,
              }))
              if (rows.length > 0) setSetupData((p) => ({ ...p, interestRateTable: rows }))
            }}
          />
          <SecondaryButton onClick={addRow}>+ Add</SecondaryButton>
        </div>
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[800px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Term (Months)</th>
              <th className="px-2 py-2 text-left">Strategy</th>
              <th className="px-2 py-2 text-left">Annual Rate %</th>
              <th className="px-2 py-2 text-left">Effective From</th>
              <th className="px-2 py-2 text-left">Effective To</th>
              <th className="px-2 py-2 text-left">Active</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {setupData.interestRateTable.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <NumInput
                    value={row.termMonths}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        interestRateTable: p.interestRateTable.map((x) =>
                          x.id === row.id ? { ...x, termMonths: v } : x,
                        ),
                      }))
                    }
                    min={1}
                  />
                </td>
                <td className="px-2 py-1">
                  <SelectInput
                    value={row.strategy}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        interestRateTable: p.interestRateTable.map((x) =>
                          x.id === row.id ? { ...x, strategy: v as 'A' | 'B' } : x,
                        ),
                      }))
                    }
                    options={[
                      { value: 'A', label: 'A - Flat' },
                      { value: 'B', label: 'B - Reducing' },
                    ]}
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.annualRatePercent}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        interestRateTable: p.interestRateTable.map((x) =>
                          x.id === row.id ? { ...x, annualRatePercent: v } : x,
                        ),
                      }))
                    }
                    min={0}
                    max={25}
                    step={0.01}
                  />
                </td>
                <td className="px-2 py-1">
                  <DateInput
                    value={row.effectiveFrom}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        interestRateTable: p.interestRateTable.map((x) =>
                          x.id === row.id ? { ...x, effectiveFrom: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <DateInput
                    value={row.effectiveTo ?? ''}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        interestRateTable: p.interestRateTable.map((x) =>
                          x.id === row.id
                            ? (() => {
                                const { effectiveTo: _, ...rest } = x
                                return v ? { ...rest, effectiveTo: v } : rest
                              })()
                            : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <BoolInput
                    value={row.isActive}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        interestRateTable: p.interestRateTable.map((x) =>
                          x.id === row.id ? { ...x, isActive: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <button
                    type="button"
                    onClick={() => removeRow(row.id)}
                    className="text-red-400 hover:text-red-600"
                  >
                    x
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

// ─── D3: Residual Value Table ────────────────────────────────────────────────

function PricingTableResidualValue({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  function addRow() {
    const today = new Date().toISOString().slice(0, 10)
    const row: ResidualValueSetupRow = {
      id: `rv-${Date.now()}`,
      vehicleType: '',
      termMonths: 24,
      rvPercent: 35,
      effectiveFrom: today,
      isActive: true,
    }
    setSetupData((p) => ({ ...p, residualValueTable: [...p.residualValueTable, row] }))
  }

  function removeRow(id: string) {
    setSetupData((p) => ({
      ...p,
      residualValueTable: p.residualValueTable.filter((x) => x.id !== id),
    }))
  }

  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Residual Value Table</h3>
        <div className="flex gap-2">
          <CsvUpload
            title="Bulk Upload CSV"
            onUploaded={(text) => {
              const rows = parseCsvWithHeader<ResidualValueSetupRow>(text, (r, i) => ({
                id: r.id || `rv-${Date.now()}-${i}`,
                vehicleType: r.vehicletype || '',
                termMonths: toNum(r.termmonths, 24),
                rvPercent: toNum(r.rvpercent, 35),
                effectiveFrom: r.effectivefrom || new Date().toISOString().slice(0, 10),
                ...(r.effectiveto ? { effectiveTo: r.effectiveto } : {}),
                isActive: r.isactive ? toBool(r.isactive) : true,
              }))
              if (rows.length > 0) setSetupData((p) => ({ ...p, residualValueTable: rows }))
            }}
          />
          <SecondaryButton onClick={addRow}>+ Add</SecondaryButton>
        </div>
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[800px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Vehicle Type</th>
              <th className="px-2 py-2 text-left">Term (Months)</th>
              <th className="px-2 py-2 text-left">RV %</th>
              <th className="px-2 py-2 text-left">Effective From</th>
              <th className="px-2 py-2 text-left">Effective To</th>
              <th className="px-2 py-2 text-left">Active</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {setupData.residualValueTable.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.vehicleType}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        residualValueTable: p.residualValueTable.map((x) =>
                          x.id === row.id ? { ...x, vehicleType: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.termMonths}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        residualValueTable: p.residualValueTable.map((x) =>
                          x.id === row.id ? { ...x, termMonths: v } : x,
                        ),
                      }))
                    }
                    min={1}
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.rvPercent}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        residualValueTable: p.residualValueTable.map((x) =>
                          x.id === row.id ? { ...x, rvPercent: v } : x,
                        ),
                      }))
                    }
                    min={0}
                    max={100}
                    step={0.1}
                  />
                </td>
                <td className="px-2 py-1">
                  <DateInput
                    value={row.effectiveFrom}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        residualValueTable: p.residualValueTable.map((x) =>
                          x.id === row.id ? { ...x, effectiveFrom: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <DateInput
                    value={row.effectiveTo ?? ''}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        residualValueTable: p.residualValueTable.map((x) =>
                          x.id === row.id
                            ? (() => {
                                const { effectiveTo: _, ...rest } = x
                                return v ? { ...rest, effectiveTo: v } : rest
                              })()
                            : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <BoolInput
                    value={row.isActive}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        residualValueTable: p.residualValueTable.map((x) =>
                          x.id === row.id ? { ...x, isActive: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <button
                    type="button"
                    onClick={() => removeRow(row.id)}
                    className="text-red-400 hover:text-red-600"
                  >
                    x
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

// ─── D4: Replacement Policy ──────────────────────────────────────────────────

function PricingTableReplacementPolicy({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  function addRow() {
    const row: ReplacementPolicySetupRow = {
      id: `rp-${Date.now()}`,
      policyName: '',
      strategy: 'PERMANENT',
      replacementRatePercent: 0,
      maxReplacementsPerTerm: 0,
      isActive: true,
    }
    setSetupData((p) => ({ ...p, replacementPolicy: [...p.replacementPolicy, row] }))
  }

  function removeRow(id: string) {
    setSetupData((p) => ({
      ...p,
      replacementPolicy: p.replacementPolicy.filter((x) => x.id !== id),
    }))
  }

  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Replacement Policy</h3>
        <div className="flex gap-2">
          <CsvUpload
            title="Bulk Upload CSV"
            onUploaded={(text) => {
              const rows = parseCsvWithHeader<ReplacementPolicySetupRow>(text, (r, i) => ({
                id: r.id || `rp-${Date.now()}-${i}`,
                policyName: r.policyname || '',
                strategy: (r.strategy === 'OPEN' ? 'OPEN' : 'PERMANENT') as 'OPEN' | 'PERMANENT',
                replacementRatePercent: toNum(r.replacementratepercent),
                maxReplacementsPerTerm: toNum(r.maxreplacementsperterm),
                isActive: r.isactive ? toBool(r.isactive) : true,
              }))
              if (rows.length > 0) setSetupData((p) => ({ ...p, replacementPolicy: rows }))
            }}
          />
          <SecondaryButton onClick={addRow}>+ Add</SecondaryButton>
        </div>
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[750px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Policy Name</th>
              <th className="px-2 py-2 text-left">Strategy</th>
              <th className="px-2 py-2 text-left">Rate %</th>
              <th className="px-2 py-2 text-left">Max Replacements</th>
              <th className="px-2 py-2 text-left">Active</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {setupData.replacementPolicy.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.policyName}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        replacementPolicy: p.replacementPolicy.map((x) =>
                          x.id === row.id ? { ...x, policyName: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <SelectInput
                    value={row.strategy}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        replacementPolicy: p.replacementPolicy.map((x) =>
                          x.id === row.id ? { ...x, strategy: v as 'OPEN' | 'PERMANENT' } : x,
                        ),
                      }))
                    }
                    options={[
                      { value: 'OPEN', label: 'Open' },
                      { value: 'PERMANENT', label: 'Permanent' },
                    ]}
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.replacementRatePercent}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        replacementPolicy: p.replacementPolicy.map((x) =>
                          x.id === row.id ? { ...x, replacementRatePercent: v } : x,
                        ),
                      }))
                    }
                    min={0}
                    max={100}
                    step={0.1}
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.maxReplacementsPerTerm}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        replacementPolicy: p.replacementPolicy.map((x) =>
                          x.id === row.id ? { ...x, maxReplacementsPerTerm: v } : x,
                        ),
                      }))
                    }
                    min={0}
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <BoolInput
                    value={row.isActive}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        replacementPolicy: p.replacementPolicy.map((x) =>
                          x.id === row.id ? { ...x, isActive: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <button
                    type="button"
                    onClick={() => removeRow(row.id)}
                    className="text-red-400 hover:text-red-600"
                  >
                    x
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

// ─── D5: Fee Master ──────────────────────────────────────────────────────────

const FEE_CODE_OPTIONS: { value: string; label: string }[] = [
  { value: 'ADMIN', label: 'Admin' },
  { value: 'REGISTRATION', label: 'Registration' },
  { value: 'CARD_FEE', label: 'Card Fee' },
  { value: 'TRACKING', label: 'Tracking' },
  { value: 'CAR_WASH_MANPOWER', label: 'Car Wash/Manpower' },
]

const CALC_METHOD_OPTIONS: { value: string; label: string }[] = [
  { value: 'FIXED_AMOUNT', label: 'Fixed Amount' },
  { value: 'PERCENT_OF_TFV', label: '% of TFV' },
  { value: 'PERCENT_OF_INSTALLMENT', label: '% of Installment' },
]

const FREQUENCY_OPTIONS: { value: string; label: string }[] = [
  { value: 'MONTHLY', label: 'Monthly' },
  { value: 'ANNUAL', label: 'Annual' },
  { value: 'ONE_TIME', label: 'One-Time' },
]

function PricingTableFeeMaster({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  function addRow() {
    const row: FeeMasterSetupRow = {
      id: `fee-${Date.now()}`,
      feeCode: 'ADMIN',
      feeName: '',
      calculationMethod: 'FIXED_AMOUNT',
      feeValue: 0,
      frequency: 'MONTHLY',
      isActive: true,
    }
    setSetupData((p) => ({ ...p, feeMaster: [...p.feeMaster, row] }))
  }

  function removeRow(id: string) {
    setSetupData((p) => ({ ...p, feeMaster: p.feeMaster.filter((x) => x.id !== id) }))
  }

  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Fee Master</h3>
        <div className="flex gap-2">
          <CsvUpload
            title="Bulk Upload CSV"
            onUploaded={(text) => {
              const validCodes = new Set([
                'ADMIN',
                'REGISTRATION',
                'CARD_FEE',
                'TRACKING',
                'CAR_WASH_MANPOWER',
              ])
              const rows = parseCsvWithHeader<FeeMasterSetupRow>(text, (r, i) => ({
                id: r.id || `fee-${Date.now()}-${i}`,
                feeCode: (validCodes.has(r.feecode?.toUpperCase() ?? '')
                  ? r.feecode!.toUpperCase()
                  : 'ADMIN') as FeeCode,
                feeName: r.feename || '',
                calculationMethod:
                  (r.calculationmethod as FeeMasterSetupRow['calculationMethod']) || 'FIXED_AMOUNT',
                feeValue: toNum(r.feevalue),
                frequency: (r.frequency as FeeMasterSetupRow['frequency']) || 'MONTHLY',
                isActive: r.isactive ? toBool(r.isactive) : true,
              }))
              if (rows.length > 0) setSetupData((p) => ({ ...p, feeMaster: rows }))
            }}
          />
          <SecondaryButton onClick={addRow}>+ Add</SecondaryButton>
        </div>
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[900px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Fee Code</th>
              <th className="px-2 py-2 text-left">Fee Name</th>
              <th className="px-2 py-2 text-left">Calculation Method</th>
              <th className="px-2 py-2 text-left">Value</th>
              <th className="px-2 py-2 text-left">Frequency</th>
              <th className="px-2 py-2 text-left">Active</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {setupData.feeMaster.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <SelectInput
                    value={row.feeCode}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        feeMaster: p.feeMaster.map((x) =>
                          x.id === row.id ? { ...x, feeCode: v as FeeCode } : x,
                        ),
                      }))
                    }
                    options={FEE_CODE_OPTIONS}
                  />
                </td>
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.feeName}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        feeMaster: p.feeMaster.map((x) =>
                          x.id === row.id ? { ...x, feeName: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <SelectInput
                    value={row.calculationMethod}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        feeMaster: p.feeMaster.map((x) =>
                          x.id === row.id
                            ? { ...x, calculationMethod: v as FeeMasterSetupRow['calculationMethod'] }
                            : x,
                        ),
                      }))
                    }
                    options={CALC_METHOD_OPTIONS}
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.feeValue}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        feeMaster: p.feeMaster.map((x) =>
                          x.id === row.id ? { ...x, feeValue: v } : x,
                        ),
                      }))
                    }
                    min={0}
                    step={0.01}
                  />
                </td>
                <td className="px-2 py-1">
                  <SelectInput
                    value={row.frequency}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        feeMaster: p.feeMaster.map((x) =>
                          x.id === row.id
                            ? { ...x, frequency: v as FeeMasterSetupRow['frequency'] }
                            : x,
                        ),
                      }))
                    }
                    options={FREQUENCY_OPTIONS}
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <BoolInput
                    value={row.isActive}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        feeMaster: p.feeMaster.map((x) =>
                          x.id === row.id ? { ...x, isActive: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <button
                    type="button"
                    onClick={() => removeRow(row.id)}
                    className="text-red-400 hover:text-red-600"
                  >
                    x
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

// ─── D6: Commission Rates ────────────────────────────────────────────────────

function PricingTableCommissionRates({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  function addRow() {
    const today = new Date().toISOString().slice(0, 10)
    const row: CommissionRateSetupRow = {
      id: `comm-${Date.now()}`,
      channelName: '',
      commissionPercent: 0,
      effectiveFrom: today,
      isActive: true,
    }
    setSetupData((p) => ({ ...p, commissionRateTable: [...p.commissionRateTable, row] }))
  }

  function removeRow(id: string) {
    setSetupData((p) => ({
      ...p,
      commissionRateTable: p.commissionRateTable.filter((x) => x.id !== id),
    }))
  }

  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Commission Rates</h3>
        <div className="flex gap-2">
          <CsvUpload
            title="Bulk Upload CSV"
            onUploaded={(text) => {
              const rows = parseCsvWithHeader<CommissionRateSetupRow>(text, (r, i) => ({
                id: r.id || `comm-${Date.now()}-${i}`,
                channelName: r.channelname || '',
                commissionPercent: toNum(r.commissionpercent),
                effectiveFrom: r.effectivefrom || new Date().toISOString().slice(0, 10),
                ...(r.effectiveto ? { effectiveTo: r.effectiveto } : {}),
                isActive: r.isactive ? toBool(r.isactive) : true,
              }))
              if (rows.length > 0) setSetupData((p) => ({ ...p, commissionRateTable: rows }))
            }}
          />
          <SecondaryButton onClick={addRow}>+ Add</SecondaryButton>
        </div>
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[700px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Channel Name</th>
              <th className="px-2 py-2 text-left">Commission %</th>
              <th className="px-2 py-2 text-left">Effective From</th>
              <th className="px-2 py-2 text-left">Effective To</th>
              <th className="px-2 py-2 text-left">Active</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {setupData.commissionRateTable.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.channelName}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        commissionRateTable: p.commissionRateTable.map((x) =>
                          x.id === row.id ? { ...x, channelName: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.commissionPercent}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        commissionRateTable: p.commissionRateTable.map((x) =>
                          x.id === row.id ? { ...x, commissionPercent: v } : x,
                        ),
                      }))
                    }
                    min={0}
                    max={100}
                    step={0.01}
                  />
                </td>
                <td className="px-2 py-1">
                  <DateInput
                    value={row.effectiveFrom}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        commissionRateTable: p.commissionRateTable.map((x) =>
                          x.id === row.id ? { ...x, effectiveFrom: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <DateInput
                    value={row.effectiveTo ?? ''}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        commissionRateTable: p.commissionRateTable.map((x) =>
                          x.id === row.id
                            ? (() => {
                                const { effectiveTo: _, ...rest } = x
                                return v ? { ...rest, effectiveTo: v } : rest
                              })()
                            : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <BoolInput
                    value={row.isActive}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        commissionRateTable: p.commissionRateTable.map((x) =>
                          x.id === row.id ? { ...x, isActive: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <button
                    type="button"
                    onClick={() => removeRow(row.id)}
                    className="text-red-400 hover:text-red-600"
                  >
                    x
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

// ─── D7: Profit Margin ───────────────────────────────────────────────────────

function PricingTableProfitMargin({
  setupData,
  setSetupData,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
}) {
  function addRow() {
    const today = new Date().toISOString().slice(0, 10)
    const row: ProfitMarginSetupRow = {
      id: `pm-${Date.now()}`,
      vehicleType: '',
      marginPercent: 8,
      effectiveFrom: today,
      isActive: true,
    }
    setSetupData((p) => ({ ...p, profitMarginSetup: [...p.profitMarginSetup, row] }))
  }

  function removeRow(id: string) {
    setSetupData((p) => ({
      ...p,
      profitMarginSetup: p.profitMarginSetup.filter((x) => x.id !== id),
    }))
  }

  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Profit Margin Setup</h3>
        <div className="flex gap-2">
          <CsvUpload
            title="Bulk Upload CSV"
            onUploaded={(text) => {
              const rows = parseCsvWithHeader<ProfitMarginSetupRow>(text, (r, i) => ({
                id: r.id || `pm-${Date.now()}-${i}`,
                vehicleType: r.vehicletype || '',
                marginPercent: toNum(r.marginpercent, 8),
                effectiveFrom: r.effectivefrom || new Date().toISOString().slice(0, 10),
                ...(r.effectiveto ? { effectiveTo: r.effectiveto } : {}),
                isActive: r.isactive ? toBool(r.isactive) : true,
              }))
              if (rows.length > 0) setSetupData((p) => ({ ...p, profitMarginSetup: rows }))
            }}
          />
          <SecondaryButton onClick={addRow}>+ Add</SecondaryButton>
        </div>
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[700px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Vehicle Type</th>
              <th className="px-2 py-2 text-left">Margin %</th>
              <th className="px-2 py-2 text-left">Effective From</th>
              <th className="px-2 py-2 text-left">Effective To</th>
              <th className="px-2 py-2 text-left">Active</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {setupData.profitMarginSetup.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.vehicleType}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        profitMarginSetup: p.profitMarginSetup.map((x) =>
                          x.id === row.id ? { ...x, vehicleType: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <NumInput
                    value={row.marginPercent}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        profitMarginSetup: p.profitMarginSetup.map((x) =>
                          x.id === row.id ? { ...x, marginPercent: v } : x,
                        ),
                      }))
                    }
                    min={0}
                    max={100}
                    step={0.01}
                  />
                </td>
                <td className="px-2 py-1">
                  <DateInput
                    value={row.effectiveFrom}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        profitMarginSetup: p.profitMarginSetup.map((x) =>
                          x.id === row.id ? { ...x, effectiveFrom: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <DateInput
                    value={row.effectiveTo ?? ''}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        profitMarginSetup: p.profitMarginSetup.map((x) =>
                          x.id === row.id
                            ? (() => {
                                const { effectiveTo: _, ...rest } = x
                                return v ? { ...rest, effectiveTo: v } : rest
                              })()
                            : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <BoolInput
                    value={row.isActive}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        profitMarginSetup: p.profitMarginSetup.map((x) =>
                          x.id === row.id ? { ...x, isActive: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <button
                    type="button"
                    onClick={() => removeRow(row.id)}
                    className="text-red-400 hover:text-red-600"
                  >
                    x
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

// ─── D8: Calendar Periods ────────────────────────────────────────────────────

function PricingTableCalendarPeriods({
  setupData,
  setSetupData,
  thisYear,
}: {
  setupData: QuotationPricingSetupData
  setSetupData: React.Dispatch<React.SetStateAction<QuotationPricingSetupData>>
  thisYear: number
}) {
  function addRow() {
    const row: CalendarPeriodSetupRow = {
      id: `period-${Date.now()}`,
      periodLabel: '',
      periodStart: '',
      periodEnd: '',
    }
    setSetupData((p) => ({ ...p, calendarPeriods: [...p.calendarPeriods, row] }))
  }

  function removeRow(id: string) {
    setSetupData((p) => ({
      ...p,
      calendarPeriods: p.calendarPeriods.filter((x) => x.id !== id),
    }))
  }

  function generateYear(year: number) {
    const periods: CalendarPeriodSetupRow[] = Array.from({ length: 12 }, (_, i) => {
      const month = i + 1
      const label = `${year}-${String(month).padStart(2, '0')}`
      const start = `${label}-01`
      const endDate = new Date(year, month, 0)
      const end = `${label}-${String(endDate.getDate()).padStart(2, '0')}`
      return { id: `period-${label}`, periodLabel: label, periodStart: start, periodEnd: end }
    })
    setSetupData((p) => {
      const existingIds = new Set(periods.map((x) => x.id))
      const kept = p.calendarPeriods.filter((x) => !existingIds.has(x.id))
      return { ...p, calendarPeriods: [...kept, ...periods] }
    })
  }

  return (
    <>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-800">Calendar Periods</h3>
        <div className="flex gap-2">
          <CsvUpload
            title="Bulk Upload CSV"
            onUploaded={(text) => {
              const rows = parseCsvWithHeader<CalendarPeriodSetupRow>(text, (r, i) => ({
                id: r.id || `period-${Date.now()}-${i}`,
                periodLabel: r.periodlabel || '',
                periodStart: r.periodstart || '',
                periodEnd: r.periodend || '',
              }))
              if (rows.length > 0) setSetupData((p) => ({ ...p, calendarPeriods: rows }))
            }}
          />
          <SecondaryButton onClick={() => generateYear(thisYear)}>
            Generate {thisYear}
          </SecondaryButton>
          <SecondaryButton onClick={() => generateYear(thisYear + 1)}>
            Generate {thisYear + 1}
          </SecondaryButton>
          <SecondaryButton onClick={addRow}>+ Add</SecondaryButton>
        </div>
      </div>
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="min-w-[600px] text-xs">
          <thead className="bg-slate-100">
            <tr>
              <th className="px-2 py-2 text-left">Period Label</th>
              <th className="px-2 py-2 text-left">Start Date</th>
              <th className="px-2 py-2 text-left">End Date</th>
              <th className="w-10 px-2 py-2" />
            </tr>
          </thead>
          <tbody>
            {setupData.calendarPeriods.map((row) => (
              <tr key={row.id} className="border-t border-slate-100">
                <td className="px-2 py-1">
                  <TxtInput
                    value={row.periodLabel}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        calendarPeriods: p.calendarPeriods.map((x) =>
                          x.id === row.id ? { ...x, periodLabel: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <DateInput
                    value={row.periodStart}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        calendarPeriods: p.calendarPeriods.map((x) =>
                          x.id === row.id ? { ...x, periodStart: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1">
                  <DateInput
                    value={row.periodEnd}
                    onChange={(v) =>
                      setSetupData((p) => ({
                        ...p,
                        calendarPeriods: p.calendarPeriods.map((x) =>
                          x.id === row.id ? { ...x, periodEnd: v } : x,
                        ),
                      }))
                    }
                  />
                </td>
                <td className="px-2 py-1 text-center">
                  <button
                    type="button"
                    onClick={() => removeRow(row.id)}
                    className="text-red-400 hover:text-red-600"
                  >
                    x
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}
