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
}

export const bff = new BffClient()
