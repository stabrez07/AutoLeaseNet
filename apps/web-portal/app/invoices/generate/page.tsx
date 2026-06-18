'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import {
  bff,
  type BulkGenerateResult,
  type LeaseSummary,
  type Invoice,
  type DamageRecord,
  type TrafficViolation,
  type PagedResult,
} from '../../../lib/bff-client'
import {
  Card,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  Spinner,
} from '../../../components/ui'

// ─── Types ───────────────────────────────────────────────────────────────────

interface PreGenChecks {
  activeContracts: number
  missingEmail: number
  pendingDamages: number
  pendingViolations: number
  alreadyGenerated: number
}

type GenerateScope = 'all' | 'selected-customers' | 'selected-contracts'

// ─── Helpers ─────────────────────────────────────────────────────────────────

function getMonthRange(m: string) {
  const [y, mo] = m.split('-').map(Number)
  const start = `${m}-01`
  const lastDay = new Date(y!, mo!, 0).getDate()
  const end = `${m}-${String(lastDay).padStart(2, '0')}`
  return { start, end }
}

// ─── Page component ──────────────────────────────────────────────────────────

export default function BulkGenerateInvoicesPage() {
  const router = useRouter()
  const now = new Date()
  const defaultMonth = now.toISOString().substring(0, 7)

  // Step state
  const [step, setStep] = useState<1 | 2 | 3 | 4>(1)
  const [month, setMonth] = useState(defaultMonth)

  // Pre-gen checks state
  const [checking, setChecking] = useState(false)
  const [checks, setChecks] = useState<PreGenChecks | null>(null)
  const [checkError, setCheckError] = useState<string | null>(null)

  // Generation options
  const [scope, setScope] = useState<GenerateScope>('all')
  const [includeDamages, setIncludeDamages] = useState(true)
  const [includeViolations, setIncludeViolations] = useState(true)

  // Generation state
  const [generating, setGenerating] = useState(false)
  const [result, setResult] = useState<BulkGenerateResult | null>(null)
  const [genError, setGenError] = useState<string | null>(null)

  const { start, end } = getMonthRange(month)

  // ─── Step 2: Pre-generation checks ──────────────────────────────────────

  async function runChecks() {
    setChecking(true)
    setCheckError(null)
    setChecks(null)
    try {
      // Fetch active leases
      const leasesResult: PagedResult<LeaseSummary> = await bff.getLeases(1, 500, undefined, 'Active')
      const activeLeases = leasesResult.items
      const extendedResult: PagedResult<LeaseSummary> = await bff.getLeases(1, 500, undefined, 'Extended')
      const allActive = [...activeLeases, ...extendedResult.items]

      // Check for existing invoices in this period
      const invoicesResult: PagedResult<Invoice> = await bff.getInvoices(1, 1000)
      const existingForPeriod = invoicesResult.items.filter(
        (inv) => inv.billingPeriodStart === start
      )

      // Count missing emails (leases whose customer might not have email)
      // Since we don't have customer detail in lease summary, estimate from display name
      const missingEmail = 0 // Would need customer detail fetch; show 0 for now

      // Count pending damages and violations (across all leases)
      let pendingDamages = 0
      let pendingViolations = 0

      // Sample a few leases to count pending items
      const sampleLeases = allActive.slice(0, 20)
      const damagePromises: Promise<DamageRecord[]>[] = sampleLeases.map((l) =>
        bff.getDamageRecords(l.id).catch(() => [] as DamageRecord[])
      )
      const violationPromises: Promise<TrafficViolation[]>[] = sampleLeases.map((l) =>
        bff.getTrafficViolations(l.id).catch(() => [] as TrafficViolation[])
      )
      const damageResults = await Promise.all(damagePromises)
      const violationResults = await Promise.all(violationPromises)

      damageResults.forEach((damages) => {
        pendingDamages += damages.filter((d) => d.chargeToCustomer && d.repairStatus !== 'Completed' && d.repairStatus !== 'Waived').length
      })
      violationResults.forEach((violations) => {
        pendingViolations += violations.filter((v) => v.paymentStatus === 'Unpaid' && v.responsibleParty === 'Customer').length
      })

      // Extrapolate for remaining leases
      if (sampleLeases.length > 0 && allActive.length > sampleLeases.length) {
        const factor = allActive.length / sampleLeases.length
        pendingDamages = Math.round(pendingDamages * factor)
        pendingViolations = Math.round(pendingViolations * factor)
      }

      setChecks({
        activeContracts: allActive.length,
        missingEmail,
        pendingDamages,
        pendingViolations,
        alreadyGenerated: existingForPeriod.length,
      })
      setStep(2)
    } catch (e) {
      setCheckError((e as Error).message)
    } finally {
      setChecking(false)
    }
  }

  // ─── Step 4: Generate ───────────────────────────────────────────────────

  async function handleGenerate() {
    setGenerating(true)
    setGenError(null)
    setResult(null)
    try {
      const res = await bff.bulkGenerateInvoices(start, end, crypto.randomUUID())
      setResult(res)
      setStep(4)
    } catch (e) {
      setGenError((e as Error).message)
    } finally {
      setGenerating(false)
    }
  }

  // ─── Render ─────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      <PageHeader
        title="Bulk Generate Invoices"
        subtitle="Generate monthly rental invoices for all active contracts."
        action={<SecondaryButton onClick={() => router.push('/invoices')} className="px-3 py-1.5 text-xs">Back to Invoices</SecondaryButton>}
      />

      {/* ── Step indicators ─────────────────────────────────────────────────── */}
      <div className="flex items-center gap-2 text-sm">
        {[
          { n: 1, label: 'Select Month' },
          { n: 2, label: 'Pre-checks' },
          { n: 3, label: 'Options' },
          { n: 4, label: 'Results' },
        ].map(({ n, label }) => (
          <div key={n} className="flex items-center gap-2">
            {n > 1 && <div className={`h-px w-6 ${step >= n ? 'bg-brand-400' : 'bg-slate-200'}`} />}
            <div className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold ${
              step >= n ? 'bg-brand-600 text-white' : 'bg-slate-200 text-slate-500'
            }`}>
              {n}
            </div>
            <span className={step >= n ? 'font-medium text-slate-800' : 'text-slate-400'}>{label}</span>
          </div>
        ))}
      </div>

      {/* ── Step 1: Select billing month ────────────────────────────────────── */}
      {step >= 1 && (
        <Card className="max-w-lg p-6">
          <h3 className="mb-4 font-semibold text-slate-800">Step 1: Select Billing Month</h3>
          <div className="space-y-4">
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Billing Month</label>
              <input
                type="month"
                value={month}
                onChange={(e) => { setMonth(e.target.value); setStep(1); setChecks(null); setResult(null) }}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
              />
            </div>
            <div className="rounded-lg bg-slate-50 p-3 text-sm text-slate-600">
              <p>Billing period: <span className="font-semibold">{start}</span> to <span className="font-semibold">{end}</span></p>
            </div>
            {checkError && <p className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{checkError}</p>}
            {step === 1 && (
              <PrimaryButton onClick={runChecks} disabled={checking} className="w-full py-2.5 text-sm">
                {checking ? 'Running pre-checks...' : 'Run Pre-Generation Checks'}
              </PrimaryButton>
            )}
            {checking && <Spinner label="Analyzing active contracts..." />}
          </div>
        </Card>
      )}

      {/* ── Step 2: Pre-generation checks ───────────────────────────────────── */}
      {step >= 2 && checks && (
        <Card className="max-w-lg p-6">
          <h3 className="mb-4 font-semibold text-slate-800">Step 2: Pre-Generation Checks</h3>
          <div className="space-y-3">
            <div className="flex items-center justify-between rounded-lg bg-slate-50 px-4 py-3">
              <span className="text-sm text-slate-700">Active contracts</span>
              <span className="text-sm font-bold text-slate-900">{checks.activeContracts}</span>
            </div>

            {checks.missingEmail > 0 && (
              <div className="flex items-center justify-between rounded-lg bg-amber-50 px-4 py-3">
                <span className="text-sm text-amber-800">Contracts with missing customer email</span>
                <span className="text-sm font-bold text-amber-900">{checks.missingEmail}</span>
              </div>
            )}

            {checks.pendingDamages > 0 && (
              <div className="flex items-center justify-between rounded-lg bg-amber-50 px-4 py-3">
                <span className="text-sm text-amber-800">Pending damages not yet invoiced (est.)</span>
                <span className="text-sm font-bold text-amber-900">{checks.pendingDamages}</span>
              </div>
            )}

            {checks.pendingViolations > 0 && (
              <div className="flex items-center justify-between rounded-lg bg-amber-50 px-4 py-3">
                <span className="text-sm text-amber-800">Pending violations not yet invoiced (est.)</span>
                <span className="text-sm font-bold text-amber-900">{checks.pendingViolations}</span>
              </div>
            )}

            {checks.alreadyGenerated > 0 && (
              <div className="flex items-center justify-between rounded-lg bg-blue-50 px-4 py-3">
                <span className="text-sm text-blue-800">Already-generated invoices for this period (will be skipped)</span>
                <span className="text-sm font-bold text-blue-900">{checks.alreadyGenerated}</span>
              </div>
            )}

            {checks.pendingDamages === 0 && checks.pendingViolations === 0 && checks.alreadyGenerated === 0 && (
              <div className="rounded-lg bg-green-50 px-4 py-3 text-sm text-green-800">
                All checks passed. Ready to configure generation options.
              </div>
            )}

            {step === 2 && (
              <PrimaryButton onClick={() => setStep(3)} className="w-full py-2.5 text-sm">
                Continue to Options
              </PrimaryButton>
            )}
          </div>
        </Card>
      )}

      {/* ── Step 3: Generation options ──────────────────────────────────────── */}
      {step >= 3 && step < 4 && (
        <Card className="max-w-lg p-6">
          <h3 className="mb-4 font-semibold text-slate-800">Step 3: Generation Options</h3>
          <div className="space-y-4">
            <div>
              <label className="mb-2 block text-sm font-medium text-slate-700">Generate for</label>
              <div className="space-y-2">
                {[
                  { value: 'all' as GenerateScope, label: 'All active contracts' },
                  { value: 'selected-customers' as GenerateScope, label: 'Selected customers only' },
                  { value: 'selected-contracts' as GenerateScope, label: 'Selected contracts only' },
                ].map(({ value, label }) => (
                  <label key={value} className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="radio"
                      name="scope"
                      value={value}
                      checked={scope === value}
                      onChange={() => setScope(value)}
                      className="h-4 w-4 text-brand-600"
                    />
                    <span className="text-sm text-slate-700">{label}</span>
                  </label>
                ))}
              </div>
              {scope !== 'all' && (
                <p className="mt-2 text-xs text-slate-500">
                  Note: Customer/contract selection is not available in mock mode. All active contracts will be included.
                </p>
              )}
            </div>

            <div className="space-y-2 border-t border-slate-200 pt-4">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={includeDamages}
                  onChange={(e) => setIncludeDamages(e.target.checked)}
                  className="h-4 w-4 rounded text-brand-600"
                />
                <span className="text-sm text-slate-700">Include damage charges</span>
              </label>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={includeViolations}
                  onChange={(e) => setIncludeViolations(e.target.checked)}
                  className="h-4 w-4 rounded text-brand-600"
                />
                <span className="text-sm text-slate-700">Include violation charges</span>
              </label>
            </div>

            {genError && <p className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{genError}</p>}

            <PrimaryButton onClick={handleGenerate} disabled={generating} className="w-full py-2.5 text-sm">
              {generating ? 'Generating invoices...' : `Generate Invoices for ${month}`}
            </PrimaryButton>
            {generating && <Spinner label="Generating invoices for active contracts..." />}
          </div>
        </Card>
      )}

      {/* ── Step 4: Results ──────────────────────────────────────────────────── */}
      {step === 4 && result && (
        <div className="space-y-4">
          <Card className="p-6">
            <h3 className="mb-4 font-semibold text-slate-800">Step 4: Generation Results</h3>
            <div className="grid grid-cols-3 gap-4">
              <div className="rounded-lg bg-green-50 p-4 text-center">
                <p className="text-3xl font-bold text-green-700">{result.generated}</p>
                <p className="mt-1 text-sm text-slate-600">Generated</p>
              </div>
              <div className="rounded-lg bg-slate-50 p-4 text-center">
                <p className="text-3xl font-bold text-slate-500">{result.skipped}</p>
                <p className="mt-1 text-sm text-slate-600">Skipped (already exists)</p>
              </div>
              <div className="rounded-lg bg-red-50 p-4 text-center">
                <p className="text-3xl font-bold text-red-600">{result.errors.length}</p>
                <p className="mt-1 text-sm text-slate-600">Errors</p>
              </div>
            </div>
          </Card>

          {result.errors.length > 0 && (
            <Card className="p-4">
              <h4 className="mb-3 font-semibold text-slate-800">Errors</h4>
              <div className="space-y-2">
                {result.errors.map((e, i) => (
                  <div key={i} className="flex items-start gap-2 rounded-lg bg-red-50 px-3 py-2 text-sm">
                    <span className="font-mono font-semibold text-red-700">{e.leaseNumber}</span>
                    <span className="text-red-600">{e.error}</span>
                  </div>
                ))}
              </div>
            </Card>
          )}

          {result.generated > 0 && (
            <Card className="p-4">
              <h4 className="mb-3 font-semibold text-slate-800">Generated Invoices</h4>
              <p className="mb-3 text-sm text-slate-600">
                {result.generated} invoice{result.generated !== 1 ? 's' : ''} generated for billing period {start} to {end}.
              </p>
              <div className="flex gap-3">
                <PrimaryButton onClick={() => router.push('/invoices?status=Issued')}>
                  View Generated Invoices
                </PrimaryButton>
                <SecondaryButton onClick={() => { setStep(1); setResult(null); setChecks(null) }}>
                  Generate Another Month
                </SecondaryButton>
              </div>
            </Card>
          )}

          {result.generated === 0 && result.errors.length === 0 && (
            <Card className="p-4">
              <p className="text-sm text-slate-600">
                No new invoices were generated. All active contracts already have invoices for this billing period.
              </p>
              <div className="mt-3">
                <SecondaryButton onClick={() => { setStep(1); setResult(null); setChecks(null) }}>
                  Try Another Month
                </SecondaryButton>
              </div>
            </Card>
          )}
        </div>
      )}
    </div>
  )
}
