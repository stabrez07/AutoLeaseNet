export interface QuotationPricingVehicleProfile {
  id: string
  make: string
  model: string
  vehicleType: string
  year: number
  engineSizeCc: number
  basePriceSar: number
  monthlyLeasePriceSar: number
  maintenanceCostSar: number
  insuranceCoverageSar: number
  interestRatePercent: number
  defaultDurationMonths: number
  leaseDurationMonths: number
  otherServicesSar: number
  adminChargesSar: number
  operationChargesSar: number
  fuelAllowanceSar: number
  deliveryChargesSar: number
  customerServiceChargesSar: number
}

export interface InsuranceSetupRow {
  id: string
  make: string
  model: string
  vehicleType: string
  minVehicleValueSar: number
  maxVehicleValueSar: number
  ratePercent: number // 0% to 3%
  minPremiumSar: number
}

export interface VehicleInterestSetupRow {
  id: string
  make: string
  model: string
  vehicleType: string
  interestRatePercent: number // 0% to 25%
  adminFeeSar: number
  replacementType: string
  replacementChargesPercent: number
}

export interface DepreciationSetupRow {
  id: string
  make: string
  model: string
  vehicleType: string
  annualDepRatePercent: number
}

export interface MaintenanceSetupRow {
  id: string
  manufacturer: string
  vehicleType: string
  minMileageKm: number
  maxMileageKm: number
  mtcRateSar: number
  strategy?: 'A' | 'B'
  rateType?: 'FIXED_AMOUNT' | 'PERCENT_OF_TFV'
  rateValue?: number
}

export interface DiscountOptionSetupRow {
  id: string
  optionName: string
  discountPercent: number
  requiresWorkflowApproval: boolean
}

export interface TrackingChargeSetupRow {
  id: string
  vehicleType: string
  trackingChargesSar: number
  tireCountIncluded: number
  tireChangeChargesSar: number
}

export interface LeaseTermSetupRow {
  id: string
  termMonths: number
  description: string
}

export interface InterestRateSetupRow {
  id: string
  termMonths: number
  strategy: 'A' | 'B'
  annualRatePercent: number
  effectiveFrom: string
  effectiveTo?: string
  isActive: boolean
}

export interface ResidualValueSetupRow {
  id: string
  vehicleType: string
  termMonths: number
  rvPercent: number
  effectiveFrom: string
  effectiveTo?: string
  isActive: boolean
}

export interface ReplacementPolicySetupRow {
  id: string
  policyName: string
  strategy: 'OPEN' | 'PERMANENT'
  replacementRatePercent: number
  maxReplacementsPerTerm: number
  isActive: boolean
}

export type FeeCode = 'ADMIN' | 'REGISTRATION' | 'CARD_FEE' | 'TRACKING' | 'CAR_WASH_MANPOWER'

export interface FeeMasterSetupRow {
  id: string
  feeCode: FeeCode
  feeName: string
  calculationMethod: 'FIXED_AMOUNT' | 'PERCENT_OF_TFV' | 'PERCENT_OF_INSTALLMENT'
  feeValue: number
  frequency: 'ONE_TIME' | 'MONTHLY' | 'ANNUAL'
  isActive: boolean
}

export interface CommissionRateSetupRow {
  id: string
  channelName: string
  commissionPercent: number
  effectiveFrom: string
  effectiveTo?: string
  isActive: boolean
}

export interface ProfitMarginSetupRow {
  id: string
  vehicleType: string
  marginPercent: number
  effectiveFrom: string
  effectiveTo?: string
  isActive: boolean
}

export interface CalendarPeriodSetupRow {
  id: string
  periodLabel: string
  periodStart: string
  periodEnd: string
}

export interface QuotationPricingSetupData {
  vehicles: QuotationPricingVehicleProfile[]
  insurance: InsuranceSetupRow[]
  vehicleInterest: VehicleInterestSetupRow[]
  depreciation: DepreciationSetupRow[]
  maintenance: MaintenanceSetupRow[]
  discountOptions: DiscountOptionSetupRow[]
  trackingCharges: TrackingChargeSetupRow[]
  leaseTerms: LeaseTermSetupRow[]
  interestRateTable: InterestRateSetupRow[]
  residualValueTable: ResidualValueSetupRow[]
  replacementPolicy: ReplacementPolicySetupRow[]
  feeMaster: FeeMasterSetupRow[]
  commissionRateTable: CommissionRateSetupRow[]
  profitMarginSetup: ProfitMarginSetupRow[]
  calendarPeriods: CalendarPeriodSetupRow[]
}

export const QUOTATION_PRICING_STORAGE_KEY = 'autoleasenet.quotationPricingSetup.v2'

function currentYear(): number {
  return new Date().getFullYear()
}

export function buildDummyQuotationPricingVehicles(
  year: number = currentYear(),
): QuotationPricingVehicleProfile[] {
  return [
    {
      id: `${year}-camry-le`,
      make: 'Toyota',
      model: 'Camry',
      vehicleType: 'Sedan',
      year,
      engineSizeCc: 2500,
      basePriceSar: 118000,
      monthlyLeasePriceSar: 2350,
      maintenanceCostSar: 180,
      insuranceCoverageSar: 210,
      interestRatePercent: 4.25,
      defaultDurationMonths: 12,
      leaseDurationMonths: 24,
      otherServicesSar: 85,
      adminChargesSar: 55,
      operationChargesSar: 70,
      fuelAllowanceSar: 240,
      deliveryChargesSar: 95,
      customerServiceChargesSar: 40,
    },
    {
      id: `${year}-corolla-xli`,
      make: 'Toyota',
      model: 'Corolla',
      vehicleType: 'Sedan',
      year,
      engineSizeCc: 1800,
      basePriceSar: 86000,
      monthlyLeasePriceSar: 1780,
      maintenanceCostSar: 150,
      insuranceCoverageSar: 180,
      interestRatePercent: 4.1,
      defaultDurationMonths: 12,
      leaseDurationMonths: 24,
      otherServicesSar: 70,
      adminChargesSar: 50,
      operationChargesSar: 62,
      fuelAllowanceSar: 200,
      deliveryChargesSar: 90,
      customerServiceChargesSar: 35,
    },
    {
      id: `${year}-sonata-mid`,
      make: 'Hyundai',
      model: 'Sonata',
      vehicleType: 'Sedan',
      year,
      engineSizeCc: 2500,
      basePriceSar: 102000,
      monthlyLeasePriceSar: 2100,
      maintenanceCostSar: 170,
      insuranceCoverageSar: 200,
      interestRatePercent: 4.35,
      defaultDurationMonths: 12,
      leaseDurationMonths: 24,
      otherServicesSar: 80,
      adminChargesSar: 55,
      operationChargesSar: 66,
      fuelAllowanceSar: 220,
      deliveryChargesSar: 92,
      customerServiceChargesSar: 38,
    },
    {
      id: `${year}-tucson-smart`,
      make: 'Hyundai',
      model: 'Tucson',
      vehicleType: 'SUV',
      year,
      engineSizeCc: 2000,
      basePriceSar: 112000,
      monthlyLeasePriceSar: 2280,
      maintenanceCostSar: 210,
      insuranceCoverageSar: 235,
      interestRatePercent: 4.5,
      defaultDurationMonths: 12,
      leaseDurationMonths: 24,
      otherServicesSar: 95,
      adminChargesSar: 58,
      operationChargesSar: 75,
      fuelAllowanceSar: 260,
      deliveryChargesSar: 110,
      customerServiceChargesSar: 42,
    },
    {
      id: `${year}-patrol-se`,
      make: 'Nissan',
      model: 'Patrol',
      vehicleType: 'SUV',
      year,
      engineSizeCc: 4000,
      basePriceSar: 248000,
      monthlyLeasePriceSar: 4600,
      maintenanceCostSar: 340,
      insuranceCoverageSar: 410,
      interestRatePercent: 4.9,
      defaultDurationMonths: 12,
      leaseDurationMonths: 36,
      otherServicesSar: 130,
      adminChargesSar: 70,
      operationChargesSar: 120,
      fuelAllowanceSar: 420,
      deliveryChargesSar: 130,
      customerServiceChargesSar: 60,
    },
    {
      id: `${year}-hilux-dc`,
      make: 'Toyota',
      model: 'Hilux',
      vehicleType: 'Pickup',
      year,
      engineSizeCc: 2800,
      basePriceSar: 138000,
      monthlyLeasePriceSar: 2800,
      maintenanceCostSar: 250,
      insuranceCoverageSar: 280,
      interestRatePercent: 4.65,
      defaultDurationMonths: 12,
      leaseDurationMonths: 24,
      otherServicesSar: 110,
      adminChargesSar: 60,
      operationChargesSar: 88,
      fuelAllowanceSar: 300,
      deliveryChargesSar: 120,
      customerServiceChargesSar: 45,
    },
  ]
}

export function buildDummyInsuranceSetupRows(year: number = currentYear()): InsuranceSetupRow[] {
  const y = year
  return [
    {
      id: `${y}-ins-camry`,
      make: 'Toyota',
      model: 'Camry',
      vehicleType: 'Sedan',
      minVehicleValueSar: 60000,
      maxVehicleValueSar: 140000,
      ratePercent: 1.25,
      minPremiumSar: 850,
    },
    {
      id: `${y}-ins-sonata`,
      make: 'Hyundai',
      model: 'Sonata',
      vehicleType: 'Sedan',
      minVehicleValueSar: 55000,
      maxVehicleValueSar: 130000,
      ratePercent: 1.35,
      minPremiumSar: 800,
    },
    {
      id: `${y}-ins-patrol`,
      make: 'Nissan',
      model: 'Patrol',
      vehicleType: 'SUV',
      minVehicleValueSar: 150000,
      maxVehicleValueSar: 320000,
      ratePercent: 2.1,
      minPremiumSar: 2200,
    },
  ]
}

export function buildDummyVehicleInterestSetupRows(
  year: number = currentYear(),
): VehicleInterestSetupRow[] {
  const y = year
  return [
    {
      id: `${y}-intr-camry`,
      make: 'Toyota',
      model: 'Camry',
      vehicleType: 'Sedan',
      interestRatePercent: 4.25,
      adminFeeSar: 350,
      replacementType: 'Same Class',
      replacementChargesPercent: 3,
    },
    {
      id: `${y}-intr-hilux`,
      make: 'Toyota',
      model: 'Hilux',
      vehicleType: 'Pickup',
      interestRatePercent: 4.65,
      adminFeeSar: 420,
      replacementType: 'Equivalent Utility',
      replacementChargesPercent: 4,
    },
    {
      id: `${y}-intr-patrol`,
      make: 'Nissan',
      model: 'Patrol',
      vehicleType: 'SUV',
      interestRatePercent: 5.1,
      adminFeeSar: 650,
      replacementType: 'Premium SUV',
      replacementChargesPercent: 5,
    },
  ]
}

export function buildDummyDepreciationSetupRows(
  year: number = currentYear(),
): DepreciationSetupRow[] {
  const y = year
  return [
    {
      id: `${y}-dep-camry`,
      make: 'Toyota',
      model: 'Camry',
      vehicleType: 'Sedan',
      annualDepRatePercent: 12,
    },
    {
      id: `${y}-dep-tucson`,
      make: 'Hyundai',
      model: 'Tucson',
      vehicleType: 'SUV',
      annualDepRatePercent: 13.5,
    },
    {
      id: `${y}-dep-hilux`,
      make: 'Toyota',
      model: 'Hilux',
      vehicleType: 'Pickup',
      annualDepRatePercent: 11,
    },
  ]
}

export function buildDummyMaintenanceSetupRows(
  year: number = currentYear(),
): MaintenanceSetupRow[] {
  const y = year
  return [
    {
      id: `${y}-mtc-toyota-sedan-1`,
      manufacturer: 'Toyota',
      vehicleType: 'Sedan',
      minMileageKm: 0,
      maxMileageKm: 30000,
      mtcRateSar: 0.09,
    },
    {
      id: `${y}-mtc-toyota-sedan-2`,
      manufacturer: 'Toyota',
      vehicleType: 'Sedan',
      minMileageKm: 30001,
      maxMileageKm: 120000,
      mtcRateSar: 0.14,
    },
    {
      id: `${y}-mtc-nissan-suv-1`,
      manufacturer: 'Nissan',
      vehicleType: 'SUV',
      minMileageKm: 0,
      maxMileageKm: 120000,
      mtcRateSar: 0.2,
    },
  ]
}

export function buildDummyDiscountOptionSetupRows(
  year: number = currentYear(),
): DiscountOptionSetupRow[] {
  const y = year
  return [
    {
      id: `${y}-disc-standard`,
      optionName: 'Standard Corporate',
      discountPercent: 10,
      requiresWorkflowApproval: false,
    },
    {
      id: `${y}-disc-preferred`,
      optionName: 'Preferred Account',
      discountPercent: 15,
      requiresWorkflowApproval: true,
    },
    {
      id: `${y}-disc-campaign`,
      optionName: 'Campaign Promo',
      discountPercent: 20,
      requiresWorkflowApproval: true,
    },
  ]
}

export function buildDummyTrackingChargeSetupRows(
  year: number = currentYear(),
): TrackingChargeSetupRow[] {
  const y = year
  return [
    {
      id: `${y}-track-sedan`,
      vehicleType: 'Sedan',
      trackingChargesSar: 55,
      tireCountIncluded: 4,
      tireChangeChargesSar: 180,
    },
    {
      id: `${y}-track-suv`,
      vehicleType: 'SUV',
      trackingChargesSar: 75,
      tireCountIncluded: 4,
      tireChangeChargesSar: 230,
    },
    {
      id: `${y}-track-pickup`,
      vehicleType: 'Pickup',
      trackingChargesSar: 82,
      tireCountIncluded: 4,
      tireChangeChargesSar: 240,
    },
  ]
}

export function buildDummyLeaseTermsSetupRows(): LeaseTermSetupRow[] {
  return [
    { id: 'term-12', termMonths: 12, description: '1 year' },
    { id: 'term-24', termMonths: 24, description: '2 years' },
    { id: 'term-36', termMonths: 36, description: '3 years' },
    { id: 'term-48', termMonths: 48, description: '4 years' },
  ]
}

export function buildDummyInterestRateTableRows(
  year: number = currentYear(),
): InterestRateSetupRow[] {
  const from = `${year}-01-01`
  return [
    {
      id: `${year}-ir-12-a`,
      termMonths: 12,
      strategy: 'A',
      annualRatePercent: 5.2,
      effectiveFrom: from,
      isActive: true,
    },
    {
      id: `${year}-ir-24-b`,
      termMonths: 24,
      strategy: 'B',
      annualRatePercent: 5.9,
      effectiveFrom: from,
      isActive: true,
    },
    {
      id: `${year}-ir-36-b`,
      termMonths: 36,
      strategy: 'B',
      annualRatePercent: 6.3,
      effectiveFrom: from,
      isActive: true,
    },
  ]
}

export function buildDummyResidualValueSetupRows(
  year: number = currentYear(),
): ResidualValueSetupRow[] {
  const from = `${year}-01-01`
  return [
    {
      id: `${year}-rv-sedan-24`,
      vehicleType: 'Sedan',
      termMonths: 24,
      rvPercent: 38,
      effectiveFrom: from,
      isActive: true,
    },
    {
      id: `${year}-rv-suv-24`,
      vehicleType: 'SUV',
      termMonths: 24,
      rvPercent: 42,
      effectiveFrom: from,
      isActive: true,
    },
    {
      id: `${year}-rv-pickup-24`,
      vehicleType: 'Pickup',
      termMonths: 24,
      rvPercent: 40,
      effectiveFrom: from,
      isActive: true,
    },
  ]
}

export function buildDummyReplacementPolicySetupRows(
  year: number = currentYear(),
): ReplacementPolicySetupRow[] {
  return [
    {
      id: `${year}-rp-open`,
      policyName: 'Open Replacement',
      strategy: 'OPEN',
      replacementRatePercent: 2.5,
      maxReplacementsPerTerm: 2,
      isActive: true,
    },
    {
      id: `${year}-rp-permanent`,
      policyName: 'Permanent Vehicle',
      strategy: 'PERMANENT',
      replacementRatePercent: 0,
      maxReplacementsPerTerm: 0,
      isActive: true,
    },
  ]
}

export function buildDummyFeeMasterSetupRows(year: number = currentYear()): FeeMasterSetupRow[] {
  return [
    {
      id: `${year}-fee-admin`,
      feeCode: 'ADMIN',
      feeName: 'Admin',
      calculationMethod: 'FIXED_AMOUNT',
      feeValue: 75,
      frequency: 'MONTHLY',
      isActive: true,
    },
    {
      id: `${year}-fee-reg`,
      feeCode: 'REGISTRATION',
      feeName: 'Registration & Fees',
      calculationMethod: 'FIXED_AMOUNT',
      feeValue: 1500,
      frequency: 'ONE_TIME',
      isActive: true,
    },
    {
      id: `${year}-fee-card`,
      feeCode: 'CARD_FEE',
      feeName: 'Card Fee',
      calculationMethod: 'PERCENT_OF_INSTALLMENT',
      feeValue: 1.5,
      frequency: 'MONTHLY',
      isActive: true,
    },
    {
      id: `${year}-fee-track`,
      feeCode: 'TRACKING',
      feeName: 'Tracking',
      calculationMethod: 'FIXED_AMOUNT',
      feeValue: 65,
      frequency: 'MONTHLY',
      isActive: true,
    },
    {
      id: `${year}-fee-cwm`,
      feeCode: 'CAR_WASH_MANPOWER',
      feeName: 'Car Wash/Manpower',
      calculationMethod: 'FIXED_AMOUNT',
      feeValue: 45,
      frequency: 'MONTHLY',
      isActive: true,
    },
  ]
}

export function buildDummyCommissionRateTableRows(
  year: number = currentYear(),
): CommissionRateSetupRow[] {
  const from = `${year}-01-01`
  return [
    {
      id: `${year}-comm-direct`,
      channelName: 'Direct',
      commissionPercent: 2,
      effectiveFrom: from,
      isActive: true,
    },
    {
      id: `${year}-comm-broker`,
      channelName: 'Broker',
      commissionPercent: 3.5,
      effectiveFrom: from,
      isActive: true,
    },
  ]
}

export function buildDummyProfitMarginSetupRows(
  year: number = currentYear(),
): ProfitMarginSetupRow[] {
  const from = `${year}-01-01`
  return [
    {
      id: `${year}-pm-sedan`,
      vehicleType: 'Sedan',
      marginPercent: 8,
      effectiveFrom: from,
      isActive: true,
    },
    {
      id: `${year}-pm-suv`,
      vehicleType: 'SUV',
      marginPercent: 9,
      effectiveFrom: from,
      isActive: true,
    },
    {
      id: `${year}-pm-pickup`,
      vehicleType: 'Pickup',
      marginPercent: 8.5,
      effectiveFrom: from,
      isActive: true,
    },
  ]
}

export function buildDummyCalendarPeriodsSetupRows(
  year: number = currentYear(),
): CalendarPeriodSetupRow[] {
  return Array.from({ length: 12 }, (_, i) => {
    const month = i + 1
    const label = `${year}-${String(month).padStart(2, '0')}`
    const start = `${label}-01`
    const endDate = new Date(year, month, 0)
    const end = `${label}-${String(endDate.getDate()).padStart(2, '0')}`
    return {
      id: `period-${label}`,
      periodLabel: label,
      periodStart: start,
      periodEnd: end,
    }
  })
}

export function buildDummyQuotationPricingSetupData(
  year: number = currentYear(),
): QuotationPricingSetupData {
  return {
    vehicles: buildDummyQuotationPricingVehicles(year),
    insurance: buildDummyInsuranceSetupRows(year),
    vehicleInterest: buildDummyVehicleInterestSetupRows(year),
    depreciation: buildDummyDepreciationSetupRows(year),
    maintenance: buildDummyMaintenanceSetupRows(year),
    discountOptions: buildDummyDiscountOptionSetupRows(year),
    trackingCharges: buildDummyTrackingChargeSetupRows(year),
    leaseTerms: buildDummyLeaseTermsSetupRows(),
    interestRateTable: buildDummyInterestRateTableRows(year),
    residualValueTable: buildDummyResidualValueSetupRows(year),
    replacementPolicy: buildDummyReplacementPolicySetupRows(year),
    feeMaster: buildDummyFeeMasterSetupRows(year),
    commissionRateTable: buildDummyCommissionRateTableRows(year),
    profitMarginSetup: buildDummyProfitMarginSetupRows(year),
    calendarPeriods: buildDummyCalendarPeriodsSetupRows(year),
  }
}

function canUseStorage(): boolean {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined'
}

export function loadQuotationPricingVehicles(): QuotationPricingVehicleProfile[] {
  return loadQuotationPricingSetupData().vehicles
}

export function loadQuotationPricingSetupData(): QuotationPricingSetupData {
  if (!canUseStorage()) {
    return {
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
    }
  }
  try {
    const raw = window.localStorage.getItem(QUOTATION_PRICING_STORAGE_KEY)
    if (!raw) {
      return {
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
      }
    }
    const parsed = JSON.parse(raw) as QuotationPricingSetupData
    if (!parsed || typeof parsed !== 'object') {
      throw new Error('Invalid setup format')
    }
    return {
      vehicles: Array.isArray(parsed.vehicles) ? parsed.vehicles : [],
      insurance: Array.isArray(parsed.insurance) ? parsed.insurance : [],
      vehicleInterest: Array.isArray(parsed.vehicleInterest) ? parsed.vehicleInterest : [],
      depreciation: Array.isArray(parsed.depreciation) ? parsed.depreciation : [],
      maintenance: Array.isArray(parsed.maintenance) ? parsed.maintenance : [],
      discountOptions: Array.isArray(parsed.discountOptions) ? parsed.discountOptions : [],
      trackingCharges: Array.isArray(parsed.trackingCharges) ? parsed.trackingCharges : [],
      leaseTerms: Array.isArray(parsed.leaseTerms) ? parsed.leaseTerms : [],
      interestRateTable: Array.isArray(parsed.interestRateTable) ? parsed.interestRateTable : [],
      residualValueTable: Array.isArray(parsed.residualValueTable) ? parsed.residualValueTable : [],
      replacementPolicy: Array.isArray(parsed.replacementPolicy) ? parsed.replacementPolicy : [],
      feeMaster: Array.isArray(parsed.feeMaster) ? parsed.feeMaster : [],
      commissionRateTable: Array.isArray(parsed.commissionRateTable)
        ? parsed.commissionRateTable
        : [],
      profitMarginSetup: Array.isArray(parsed.profitMarginSetup) ? parsed.profitMarginSetup : [],
      calendarPeriods: Array.isArray(parsed.calendarPeriods) ? parsed.calendarPeriods : [],
    }
  } catch {
    return {
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
    }
  }
}

export function saveQuotationPricingVehicles(items: QuotationPricingVehicleProfile[]): void {
  const existing = loadQuotationPricingSetupData()
  saveQuotationPricingSetupData({
    ...existing,
    vehicles: items,
  })
}

export function saveQuotationPricingSetupData(items: QuotationPricingSetupData): void {
  if (!canUseStorage()) return
  window.localStorage.setItem(QUOTATION_PRICING_STORAGE_KEY, JSON.stringify(items))
}

export function seedQuotationPricingVehiclesIfEmpty(
  year: number = currentYear(),
): QuotationPricingVehicleProfile[] {
  return seedQuotationPricingSetupIfEmpty(year).vehicles
}

export function seedQuotationPricingSetupIfEmpty(
  year: number = currentYear(),
): QuotationPricingSetupData {
  const existing = loadQuotationPricingSetupData()
  if (existing.vehicles.length > 0) return existing

  const seeded = buildDummyQuotationPricingSetupData(year)
  saveQuotationPricingSetupData(seeded)
  return seeded
}

export function resolveDefaultDiscountPercent(data: QuotationPricingSetupData): number {
  const preferred = data.discountOptions
    .filter((x) => !x.requiresWorkflowApproval)
    .sort((a, b) => a.discountPercent - b.discountPercent)[0]

  if (preferred) return preferred.discountPercent

  const fallback = data.discountOptions.sort((a, b) => a.discountPercent - b.discountPercent)[0]

  return fallback ? fallback.discountPercent : 0
}
