// Typed BFF client. Shapes mirror Specs/06-bff-api-surface.md and the actual handlers
// in services/bff/Endpoints/*.cs. When packages/contracts/generated/schema.d.ts lands
// (via `pnpm openapi:gen`), these interfaces should be replaced with the generated
// types. Until then we hand-maintain them deliberately small.

export const BFF_BASE_URL = process.env.NEXT_PUBLIC_BFF_BASE_URL ?? 'http://localhost:5000'

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

export const bff = new BffClient()
