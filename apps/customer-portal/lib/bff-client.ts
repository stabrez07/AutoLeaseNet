// Typed BFF client for the Customer Portal.
// In development USE_MOCK_BFF=true (default) returns rich in-memory data so
// the portal works without the .NET backend running.

import { DEV_DEMO_CUSTOMER } from './dev-customer'

export const BFF_BASE_URL = process.env.NEXT_PUBLIC_BFF_BASE_URL ?? 'http://localhost:5000'
export const USE_MOCK_BFF =
  process.env.NODE_ENV !== 'production' && (process.env.NEXT_PUBLIC_USE_MOCK ?? 'true') !== 'false'

// ─── Shared types ────────────────────────────────────────────────────────────

export interface MyLease {
  id: string
  tajeerContractNumber: number | null
  // LeaseStatus: 0=Draft 1=SaveFailed 2=PendingIssuance 3=Active 4=Extended
  //              5=Suspended 6=Closed 7=Cancelled 8=ExpiredDraft
  status: number
  contractStartUtc: string
  contractEndUtc: string
  issuedAtUtc: string | null
  closedAtUtc: string | null
  rentAmount: number
  totalAmount: number | null
  vehicleMakeModel: string
  vehiclePlate: string
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
  licenseExpiryDate: string | null
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

export interface LeaseInspectionSummary {
  id: string
  type: string
  inspectedAtUtc: string
  odometer: number
  conditionCode: string
  notes: string | null
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
  inspections: LeaseInspectionSummary[]
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}

// ─── Mock state ──────────────────────────────────────────────────────────────

type MockVehicle = MyVehicleDetail & { plateLettersDisplay: string }

function mockId(prefix: string, n: number) {
  return `${prefix}-${n.toString().padStart(5, '0')}`
}

function pick<T>(arr: T[], i: number): T {
  return arr[i % arr.length] as T
}

function buildCustomerMockState() {
  const now = new Date()
  const MAKES = ['Toyota', 'Hyundai', 'Nissan', 'Kia', 'GMC']
  const MODELS = ['Camry', 'Sonata', 'Altima', 'Sportage', 'Yukon']
  const COLORS = ['White', 'Silver', 'Black', 'Grey', 'Red']
  const PLATE_LETTERS = ['أ ب ج', 'د هـ و', 'ز ح ط', 'ي ك ل', 'م ن هـ']

  const vehicles: MockVehicle[] = Array.from({ length: 8 }).map((_, i) => ({
    id: mockId('my-vehicle', i + 1),
    plateNumber: `${5000 + i}`,
    plateLetters: pick(PLATE_LETTERS, i),
    plateLettersDisplay: pick(PLATE_LETTERS, i),
    plateTypeCode: 1,
    make: pick(MAKES, i),
    model: pick(MODELS, i),
    modelYear: 2022 + (i % 3),
    color: pick(COLORS, i),
    fuelTypeCode: 1,
    transmissionTypeCode: 1,
    bodyTypeCode: i % 3 === 0 ? 2 : 1,
    seats: i % 2 === 0 ? 5 : 7,
    currentKm: 12000 + i * 3500,
    licenseExpiryDate: `2027-${String((i % 12) + 1).padStart(2, '0')}-15`,
    insuranceExpiryDate: `2027-${String((i % 12) + 1).padStart(2, '0')}-20`,
    inspectionExpiryDate: `2027-${String((i % 12) + 1).padStart(2, '0')}-25`,
    insuranceCompany: 'Tawuniya',
    insurancePolicyNumber: `POL-${200000 + i}`,
    nextServiceDueKm: 15000 + i * 3500,
    nextServiceDueDate: `2026-${String((i % 12) + 1).padStart(2, '0')}-01`,
  }))

  const STATUSES = [3, 3, 4, 3, 6, 3, 5, 6] // Active, Active, Extended, Active, Closed, Active, Suspended, Closed
  const CONTRACT_TYPES = [1, 2, 2, 1, 1, 2, 1, 2] // Daily=1 Monthly=2
  const PAYMENT_METHODS = [1, 2, 1, 2, 1, 2, 1, 2] // Cash=1 CreditCard=2

  const leases: MyLeaseDetail[] = Array.from({ length: 12 }).map((_, i) => {
    const vehicle = vehicles[i % vehicles.length]!
    const status = STATUSES[i % STATUSES.length]!
    const isActive = status === 3 || status === 4
    const isClosed = status === 6
    const contractStart = new Date(now.getTime() - (365 - i * 28) * 86400000)
    const contractEnd = new Date(contractStart.getTime() + (6 + i % 12) * 30 * 86400000)
    const rentAmt = 1800 + i * 300
    const vatAmt = Math.round(rentAmt * 0.15 * 100) / 100
    const total = rentAmt + vatAmt

    return {
      id: mockId('my-lease', i + 1),
      tajeerContractNumber: status >= 3 ? 9000100000 + i + 1 : null,
      status,
      contractTypeCode: CONTRACT_TYPES[i % CONTRACT_TYPES.length]!,
      contractStartUtc: contractStart.toISOString(),
      contractEndUtc: contractEnd.toISOString(),
      actualReturnUtc: isClosed ? contractEnd.toISOString() : null,
      allowedKmPerHour: pick([10, 15, 20], i),
      allowedKmPerDay: pick([100, 150, 200, 250], i),
      unlimitedKm: false,
      allowedLateHours: 2,
      extensionCount: status === 4 ? 1 : 0,
      rentAmount: rentAmt,
      paidAmount: isActive || isClosed ? rentAmt : 0,
      remainingAmount: isActive ? vatAmt : isClosed ? 0 : total,
      vatAmount: vatAmt,
      totalAmount: total,
      paymentMethodCode: PAYMENT_METHODS[i % PAYMENT_METHODS.length]!,
      discountType: null,
      discountValue: null,
      savedAtUtc: contractStart.toISOString(),
      issuedAtUtc: status >= 3 ? contractStart.toISOString() : null,
      suspendedAtUtc: status === 5 ? new Date(contractStart.getTime() + 30 * 86400000).toISOString() : null,
      resumedAtUtc: null,
      closedAtUtc: isClosed ? contractEnd.toISOString() : null,
      cancelledAtUtc: status === 7 ? contractStart.toISOString() : null,
      expiredAtUtc: null,
      suspensionReasonCode: status === 5 ? 1 : null,
      closureMainReasonCode: isClosed ? 1 : null,
      closureSubReasonCode: isClosed ? 1 : null,
      vehicle: {
        id: vehicle.id,
        plateNumber: vehicle.plateNumber,
        plateLetters: vehicle.plateLetters,
        plateTypeCode: vehicle.plateTypeCode,
        make: vehicle.make,
        model: vehicle.model,
        modelYear: vehicle.modelYear,
        color: vehicle.color,
      },
      inspections: isActive || isClosed ? [
        {
          id: mockId('insp', i * 2 + 1),
          type: 'CheckOut',
          inspectedAtUtc: contractStart.toISOString(),
          odometer: vehicle.currentKm - 3000,
          conditionCode: 'Good',
          notes: 'Vehicle checked out — no damage noted.',
        },
        ...(isClosed ? [{
          id: mockId('insp', i * 2 + 2),
          type: 'CheckIn',
          inspectedAtUtc: contractEnd.toISOString(),
          odometer: vehicle.currentKm,
          conditionCode: i % 5 === 0 ? 'Fair' : 'Good',
          notes: i % 5 === 0 ? 'Minor wear noted on rear bumper.' : 'Returned in good condition.',
        }] : [])
      ] : [],
    }
  })

  // Summary form for list views
  const leaseSummaries: MyLease[] = leases.map((l) => ({
    id: l.id,
    tajeerContractNumber: l.tajeerContractNumber,
    status: l.status,
    contractStartUtc: l.contractStartUtc,
    contractEndUtc: l.contractEndUtc,
    issuedAtUtc: l.issuedAtUtc,
    closedAtUtc: l.closedAtUtc,
    rentAmount: l.rentAmount,
    totalAmount: l.totalAmount,
    vehicleMakeModel: l.vehicle ? `${l.vehicle.make} ${l.vehicle.model}` : '—',
    vehiclePlate: l.vehicle ? `${l.vehicle.plateLetters} ${l.vehicle.plateNumber}` : '—',
  }))

  const vehicleSummaries: MyVehicle[] = vehicles.map((v) => ({
    id: v.id,
    plateNumber: v.plateNumber,
    plateLetters: v.plateLetters,
    plateTypeCode: v.plateTypeCode,
    make: v.make,
    model: v.model,
    modelYear: v.modelYear,
    color: v.color,
    currentKm: v.currentKm,
    licenseExpiryDate: v.licenseExpiryDate,
    insuranceExpiryDate: v.insuranceExpiryDate,
  }))

  return { leases, leaseSummaries, vehicles, vehicleSummaries }
}

// ─── Mock client ─────────────────────────────────────────────────────────────

class MockCustomerBffClient {
  private state = buildCustomerMockState()

  getMyLeases(): Promise<MyLease[]> {
    return Promise.resolve(this.state.leaseSummaries)
  }

  getMyVehicles(): Promise<MyVehicle[]> {
    return Promise.resolve(this.state.vehicleSummaries)
  }

  getMyLeaseDetail(leaseId: string): Promise<MyLeaseDetail> {
    const lease = this.state.leases.find((l) => l.id === leaseId)
    if (!lease) return Promise.reject(new Error('Lease not found'))
    return Promise.resolve(lease)
  }

  getMyVehicleDetail(vehicleId: string): Promise<MyVehicleDetail> {
    const vehicle = this.state.vehicles.find((v) => v.id === vehicleId)
    if (!vehicle) return Promise.reject(new Error('Vehicle not found'))
    return Promise.resolve(vehicle)
  }
}

// ─── Real client ─────────────────────────────────────────────────────────────

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

export const bff = (USE_MOCK_BFF ? new MockCustomerBffClient() : new CustomerBffClient()) as unknown as CustomerBffClient
