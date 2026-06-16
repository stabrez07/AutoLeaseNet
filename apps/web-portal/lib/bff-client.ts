// Typed BFF client. Shapes mirror Specs/06-bff-api-surface.md and the actual handlers
// in services/bff/Endpoints/*.cs. When packages/contracts/generated/schema.d.ts lands
// (via `pnpm openapi:gen`), these interfaces should be replaced with the generated
// types. Until then we hand-maintain them deliberately small.

export const BFF_BASE_URL = process.env.NEXT_PUBLIC_BFF_BASE_URL ?? 'http://localhost:5000'
export const USE_MOCK_BFF =
  process.env.NODE_ENV !== 'production' && (process.env.NEXT_PUBLIC_USE_MOCK ?? 'true') !== 'false'

// Dev tenant id matches the seed tenant + Phase 1 fallback in TajeerWebhookEndpoints.cs.
export const DEV_TENANT_ID =
  process.env.NEXT_PUBLIC_DEV_TENANT_ID ?? 'a1a1a1a1-0001-0000-0000-000000000001'

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface CustomerSummary {
  id: string
  displayName: string
  type: number // 1 = B2B, 2 = B2C
  mobile?: string | null
  isActive: boolean
}

export interface VehicleSummary {
  id: string
  plateNumber: string
  make: string
  model: string
  modelYear?: number
  status: number // 1=Available 2=Reserved 3=OnLease 4=InService 5=Retired
  currentKm: number
}

export interface DriverSummary {
  id: string
  personNameEn?: string
  personNameAr?: string
  driverLicenseNumber: string
  licenseExpiryDate: string
  status: number // 1=Active 2=Suspended 3=Retired
}

export interface BranchDto {
  id: string
  code: string
  nameEn: string
  nameAr: string
  city?: string
  isActive: boolean
}

export interface RentPolicyDto {
  id: string
  code: string
  nameEn: string
  nameAr: string
  isActive: boolean
}

export interface ExtendedCoverageDto {
  id: string
  code: string
  nameEn: string
  nameAr: string
  isActive: boolean
}

export interface SaveContractRequest {
  customerId: string
  vehicleId: string
  primaryDriverId: string
  rentPolicyId: string
  workingBranchId: string
  receiveBranchId: string
  returnBranchId: string
  contractStartUtc: string
  contractEndUtc: string
  contractTypeCode: number
  allowedKmPerDay: number
  rentAmount: number
  paidAmount: number
  paymentMethodCode: number
}

export interface SaveContractResponse {
  leaseId: string
  tajeerContractNumber: number
  issuanceUrl: string
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errorCode?: string
}

// ─── Quotation types ────────────────────────────────────────────────────────

export interface QuotationSummary {
  id: string
  quoteNumber: string
  customerId: string
  customerDisplayName?: string
  status: string
  contractType: string
  totalSar: number
  subTotalSar: number
  vatSar: number
  discountPercent: number
  quoteDate: string
  validUntilDate: string
  estimatedDurationMonths: number
  submittedAtUtc?: string | null
  approvedAtUtc?: string | null
  sentAtUtc?: string | null
  acceptedAtUtc?: string | null
}

export interface QuotationLine {
  id: string
  lineNumber: number
  itemType: string
  description: string
  vehicleSpecRef?: string | null
  quantity: number
  unitPriceSar: number
  discountPercent: number
  lineTotalSar: number
}

export interface QuotationApproval {
  tierLevel: number
  requiredRoleCode: string
  status: string
  decidedByUserId?: string | null
  comment?: string | null
  decidedAtUtc?: string | null
}

export interface QuotationDetail extends QuotationSummary {
  lines: QuotationLine[]
  approvals: QuotationApproval[]
  pdfBlobUri?: string | null
  acceptedByCustomerSignature?: string | null
}

export interface CreateQuotationRequest {
  customerId: string
  accountManagerId: string
  quoteDate: string         // YYYY-MM-DD
  validUntilDate: string    // YYYY-MM-DD
  contractType: string
  estimatedDurationMonths: number
  discountPercent: number
  termsAndConditionsMd?: string | undefined
}

export interface AddQuotationLineRequest {
  itemType: string
  description: string
  vehicleSpecRef?: string | undefined
  quantity: number
  unitPriceSar: number
  discountPercent: number
}

export interface QuotationCommandResult {
  success: boolean
  quotationId?: string | null
  status?: string | null
  nextTierLevel?: number | null
  nextRequiredRoleCode?: string | null
  errorCode?: string | null
  errorMessage?: string | null
}

export interface AcceptQuotationResult {
  success: boolean
  quotationId?: string | null
  quoteNumber?: string | null
  status?: string | null
  acceptedAtUtc?: string | null
  errorCode?: string | null
  errorMessage?: string | null
}

class BffClient {
  private headers(extra: Record<string, string> = {}): HeadersInit {
    return {
      'X-Dev-Tenant-Id': DEV_TENANT_ID,
      'X-Dev-User-Type': 'InternalStaff',
      ...extra,
    }
  }

  async getJson<T>(path: string, init?: RequestInit): Promise<T> {
    const res = await fetch(`${BFF_BASE_URL}${path}`, {
      ...init,
      cache: 'no-store',
      headers: { ...this.headers(), ...(init?.headers as Record<string, string>) },
    })
    if (!res.ok) {
      const problem = await this.tryReadProblem(res)
      throw Object.assign(new Error(problem.title ?? `BFF GET ${path} failed (${res.status})`), {
        status: res.status,
        problem,
      })
    }
    return (await res.json()) as T
  }

  async postJson<TResponse, TBody>(
    path: string,
    body: TBody,
    extraHeaders: Record<string, string> = {},
  ): Promise<TResponse> {
    const res = await fetch(`${BFF_BASE_URL}${path}`, {
      method: 'POST',
      headers: {
        ...this.headers(extraHeaders),
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    })
    if (!res.ok) {
      const problem = await this.tryReadProblem(res)
      throw Object.assign(new Error(problem.title ?? `BFF POST ${path} failed (${res.status})`), {
        status: res.status,
        problem,
      })
    }
    return (await res.json()) as TResponse
  }

  private async tryReadProblem(res: Response): Promise<ProblemDetails> {
    try {
      return (await res.json()) as ProblemDetails
    } catch {
      return { title: res.statusText, status: res.status }
    }
  }

  // Lookups
  getBranches() {
    return this.getJson<BranchDto[]>('/api/v1/lookups/branches')
  }
  getRentPolicies() {
    return this.getJson<RentPolicyDto[]>('/api/v1/lookups/rent-policies')
  }
  getExtendedCoverages() {
    return this.getJson<ExtendedCoverageDto[]>('/api/v1/lookups/extended-coverages')
  }
  getCustomers(page = 1, pageSize = 20, search?: string) {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search) q.set('search', search)
    return this.getJson<PagedResult<CustomerSummary>>(`/api/v1/lookups/customers?${q.toString()}`)
  }
  getVehicles(page = 1, pageSize = 20, search?: string, status?: number) {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search) q.set('search', search)
    if (status) q.set('status', String(status))
    return this.getJson<PagedResult<VehicleSummary>>(`/api/v1/lookups/vehicles?${q.toString()}`)
  }
  getDrivers(page = 1, pageSize = 20, search?: string) {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search) q.set('search', search)
    return this.getJson<PagedResult<DriverSummary>>(`/api/v1/lookups/drivers?${q.toString()}`)
  }

  // Dev SaveContract
  saveContract(body: SaveContractRequest, idempotencyKey: string) {
    return this.postJson<SaveContractResponse, SaveContractRequest>(
      '/api/v1/dev/save-contract',
      body,
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  // ─── Quotations ────────────────────────────────────────────────────────────

  getQuotations(page = 1, pageSize = 20, search?: string) {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search) q.set('search', search)
    return this.getJson<PagedResult<QuotationSummary>>(`/api/v1/quotations?${q.toString()}`)
  }

  getQuotation(id: string) {
    return this.getJson<QuotationDetail>(`/api/v1/quotations/${id}`)
  }

  createQuotation(body: CreateQuotationRequest, idempotencyKey: string) {
    return this.postJson<QuotationDetail, CreateQuotationRequest>(
      '/api/v1/quotations',
      body,
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  addQuotationLine(quotationId: string, body: AddQuotationLineRequest, idempotencyKey: string) {
    return this.postJson<QuotationDetail, AddQuotationLineRequest>(
      `/api/v1/quotations/${quotationId}/lines`,
      body,
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  submitQuotationForApproval(quotationId: string, idempotencyKey: string) {
    return this.postJson<QuotationCommandResult, Record<string, never>>(
      `/api/v1/quotations/${quotationId}/submit-approval`,
      {},
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  recordApprovalDecision(
    quotationId: string,
    tierLevel: number,
    approved: boolean,
    comment: string | undefined,
    idempotencyKey: string,
  ) {
    return this.postJson<QuotationCommandResult, { approved: boolean; comment?: string | undefined }>(
      `/api/v1/quotations/${quotationId}/approvals/${tierLevel}/decision`,
      { approved, comment },
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  acceptQuotation(quotationId: string, customerSignature: string | undefined, idempotencyKey: string) {
    return this.postJson<AcceptQuotationResult, { customerSignature?: string | undefined }>(
      `/api/v1/quotations/${quotationId}/accept`,
      { customerSignature },
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  // ─── Customers CRUD ─────────────────────────────────────────────────────────

  getCustomerById(id: string) {
    return this.getJson<CustomerDetail>(`/api/v1/customers/${id}`)
  }

  createCustomerB2B(body: CreateCustomerB2BRequest, idempotencyKey: string) {
    return this.postJson<CustomerCommandResult, CreateCustomerB2BRequest>(
      '/api/v1/customers/b2b',
      body,
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  createCustomerB2C(body: CreateCustomerB2CRequest, idempotencyKey: string) {
    return this.postJson<CustomerCommandResult, CreateCustomerB2CRequest>(
      '/api/v1/customers/b2c',
      body,
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  updateCustomerStatus(id: string, action: string, idempotencyKey: string) {
    return this.postJson<CustomerCommandResult, { action: string }>(
      `/api/v1/customers/${id}/status`,
      { action },
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  // ─── Vehicles CRUD ──────────────────────────────────────────────────────────

  getVehicleById(id: string) {
    return this.getJson<VehicleDetail>(`/api/v1/vehicles/${id}`)
  }

  createVehicle(body: CreateVehicleRequest, idempotencyKey: string) {
    return this.postJson<VehicleCommandResult, CreateVehicleRequest>(
      '/api/v1/vehicles',
      body,
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  // ─── Drivers CRUD ───────────────────────────────────────────────────────────

  getDriverById(id: string) {
    return this.getJson<DriverDetail>(`/api/v1/drivers/${id}`)
  }

  createDriver(body: CreateDriverRequest, idempotencyKey: string) {
    return this.postJson<DriverCommandResult, CreateDriverRequest>(
      '/api/v1/drivers',
      body,
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  // ─── Branches CRUD ──────────────────────────────────────────────────────────

  getBranchById(id: string) {
    return this.getJson<BranchDetail>(`/api/v1/branches/${id}`)
  }

  createBranch(body: CreateBranchRequest, idempotencyKey: string) {
    return this.postJson<BranchCommandResult, CreateBranchRequest>(
      '/api/v1/branches',
      body,
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  updateBranchStatus(id: string, activate: boolean, idempotencyKey: string) {
    return this.postJson<BranchCommandResult, { activate: boolean }>(
      `/api/v1/branches/${id}/status`,
      { activate },
      { 'Idempotency-Key': idempotencyKey },
    )
  }
}

// ─── CRUD types ─────────────────────────────────────────────────────────────

export interface CustomerDetail {
  id: string; tenantId: string
  type: string; status: string
  displayName: string; displayNameAr?: string | null
  email?: string | null; mobile?: string | null
  nationalAddress?: string | null; preferredLanguage: string
  legalName?: string | null; legalNameAr?: string | null
  commercialRegistration?: string | null; vatNumber?: string | null
  billingAddress?: string | null
  creditLimit?: number | null; creditCurrency?: string | null
  personNameEn?: string | null; personNameAr?: string | null
  idTypeCode?: number | null; personIdNumber?: string | null
  dateOfBirth?: string | null; nationalityCode?: string | null
  kycVerified: boolean; kycVerifiedAtUtc?: string | null; kycVerifiedBy?: string | null
  createdAtUtc: string; updatedAtUtc: string
}

export interface CustomerCommandResult {
  success: boolean; customerId?: string | null; status?: string | null
  errorCode?: string | null; errorMessage?: string | null
}

export interface CreateCustomerB2BRequest {
  legalName: string; legalNameAr?: string | undefined
  commercialRegistration: string; vatNumber?: string | undefined
  email?: string | undefined; mobile?: string | undefined
  nationalAddress?: string | undefined; billingAddress?: string | undefined
  creditLimit?: number | undefined; creditCurrency?: string | undefined
}

export interface CreateCustomerB2CRequest {
  personNameEn: string; personNameAr?: string | undefined
  idTypeCode: number; personIdNumber: string
  dateOfBirth?: string | undefined; nationalityCode?: string | undefined
  email?: string | undefined; mobile?: string | undefined; nationalAddress?: string | undefined
}

export interface VehicleDetail {
  id: string; tenantId: string; status: string
  plateNumber: string; plateLetters: string; plateTypeCode: number
  vin: string; engineNumber?: string | null
  make: string; model: string; modelYear: number; color?: string | null
  fuelType: string; transmissionType: string; bodyType: string; seats: number
  licenseExpiryDate?: string | null; insuranceExpiryDate?: string | null; inspectionExpiryDate?: string | null
  insuranceCompany?: string | null; insurancePolicyNumber?: string | null
  ownerBranchId: string; currentBranchId: string
  currentKm: number; purchasePrice?: number | null; purchaseDate?: string | null
  createdAtUtc: string; updatedAtUtc: string
}

export interface VehicleCommandResult {
  success: boolean; vehicleId?: string | null
  errorCode?: string | null; errorMessage?: string | null
}

export interface CreateVehicleRequest {
  plateNumber: string; plateLetters: string; plateTypeCode: number
  vin: string; engineNumber?: string | undefined
  make: string; model: string; modelYear: number; color?: string | undefined
  fuelType: number; transmissionType: number; bodyType: number; seats: number
  licenseExpiryDate?: string | undefined; insuranceExpiryDate?: string | undefined; inspectionExpiryDate?: string | undefined
  insuranceCompany?: string | undefined; insurancePolicyNumber?: string | undefined
  ownerBranchId: string; currentKm: number
  purchasePrice?: number | undefined; purchaseDate?: string | undefined
}

export interface DriverDetail {
  id: string; tenantId: string; status: string
  customerId?: string | null
  personNameEn: string; personNameAr?: string | null
  idTypeCode: number; personIdNumber: string
  dateOfBirth?: string | null; nationalityCode?: string | null
  driverLicenseNumber: string; licenseClass: number; licenseExpiryDate: string
  mobile?: string | null; email?: string | null; nationalAddress?: string | null
  tammAuthorizationStatus: string
  createdAtUtc: string; updatedAtUtc: string
}

export interface DriverCommandResult {
  success: boolean; driverId?: string | null
  errorCode?: string | null; errorMessage?: string | null
}

export interface CreateDriverRequest {
  personNameEn: string; personNameAr?: string | undefined
  idTypeCode: number; personIdNumber: string
  dateOfBirth?: string | undefined; nationalityCode?: string | undefined
  driverLicenseNumber: string; licenseClass: number; licenseExpiryDate: string
  mobile?: string | undefined; email?: string | undefined; nationalAddress?: string | undefined
  customerId?: string | undefined
}

export interface BranchDetail {
  id: string; tenantId: string
  code: string; nameEn: string; nameAr: string
  cityEn?: string | null; cityAr?: string | null
  regionEn?: string | null; regionAr?: string | null
  licenseNumber?: string | null; address?: string | null; phoneNumber?: string | null
  latitude?: number | null; longitude?: number | null
  tajeerBranchId: number; tajeerOperatorId: number
  isActive: boolean; createdAtUtc: string; updatedAtUtc: string
}

export interface BranchCommandResult {
  success: boolean; branchId?: string | null
  errorCode?: string | null; errorMessage?: string | null
}

export interface CreateBranchRequest {
  code: string; nameEn: string; nameAr: string
  cityEn?: string | undefined; cityAr?: string | undefined
  regionEn?: string | undefined; regionAr?: string | undefined
  address?: string | undefined; phoneNumber?: string | undefined; licenseNumber?: string | undefined
  latitude?: number | undefined; longitude?: number | undefined
  tajeerBranchId: number; tajeerOperatorId: number
}

type MockState = {
  customers: CustomerDetail[]
  vehicles: VehicleDetail[]
  drivers: DriverDetail[]
  branches: BranchDetail[]
  quotations: QuotationDetail[]
}

function mockId(prefix: string, n: number) {
  return `${prefix}-${n.toString().padStart(5, '0')}`
}

function paginate<T>(items: T[], page = 1, pageSize = 20): PagedResult<T> {
  const safePage = Math.max(1, page)
  const safePageSize = Math.max(1, pageSize)
  const totalCount = items.length
  const totalPages = Math.max(1, Math.ceil(totalCount / safePageSize))
  const start = (safePage - 1) * safePageSize
  return {
    items: items.slice(start, start + safePageSize),
    page: safePage,
    pageSize: safePageSize,
    totalCount,
    totalPages,
  }
}

function pick<T>(arr: T[], i: number): T {
  return arr[i % arr.length] as T
}

function buildMockState(): MockState {
  const now = new Date()
  const branches: BranchDetail[] = Array.from({ length: 80 }).map((_, i) => ({
    id: mockId('branch', i + 1),
    tenantId: DEV_TENANT_ID,
    code: `BR-${(i + 1).toString().padStart(3, '0')}`,
    nameEn: `Branch ${i + 1}`,
    nameAr: `فرع ${i + 1}`,
    cityEn: pick(['Riyadh', 'Jeddah', 'Dammam', 'Makkah', 'Madinah'], i),
    cityAr: pick(['الرياض', 'جدة', 'الدمام', 'مكة', 'المدينة'], i),
    regionEn: 'KSA',
    regionAr: 'السعودية',
    licenseNumber: `LIC-${1000 + i}`,
    address: `District ${i + 1}, Saudi Arabia`,
    phoneNumber: `+96611${(100000 + i).toString().slice(-6)}`,
    latitude: 24.5 + (i % 10) * 0.01,
    longitude: 46.6 + (i % 10) * 0.01,
    tajeerBranchId: i + 1,
    tajeerOperatorId: 1000 + i,
    isActive: i % 7 !== 0,
    createdAtUtc: now.toISOString(),
    updatedAtUtc: now.toISOString(),
  }))

  const customers: CustomerDetail[] = Array.from({ length: 320 }).map((_, i) => {
    const isB2B = i % 5 === 0
    const status = i % 11 === 0 ? 'Suspended' : i % 17 === 0 ? 'Closed' : 'Active'
    return {
      id: mockId('customer', i + 1),
      tenantId: DEV_TENANT_ID,
      type: isB2B ? 'B2B' : 'B2C',
      status,
      displayName: isB2B ? `Company ${i + 1}` : `Customer ${i + 1}`,
      displayNameAr: isB2B ? `شركة ${i + 1}` : `عميل ${i + 1}`,
      email: `customer${i + 1}@example.sa`,
      mobile: `+9665${(10000000 + i).toString().slice(-8)}`,
      nationalAddress: `Address ${i + 1}`,
      preferredLanguage: i % 2 === 0 ? 'ar' : 'en',
      legalName: isB2B ? `Company ${i + 1} LLC` : null,
      legalNameAr: isB2B ? `شركة ${i + 1}` : null,
      commercialRegistration: isB2B ? `10${(10000000 + i).toString().slice(-8)}` : null,
      vatNumber: isB2B ? `30${(100000000000000 + i).toString().slice(-15)}` : null,
      billingAddress: isB2B ? `Billing ${i + 1}` : null,
      creditLimit: isB2B ? 100000 + i * 100 : null,
      creditCurrency: isB2B ? 'SAR' : null,
      personNameEn: isB2B ? null : `Person ${i + 1}`,
      personNameAr: isB2B ? null : `شخص ${i + 1}`,
      idTypeCode: isB2B ? null : i % 3 === 0 ? 2 : 1,
      personIdNumber: isB2B ? null : `${i % 3 === 0 ? '2' : '1'}${(100000000 + i).toString().slice(-9)}`,
      dateOfBirth: isB2B ? null : `19${80 + (i % 20)}-01-15`,
      nationalityCode: isB2B ? null : i % 3 === 0 ? 'EG' : 'SA',
      kycVerified: i % 4 !== 0,
      kycVerifiedAtUtc: i % 4 !== 0 ? now.toISOString() : null,
      kycVerifiedBy: i % 4 !== 0 ? 'system' : null,
      createdAtUtc: now.toISOString(),
      updatedAtUtc: now.toISOString(),
    }
  })

  const vehicles: VehicleDetail[] = Array.from({ length: 420 }).map((_, i) => {
    const st = pick(['Available', 'Reserved', 'OnRent', 'InService', 'Damaged'], i)
    const branch = pick(branches, i)
    return {
      id: mockId('vehicle', i + 1),
      tenantId: DEV_TENANT_ID,
      status: st,
      plateNumber: `${1000 + i}`,
      plateLetters: pick(['أ ب ج', 'د هـ و', 'ز ح ط', 'ي ك ل'], i),
      plateTypeCode: 1,
      vin: `VIN${(100000000000 + i).toString().slice(-12)}`,
      engineNumber: `ENG-${i + 1}`,
      make: pick(['Toyota', 'Hyundai', 'Nissan', 'Kia'], i),
      model: pick(['Camry', 'Sonata', 'Altima', 'Sportage'], i),
      modelYear: 2021 + (i % 5),
      color: pick(['White', 'Silver', 'Black', 'Grey'], i),
      fuelType: pick(['Petrol91', 'Petrol95', 'Diesel', 'Hybrid'], i),
      transmissionType: pick(['Automatic', 'Manual', 'CVT'], i),
      bodyType: pick(['Sedan', 'Suv', 'Pickup', 'Van'], i),
      seats: pick([4, 5, 7], i),
      licenseExpiryDate: `2027-${String((i % 12) + 1).padStart(2, '0')}-15`,
      insuranceExpiryDate: `2027-${String((i % 12) + 1).padStart(2, '0')}-20`,
      inspectionExpiryDate: `2027-${String((i % 12) + 1).padStart(2, '0')}-25`,
      insuranceCompany: 'Tawuniya',
      insurancePolicyNumber: `POL-${100000 + i}`,
      ownerBranchId: branch.id,
      currentBranchId: branch.id,
      currentKm: 10000 + i * 120,
      purchasePrice: 80000 + i * 50,
      purchaseDate: `202${i % 4}-01-10`,
      createdAtUtc: now.toISOString(),
      updatedAtUtc: now.toISOString(),
    }
  })

  const drivers: DriverDetail[] = Array.from({ length: 520 }).map((_, i) => ({
    id: mockId('driver', i + 1),
    tenantId: DEV_TENANT_ID,
    status: pick(['Active', 'Suspended', 'Retired'], i),
    customerId: pick(customers, i).id,
    personNameEn: `Driver ${i + 1}`,
    personNameAr: `سائق ${i + 1}`,
    idTypeCode: i % 3 === 0 ? 2 : 1,
    personIdNumber: `${i % 3 === 0 ? '2' : '1'}${(200000000 + i).toString().slice(-9)}`,
    dateOfBirth: `19${75 + (i % 20)}-02-10`,
    nationalityCode: i % 3 === 0 ? 'EG' : 'SA',
    driverLicenseNumber: `DL-${300000 + i}`,
    licenseClass: pick([1, 2, 3], i),
    licenseExpiryDate: `2027-${String((i % 12) + 1).padStart(2, '0')}-10`,
    mobile: `+9665${(30000000 + i).toString().slice(-8)}`,
    email: `driver${i + 1}@example.sa`,
    nationalAddress: `Driver Address ${i + 1}`,
    tammAuthorizationStatus: pick(['NotRequested', 'Pending', 'Authorized', 'Rejected'], i),
    createdAtUtc: now.toISOString(),
    updatedAtUtc: now.toISOString(),
  }))

  const quotations: QuotationDetail[] = Array.from({ length: 180 }).map((_, i) => {
    const id = mockId('quote', i + 1)
    const sub = 3000 + i * 120
    const vat = Math.round(sub * 0.15 * 100) / 100
    const total = sub + vat
    const status = pick(['Draft', 'PendingApproval', 'Approved', 'SentToCustomer', 'Accepted'], i)
    return {
      id,
      quoteNumber: `QT-${(i + 1).toString().padStart(6, '0')}`,
      customerId: pick(customers, i).id,
      customerDisplayName: pick(customers, i).displayName,
      status,
      contractType: pick(['Daily', 'Monthly'], i),
      totalSar: total,
      subTotalSar: sub,
      vatSar: vat,
      discountPercent: i % 10,
      quoteDate: now.toISOString().substring(0, 10),
      validUntilDate: new Date(now.getTime() + 14 * 86400000).toISOString().substring(0, 10),
      estimatedDurationMonths: 12,
      submittedAtUtc: status === 'Draft' ? null : now.toISOString(),
      approvedAtUtc: status === 'Approved' || status === 'SentToCustomer' || status === 'Accepted' ? now.toISOString() : null,
      sentAtUtc: status === 'SentToCustomer' || status === 'Accepted' ? now.toISOString() : null,
      acceptedAtUtc: status === 'Accepted' ? now.toISOString() : null,
      lines: [{
        id: mockId('line', i + 1),
        lineNumber: 1,
        itemType: 'Vehicle',
        description: pick(vehicles, i).make + ' ' + pick(vehicles, i).model,
        vehicleSpecRef: pick(vehicles, i).id,
        quantity: 1,
        unitPriceSar: sub,
        discountPercent: i % 10,
        lineTotalSar: sub,
      }],
      approvals: [
        { tierLevel: 1, requiredRoleCode: 'SALES_TIER_1', status: status === 'Draft' ? 'Pending' : 'Approved', comment: null, decidedByUserId: null, decidedAtUtc: null },
      ],
      pdfBlobUri: null,
      acceptedByCustomerSignature: status === 'Accepted' ? 'Signed Customer' : null,
    }
  })

  return { customers, vehicles, drivers, branches, quotations }
}

class MockBffClient {
  private state = buildMockState()

  getBranches() {
    return Promise.resolve(this.state.branches.map((b) => ({
      id: b.id, code: b.code, nameEn: b.nameEn, nameAr: b.nameAr, city: b.cityEn ?? undefined, isActive: b.isActive,
    })))
  }
  getRentPolicies() { return Promise.resolve([{ id: 'rp-1', code: 'STD', nameEn: 'Standard', nameAr: 'قياسي', isActive: true }]) }
  getExtendedCoverages() { return Promise.resolve([{ id: 'ec-1', code: 'CDW', nameEn: 'CDW', nameAr: 'تأمين', isActive: true }]) }

  getCustomers(page = 1, pageSize = 20, search?: string) {
    const filtered = this.state.customers.filter((c) => !search || c.displayName.toLowerCase().includes(search.toLowerCase()) || (c.mobile ?? '').includes(search))
    const items: CustomerSummary[] = filtered.map((c) => ({
      id: c.id,
      displayName: c.displayName,
      type: c.type === 'B2B' ? 1 : 2,
      mobile: c.mobile ?? null,
      isActive: c.status === 'Active',
    }))
    return Promise.resolve(paginate(items, page, pageSize))
  }
  getVehicles(page = 1, pageSize = 20, search?: string, status?: number) {
    const mapped = this.state.vehicles.map((v) => ({
      id: v.id, plateNumber: v.plateNumber, make: v.make, model: v.model, modelYear: v.modelYear, currentKm: v.currentKm,
      status: v.status === 'Available' ? 1 : v.status === 'Reserved' ? 2 : v.status === 'OnRent' ? 3 : v.status === 'InService' ? 4 : 5,
    }))
    const filtered = mapped.filter((v) =>
      (!search || `${v.plateNumber} ${v.make} ${v.model}`.toLowerCase().includes(search.toLowerCase())) &&
      (!status || v.status === status))
    return Promise.resolve(paginate(filtered, page, pageSize))
  }
  getDrivers(page = 1, pageSize = 20, search?: string) {
    const mapped: DriverSummary[] = this.state.drivers.map((d) => ({
      id: d.id,
      personNameEn: d.personNameEn,
      ...(d.personNameAr ? { personNameAr: d.personNameAr } : {}),
      driverLicenseNumber: d.driverLicenseNumber,
      licenseExpiryDate: d.licenseExpiryDate,
      status: d.status === 'Active' ? 1 : d.status === 'Suspended' ? 2 : 3,
    }))
    const filtered = mapped.filter((d) => !search || `${d.personNameEn ?? ''} ${d.personNameAr ?? ''} ${d.driverLicenseNumber}`.toLowerCase().includes(search.toLowerCase()))
    return Promise.resolve(paginate(filtered, page, pageSize))
  }

  saveContract(_body: SaveContractRequest, _idempotencyKey: string) {
    return Promise.resolve({ leaseId: mockId('lease', Date.now()), tajeerContractNumber: 9000000001, issuanceUrl: 'https://demo.local/issuance' })
  }

  getQuotations(page = 1, pageSize = 20, search?: string) {
    const filtered = this.state.quotations.filter((q) => !search || `${q.quoteNumber} ${q.customerDisplayName ?? ''}`.toLowerCase().includes(search.toLowerCase()))
    return Promise.resolve(paginate(filtered.map((q) => ({ ...q })), page, pageSize))
  }
  getQuotation(id: string) {
    const q = this.state.quotations.find((x) => x.id === id)
    if (!q) throw new Error('Quotation not found')
    return Promise.resolve(q)
  }
  createQuotation(body: CreateQuotationRequest, _idempotencyKey: string) {
    const id = mockId('quote', this.state.quotations.length + 1)
    const customerDisplayName = this.state.customers.find((c) => c.id === body.customerId)?.displayName
    const detail: QuotationDetail = {
      id, quoteNumber: `QT-${String(this.state.quotations.length + 1).padStart(6, '0')}`,
      customerId: body.customerId,
      ...(customerDisplayName ? { customerDisplayName } : {}),
      status: 'Draft', contractType: body.contractType, totalSar: 0, subTotalSar: 0, vatSar: 0, discountPercent: body.discountPercent,
      quoteDate: body.quoteDate, validUntilDate: body.validUntilDate, estimatedDurationMonths: body.estimatedDurationMonths,
      submittedAtUtc: null, approvedAtUtc: null, sentAtUtc: null, acceptedAtUtc: null, lines: [], approvals: [], pdfBlobUri: null, acceptedByCustomerSignature: null,
    }
    this.state.quotations.unshift(detail)
    return Promise.resolve(detail)
  }
  addQuotationLine(quotationId: string, body: AddQuotationLineRequest, _idempotencyKey: string) {
    const q = this.state.quotations.find((x) => x.id === quotationId)
    if (!q) throw new Error('Quotation not found')
    q.lines.push({
      id: mockId('line', q.lines.length + 1), lineNumber: q.lines.length + 1, itemType: body.itemType, description: body.description, vehicleSpecRef: body.vehicleSpecRef ?? null,
      quantity: body.quantity, unitPriceSar: body.unitPriceSar, discountPercent: body.discountPercent, lineTotalSar: body.quantity * body.unitPriceSar,
    })
    q.subTotalSar = q.lines.reduce((s, l) => s + l.lineTotalSar, 0)
    q.vatSar = Math.round(q.subTotalSar * 0.15 * 100) / 100
    q.totalSar = q.subTotalSar + q.vatSar
    return Promise.resolve(q)
  }
  submitQuotationForApproval(quotationId: string, _idempotencyKey: string) {
    const q = this.state.quotations.find((x) => x.id === quotationId); if (!q) throw new Error('Quotation not found')
    q.status = 'PendingApproval'
    q.approvals = [{ tierLevel: 1, requiredRoleCode: 'SALES_TIER_1', status: 'Pending', decidedByUserId: null, comment: null, decidedAtUtc: null }]
    return Promise.resolve({ success: true, quotationId, status: q.status, nextTierLevel: 1, nextRequiredRoleCode: 'SALES_TIER_1' })
  }
  recordApprovalDecision(quotationId: string, tierLevel: number, approved: boolean, comment: string | undefined, _idempotencyKey: string) {
    const q = this.state.quotations.find((x) => x.id === quotationId); if (!q) throw new Error('Quotation not found')
    const tier = q.approvals.find((a) => a.tierLevel === tierLevel)
    if (tier) { tier.status = approved ? 'Approved' : 'Rejected'; tier.comment = comment ?? null; tier.decidedAtUtc = new Date().toISOString() }
    q.status = approved ? 'Approved' : 'Rejected'
    return Promise.resolve({ success: true, quotationId, status: q.status })
  }
  acceptQuotation(quotationId: string, customerSignature: string | undefined, _idempotencyKey: string) {
    const q = this.state.quotations.find((x) => x.id === quotationId); if (!q) throw new Error('Quotation not found')
    q.status = 'Accepted'; q.acceptedAtUtc = new Date().toISOString(); q.acceptedByCustomerSignature = customerSignature ?? null
    return Promise.resolve({ success: true, quotationId, quoteNumber: q.quoteNumber, status: q.status, acceptedAtUtc: q.acceptedAtUtc })
  }

  getCustomerById(id: string) { const x = this.state.customers.find((c) => c.id === id); if (!x) throw new Error('Customer not found'); return Promise.resolve(x) }
  createCustomerB2B(body: CreateCustomerB2BRequest, _idempotencyKey: string) {
    const id = mockId('customer', this.state.customers.length + 1)
    this.state.customers.unshift({
      id, tenantId: DEV_TENANT_ID, type: 'B2B', status: 'Active', displayName: body.legalName, displayNameAr: body.legalNameAr ?? null,
      email: body.email ?? null, mobile: body.mobile ?? null, nationalAddress: body.nationalAddress ?? null, preferredLanguage: 'ar',
      legalName: body.legalName, legalNameAr: body.legalNameAr ?? null, commercialRegistration: body.commercialRegistration, vatNumber: body.vatNumber ?? null,
      billingAddress: body.billingAddress ?? null, creditLimit: body.creditLimit ?? null, creditCurrency: body.creditCurrency ?? null,
      personNameEn: null, personNameAr: null, idTypeCode: null, personIdNumber: null, dateOfBirth: null, nationalityCode: null,
      kycVerified: false, kycVerifiedAtUtc: null, kycVerifiedBy: null, createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
    })
    return Promise.resolve({ success: true, customerId: id, status: 'Active' })
  }
  createCustomerB2C(body: CreateCustomerB2CRequest, _idempotencyKey: string) {
    const id = mockId('customer', this.state.customers.length + 1)
    this.state.customers.unshift({
      id, tenantId: DEV_TENANT_ID, type: 'B2C', status: 'Active', displayName: body.personNameEn, displayNameAr: body.personNameAr ?? null,
      email: body.email ?? null, mobile: body.mobile ?? null, nationalAddress: body.nationalAddress ?? null, preferredLanguage: 'ar',
      legalName: null, legalNameAr: null, commercialRegistration: null, vatNumber: null, billingAddress: null, creditLimit: null, creditCurrency: null,
      personNameEn: body.personNameEn, personNameAr: body.personNameAr ?? null, idTypeCode: body.idTypeCode, personIdNumber: body.personIdNumber,
      dateOfBirth: body.dateOfBirth ?? null, nationalityCode: body.nationalityCode ?? null, kycVerified: false, kycVerifiedAtUtc: null, kycVerifiedBy: null,
      createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
    })
    return Promise.resolve({ success: true, customerId: id, status: 'Active' })
  }
  updateCustomerStatus(id: string, action: string, _idempotencyKey: string) {
    const x = this.state.customers.find((c) => c.id === id); if (!x) throw new Error('Customer not found')
    x.status = action === 'suspend' ? 'Suspended' : action === 'close' ? 'Closed' : 'Active'
    x.updatedAtUtc = new Date().toISOString()
    return Promise.resolve({ success: true, customerId: id, status: x.status })
  }

  getVehicleById(id: string) { const x = this.state.vehicles.find((v) => v.id === id); if (!x) throw new Error('Vehicle not found'); return Promise.resolve(x) }
  createVehicle(body: CreateVehicleRequest, _idempotencyKey: string) {
    const id = mockId('vehicle', this.state.vehicles.length + 1)
    this.state.vehicles.unshift({
      id, tenantId: DEV_TENANT_ID, status: 'Available', plateNumber: body.plateNumber, plateLetters: body.plateLetters, plateTypeCode: body.plateTypeCode,
      vin: body.vin, engineNumber: body.engineNumber ?? null, make: body.make, model: body.model, modelYear: body.modelYear, color: body.color ?? null,
      fuelType: String(body.fuelType), transmissionType: String(body.transmissionType), bodyType: String(body.bodyType), seats: body.seats,
      licenseExpiryDate: body.licenseExpiryDate ?? null, insuranceExpiryDate: body.insuranceExpiryDate ?? null, inspectionExpiryDate: body.inspectionExpiryDate ?? null,
      insuranceCompany: body.insuranceCompany ?? null, insurancePolicyNumber: body.insurancePolicyNumber ?? null,
      ownerBranchId: body.ownerBranchId, currentBranchId: body.ownerBranchId, currentKm: body.currentKm, purchasePrice: body.purchasePrice ?? null, purchaseDate: body.purchaseDate ?? null,
      createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
    })
    return Promise.resolve({ success: true, vehicleId: id })
  }

  getDriverById(id: string) { const x = this.state.drivers.find((d) => d.id === id); if (!x) throw new Error('Driver not found'); return Promise.resolve(x) }
  createDriver(body: CreateDriverRequest, _idempotencyKey: string) {
    const id = mockId('driver', this.state.drivers.length + 1)
    this.state.drivers.unshift({
      id, tenantId: DEV_TENANT_ID, status: 'Active', customerId: body.customerId ?? null, personNameEn: body.personNameEn, personNameAr: body.personNameAr ?? null,
      idTypeCode: body.idTypeCode, personIdNumber: body.personIdNumber, dateOfBirth: body.dateOfBirth ?? null, nationalityCode: body.nationalityCode ?? null,
      driverLicenseNumber: body.driverLicenseNumber, licenseClass: body.licenseClass, licenseExpiryDate: body.licenseExpiryDate,
      mobile: body.mobile ?? null, email: body.email ?? null, nationalAddress: body.nationalAddress ?? null, tammAuthorizationStatus: 'NotRequested',
      createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
    })
    return Promise.resolve({ success: true, driverId: id })
  }

  getBranchById(id: string) { const x = this.state.branches.find((b) => b.id === id); if (!x) throw new Error('Branch not found'); return Promise.resolve(x) }
  createBranch(body: CreateBranchRequest, _idempotencyKey: string) {
    const id = mockId('branch', this.state.branches.length + 1)
    this.state.branches.unshift({
      id, tenantId: DEV_TENANT_ID, code: body.code, nameEn: body.nameEn, nameAr: body.nameAr, cityEn: body.cityEn ?? null, cityAr: body.cityAr ?? null,
      regionEn: body.regionEn ?? null, regionAr: body.regionAr ?? null, licenseNumber: body.licenseNumber ?? null, address: body.address ?? null,
      phoneNumber: body.phoneNumber ?? null, latitude: body.latitude ?? null, longitude: body.longitude ?? null, tajeerBranchId: body.tajeerBranchId, tajeerOperatorId: body.tajeerOperatorId,
      isActive: true, createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
    })
    return Promise.resolve({ success: true, branchId: id })
  }
  updateBranchStatus(id: string, activate: boolean, _idempotencyKey: string) {
    const x = this.state.branches.find((b) => b.id === id); if (!x) throw new Error('Branch not found')
    x.isActive = activate; x.updatedAtUtc = new Date().toISOString()
    return Promise.resolve({ success: true, branchId: id })
  }
}

export const bff = (USE_MOCK_BFF ? new MockBffClient() : new BffClient()) as unknown as BffClient
