// Typed BFF client for the Customer Portal. Always sends EXTERNAL_INDIVIDUAL
// headers + the demo customer id; RLS at the DB layer enforces that the user
// only sees their own data (Day-9 workstream).

import { DEV_DEMO_CUSTOMER } from './dev-customer'

export const BFF_BASE_URL = process.env.NEXT_PUBLIC_BFF_BASE_URL ?? 'http://localhost:5000'

export interface MyLease {
  id: string
  tajeerContractNumber: number | null
  // LeaseStatus enum: 0=Draft 1=SaveFailed 2=PendingIssuance 3=Active 4=Extended
  //                    5=Suspended 6=Closed 7=Cancelled 8=ExpiredDraft
  status: number
  contractStartUtc: string
  contractEndUtc: string
  issuedAtUtc: string | null
  closedAtUtc: string | null
  rentAmount: number
  totalAmount: number | null
}

export interface MyVehicle {
  id: string
  plateNumber: string
  plateLetters: string
  plateTypeCode: number
  make: string
  model: string
  modelYear: number
  color: string | null
  currentKm: number
  licenseExpiryDate: string | null  // ISO date (yyyy-MM-dd)
  insuranceExpiryDate: string | null
}

export interface MyVehicleDetail {
  id: string
  plateNumber: string
  plateLetters: string
  plateTypeCode: number
  make: string
  model: string
  modelYear: number
  color: string | null
  fuelTypeCode: number
  transmissionTypeCode: number
  bodyTypeCode: number
  seats: number
  currentKm: number
  licenseExpiryDate: string | null
  insuranceExpiryDate: string | null
  inspectionExpiryDate: string | null
  insuranceCompany: string | null
  insurancePolicyNumber: string | null
  nextServiceDueKm: number | null
  nextServiceDueDate: string | null
}

export interface LeaseVehicleSummary {
  id: string
  plateNumber: string
  plateLetters: string
  plateTypeCode: number
  make: string
  model: string
  modelYear: number
  color: string | null
}

export interface MyLeaseDetail {
  id: string
  tajeerContractNumber: number | null
  status: number
  contractTypeCode: number
  contractStartUtc: string
  contractEndUtc: string
  actualReturnUtc: string | null
  allowedKmPerHour: number
  allowedKmPerDay: number
  unlimitedKm: boolean
  allowedLateHours: number
  extensionCount: number
  rentAmount: number
  paidAmount: number
  remainingAmount: number
  vatAmount: number
  totalAmount: number
  paymentMethodCode: number
  discountType: number | null
  discountValue: number | null
  savedAtUtc: string | null
  issuedAtUtc: string | null
  suspendedAtUtc: string | null
  resumedAtUtc: string | null
  closedAtUtc: string | null
  cancelledAtUtc: string | null
  expiredAtUtc: string | null
  suspensionReasonCode: number | null
  closureMainReasonCode: number | null
  closureSubReasonCode: number | null
  vehicle: LeaseVehicleSummary | null
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}

class CustomerBffClient {
  private headers(extra: Record<string, string> = {}): HeadersInit {
    return {
      'X-Dev-Tenant-Id': DEV_DEMO_CUSTOMER.tenantId,
      'X-Dev-User-Type': DEV_DEMO_CUSTOMER.userType,
      'X-Dev-Customer-Id': DEV_DEMO_CUSTOMER.customerId,
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
      throw Object.assign(
        new Error(problem.title ?? `BFF GET ${path} failed (${res.status})`),
        { status: res.status, problem },
      )
    }
    return (await res.json()) as T
  }

  private async tryReadProblem(res: Response): Promise<ProblemDetails> {
    try {
      return (await res.json()) as ProblemDetails
    } catch {
      return { title: res.statusText, status: res.status }
    }
  }

  getMyLeases() {
    return this.getJson<MyLease[]>('/api/v1/me/leases')
  }

  getMyVehicles() {
    return this.getJson<MyVehicle[]>('/api/v1/me/vehicles')
  }

  getMyLeaseDetail(leaseId: string) {
    return this.getJson<MyLeaseDetail>(`/api/v1/me/leases/${encodeURIComponent(leaseId)}`)
  }

  getMyVehicleDetail(vehicleId: string) {
    return this.getJson<MyVehicleDetail>(`/api/v1/me/vehicles/${encodeURIComponent(vehicleId)}`)
  }
}

export const bff = new CustomerBffClient()
