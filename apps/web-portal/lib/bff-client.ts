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
  email?: string | null
  commercialRegistration?: string | null
  vatNumber?: string | null
  contactPerson?: string | null
  contactPersonMobile?: string | null
  city?: string | null
  isActive: boolean
}

export interface VehicleSummary {
  id: string
  plateNumber: string
  make: string
  model: string
  modelYear?: number
  color?: string | null
  bodyType?: string | null
  fuelType?: string | null
  transmissionType?: string | null
  seats?: number | null
  thumbnailUrl?: string | null
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

// ─── Damage Recording ─────────────────────────────────────────────────────────

export type DamageType = 'Accident' | 'ScratchDent' | 'Glass' | 'TyreWheel' | 'Mechanical' | 'Flood' | 'TheftVandalism' | 'Fire' | 'Other'
export type DamageLocation = 'Front' | 'Rear' | 'LeftSide' | 'RightSide' | 'Roof' | 'Underbody' | 'Interior' | 'Multiple'
export type DamageSeverity = 'Minor' | 'Moderate' | 'Major' | 'TotalLoss'
export type DamageFault = 'Customer' | 'ThirdParty' | 'Unknown' | 'ActOfGod'
export type RepairStatus = 'Pending' | 'InProgress' | 'Completed' | 'Waived'

export interface DamageRecord {
  id: string
  leaseId: string
  vehicleId: string
  type: DamageType
  location: DamageLocation
  severity: DamageSeverity
  fault: DamageFault
  description: string
  occurredAt: string        // YYYY-MM-DD
  estimatedCostSar: number | null
  actualCostSar: number | null
  repairStatus: RepairStatus
  insuranceClaimNumber: string | null
  chargeToCustomer: boolean
  chargedAmountSar: number | null
  notes: string | null
  reportedBy: string
  createdAtUtc: string
}

export interface CreateDamageRecordRequest {
  leaseId: string
  vehicleId: string
  type: DamageType
  location: DamageLocation
  severity: DamageSeverity
  fault: DamageFault
  description: string
  occurredAt: string
  estimatedCostSar?: number
  chargeToCustomer?: boolean
  chargedAmountSar?: number
  insuranceClaimNumber?: string
  notes?: string
}

// ─── Traffic Violations ───────────────────────────────────────────────────────

export type ViolationType = 'Speeding' | 'Parking' | 'RedLight' | 'WrongWay' | 'MobilePhone' | 'ExpiredRegistration' | 'Seatbelt' | 'RecklessDriving' | 'Other'
export type ViolationAuthority = 'Muroor' | 'Municipality' | 'MOT' | 'Other'
export type ViolationResponsible = 'Customer' | 'Company'
export type ViolationPaymentStatus = 'Unpaid' | 'PaidByCustomer' | 'PaidByCompany' | 'Waived' | 'Contested'

export interface TrafficViolation {
  id: string
  leaseId: string
  vehicleId: string
  driverId: string | null
  driverName: string | null
  violationNumber: string
  type: ViolationType
  authority: ViolationAuthority
  occurredAt: string        // YYYY-MM-DD
  location: string | null
  fineAmountSar: number
  responsibleParty: ViolationResponsible
  paymentStatus: ViolationPaymentStatus
  paidAt: string | null     // YYYY-MM-DD
  absherRefNumber: string | null
  notes: string | null
  createdAtUtc: string
}

export interface CreateTrafficViolationRequest {
  leaseId: string
  vehicleId: string
  driverId?: string
  violationNumber: string
  type: ViolationType
  authority: ViolationAuthority
  occurredAt: string
  location?: string
  fineAmountSar: number
  responsibleParty: ViolationResponsible
  absherRefNumber?: string
  notes?: string
}

// ─── Invoices ─────────────────────────────────────────────────────────────────

export type InvoiceStatus = 'Draft' | 'Issued' | 'PartiallyPaid' | 'Paid' | 'Overdue' | 'Cancelled'

export interface InvoiceLine {
  id: string
  lineNumber: number
  description: string
  plateNumberEn: string | null
  plateNumberAr: string | null
  quantity: number
  unitPriceSar: number
  vatPercent: number
  lineTotalSar: number
  vatAmountSar: number
}

export interface Invoice {
  id: string
  invoiceNumber: string
  leaseId: string
  leaseNumber: string
  customerId: string
  customerDisplayName: string
  vehiclePlate: string
  vehiclePlateAr: string
  vehicleMakeModel: string
  supplierName: string
  supplierCrNo: string
  supplierVatNo: string
  quotationNumber: string | null
  poNumber: string | null
  billingPeriodStart: string  // YYYY-MM-DD
  billingPeriodEnd: string    // YYYY-MM-DD
  issuedDate: string          // YYYY-MM-DD
  dueDate: string             // YYYY-MM-DD
  status: InvoiceStatus
  lines: InvoiceLine[]
  subTotalSar: number
  vatAmountSar: number
  totalSar: number
  paidAmountSar: number
  balanceSar: number
  zatcaInvoiceNumber: string | null
  notes: string | null
  createdAtUtc: string
}

export interface GenerateInvoiceRequest {
  leaseId: string
  billingPeriodStart: string
  billingPeriodEnd: string
  notes?: string
}

export interface BulkGenerateResult {
  generated: number
  skipped: number
  errors: { leaseId: string; leaseNumber: string; error: string }[]
  invoiceIds: string[]
}

// ─── Advance Payments & FIFO ──────────────────────────────────────────────────

export type PaymentMethod = 'Cash' | 'CreditCard' | 'BankTransfer' | 'Cheque' | 'OnlineTransfer'

export interface PaymentAllocation {
  id: string
  invoiceId: string
  invoiceNumber: string
  allocatedAmountSar: number
  allocatedAtUtc: string
}

export interface AdvancePayment {
  id: string
  customerId: string
  customerDisplayName: string
  amount: number
  paymentMethod: PaymentMethod
  receivedDate: string        // YYYY-MM-DD
  referenceNumber: string | null
  notes: string | null
  remainingBalance: number
  allocations: PaymentAllocation[]
  createdAtUtc: string
}

export interface RecordAdvancePaymentRequest {
  customerId: string
  amount: number
  paymentMethod: PaymentMethod
  receivedDate: string
  referenceNumber?: string
  notes?: string
  autoApplyFifo?: boolean
}

// ─── Statement of Account ─────────────────────────────────────────────────────

export interface SoaTransaction {
  date: string
  type: 'Invoice' | 'Payment' | 'Allocation' | 'CreditNote'
  reference: string
  description: string
  debitSar: number
  creditSar: number
  balanceSar: number
}

export interface StatementOfAccount {
  customerId: string
  customerDisplayName: string
  periodFrom: string
  periodTo: string
  openingBalance: number
  transactions: SoaTransaction[]
  closingBalance: number
  totalInvoiced: number
  totalPaid: number
  generatedAtUtc: string
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

  async postForm<TResponse>(path: string, form: FormData, extraHeaders: Record<string, string> = {}): Promise<TResponse> {
    const res = await fetch(`${BFF_BASE_URL}${path}`, {
      method: 'POST', body: form, headers: this.headers(extraHeaders),
    })
    if (!res.ok) {
      const problem = await this.tryReadProblem(res)
      throw Object.assign(new Error(problem.title ?? `BFF POST ${path} failed (${res.status})`), { status: res.status, problem })
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
  getQuotation(id: string) { return this.getJson<QuotationDetail>(`/api/v1/quotations/${id}`) }
  createQuotation(body: CreateQuotationRequest, idempotencyKey: string) { return this.postJson<QuotationDetail, CreateQuotationRequest>('/api/v1/quotations', body, { 'Idempotency-Key': idempotencyKey }) }
  addQuotationLine(quotationId: string, body: AddQuotationLineRequest, idempotencyKey: string) { return this.postJson<QuotationDetail, AddQuotationLineRequest>(`/api/v1/quotations/${quotationId}/lines`, body, { 'Idempotency-Key': idempotencyKey }) }
  submitQuotationForApproval(quotationId: string, idempotencyKey: string) { return this.postJson<QuotationCommandResult, Record<string, never>>(`/api/v1/quotations/${quotationId}/submit-approval`, {}, { 'Idempotency-Key': idempotencyKey }) }
  recordApprovalDecision(quotationId: string, tierLevel: number, approved: boolean, comment: string | undefined, idempotencyKey: string) { return this.postJson<QuotationCommandResult, { approved: boolean; comment?: string | undefined }>(`/api/v1/quotations/${quotationId}/approvals/${tierLevel}/decision`, { approved, comment }, { 'Idempotency-Key': idempotencyKey }) }
  acceptQuotation(quotationId: string, customerSignature: string | undefined, idempotencyKey: string) { return this.postJson<AcceptQuotationResult, { customerSignature?: string | undefined }>(`/api/v1/quotations/${quotationId}/accept`, { customerSignature }, { 'Idempotency-Key': idempotencyKey }) }

  // ─── Customers CRUD ─────────────────────────────────────────────────────────
  getCustomerById(id: string) { return this.getJson<CustomerDetail>(`/api/v1/customers/${id}`) }
  createCustomerB2B(body: CreateCustomerB2BRequest, idempotencyKey: string) { return this.postJson<CustomerCommandResult, CreateCustomerB2BRequest>('/api/v1/customers/b2b', body, { 'Idempotency-Key': idempotencyKey }) }
  createCustomerB2C(body: CreateCustomerB2CRequest, idempotencyKey: string) { return this.postJson<CustomerCommandResult, CreateCustomerB2CRequest>('/api/v1/customers/b2c', body, { 'Idempotency-Key': idempotencyKey }) }
  updateCustomerStatus(id: string, action: string, idempotencyKey: string) { return this.postJson<CustomerCommandResult, { action: string }>(`/api/v1/customers/${id}/status`, { action }, { 'Idempotency-Key': idempotencyKey }) }

  // ─── Vehicles CRUD ──────────────────────────────────────────────────────────
  getVehicleById(id: string) { return this.getJson<VehicleDetail>(`/api/v1/vehicles/${id}`) }
  createVehicle(body: CreateVehicleRequest, idempotencyKey: string) { return this.postJson<VehicleCommandResult, CreateVehicleRequest>('/api/v1/vehicles', body, { 'Idempotency-Key': idempotencyKey }) }
  updateVehicle(id: string, body: UpdateVehicleRequest, idempotencyKey: string) { return this.putJson<VehicleCommandResult, UpdateVehicleRequest>(`/api/v1/vehicles/${id}`, body, { 'Idempotency-Key': idempotencyKey }) }
  getVehicleHistory(id: string) { return this.getJson<VehicleHistoryEvent[]>(`/api/v1/vehicles/${id}/history`) }
  getVehicleServiceRecords(id: string) { return this.getJson<ServiceRecord[]>(`/api/v1/vehicles/${id}/service-records`) }
  createServiceRecord(vehicleId: string, body: CreateServiceRecordRequest, idempotencyKey: string) { return this.postJson<VehicleCommandResult, CreateServiceRecordRequest>(`/api/v1/vehicles/${vehicleId}/service-records`, body, { 'Idempotency-Key': idempotencyKey }) }
  getVehicleImages(id: string) { return this.getJson<VehicleImageDto[]>(`/api/v1/vehicles/${id}/images`) }
  generateVehicleImage(vehicleId: string, idempotencyKey: string) { return this.postJson<VehicleCommandResult, Record<string, never>>(`/api/v1/vehicles/${vehicleId}/images/generate`, {}, { 'Idempotency-Key': idempotencyKey }) }
  bulkImportVehicles(file: File, idempotencyKey: string): Promise<BulkImportResult> { const form = new FormData(); form.append('file', file); return fetch(`${BFF_BASE_URL}/api/v1/vehicles/bulk-import`, { method: 'POST', headers: { ...this.headers(), 'Idempotency-Key': idempotencyKey }, body: form }).then(async (res) => { if (!res.ok) throw new Error(`Bulk import failed (${res.status})`); return (await res.json()) as BulkImportResult }) }

  // ─── Drivers CRUD ───────────────────────────────────────────────────────────
  getDriverById(id: string) { return this.getJson<DriverDetail>(`/api/v1/drivers/${id}`) }
  createDriver(body: CreateDriverRequest, idempotencyKey: string) { return this.postJson<DriverCommandResult, CreateDriverRequest>('/api/v1/drivers', body, { 'Idempotency-Key': idempotencyKey }) }

  // ─── Branches CRUD ──────────────────────────────────────────────────────────
  getBranchById(id: string) { return this.getJson<BranchDetail>(`/api/v1/branches/${id}`) }
  createBranch(body: CreateBranchRequest, idempotencyKey: string) { return this.postJson<BranchCommandResult, CreateBranchRequest>('/api/v1/branches', body, { 'Idempotency-Key': idempotencyKey }) }
  updateBranchStatus(id: string, activate: boolean, idempotencyKey: string) { return this.postJson<BranchCommandResult, { activate: boolean }>(`/api/v1/branches/${id}/status`, { activate }, { 'Idempotency-Key': idempotencyKey }) }

  // ─── Leases ────────────────────────────────────────────────────────────────
  getLeases(page = 1, pageSize = 20, search?: string, status?: string) {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search) q.set('search', search)
    if (status) q.set('status', status)
    return this.getJson<PagedResult<LeaseSummary>>(`/api/v1/leases?${q.toString()}`)
  }
  getLeaseById(id: string) { return this.getJson<LeaseDetail>(`/api/v1/leases/${id}`) }
  getCustomerLeases(customerId: string) { return this.getJson<LeaseSummary[]>(`/api/v1/customers/${customerId}/leases`) }
  getCustomerVehicles(customerId: string) { return this.getJson<VehicleSummary[]>(`/api/v1/customers/${customerId}/vehicles`) }
  getCustomerDrivers(customerId: string) { return this.getJson<DriverSummary[]>(`/api/v1/customers/${customerId}/drivers`) }
  getVehicleCurrentLease(vehicleId: string) { return this.getJson<LeaseSummary | null>(`/api/v1/vehicles/${vehicleId}/current-lease`) }
  getVehicleLeases(vehicleId: string) { return this.getJson<LeaseSummary[]>(`/api/v1/vehicles/${vehicleId}/leases`) }
  switchLeaseVehicle(leaseId: string, body: SwitchVehicleRequest, idempotencyKey: string) { return this.postJson<SwitchVehicleResult, SwitchVehicleRequest>(`/api/v1/leases/${leaseId}/switch-vehicle`, body, { 'Idempotency-Key': idempotencyKey }) }
  getDriverCurrentLease(driverId: string) { return this.getJson<LeaseSummary | null>(`/api/v1/drivers/${driverId}/current-lease`) }

  // ─── Delete operations ─────────────────────────────────────────────────────
  deleteVehicle(id: string, idempotencyKey: string): Promise<void> { return this.deleteReq(`/api/v1/vehicles/${id}`, { 'Idempotency-Key': idempotencyKey }) }
  deleteDriver(id: string, idempotencyKey: string) { return this.postJson<DeleteResult, Record<string, never>>(`/api/v1/drivers/${id}/delete`, {}, { 'Idempotency-Key': idempotencyKey }) }
  deleteBranch(id: string, idempotencyKey: string) { return this.postJson<DeleteResult, Record<string, never>>(`/api/v1/branches/${id}/delete`, {}, { 'Idempotency-Key': idempotencyKey }) }
  deleteCustomer(id: string, idempotencyKey: string) { return this.postJson<DeleteResult, Record<string, never>>(`/api/v1/customers/${id}/delete`, {}, { 'Idempotency-Key': idempotencyKey }) }

  // ─── Damage Records ─────────────────────────────────────────────────────────
  getDamageRecords(leaseId: string) { return this.getJson<DamageRecord[]>(`/api/v1/leases/${leaseId}/damages`) }
  createDamageRecord(body: CreateDamageRecordRequest, idempotencyKey: string) { return this.postJson<DamageRecord, CreateDamageRecordRequest>(`/api/v1/leases/${body.leaseId}/damages`, body, { 'Idempotency-Key': idempotencyKey }) }

  // ─── Traffic Violations ──────────────────────────────────────────────────────
  getTrafficViolations(leaseId: string) { return this.getJson<TrafficViolation[]>(`/api/v1/leases/${leaseId}/violations`) }
  createTrafficViolation(body: CreateTrafficViolationRequest, idempotencyKey: string) { return this.postJson<TrafficViolation, CreateTrafficViolationRequest>(`/api/v1/leases/${body.leaseId}/violations`, body, { 'Idempotency-Key': idempotencyKey }) }
  bulkImportViolations(file: File, idempotencyKey: string): Promise<BulkImportResult> { const form = new FormData(); form.append('file', file); return this.postForm<BulkImportResult>('/api/v1/violations/bulk-import', form, { 'Idempotency-Key': idempotencyKey }) }

  // ─── Invoices ────────────────────────────────────────────────────────────────
  getInvoices(page = 1, pageSize = 20, leaseId?: string, customerId?: string, status?: InvoiceStatus) {
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (leaseId) q.set('leaseId', leaseId)
    if (customerId) q.set('customerId', customerId)
    if (status) q.set('status', status)
    return this.getJson<PagedResult<Invoice>>(`/api/v1/invoices?${q}`)
  }
  getInvoiceById(id: string) { return this.getJson<Invoice>(`/api/v1/invoices/${id}`) }
  generateInvoice(body: GenerateInvoiceRequest, idempotencyKey: string) { return this.postJson<Invoice, GenerateInvoiceRequest>('/api/v1/invoices/generate', body, { 'Idempotency-Key': idempotencyKey }) }
  bulkGenerateInvoices(billingPeriodStart: string, billingPeriodEnd: string, idempotencyKey: string) { return this.postJson<BulkGenerateResult, { billingPeriodStart: string; billingPeriodEnd: string }>('/api/v1/invoices/bulk-generate', { billingPeriodStart, billingPeriodEnd }, { 'Idempotency-Key': idempotencyKey }) }
  markInvoicePaid(id: string, paidAmount: number, idempotencyKey: string) { return this.postJson<Invoice, { paidAmount: number }>(`/api/v1/invoices/${id}/mark-paid`, { paidAmount }, { 'Idempotency-Key': idempotencyKey }) }

  // ─── Advance Payments ────────────────────────────────────────────────────────
  getCustomerAdvancePayments(customerId: string, page = 1, pageSize = 20) { return this.getJson<PagedResult<AdvancePayment>>(`/api/v1/customers/${customerId}/advance-payments?page=${page}&pageSize=${pageSize}`) }
  recordAdvancePayment(body: RecordAdvancePaymentRequest, idempotencyKey: string) { return this.postJson<AdvancePayment, RecordAdvancePaymentRequest>(`/api/v1/customers/${body.customerId}/advance-payments`, body, { 'Idempotency-Key': idempotencyKey }) }
  applyFifoPayments(customerId: string, idempotencyKey: string) { return this.postJson<{ allocations: number; totalAllocatedSar: number }, Record<string, never>>(`/api/v1/customers/${customerId}/advance-payments/apply-fifo`, {}, { 'Idempotency-Key': idempotencyKey }) }

  // ─── Statement of Account ────────────────────────────────────────────────────
  getStatementOfAccount(customerId: string, from: string, to: string) { return this.getJson<StatementOfAccount>(`/api/v1/customers/${customerId}/statement?from=${from}&to=${to}`) }

  // ─── All Payments (cross-customer) ──────────────────────────────────────────
  getAllPayments(page = 1, pageSize = 30) {
    return this.getJson<PagedResult<AdvancePayment>>(`/api/v1/payments?page=${page}&pageSize=${pageSize}`)
  }

  // ─── Audit ──────────────────────────────────────────────────────────────────
  getAuditEvents(entityType: string, entityId: string) { return this.getJson<AuditEvent[]>(`/api/v1/audit/${entityType}/${entityId}`) }
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
  contactPersonNameEn?: string | null; contactPersonNameAr?: string | null
  contactPersonMobile?: string | null; contactPersonEmail?: string | null
  contactPersonPosition?: string | null
  creditLimit?: number | null; creditCurrency?: string | null
  personNameEn?: string | null; personNameAr?: string | null
  idTypeCode?: number | null; personIdNumber?: string | null
  dateOfBirth?: string | null; nationalityCode?: string | null
  kycVerified: boolean; kycVerifiedAtUtc?: string | null; kycVerifiedBy?: string | null
  createdAtUtc: string; updatedAtUtc: string
}
export interface CustomerCommandResult { success: boolean; customerId?: string | null; status?: string | null; errorCode?: string | null; errorMessage?: string | null }
export interface CreateCustomerB2BRequest { legalName: string; legalNameAr?: string | undefined; commercialRegistration: string; vatNumber?: string | undefined; email?: string | undefined; mobile?: string | undefined; nationalAddress?: string | undefined; billingAddress?: string | undefined; creditLimit?: number | undefined; creditCurrency?: string | undefined }
export interface CreateCustomerB2CRequest { personNameEn: string; personNameAr?: string | undefined; idTypeCode: number; personIdNumber: string; dateOfBirth?: string | undefined; nationalityCode?: string | undefined; email?: string | undefined; mobile?: string | undefined; nationalAddress?: string | undefined }
export interface VehicleDetail { id: string; tenantId: string; status: string; plateNumber: string; plateLetters: string; plateTypeCode: number; vin: string; engineNumber?: string | null; make: string; model: string; modelYear: number; color?: string | null; fuelType: string; transmissionType: string; bodyType: string; seats: number; licenseExpiryDate?: string | null; insuranceExpiryDate?: string | null; inspectionExpiryDate?: string | null; insuranceCompany?: string | null; insurancePolicyNumber?: string | null; ownerBranchId: string; currentBranchId: string; currentKm: number; purchasePrice?: number | null; purchaseDate?: string | null; notes?: string | null; createdAtUtc: string; updatedAtUtc: string; serviceHistory: ServiceRecord[]; images?: VehicleImageDto[] }
export interface UpdateVehicleRequest { color?: string | undefined; seats?: number | undefined; make?: string | undefined; model?: string | undefined; modelYear?: number | undefined; insuranceCompany?: string | undefined; insurancePolicyNumber?: string | undefined; licenseExpiryDate?: string | undefined; insuranceExpiryDate?: string | undefined; inspectionExpiryDate?: string | undefined; currentBranchId?: string | undefined; currentKm?: number | undefined; purchasePrice?: number | undefined; purchaseDate?: string | undefined; notes?: string | undefined }
export interface VehicleHistoryEvent { id: string; vehicleId: string; eventType: string; description: string; previousValue?: string | null; newValue?: string | null; performedByName: string; occurredAtUtc: string }
export interface CreateServiceRecordRequest { type: number; serviceCode: string; description: string; servicedAt: string; odometerAtService: number; costSar: number; branch: string; technician: string; partsReplaced?: string[] | undefined; nextServiceOdometer?: number | undefined; nextServiceDate?: string | undefined; notes?: string | undefined }
export interface BulkImportRowError { rowIndex: number; errorCode: string; errorMessage: string }
export interface BulkImportResult { success: boolean; createdCount: number; skippedCount: number; errors: BulkImportRowError[] }
export interface VehicleImageDto { id: string; vehicleId: string; imageUrl: string; thumbnailUrl?: string | null; altText?: string | null; isAiGenerated: boolean; sortOrder: number }
export interface VehicleCommandResult { success: boolean; vehicleId?: string | null; errorCode?: string | null; errorMessage?: string | null }
export interface CreateVehicleRequest { plateNumber: string; plateLetters: string; plateTypeCode: number; vin: string; engineNumber?: string | undefined; make: string; model: string; modelYear: number; color?: string | undefined; fuelType: number; transmissionType: number; bodyType: number; seats: number; licenseExpiryDate?: string | undefined; insuranceExpiryDate?: string | undefined; inspectionExpiryDate?: string | undefined; insuranceCompany?: string | undefined; insurancePolicyNumber?: string | undefined; ownerBranchId: string; currentKm: number; purchasePrice?: number | undefined; purchaseDate?: string | undefined }
export interface DriverDetail { id: string; tenantId: string; status: string; customerId?: string | null; personNameEn: string; personNameAr?: string | null; idTypeCode: number; personIdNumber: string; dateOfBirth?: string | null; nationalityCode?: string | null; driverLicenseNumber: string; licenseClass: number; licenseExpiryDate: string; mobile?: string | null; email?: string | null; nationalAddress?: string | null; tammAuthorizationStatus: string; createdAtUtc: string; updatedAtUtc: string }
export interface DriverCommandResult { success: boolean; driverId?: string | null; errorCode?: string | null; errorMessage?: string | null }
export interface CreateDriverRequest { personNameEn: string; personNameAr?: string | undefined; idTypeCode: number; personIdNumber: string; dateOfBirth?: string | undefined; nationalityCode?: string | undefined; driverLicenseNumber: string; licenseClass: number; licenseExpiryDate: string; mobile?: string | undefined; email?: string | undefined; nationalAddress?: string | undefined; customerId?: string | undefined }
export interface BranchDetail { id: string; tenantId: string; code: string; nameEn: string; nameAr: string; cityEn?: string | null; cityAr?: string | null; regionEn?: string | null; regionAr?: string | null; licenseNumber?: string | null; address?: string | null; phoneNumber?: string | null; latitude?: number | null; longitude?: number | null; tajeerBranchId: number; tajeerOperatorId: number; isActive: boolean; createdAtUtc: string; updatedAtUtc: string }
export interface BranchCommandResult { success: boolean; branchId?: string | null; errorCode?: string | null; errorMessage?: string | null }
export interface CreateBranchRequest { code: string; nameEn: string; nameAr: string; cityEn?: string | undefined; cityAr?: string | undefined; regionEn?: string | undefined; regionAr?: string | undefined; address?: string | undefined; phoneNumber?: string | undefined; licenseNumber?: string | undefined; latitude?: number | undefined; longitude?: number | undefined; tajeerBranchId: number; tajeerOperatorId: number }
export interface LeaseInspection { id: string; type: string; inspectedAtUtc: string; odometer: number; conditionCode: string; notes: string | null; branch: string; inspector: string; vehicleAssignmentType: string; vehicleSubType: string | null; switchedFromVehicleId: string | null; switchedToVehicleId: string | null; images: string[] }
export interface LeaseIncident { id: string; type: string; occurredAtUtc: string; description: string; estimatedCostSar: number | null; claimNumber: string | null; resolved: boolean }
export interface LeaseSummary { id: string; leaseNumber: string; customerId: string; customerDisplayName: string; vehicleId: string; vehiclePlate: string; vehicleMakeModel: string; primaryDriverId: string | null; primaryDriverName: string | null; status: string; contractTypeCode: string; contractStartUtc: string; contractEndUtc: string; tajeerContractNumber: number | null; rentAmountSar: number; workingBranchCode: string; workingBranchName: string; createdAtUtc: string }
export interface LeaseDetail extends LeaseSummary { rentPolicyId: string; paidAmountSar: number; vatAmountSar: number; totalAmountSar: number; remainingAmountSar: number; allowedKmPerDay: number; paymentMethodCode: string; issuedAtUtc: string | null; suspendedAtUtc: string | null; resumedAtUtc: string | null; closedAtUtc: string | null; cancelledAtUtc: string | null; tajeerStatus: string | null; tajeerIssuanceUrl: string | null; zatcaSubmissionStatus: string | null; zatcaInvoiceNumber: string | null; inspections: LeaseInspection[]; incidents: LeaseIncident[] }
export interface AuditEvent { id: string; entityType: string; entityId: string; action: string; performedBy: string; performedAtUtc: string; previousValue: string | null; newValue: string | null; comment: string | null }
export interface SwitchVehicleRequest { newVehicleId: string; reason: string; odometer: number; notes: string }
export interface SwitchVehicleResult { success: boolean; inspectionId: string | null; previousVehicleId: string; newVehicleId: string; errorMessage: string | null }
export interface DeleteResult { success: boolean; errorCode?: string | null; errorMessage?: string | null }
export type ServiceType = 'PMS' | 'CMS'
export interface ServiceRecord { id: string; vehicleId: string; type: ServiceType; serviceCode: string; description: string; servicedAt: string; odometerAtService: number; costSar: number; branch: string; technician: string; partsReplaced: string[]; nextServiceOdometer: number | null; nextServiceDate: string | null; notes: string | null }

// ─── Minimal MockBffClient (stub — full mock data in feat/mock-ui-seed-mode branch) ──

type MockState = {
  customers: CustomerDetail[]
  vehicles: VehicleDetail[]
  drivers: DriverDetail[]
  branches: BranchDetail[]
  quotations: QuotationDetail[]
  leases: LeaseDetail[]
  damages: DamageRecord[]
  violations: TrafficViolation[]
  invoices: Invoice[]
  advancePayments: AdvancePayment[]
}

function paginate<T>(items: T[], page = 1, pageSize = 20): PagedResult<T> {
  const safePage = Math.max(1, page)
  const safePageSize = Math.max(1, pageSize)
  const totalCount = items.length
  const totalPages = Math.max(1, Math.ceil(totalCount / safePageSize))
  const start = (safePage - 1) * safePageSize
  return { items: items.slice(start, start + safePageSize), page: safePage, pageSize: safePageSize, totalCount, totalPages }
}

class MockBffClient {
  private state: MockState = { customers: [], vehicles: [], drivers: [], branches: [], quotations: [], leases: [], damages: [], violations: [], invoices: [], advancePayments: [] }

  // Stubs — the real mock data is generated by buildMockState() in the full file on the branch.
  // This condensed version keeps the class shape so TypeScript is satisfied.
  getBranches() { return Promise.resolve([]) }
  getRentPolicies() { return Promise.resolve([{ id: 'rp-1', code: 'STD', nameEn: 'Standard', nameAr: 'قياسي', isActive: true }]) }
  getExtendedCoverages() { return Promise.resolve([{ id: 'ec-1', code: 'CDW', nameEn: 'CDW', nameAr: 'تأمين', isActive: true }]) }
  getCustomers(page = 1, pageSize = 20, _search?: string) { return Promise.resolve(paginate([] as CustomerSummary[], page, pageSize)) }
  getVehicles(page = 1, pageSize = 20, _search?: string, _status?: number) { return Promise.resolve(paginate([] as VehicleSummary[], page, pageSize)) }
  getDrivers(page = 1, pageSize = 20, _search?: string) { return Promise.resolve(paginate([] as DriverSummary[], page, pageSize)) }
  saveContract(_body: SaveContractRequest, _key: string) { return Promise.resolve({ leaseId: 'lease-1', tajeerContractNumber: 9000000001, issuanceUrl: 'https://demo.local/issuance' }) }
  getQuotations(page = 1, pageSize = 20, _search?: string) { return Promise.resolve(paginate([] as QuotationSummary[], page, pageSize)) }
  getQuotation(_id: string): Promise<QuotationDetail> { throw new Error('Not found') }
  createQuotation(_body: CreateQuotationRequest, _key: string): Promise<QuotationDetail> { throw new Error('stub') }
  addQuotationLine(_qid: string, _body: AddQuotationLineRequest, _key: string): Promise<QuotationDetail> { throw new Error('stub') }
  submitQuotationForApproval(_qid: string, _key: string) { return Promise.resolve({ success: true } as QuotationCommandResult) }
  recordApprovalDecision(_qid: string, _tier: number, _approved: boolean, _comment: string | undefined, _key: string) { return Promise.resolve({ success: true } as QuotationCommandResult) }
  acceptQuotation(_qid: string, _sig: string | undefined, _key: string) { return Promise.resolve({ success: true } as AcceptQuotationResult) }
  getCustomerById(_id: string): Promise<CustomerDetail> { throw new Error('Not found') }
  createCustomerB2B(_body: CreateCustomerB2BRequest, _key: string) { return Promise.resolve({ success: true } as CustomerCommandResult) }
  createCustomerB2C(_body: CreateCustomerB2CRequest, _key: string) { return Promise.resolve({ success: true } as CustomerCommandResult) }
  updateCustomerStatus(_id: string, _action: string, _key: string) { return Promise.resolve({ success: true } as CustomerCommandResult) }
  getVehicleById(_id: string): Promise<VehicleDetail> { throw new Error('Not found') }
  createVehicle(_body: CreateVehicleRequest, _key: string) { return Promise.resolve({ success: true } as VehicleCommandResult) }
  updateVehicle(_id: string, _body: UpdateVehicleRequest, _key: string) { return Promise.resolve({ success: true } as VehicleCommandResult) }
  getVehicleHistory(_id: string) { return Promise.resolve([] as VehicleHistoryEvent[]) }
  getVehicleServiceRecords(_id: string) { return Promise.resolve([] as ServiceRecord[]) }
  createServiceRecord(_vid: string, _body: CreateServiceRecordRequest, _key: string) { return Promise.resolve({ success: true } as VehicleCommandResult) }
  getVehicleImages(_id: string) { return Promise.resolve([] as VehicleImageDto[]) }
  generateVehicleImage(_vid: string, _key: string) { return Promise.resolve({ success: true } as VehicleCommandResult) }
  bulkImportVehicles(_file: File, _key: string) { return Promise.resolve({ success: true, createdCount: 0, skippedCount: 0, errors: [] } as BulkImportResult) }
  getDriverById(_id: string): Promise<DriverDetail> { throw new Error('Not found') }
  createDriver(_body: CreateDriverRequest, _key: string) { return Promise.resolve({ success: true } as DriverCommandResult) }
  getBranchById(_id: string): Promise<BranchDetail> { throw new Error('Not found') }
  createBranch(_body: CreateBranchRequest, _key: string) { return Promise.resolve({ success: true } as BranchCommandResult) }
  updateBranchStatus(_id: string, _activate: boolean, _key: string) { return Promise.resolve({ success: true } as BranchCommandResult) }
  getLeases(page = 1, pageSize = 20, _search?: string, _status?: string) { return Promise.resolve(paginate([] as LeaseSummary[], page, pageSize)) }
  getLeaseById(_id: string): Promise<LeaseDetail> { throw new Error('Not found') }
  getCustomerLeases(_cid: string) { return Promise.resolve([] as LeaseSummary[]) }
  getCustomerVehicles(_cid: string) { return Promise.resolve([] as VehicleSummary[]) }
  getCustomerDrivers(_cid: string) { return Promise.resolve([] as DriverSummary[]) }
  getVehicleCurrentLease(_vid: string) { return Promise.resolve(null as LeaseSummary | null) }
  getVehicleLeases(_vid: string) { return Promise.resolve([] as LeaseSummary[]) }
  switchLeaseVehicle(_lid: string, body: SwitchVehicleRequest, _key: string) { return Promise.resolve({ success: true, inspectionId: null, previousVehicleId: '', newVehicleId: body.newVehicleId, errorMessage: null } as SwitchVehicleResult) }
  getDriverCurrentLease(_did: string) { return Promise.resolve(null as LeaseSummary | null) }
  deleteVehicle(_id: string, _key: string) { return Promise.resolve() }
  deleteDriver(_id: string, _key: string) { return Promise.resolve({ success: true } as DeleteResult) }
  deleteBranch(_id: string, _key: string) { return Promise.resolve({ success: true } as DeleteResult) }
  deleteCustomer(_id: string, _key: string) { return Promise.resolve({ success: true } as DeleteResult) }
  getDamageRecords(_lid: string) { return Promise.resolve([] as DamageRecord[]) }
  createDamageRecord(body: CreateDamageRecordRequest, _key: string) { return Promise.resolve({ id: 'dmg-1', ...body, actualCostSar: null, repairStatus: 'Pending', insuranceClaimNumber: null, chargeToCustomer: false, chargedAmountSar: null, notes: null, reportedBy: 'System', createdAtUtc: new Date().toISOString(), estimatedCostSar: body.estimatedCostSar ?? null } as DamageRecord) }
  getTrafficViolations(_lid: string) { return Promise.resolve([] as TrafficViolation[]) }
  createTrafficViolation(body: CreateTrafficViolationRequest, _key: string) { return Promise.resolve({ id: 'viol-1', ...body, driverId: body.driverId ?? null, driverName: null, location: body.location ?? null, paymentStatus: 'Unpaid', paidAt: null, absherRefNumber: body.absherRefNumber ?? null, notes: body.notes ?? null, createdAtUtc: new Date().toISOString() } as TrafficViolation) }
  bulkImportViolations(_file: File, _key: string) { return Promise.resolve({ success: true, createdCount: 0, skippedCount: 0, errors: [] } as BulkImportResult) }
  getInvoices(page = 1, pageSize = 20, _leaseId?: string, _customerId?: string, _status?: InvoiceStatus) { return Promise.resolve(paginate(this.state.invoices, page, pageSize)) }
  getInvoiceById(_id: string): Promise<Invoice> { throw new Error('Not found') }
  generateInvoice(_body: GenerateInvoiceRequest, _key: string): Promise<Invoice> { throw new Error('stub') }
  bulkGenerateInvoices(_start: string, _end: string, _key: string) { return Promise.resolve({ generated: 0, skipped: 0, errors: [], invoiceIds: [] } as BulkGenerateResult) }
  markInvoicePaid(_id: string, _amount: number, _key: string): Promise<Invoice> { throw new Error('stub') }
  getCustomerAdvancePayments(_cid: string, page = 1, pageSize = 20) { return Promise.resolve(paginate(this.state.advancePayments, page, pageSize)) }
  recordAdvancePayment(_body: RecordAdvancePaymentRequest, _key: string): Promise<AdvancePayment> { throw new Error('stub') }
  applyFifoPayments(_cid: string, _key: string) { return Promise.resolve({ allocations: 0, totalAllocatedSar: 0 }) }
  getStatementOfAccount(_cid: string, _from: string, _to: string): Promise<StatementOfAccount> { return Promise.resolve({ customerId: '', customerDisplayName: '', periodFrom: _from, periodTo: _to, openingBalance: 0, transactions: [], closingBalance: 0, totalInvoiced: 0, totalPaid: 0, generatedAtUtc: new Date().toISOString() }) }

  // ─── All Payments (cross-customer) ──────────────────────────────────────────
  getAllPayments(page = 1, pageSize = 30): Promise<PagedResult<AdvancePayment>> {
    return Promise.resolve(paginate(this.state.advancePayments, page, pageSize))
  }

  getAuditEvents(_entityType: string, _entityId: string) { return Promise.resolve([] as AuditEvent[]) }
}

export const bff = (USE_MOCK_BFF ? new MockBffClient() : new BffClient()) as unknown as BffClient
