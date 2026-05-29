// Typed BFF client for the Customer Portal. Always sends EXTERNAL_INDIVIDUAL
// headers + the demo customer id; RLS at the DB layer enforces that the user
// only sees their own data (Day-9 workstream).

import { DEV_DEMO_CUSTOMER } from './dev-customer'

export const BFF_BASE_URL = process.env.NEXT_PUBLIC_BFF_BASE_URL ?? 'http://localhost:5000'

export interface MyLease {
  id: string
  tajeerContractNumber: number | null
  status: number // LeaseStatus: 1=Pending 2=Active 3=Extended 4=Suspended 5=Closed 6=Cancelled 7=Expired 99=SaveFailed
  contractStartUtc: string
  contractEndUtc: string
  issuedAtUtc: string | null
  closedAtUtc: string | null
  rentAmount: number
  totalAmount: number | null
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
}

export const bff = new CustomerBffClient()
