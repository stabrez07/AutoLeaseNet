import { BFF_BASE_URL, DEV_TENANT_ID } from './bff-client'
import {
  buildDummyQuotationPricingSetupData,
  loadQuotationPricingSetupData,
  saveQuotationPricingSetupData,
  seedQuotationPricingSetupIfEmpty,
  type QuotationPricingSetupData,
} from './quotation-pricing-catalog'

function emptySetup(): QuotationPricingSetupData {
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

function normalizeSetupData(value: unknown): QuotationPricingSetupData {
  if (!value || typeof value !== 'object') return emptySetup()

  const asObj = value as Partial<QuotationPricingSetupData>
  return {
    vehicles: Array.isArray(asObj.vehicles) ? asObj.vehicles : [],
    insurance: Array.isArray(asObj.insurance) ? asObj.insurance : [],
    vehicleInterest: Array.isArray(asObj.vehicleInterest) ? asObj.vehicleInterest : [],
    depreciation: Array.isArray(asObj.depreciation) ? asObj.depreciation : [],
    maintenance: Array.isArray(asObj.maintenance) ? asObj.maintenance : [],
    discountOptions: Array.isArray(asObj.discountOptions) ? asObj.discountOptions : [],
    trackingCharges: Array.isArray(asObj.trackingCharges) ? asObj.trackingCharges : [],
    leaseTerms: Array.isArray(asObj.leaseTerms) ? asObj.leaseTerms : [],
    interestRateTable: Array.isArray(asObj.interestRateTable) ? asObj.interestRateTable : [],
    residualValueTable: Array.isArray(asObj.residualValueTable) ? asObj.residualValueTable : [],
    replacementPolicy: Array.isArray(asObj.replacementPolicy) ? asObj.replacementPolicy : [],
    feeMaster: Array.isArray(asObj.feeMaster) ? asObj.feeMaster : [],
    commissionRateTable: Array.isArray(asObj.commissionRateTable) ? asObj.commissionRateTable : [],
    profitMarginSetup: Array.isArray(asObj.profitMarginSetup) ? asObj.profitMarginSetup : [],
    calendarPeriods: Array.isArray(asObj.calendarPeriods) ? asObj.calendarPeriods : [],
  }
}

function buildHeaders(extra: Record<string, string> = {}): HeadersInit {
  return {
    'X-Dev-Tenant-Id': DEV_TENANT_ID,
    'X-Dev-User-Type': 'INTERNAL_STAFF',
    ...extra,
  }
}

export async function loadQuotationPricingSetupFromApi(): Promise<QuotationPricingSetupData> {
  const response = await fetch(`${BFF_BASE_URL}/api/v1/admin/quotation-pricing-setup`, {
    method: 'GET',
    headers: buildHeaders(),
    cache: 'no-store',
  })

  if (!response.ok) {
    throw new Error(`Failed to load pricing setup (${response.status})`)
  }

  const data = normalizeSetupData(await response.json())
  saveQuotationPricingSetupData(data)
  return data
}

export async function saveQuotationPricingSetupToApi(
  data: QuotationPricingSetupData,
): Promise<void> {
  const response = await fetch(`${BFF_BASE_URL}/api/v1/admin/quotation-pricing-setup`, {
    method: 'PUT',
    headers: buildHeaders({
      'Content-Type': 'application/json',
      'Idempotency-Key': crypto.randomUUID(),
    }),
    body: JSON.stringify(data),
  })

  if (!response.ok) {
    throw new Error(`Failed to save pricing setup (${response.status})`)
  }

  saveQuotationPricingSetupData(data)
}

export async function loadOrSeedQuotationPricingSetup(
  year: number,
): Promise<QuotationPricingSetupData> {
  try {
    const remote = await loadQuotationPricingSetupFromApi()
    if (remote.vehicles.length > 0) return remote

    const seeded = buildDummyQuotationPricingSetupData(year)
    await saveQuotationPricingSetupToApi(seeded)
    return seeded
  } catch {
    return seedQuotationPricingSetupIfEmpty(year)
  }
}

export async function loadQuotationPricingSetupWithLocalFallback(): Promise<QuotationPricingSetupData> {
  try {
    return await loadQuotationPricingSetupFromApi()
  } catch {
    return loadQuotationPricingSetupData()
  }
}
