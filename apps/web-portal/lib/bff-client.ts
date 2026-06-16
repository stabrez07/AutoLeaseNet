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

  async putJson<TResponse, TBody>(
    path: string,
    body: TBody,
    extraHeaders: Record<string, string> = {},
  ): Promise<TResponse> {
    const res = await fetch(`${BFF_BASE_URL}${path}`, {
      method: 'PUT',
      headers: { ...this.headers(extraHeaders), 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    })
    if (!res.ok) {
      const problem = await this.tryReadProblem(res)
      throw Object.assign(new Error(problem.title ?? `BFF PUT ${path} failed (${res.status})`), { status: res.status, problem })
    }
    return (await res.json()) as TResponse
  }

  async deleteReq(path: string, extraHeaders: Record<string, string> = {}): Promise<void> {
    const res = await fetch(`${BFF_BASE_URL}${path}`, {
      method: 'DELETE',
      headers: this.headers(extraHeaders),
    })
    if (!res.ok) {
      const problem = await this.tryReadProblem(res)
      throw Object.assign(new Error(problem.title ?? `BFF DELETE ${path} failed (${res.status})`), { status: res.status, problem })
    }
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

  updateVehicle(id: string, body: UpdateVehicleRequest, idempotencyKey: string) {
    return this.putJson<VehicleCommandResult, UpdateVehicleRequest>(
      `/api/v1/vehicles/${id}`,
      body,
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  getVehicleHistory(id: string) {
    return this.getJson<VehicleHistoryEvent[]>(`/api/v1/vehicles/${id}/history`)
  }

  getVehicleServiceRecords(id: string) {
    return this.getJson<ServiceRecord[]>(`/api/v1/vehicles/${id}/service-records`)
  }

  createServiceRecord(vehicleId: string, body: CreateServiceRecordRequest, idempotencyKey: string) {
    return this.postJson<VehicleCommandResult, CreateServiceRecordRequest>(
      `/api/v1/vehicles/${vehicleId}/service-records`,
      body,
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  getVehicleImages(id: string) {
    return this.getJson<VehicleImageDto[]>(`/api/v1/vehicles/${id}/images`)
  }

  generateVehicleImage(vehicleId: string, idempotencyKey: string) {
    return this.postJson<VehicleCommandResult, Record<string, never>>(
      `/api/v1/vehicles/${vehicleId}/images/generate`,
      {},
      { 'Idempotency-Key': idempotencyKey },
    )
  }

  bulkImportVehicles(file: File, idempotencyKey: string): Promise<BulkImportResult> {
    const form = new FormData()
    form.append('file', file)
    return fetch(`${BFF_BASE_URL}/api/v1/vehicles/bulk-import`, {
      method: 'POST',
      headers: { ...this.headers(), 'Idempotency-Key': idempotencyKey },
      body: form,
    }).then(async (res) => {
      if (!res.ok) throw new Error(`Bulk import failed (${res.status})`)
      return (await res.json()) as BulkImportResult
    })
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

  // ─── Leases ────────────────────────────────────────────────────────────────

  getLeases(page = 1, pageSize = 20, search?: string, status?: string) {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search) q.set('search', search)
    if (status) q.set('status', status)
    return this.getJson<PagedResult<LeaseSummary>>(`/api/v1/leases?${q.toString()}`)
  }
  getLeaseById(id: string) {
    return this.getJson<LeaseDetail>(`/api/v1/leases/${id}`)
  }
  getCustomerLeases(customerId: string) {
    return this.getJson<LeaseSummary[]>(`/api/v1/customers/${customerId}/leases`)
  }
  getCustomerVehicles(customerId: string) {
    return this.getJson<VehicleSummary[]>(`/api/v1/customers/${customerId}/vehicles`)
  }
  getCustomerDrivers(customerId: string) {
    return this.getJson<DriverSummary[]>(`/api/v1/customers/${customerId}/drivers`)
  }
  getVehicleCurrentLease(vehicleId: string) {
    return this.getJson<LeaseSummary | null>(`/api/v1/vehicles/${vehicleId}/current-lease`)
  }
  getDriverCurrentLease(driverId: string) {
    return this.getJson<LeaseSummary | null>(`/api/v1/drivers/${driverId}/current-lease`)
  }

  // ─── Delete operations ─────────────────────────────────────────────────────

  deleteVehicle(id: string, idempotencyKey: string): Promise<void> {
    return this.deleteReq(`/api/v1/vehicles/${id}`, { 'Idempotency-Key': idempotencyKey })
  }
  deleteDriver(id: string, idempotencyKey: string) {
    return this.postJson<DeleteResult, Record<string, never>>(`/api/v1/drivers/${id}/delete`, {}, { 'Idempotency-Key': idempotencyKey })
  }
  deleteBranch(id: string, idempotencyKey: string) {
    return this.postJson<DeleteResult, Record<string, never>>(`/api/v1/branches/${id}/delete`, {}, { 'Idempotency-Key': idempotencyKey })
  }
  deleteCustomer(id: string, idempotencyKey: string) {
    return this.postJson<DeleteResult, Record<string, never>>(`/api/v1/customers/${id}/delete`, {}, { 'Idempotency-Key': idempotencyKey })
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
  notes?: string | null
  createdAtUtc: string; updatedAtUtc: string
  serviceHistory: ServiceRecord[]
  images?: VehicleImageDto[]
}

export interface UpdateVehicleRequest {
  color?: string | undefined
  seats?: number | undefined
  make?: string | undefined
  model?: string | undefined
  modelYear?: number | undefined
  insuranceCompany?: string | undefined
  insurancePolicyNumber?: string | undefined
  licenseExpiryDate?: string | undefined
  insuranceExpiryDate?: string | undefined
  inspectionExpiryDate?: string | undefined
  currentBranchId?: string | undefined
  currentKm?: number | undefined
  purchasePrice?: number | undefined
  purchaseDate?: string | undefined
  notes?: string | undefined
}

export interface VehicleHistoryEvent {
  id: string
  vehicleId: string
  eventType: string
  description: string
  previousValue?: string | null
  newValue?: string | null
  performedByName: string
  occurredAtUtc: string
}

export interface CreateServiceRecordRequest {
  type: number  // 1=PMS, 2=CMS
  serviceCode: string
  description: string
  servicedAt: string
  odometerAtService: number
  costSar: number
  branch: string
  technician: string
  partsReplaced?: string[] | undefined
  nextServiceOdometer?: number | undefined
  nextServiceDate?: string | undefined
  notes?: string | undefined
}

export interface BulkImportRowError {
  rowIndex: number
  errorCode: string
  errorMessage: string
}

export interface BulkImportResult {
  success: boolean
  createdCount: number
  skippedCount: number
  errors: BulkImportRowError[]
}

export interface VehicleImageDto {
  id: string
  vehicleId: string
  imageUrl: string
  thumbnailUrl?: string | null
  altText?: string | null
  isAiGenerated: boolean
  sortOrder: number
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

// ─── Lease types ─────────────────────────────────────────────────────────────

export interface LeaseInspection {
  id: string
  type: string // CheckOut | CheckIn | Periodic
  inspectedAtUtc: string
  odometer: number
  conditionCode: string // Good | Fair | Damaged
  notes: string | null
  branch: string
  inspector: string
}

export interface LeaseIncident {
  id: string
  type: string // Accident | Traffic | Mechanical | Theft
  occurredAtUtc: string
  description: string
  estimatedCostSar: number | null
  claimNumber: string | null
  resolved: boolean
}

export interface LeaseSummary {
  id: string
  leaseNumber: string
  customerId: string
  customerDisplayName: string
  vehicleId: string
  vehiclePlate: string
  vehicleMakeModel: string
  primaryDriverId: string | null
  primaryDriverName: string | null
  status: string // Draft | PendingIssuance | Active | Extended | Suspended | Closed | Cancelled
  contractTypeCode: string // Daily | Monthly | Annual
  contractStartUtc: string
  contractEndUtc: string
  tajeerContractNumber: number | null
  rentAmountSar: number
  workingBranchCode: string
  workingBranchName: string
  createdAtUtc: string
}

export interface LeaseDetail extends LeaseSummary {
  rentPolicyId: string
  paidAmountSar: number
  vatAmountSar: number
  totalAmountSar: number
  remainingAmountSar: number
  allowedKmPerDay: number
  paymentMethodCode: string
  issuedAtUtc: string | null
  suspendedAtUtc: string | null
  resumedAtUtc: string | null
  closedAtUtc: string | null
  cancelledAtUtc: string | null
  tajeerStatus: string | null
  tajeerIssuanceUrl: string | null
  zatcaSubmissionStatus: string | null
  zatcaInvoiceNumber: string | null
  inspections: LeaseInspection[]
  incidents: LeaseIncident[]
}

export interface DeleteResult {
  success: boolean
  errorCode?: string | null
  errorMessage?: string | null
}

// ─── Service history ──────────────────────────────────────────────────────────

export type ServiceType = 'PMS' | 'CMS'

export interface ServiceRecord {
  id: string
  vehicleId: string
  type: ServiceType
  serviceCode: string
  description: string
  servicedAt: string        // YYYY-MM-DD
  odometerAtService: number
  costSar: number
  branch: string
  technician: string
  partsReplaced: string[]
  nextServiceOdometer: number | null
  nextServiceDate: string | null
  notes: string | null
}

type MockState = {
  customers: CustomerDetail[]
  vehicles: VehicleDetail[]
  drivers: DriverDetail[]
  branches: BranchDetail[]
  quotations: QuotationDetail[]
  leases: LeaseDetail[]
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
  // ─── Service record helpers ────────────────────────────────────────────────
  const PMS_SERVICES = [
    { code: 'PMS-OIL', desc: 'Engine Oil & Filter Change', parts: ['Engine Oil 5L', 'Oil Filter'], cost: 380 },
    { code: 'PMS-AIR', desc: 'Air Filter Replacement', parts: ['Air Filter'], cost: 120 },
    { code: 'PMS-CAB', desc: 'Cabin Air Filter Replacement', parts: ['Cabin Filter'], cost: 95 },
    { code: 'PMS-TIRE', desc: 'Tire Rotation & Balancing', parts: [], cost: 200 },
    { code: 'PMS-BRK', desc: 'Brake Inspection & Fluid Top-Up', parts: ['Brake Fluid'], cost: 180 },
    { code: 'PMS-FULL', desc: 'Full Scheduled Service (60k)', parts: ['Engine Oil 5L', 'Oil Filter', 'Air Filter', 'Spark Plugs', 'Brake Fluid', 'Coolant'], cost: 1200 },
    { code: 'PMS-COOL', desc: 'Coolant Flush & Replacement', parts: ['Coolant 4L'], cost: 320 },
    { code: 'PMS-TRANS', desc: 'Transmission Fluid Service', parts: ['ATF Fluid 4L'], cost: 450 },
  ]
  const CMS_SERVICES = [
    { code: 'CMS-BRK', desc: 'Brake Pad Replacement (Front)', parts: ['Front Brake Pads x2', 'Brake Cleaner'], cost: 680 },
    { code: 'CMS-AC', desc: 'AC Compressor & Gas Recharge', parts: ['AC Refrigerant', 'Compressor Belt'], cost: 950 },
    { code: 'CMS-BAT', desc: 'Battery Replacement', parts: ['12V Battery 70Ah'], cost: 420 },
    { code: 'CMS-TIRE', desc: 'Tyre Replacement (2 units)', parts: ['225/60R17 Tyre x2'], cost: 780 },
    { code: 'CMS-SUSP', desc: 'Shock Absorber Replacement', parts: ['Front Shock Absorber x2'], cost: 1100 },
    { code: 'CMS-BELT', desc: 'Serpentine Belt Replacement', parts: ['Serpentine Belt'], cost: 340 },
    { code: 'CMS-ALT', desc: 'Alternator Replacement', parts: ['Alternator 90A'], cost: 1400 },
    { code: 'CMS-BODY', desc: 'Body Panel Repair & Repaint', parts: ['Paint', 'Filler', 'Clear Coat'], cost: 2200 },
  ]
  const TECHNICIANS = ['Ahmed Al-Rashidi', 'Khalid Bin Saleh', 'Faisal Al-Zahrani', 'Omar Mansour', 'Nawaf Al-Otaibi']

  function buildServiceHistory(vehicleId: string, branchName: string, baseKm: number, purchaseDateStr: string): ServiceRecord[] {
    const purchaseDate = new Date(purchaseDateStr || '2022-01-01')
    const records: ServiceRecord[] = []
    let km = Math.max(5000, baseKm - 80000)
    let serviceDate = new Date(purchaseDate.getTime() + 90 * 86400000)
    const vehicleHash = vehicleId.charCodeAt(vehicleId.length - 1)
    const numRecords = 3 + (vehicleHash % 6) // 3–8 records

    for (let j = 0; j < numRecords; j++) {
      const isPms = j % 3 !== 2
      const pool = isPms ? PMS_SERVICES : CMS_SERVICES
      const svc = pool[(j + vehicleHash) % pool.length]!
      const rec: ServiceRecord = {
        id: `svc-${vehicleId}-${j + 1}`,
        vehicleId,
        type: isPms ? 'PMS' : 'CMS',
        serviceCode: svc.code,
        description: svc.desc,
        servicedAt: serviceDate.toISOString().substring(0, 10),
        odometerAtService: km,
        costSar: svc.cost + (j % 3) * 50,
        branch: branchName,
        technician: pick(TECHNICIANS, j + vehicleHash),
        partsReplaced: [...svc.parts],
        nextServiceOdometer: isPms ? km + 10000 : null,
        nextServiceDate: isPms ? new Date(serviceDate.getTime() + 180 * 86400000).toISOString().substring(0, 10) : null,
        notes: j === 0 ? 'Initial service after delivery' : null,
      }
      records.push(rec)
      km += 10000 + (j % 5) * 2000
      serviceDate = new Date(serviceDate.getTime() + (150 + j * 30) * 86400000)
      if (serviceDate > now) break
    }
    return records.reverse() // most recent first
  }

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

  const customers: CustomerDetail[] = Array.from({ length: 500 }).map((_, i) => {
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

  const MAKES_MODELS: [string, string, string][] = [
    ['Toyota', 'Camry', 'Sedan'],
    ['Toyota', 'Land Cruiser', 'Suv'],
    ['Toyota', 'Hilux', 'Pickup'],
    ['Hyundai', 'Sonata', 'Sedan'],
    ['Hyundai', 'Tucson', 'Suv'],
    ['Hyundai', 'H-1', 'Van'],
    ['Nissan', 'Altima', 'Sedan'],
    ['Nissan', 'Patrol', 'Suv'],
    ['Nissan', 'Navara', 'Pickup'],
    ['Kia', 'Sportage', 'Suv'],
    ['Kia', 'Carnival', 'Van'],
    ['Kia', 'K5', 'Sedan'],
    ['GMC', 'Yukon', 'Suv'],
    ['Chevrolet', 'Tahoe', 'Suv'],
    ['Ford', 'F-150', 'Pickup'],
    ['Mercedes', 'E-Class', 'Sedan'],
    ['BMW', '5 Series', 'Sedan'],
    ['Honda', 'Accord', 'Sedan'],
    ['Lexus', 'LX 600', 'Suv'],
    ['Mitsubishi', 'L200', 'Pickup'],
    ['Toyota', 'Coaster', 'Bus'],
    ['Toyota', 'Corolla', 'Hatchback'],
    ['Hyundai', 'Accent', 'Hatchback'],
    ['BMW', '4 Series', 'Coupe'],
    ['Mercedes', 'C-Class Coupe', 'Coupe'],
    ['Toyota', 'HiAce', 'Van'],
  ]

  const vehicles: VehicleDetail[] = Array.from({ length: 600 }).map((_, i) => {
    const [make, model, bodyType] = MAKES_MODELS[i % MAKES_MODELS.length]!
    const st = pick(['Available', 'Reserved', 'OnRent', 'InService', 'Damaged'], i)
    const branch = pick(branches, i)
    const baseKm = 8000 + i * 180
    const purchaseDateStr = `202${i % 4}-${String((i % 12) + 1).padStart(2, '0')}-10`
    const vehicleId = mockId('vehicle', i + 1)
    return {
      id: vehicleId,
      tenantId: DEV_TENANT_ID,
      status: st,
      plateNumber: `${1000 + i}`,
      plateLetters: pick(['أ ب ج', 'د هـ و', 'ز ح ط', 'ي ك ل', 'م ن هـ'], i),
      plateTypeCode: 1,
      vin: `VIN${(100000000000 + i).toString().slice(-12)}`,
      engineNumber: `ENG-${i + 1}`,
      make,
      model,
      modelYear: 2020 + (i % 6),
      color: pick(['White', 'Silver', 'Black', 'Grey', 'Navy', 'Red', 'Bronze'], i),
      fuelType: pick(['Petrol91', 'Petrol95', 'Diesel', 'Hybrid', 'Electric'], i),
      transmissionType: pick(['Automatic', 'Manual', 'CVT'], i),
      bodyType,
      seats: bodyType === 'Bus' ? pick([14, 20, 26], i) : bodyType === 'Van' ? pick([7, 9, 12], i) : bodyType === 'Suv' ? pick([5, 7], i) : pick([4, 5], i),
      licenseExpiryDate: `2027-${String((i % 12) + 1).padStart(2, '0')}-15`,
      insuranceExpiryDate: `2027-${String((i % 12) + 1).padStart(2, '0')}-20`,
      inspectionExpiryDate: `2027-${String((i % 12) + 1).padStart(2, '0')}-25`,
      insuranceCompany: pick(['Tawuniya', 'Bupa Arabia', 'Walaa', 'Al Rajhi Takaful', 'AXA Cooperative'], i),
      insurancePolicyNumber: `POL-${100000 + i}`,
      ownerBranchId: branch.id,
      currentBranchId: branch.id,
      currentKm: baseKm,
      purchasePrice: 60000 + i * 80,
      purchaseDate: purchaseDateStr,
      createdAtUtc: now.toISOString(),
      updatedAtUtc: now.toISOString(),
      serviceHistory: buildServiceHistory(vehicleId, branch.nameEn, baseKm, purchaseDateStr),
    }
  })

  const drivers: DriverDetail[] = Array.from({ length: 800 }).map((_, i) => ({
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

  const quotations: QuotationDetail[] = Array.from({ length: 300 }).map((_, i) => {
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

  // ─── Leases (200) — built after vehicles so we can mark OnRent ──────────────
  const LEASE_STATUSES = ['Draft', 'PendingIssuance', 'Active', 'Active', 'Extended', 'Suspended', 'Closed', 'Cancelled']
  const CONTRACT_TYPES_L = ['Daily', 'Monthly', 'Annual']
  const PAYMENT_METHODS_L = ['Cash', 'CreditCard', 'BankTransfer']
  const INCIDENT_TYPES = ['Accident', 'Traffic', 'Mechanical', 'Theft']
  const CONDITIONS = ['Good', 'Fair', 'Damaged']

  const leases: LeaseDetail[] = Array.from({ length: 350 }).map((_, i) => {
    const customer = pick(customers, i * 7)
    const driver = pick(drivers, i * 3)
    const lsStatus = pick(LEASE_STATUSES, i)
    const isActive = lsStatus === 'Active' || lsStatus === 'Extended'
    const isClosed = lsStatus === 'Closed'

    // Active leases get a dedicated vehicle (first ~100 vehicles); others share
    const vIdx = isActive ? i % 120 : (i * 2 + 120) % 600
    const vehicle = vehicles[vIdx]!
    if (isActive) vehicle.status = 'OnRent'

    const contractStart = new Date(now.getTime() - (365 - i * 1.5) * 86400000)
    const contractEnd = new Date(contractStart.getTime() + (6 + i % 18) * 30 * 86400000)
    const rentSar = 1500 + (i % 30) * 200
    const vatSar = Math.round(rentSar * 0.15 * 100) / 100
    const totalSar = rentSar + vatSar
    const hasInspections = lsStatus !== 'Draft' && lsStatus !== 'PendingIssuance'
    const hasIncident = i % 7 === 0

    return {
      id: mockId('lease', i + 1),
      leaseNumber: `LC-${(i + 1).toString().padStart(6, '0')}`,
      customerId: customer.id,
      customerDisplayName: customer.displayName,
      vehicleId: vehicle.id,
      vehiclePlate: `${vehicle.plateLetters} ${vehicle.plateNumber}`,
      vehicleMakeModel: `${vehicle.make} ${vehicle.model} (${vehicle.modelYear})`,
      primaryDriverId: driver.id,
      primaryDriverName: driver.personNameEn,
      status: lsStatus,
      contractTypeCode: pick(CONTRACT_TYPES_L, i),
      contractStartUtc: contractStart.toISOString(),
      contractEndUtc: contractEnd.toISOString(),
      tajeerContractNumber: lsStatus === 'Draft' ? null : 9000000000 + i + 1,
      rentAmountSar: rentSar,
      workingBranchCode: pick(branches, i).code,
      workingBranchName: pick(branches, i).nameEn,
      createdAtUtc: contractStart.toISOString(),
      rentPolicyId: 'rp-1',
      paidAmountSar: isActive || isClosed ? rentSar : 0,
      vatAmountSar: vatSar,
      totalAmountSar: totalSar,
      remainingAmountSar: isActive ? vatSar : isClosed ? 0 : totalSar,
      allowedKmPerDay: pick([100, 150, 200, 250, 300], i),
      paymentMethodCode: pick(PAYMENT_METHODS_L, i),
      issuedAtUtc: lsStatus === 'Draft' || lsStatus === 'PendingIssuance' ? null : contractStart.toISOString(),
      suspendedAtUtc: lsStatus === 'Suspended' ? new Date(contractStart.getTime() + 30 * 86400000).toISOString() : null,
      resumedAtUtc: null,
      closedAtUtc: isClosed ? contractEnd.toISOString() : null,
      cancelledAtUtc: lsStatus === 'Cancelled' ? new Date(contractStart.getTime() + 5 * 86400000).toISOString() : null,
      tajeerStatus: lsStatus === 'Draft' ? null : lsStatus === 'PendingIssuance' ? 'Pending' : 'Confirmed',
      tajeerIssuanceUrl: lsStatus !== 'Draft' && lsStatus !== 'PendingIssuance' ? `https://rabet.staging/contract/${9000000000 + i}` : null,
      zatcaSubmissionStatus: isClosed ? 'Cleared' : isActive ? 'Pending' : null,
      zatcaInvoiceNumber: isClosed ? `INV-${(1000 + i).toString().padStart(6, '0')}` : null,
      inspections: hasInspections ? [
        {
          id: mockId('insp', i * 2 + 1), type: 'CheckOut',
          inspectedAtUtc: contractStart.toISOString(),
          odometer: vehicle.currentKm - 5000, conditionCode: 'Good',
          notes: 'Vehicle checked out — no damage.', branch: pick(branches, i).nameEn, inspector: `Staff ${i + 1}`,
        },
        ...(isClosed ? [{
          id: mockId('insp', i * 2 + 2), type: 'CheckIn',
          inspectedAtUtc: contractEnd.toISOString(),
          odometer: vehicle.currentKm, conditionCode: pick(CONDITIONS, i),
          notes: pick(CONDITIONS, i) === 'Damaged' ? 'Minor scratches on rear bumper.' : 'Returned in good condition.',
          branch: pick(branches, i).nameEn, inspector: `Staff ${i + 2}`,
        }] : []),
      ] : [],
      incidents: hasIncident ? [{
        id: mockId('inc', i + 1), type: pick(INCIDENT_TYPES, i),
        occurredAtUtc: new Date(contractStart.getTime() + 30 * 86400000).toISOString(),
        description: `${pick(INCIDENT_TYPES, i)} incident reported during rental period.`,
        estimatedCostSar: i % 3 === 0 ? null : 1500 + (i % 10) * 500,
        claimNumber: `CLM-${(100 + i).toString().padStart(6, '0')}`,
        resolved: i % 2 === 0,
      }] : [],
    }
  })

  return { customers, vehicles, drivers, branches, quotations, leases }
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
      serviceHistory: [],
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

  // ─── Leases ────────────────────────────────────────────────────────────────

  getLeases(page = 1, pageSize = 20, search?: string, status?: string) {
    const filtered = this.state.leases.filter((l) =>
      (!search || `${l.leaseNumber} ${l.customerDisplayName} ${l.vehiclePlate} ${l.vehicleMakeModel}`.toLowerCase().includes(search.toLowerCase())) &&
      (!status || l.status === status)
    )
    const summaries: LeaseSummary[] = filtered.map(({ inspections, incidents, rentPolicyId, paidAmountSar, vatAmountSar, totalAmountSar, remainingAmountSar, allowedKmPerDay, paymentMethodCode, issuedAtUtc, suspendedAtUtc, resumedAtUtc, closedAtUtc, cancelledAtUtc, tajeerStatus, tajeerIssuanceUrl, zatcaSubmissionStatus, zatcaInvoiceNumber, ...s }) => s)
    return Promise.resolve(paginate(summaries, page, pageSize))
  }
  getLeaseById(id: string) {
    const x = this.state.leases.find((l) => l.id === id)
    if (!x) throw new Error('Lease not found')
    return Promise.resolve(x)
  }
  getCustomerLeases(customerId: string): Promise<LeaseSummary[]> {
    const result = this.state.leases
      .filter((l) => l.customerId === customerId)
      .map(({ inspections, incidents, rentPolicyId, paidAmountSar, vatAmountSar, totalAmountSar, remainingAmountSar, allowedKmPerDay, paymentMethodCode, issuedAtUtc, suspendedAtUtc, resumedAtUtc, closedAtUtc, cancelledAtUtc, tajeerStatus, tajeerIssuanceUrl, zatcaSubmissionStatus, zatcaInvoiceNumber, ...s }) => s)
    return Promise.resolve(result)
  }
  getCustomerVehicles(customerId: string): Promise<VehicleSummary[]> {
    const vehicleIds = new Set(this.state.leases.filter((l) => l.customerId === customerId).map((l) => l.vehicleId))
    return Promise.resolve(this.state.vehicles.filter((v) => vehicleIds.has(v.id)).map((v) => ({
      id: v.id, plateNumber: v.plateNumber, make: v.make, model: v.model, modelYear: v.modelYear, currentKm: v.currentKm,
      status: v.status === 'Available' ? 1 : v.status === 'Reserved' ? 2 : v.status === 'OnRent' ? 3 : v.status === 'InService' ? 4 : 5,
    })))
  }
  getCustomerDrivers(customerId: string): Promise<DriverSummary[]> {
    return Promise.resolve(this.state.drivers.filter((d) => d.customerId === customerId).map((d) => ({
      id: d.id,
      personNameEn: d.personNameEn,
      ...(d.personNameAr ? { personNameAr: d.personNameAr } : {}),
      driverLicenseNumber: d.driverLicenseNumber,
      licenseExpiryDate: d.licenseExpiryDate,
      status: d.status === 'Active' ? 1 : d.status === 'Suspended' ? 2 : 3,
    })))
  }
  getVehicleCurrentLease(vehicleId: string): Promise<LeaseSummary | null> {
    const lease = this.state.leases.find((l) => l.vehicleId === vehicleId && (l.status === 'Active' || l.status === 'Extended'))
    if (!lease) return Promise.resolve(null)
    const { inspections, incidents, rentPolicyId, paidAmountSar, vatAmountSar, totalAmountSar, remainingAmountSar, allowedKmPerDay, paymentMethodCode, issuedAtUtc, suspendedAtUtc, resumedAtUtc, closedAtUtc, cancelledAtUtc, tajeerStatus, tajeerIssuanceUrl, zatcaSubmissionStatus, zatcaInvoiceNumber, ...s } = lease
    return Promise.resolve(s)
  }
  getDriverCurrentLease(driverId: string): Promise<LeaseSummary | null> {
    const lease = this.state.leases.find((l) => l.primaryDriverId === driverId && (l.status === 'Active' || l.status === 'Extended'))
    if (!lease) return Promise.resolve(null)
    const { inspections, incidents, rentPolicyId, paidAmountSar, vatAmountSar, totalAmountSar, remainingAmountSar, allowedKmPerDay, paymentMethodCode, issuedAtUtc, suspendedAtUtc, resumedAtUtc, closedAtUtc, cancelledAtUtc, tajeerStatus, tajeerIssuanceUrl, zatcaSubmissionStatus, zatcaInvoiceNumber, ...s } = lease
    return Promise.resolve(s)
  }

  // ─── Delete operations ─────────────────────────────────────────────────────

  deleteVehicle(id: string, _idempotencyKey: string): Promise<void> {
    const idx = this.state.vehicles.findIndex((v) => v.id === id)
    if (idx === -1) throw new Error('Vehicle not found')
    this.state.vehicles.splice(idx, 1)
    return Promise.resolve()
  }
  deleteDriver(id: string, _idempotencyKey: string): Promise<DeleteResult> {
    const idx = this.state.drivers.findIndex((d) => d.id === id)
    if (idx === -1) throw new Error('Driver not found')
    this.state.drivers.splice(idx, 1)
    return Promise.resolve({ success: true })
  }
  deleteBranch(id: string, _idempotencyKey: string): Promise<DeleteResult> {
    const idx = this.state.branches.findIndex((b) => b.id === id)
    if (idx === -1) throw new Error('Branch not found')
    this.state.branches.splice(idx, 1)
    return Promise.resolve({ success: true })
  }
  deleteCustomer(id: string, _idempotencyKey: string): Promise<DeleteResult> {
    const idx = this.state.customers.findIndex((c) => c.id === id)
    if (idx === -1) throw new Error('Customer not found')
    this.state.customers.splice(idx, 1)
    return Promise.resolve({ success: true })
  }

  // ─── Vehicle extended ops ──────────────────────────────────────────────────

  updateVehicle(id: string, body: UpdateVehicleRequest, _idempotencyKey: string): Promise<VehicleCommandResult> {
    const v = this.state.vehicles.find((x) => x.id === id)
    if (!v) throw new Error('Vehicle not found')
    if (body.color != null) v.color = body.color
    if (body.seats != null) v.seats = body.seats
    if (body.make != null) v.make = body.make
    if (body.model != null) v.model = body.model
    if (body.modelYear != null) v.modelYear = body.modelYear
    if (body.insuranceCompany != null) v.insuranceCompany = body.insuranceCompany
    if (body.insurancePolicyNumber != null) v.insurancePolicyNumber = body.insurancePolicyNumber
    if (body.licenseExpiryDate != null) v.licenseExpiryDate = body.licenseExpiryDate
    if (body.insuranceExpiryDate != null) v.insuranceExpiryDate = body.insuranceExpiryDate
    if (body.inspectionExpiryDate != null) v.inspectionExpiryDate = body.inspectionExpiryDate
    if (body.currentBranchId != null) v.currentBranchId = body.currentBranchId
    if (body.currentKm != null) v.currentKm = body.currentKm
    if (body.purchasePrice != null) v.purchasePrice = body.purchasePrice
    if (body.purchaseDate != null) v.purchaseDate = body.purchaseDate
    if (body.notes != null) v.notes = body.notes
    v.updatedAtUtc = new Date().toISOString()
    return Promise.resolve({ success: true, vehicleId: id })
  }

  getVehicleHistory(id: string): Promise<VehicleHistoryEvent[]> {
    const v = this.state.vehicles.find((x) => x.id === id)
    if (!v) throw new Error('Vehicle not found')
    // Generate history events from the vehicle's service records
    const events: VehicleHistoryEvent[] = v.serviceHistory.map((sr, idx) => ({
      id: `hist-${id}-svc-${idx}`,
      vehicleId: id,
      eventType: 'ServiceRecorded',
      description: `${sr.type === 'PMS' ? 'Preventive' : 'Corrective'} service: ${sr.description}`,
      previousValue: null,
      newValue: String(sr.odometerAtService),
      performedByName: sr.technician,
      occurredAtUtc: new Date(sr.servicedAt).toISOString(),
    }))
    events.push({
      id: `hist-${id}-created`,
      vehicleId: id,
      eventType: 'BulkImported',
      description: `Vehicle ${v.plateNumber} added to fleet`,
      previousValue: null,
      newValue: null,
      performedByName: 'System',
      occurredAtUtc: v.createdAtUtc,
    })
    return Promise.resolve(events.sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc)))
  }

  getVehicleServiceRecords(id: string): Promise<ServiceRecord[]> {
    const v = this.state.vehicles.find((x) => x.id === id)
    if (!v) throw new Error('Vehicle not found')
    return Promise.resolve(v.serviceHistory)
  }

  createServiceRecord(vehicleId: string, body: CreateServiceRecordRequest, _idempotencyKey: string): Promise<VehicleCommandResult> {
    const v = this.state.vehicles.find((x) => x.id === vehicleId)
    if (!v) throw new Error('Vehicle not found')
    const id = `svc-${vehicleId}-${v.serviceHistory.length + 1}`
    const record: ServiceRecord = {
      id,
      vehicleId,
      type: body.type === 1 ? 'PMS' : 'CMS',
      serviceCode: body.serviceCode,
      description: body.description,
      servicedAt: body.servicedAt,
      odometerAtService: body.odometerAtService,
      costSar: body.costSar,
      branch: body.branch,
      technician: body.technician,
      partsReplaced: body.partsReplaced ?? [],
      nextServiceOdometer: body.nextServiceOdometer ?? null,
      nextServiceDate: body.nextServiceDate ?? null,
      notes: body.notes ?? null,
    }
    v.serviceHistory.unshift(record)
    return Promise.resolve({ success: true, vehicleId })
  }

  getVehicleImages(id: string): Promise<VehicleImageDto[]> {
    const v = this.state.vehicles.find((x) => x.id === id)
    if (!v) throw new Error('Vehicle not found')
    return Promise.resolve(v.images ?? [])
  }

  generateVehicleImage(vehicleId: string, _idempotencyKey: string): Promise<VehicleCommandResult> {
    const v = this.state.vehicles.find((x) => x.id === vehicleId)
    if (!v) throw new Error('Vehicle not found')
    if (!v.images) v.images = []
    const imageId = `img-${vehicleId}-${v.images.length + 1}`
    const colorSlug = (v.color ?? 'auto').toLowerCase().replace(/ /g, '+')
    const makeSlug = encodeURIComponent(v.make.toLowerCase())
    const modelSlug = encodeURIComponent(v.model.toLowerCase())
    v.images.push({
      id: imageId,
      vehicleId,
      imageUrl: `https://source.unsplash.com/800x500/?car,${makeSlug},${modelSlug},${colorSlug}`,
      thumbnailUrl: `https://source.unsplash.com/320x200/?car,${makeSlug},${modelSlug},${colorSlug}`,
      altText: `${v.color ?? ''} ${v.make} ${v.model}`.trim(),
      isAiGenerated: true,
      sortOrder: v.images.length,
    })
    return Promise.resolve({ success: true, vehicleId: imageId })
  }

  bulkImportVehicles(file: File, _idempotencyKey: string): Promise<BulkImportResult> {
    return new Promise((resolve) => {
      const reader = new FileReader()
      reader.onload = (e) => {
        const text = (e.target?.result as string) ?? ''
        const lines = text.split('\n').filter(Boolean)
        const errors: BulkImportRowError[] = []
        let created = 0
        lines.slice(1).forEach((line, idx) => {
          const cols = line.split(',')
          if (cols.length < 14) { errors.push({ rowIndex: idx + 2, errorCode: 'PARSE_ERROR', errorMessage: `Row ${idx + 2}: need ≥14 cols, got ${cols.length}` }); return }
          const id = mockId('vehicle', this.state.vehicles.length + created + 1)
          this.state.vehicles.unshift({
            id, tenantId: DEV_TENANT_ID, status: 'Available',
            plateNumber: cols[0]!.trim(), plateLetters: cols[1]!.trim(), plateTypeCode: parseInt(cols[2]!.trim(), 10) || 1,
            vin: cols[3]!.trim(), engineNumber: null,
            make: cols[4]!.trim(), model: cols[5]!.trim(), modelYear: parseInt(cols[6]!.trim(), 10) || 2024,
            color: cols[7]!.trim() || null, fuelType: cols[8]!.trim() || 'Petrol91', transmissionType: cols[9]!.trim() || 'Automatic',
            bodyType: cols[10]!.trim() || 'Sedan', seats: parseInt(cols[11]!.trim(), 10) || 5,
            licenseExpiryDate: null, insuranceExpiryDate: null, inspectionExpiryDate: null,
            insuranceCompany: null, insurancePolicyNumber: null,
            ownerBranchId: cols[12]!.trim(), currentBranchId: cols[12]!.trim(),
            currentKm: parseInt(cols[13]!.trim(), 10) || 0, purchasePrice: null, purchaseDate: null,
            notes: null, createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
            serviceHistory: [], images: [],
          })
          created++
        })
        resolve({ success: errors.length === 0, createdCount: created, skippedCount: 0, errors })
      }
      reader.readAsText(file)
    })
  }
}

export const bff = (USE_MOCK_BFF ? new MockBffClient() : new BffClient()) as unknown as BffClient
