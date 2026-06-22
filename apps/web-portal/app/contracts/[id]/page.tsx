'use client'

import Link from 'next/link'
import { useParams, useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import { bff, type ContractDetail } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../../components/ui'

const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Draft: 'slate', Active: 'green', Suspended: 'amber', Closed: 'slate', Cancelled: 'red',
}
const LA_STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Draft: 'slate', PendingIssuance: 'amber', Active: 'green', Extended: 'blue',
  Suspended: 'amber', Closed: 'slate', Cancelled: 'red',
}
const CONTRACT_TYPES: Record<string, string> = {
  '1': 'Long Term Lease', '2': 'Short Term Rental', '3': 'Daily Rental',
  LongTermLease: 'Long Term Lease', ShortTermRental: 'Short Term Rental', Daily: 'Daily Rental',
  OperatingLease: 'Operating Lease', FinanceLease: 'Finance Lease',
}

function safeDate(s: string | null | undefined) {
  if (!s) return '—'
  return new Date(s).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}
function fmtMoney(n: number | string | null | undefined) {
  if (n == null || n === '') return '—'
  const num = typeof n === 'string' ? parseFloat(n) : n
  if (isNaN(num)) return '—'
  return num.toLocaleString('en-SA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

type Tab = 'overview' | 'quotation' | 'leases'

export default function ContractDetailPage() {
  const params = useParams()
  const id = params?.id as string
  const router = useRouter()
  const [contract, setContract] = useState<ContractDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('overview')

  async function reload() {
    setLoading(true); setError(null)
    try { setContract(await bff.getContractById(id)) }
    catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }
  useEffect(() => { reload() }, [id]) // eslint-disable-line react-hooks/exhaustive-deps

  if (loading) return <Spinner label="Loading contract..." />
  if (error) return <ErrorBox message={error} onRetry={reload} retryLabel="Retry" />
  if (!contract) return null

  const status = contract.status
  const lineTotalSum = contract.lines.reduce((s, l) => s + l.lineTotalSar, 0)
  const hasQuote = !!contract.quoteNumber
  const tabs: { key: Tab; label: string }[] = [
    { key: 'overview', label: 'Contract Details' },
    ...(hasQuote ? [{ key: 'quotation' as Tab, label: 'Quotation & T&C' }] : []),
    { key: 'leases', label: `Lease Agreements (${contract.leaseAgreements.length})` },
  ]

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <PageHeader
        title={`Contract ${contract.contractNumber}`}
        subtitle={contract.customerDisplayName}
        action={<Badge tone={STATUS_TONES[status] ?? 'slate'}>{status}</Badge>}
      />

      {/* Key metrics bar */}
      <div className="grid grid-cols-2 gap-3 md:grid-cols-5">
        {[
          { label: 'Total Vehicles', value: String(contract.totalVehicles), accent: false },
          { label: 'Monthly Rent', value: `SAR ${fmtMoney(contract.monthlyRentSar)}`, accent: false },
          { label: 'Contract Value', value: `SAR ${fmtMoney(contract.totalContractValueSar)}`, accent: true },
          { label: 'Duration', value: `${contract.durationMonths} months`, accent: false },
          { label: 'Lease Agreements', value: String(contract.leaseAgreements.length), accent: false },
        ].map((m) => (
          <Card key={m.label} className={`p-3 text-center ${m.accent ? 'border-brand-200 bg-brand-50' : ''}`}>
            <div className="text-[10px] font-semibold uppercase tracking-wide text-slate-500">{m.label}</div>
            <div className={`mt-1 text-sm font-bold ${m.accent ? 'text-brand-700' : 'text-slate-900'}`}>{m.value}</div>
          </Card>
        ))}
      </div>

      {/* Tab navigation */}
      <div className="flex gap-1 border-b border-slate-200">
        {tabs.map(({ key, label }) => (
          <button key={key} type="button" onClick={() => setTab(key)}
            className={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px ${tab === key ? 'border-brand-600 text-brand-700' : 'border-transparent text-slate-500 hover:text-slate-700'}`}>
            {label}
          </button>
        ))}
      </div>

      {/* ─── Overview Tab ─── */}
      {tab === 'overview' && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <div className="space-y-4 lg:col-span-2">
            {/* Contract summary */}
            <Card className="p-5">
              <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Contract Information</h3>
              <dl className="grid grid-cols-2 gap-3 text-xs sm:grid-cols-3">
                {([
                  ['Contract #', contract.contractNumber],
                  ['Customer', contract.customerDisplayName],
                  ['Status', status],
                  ['Type', CONTRACT_TYPES[contract.contractType ?? contract.contractTypeCode] ?? contract.contractTypeCode],
                  ['Start Date', safeDate(contract.startDate)],
                  ['End Date', safeDate(contract.endDate)],
                  ['Duration', `${contract.durationMonths} months`],
                  ['Payment Terms', `Net ${contract.paymentTermsDays} days`],
                  ['Source Quote', contract.quoteNumber ?? '—'],
                ] as [string, string][]).map(([label, value]) => (
                  <div key={label} className="rounded-md bg-slate-50 p-2.5">
                    <dt className="text-slate-500">{label}</dt>
                    <dd className="mt-0.5 font-medium text-slate-900">
                      {label === 'Source Quote' && contract.quotationId && contract.quoteNumber ? (
                        <Link href={`/quotations/${contract.quotationId}`} className="text-brand-700 hover:underline">{value}</Link>
                      ) : value}
                    </dd>
                  </div>
                ))}
              </dl>
            </Card>

            {/* Vehicle Lines (pricing by make/model) */}
            <Card className="overflow-hidden">
              <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-4 py-3">
                <h3 className="text-sm font-semibold text-slate-800">Vehicle Pricing Breakdown</h3>
                <span className="rounded-full bg-slate-200 px-2 py-0.5 text-[10px] font-bold text-slate-600">{contract.totalVehicles} vehicles</span>
              </div>
              <table className="w-full text-xs">
                <thead className="bg-slate-100 text-slate-600">
                  <tr>
                    <th className="px-3 py-2 text-start font-medium">#</th>
                    <th className="px-3 py-2 text-start font-medium">Make</th>
                    <th className="px-3 py-2 text-start font-medium">Model</th>
                    <th className="px-3 py-2 text-end font-medium">Year</th>
                    <th className="px-3 py-2 text-end font-medium">Qty</th>
                    <th className="px-3 py-2 text-end font-medium">Unit Price / mo</th>
                    <th className="px-3 py-2 text-end font-medium">Line Total / mo</th>
                  </tr>
                </thead>
                <tbody>
                  {contract.lines.length === 0 && (
                    <tr><td colSpan={7} className="px-3 py-4 text-center text-slate-400">No vehicle lines.</td></tr>
                  )}
                  {contract.lines.map((line, idx) => (
                    <tr key={line.id} className="border-t border-slate-100">
                      <td className="px-3 py-2 text-slate-500">{idx + 1}</td>
                      <td className="px-3 py-2 font-medium">{line.make}</td>
                      <td className="px-3 py-2">{line.model}</td>
                      <td className="px-3 py-2 text-end">{line.year}</td>
                      <td className="px-3 py-2 text-end font-semibold">{line.quantity}</td>
                      <td className="px-3 py-2 text-end font-mono">{fmtMoney(line.unitPriceSar)}</td>
                      <td className="px-3 py-2 text-end font-mono font-bold">{fmtMoney(line.lineTotalSar)}</td>
                    </tr>
                  ))}
                </tbody>
                {contract.lines.length > 0 && (
                  <tfoot>
                    <tr className="border-t-2 border-slate-200 bg-slate-50">
                      <td colSpan={6} className="px-3 py-2 text-end text-xs font-semibold text-slate-700">Monthly Total</td>
                      <td className="px-3 py-2 text-end font-mono text-xs font-bold text-slate-900">SAR {fmtMoney(lineTotalSum)}</td>
                    </tr>
                  </tfoot>
                )}
              </table>
            </Card>
          </div>

          {/* Right column */}
          <div className="space-y-4">
            <Card className="p-4">
              <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Actions</h3>
              <div className="space-y-2">
                <button onClick={() => setTab('leases')} className="w-full rounded-md bg-brand-700 px-3 py-2 text-sm font-medium text-white hover:bg-brand-800">
                  View Lease Agreements
                </button>
                {hasQuote && (
                  <Link href={`/quotations/${contract.quotationId}`} className="block w-full rounded-md border border-brand-300 px-3 py-2 text-center text-sm font-medium text-brand-700 hover:bg-brand-50">
                    View Source Quotation
                  </Link>
                )}
              </div>
            </Card>

            {contract.notes && (
              <Card className="p-4">
                <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">Notes</h3>
                <p className="text-xs text-slate-600 whitespace-pre-wrap">{contract.notes}</p>
              </Card>
            )}

            <Card className="p-4">
              <button onClick={() => router.push('/contracts')} className="text-xs text-brand-700 hover:underline">
                ← Back to Contracts
              </button>
            </Card>
          </div>
        </div>
      )}

      {/* ─── Quotation & T&C Tab ─── */}
      {tab === 'quotation' && hasQuote && (
        <div className="space-y-4">
          {/* Quote summary */}
          <Card className="p-5">
            <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Linked Quotation — {contract.quoteNumber}</h3>
            <dl className="grid grid-cols-2 gap-3 text-xs sm:grid-cols-4">
              {([
                ['Quote #', contract.quoteNumber ?? '—'],
                ['Quote Date', safeDate(contract.quoteDate)],
                ['Valid Until', safeDate(contract.quoteValidUntil)],
                ['Status', contract.quoteStatus ?? '—'],
                ['Type', CONTRACT_TYPES[contract.contractType ?? ''] ?? contract.contractType ?? '—'],
                ['Duration', contract.estimatedDurationMonths ? `${contract.estimatedDurationMonths} months` : '—'],
                ['Discount', contract.quoteDiscountPercent ? `${contract.quoteDiscountPercent}%` : '0%'],
                ['Subtotal', contract.quoteSubTotalSar ? `SAR ${fmtMoney(contract.quoteSubTotalSar)}` : '—'],
                ['VAT (15%)', contract.quoteVatSar ? `SAR ${fmtMoney(contract.quoteVatSar)}` : '—'],
                ['Total', contract.quoteTotalSar ? `SAR ${fmtMoney(contract.quoteTotalSar)}` : '—'],
              ] as [string, string][]).map(([label, value]) => (
                <div key={label} className="rounded-md bg-slate-50 p-2.5">
                  <dt className="text-slate-500">{label}</dt>
                  <dd className="mt-0.5 font-medium text-slate-900">{value}</dd>
                </div>
              ))}
            </dl>
          </Card>

          {/* Extras / Quote line items (includes insurance, maintenance, GPS, etc.) */}
          {contract.quoteLines && contract.quoteLines.length > 0 && (
            <Card className="overflow-hidden">
              <div className="border-b border-slate-200 bg-slate-50 px-4 py-3">
                <h3 className="text-sm font-semibold text-slate-800">Quotation Line Items & Extras</h3>
                <p className="text-[10px] text-slate-500">Includes vehicle rental, insurance, maintenance, and other agreed services.</p>
              </div>
              <table className="w-full text-xs">
                <thead className="bg-slate-100 text-slate-600">
                  <tr>
                    <th className="px-3 py-2 text-start font-medium">#</th>
                    <th className="px-3 py-2 text-start font-medium">Type</th>
                    <th className="px-3 py-2 text-start font-medium">Description</th>
                    <th className="px-3 py-2 text-end font-medium">Qty</th>
                    <th className="px-3 py-2 text-end font-medium">Unit Price</th>
                    <th className="px-3 py-2 text-end font-medium">Disc %</th>
                    <th className="px-3 py-2 text-end font-medium">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {contract.quoteLines.map((ql) => (
                    <tr key={ql.lineNumber} className="border-t border-slate-100">
                      <td className="px-3 py-2 text-slate-500">{ql.lineNumber}</td>
                      <td className="px-3 py-2"><Badge tone={ql.itemType === 'VehicleRental' ? 'blue' : 'slate'}>{ql.itemType}</Badge></td>
                      <td className="px-3 py-2">
                        <div>{ql.description}</div>
                        {ql.vehicleSpecRef && <div className="text-[10px] text-slate-400">{ql.vehicleSpecRef}</div>}
                      </td>
                      <td className="px-3 py-2 text-end">{ql.quantity}</td>
                      <td className="px-3 py-2 text-end font-mono">{fmtMoney(ql.unitPriceSar)}</td>
                      <td className="px-3 py-2 text-end">{ql.discountPercent}%</td>
                      <td className="px-3 py-2 text-end font-mono font-bold">{fmtMoney(ql.lineTotalSar)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Card>
          )}

          {/* Terms & Conditions */}
          <Card className="p-5">
            <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">Terms & Conditions</h3>
            {contract.termsAndConditions ? (
              <div className="prose prose-sm max-w-none text-xs text-slate-700 whitespace-pre-wrap">
                {contract.termsAndConditions}
              </div>
            ) : (
              <div className="rounded-md border border-slate-200 bg-slate-50 px-4 py-6 text-center">
                <p className="text-sm text-slate-400">No terms & conditions attached to this quotation.</p>
                <p className="mt-1 text-[10px] text-slate-400">Terms can be added when creating or editing the quotation.</p>
              </div>
            )}
          </Card>
        </div>
      )}

      {/* ─── Lease Agreements Tab ─── */}
      {tab === 'leases' && (
        <Card className="overflow-hidden">
          <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-4 py-3">
            <div>
              <h3 className="text-sm font-semibold text-slate-800">Lease Agreements</h3>
              <p className="text-[10px] text-slate-500">Vehicle checkouts under this contract — each LA tracks one vehicle + driver assignment.</p>
            </div>
          </div>
          {contract.leaseAgreements.length === 0 ? (
            <div className="px-4 py-10 text-center">
              <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-slate-100">
                <svg className="h-6 w-6 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M8.25 18.75a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m3 0h6m-9 0H3.375a1.125 1.125 0 01-1.125-1.125V14.25m17.25 4.5a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m3 0h1.125c.621 0 1.129-.504 1.09-1.124a17.902 17.902 0 00-3.213-9.193 2.056 2.056 0 00-1.58-.86H14.25M16.5 18.75h-2.25m0-11.177v-.958c0-.568-.422-1.048-.987-1.106a48.554 48.554 0 00-10.026 0 1.106 1.106 0 00-.987 1.106v7.635m12-6.677v6.677m0 4.5v-4.5m0 0h-12" />
                </svg>
              </div>
              <p className="text-sm font-medium text-slate-600">No lease agreements yet</p>
              <p className="mt-1 text-xs text-slate-400">Create lease agreements by checking out vehicles to drivers under this contract.</p>
            </div>
          ) : (
            <table className="w-full text-xs">
              <thead className="bg-slate-100 text-slate-600">
                <tr>
                  <th className="px-3 py-2 text-start font-medium">LA #</th>
                  <th className="px-3 py-2 text-start font-medium">Vehicle</th>
                  <th className="px-3 py-2 text-start font-medium">Plate</th>
                  <th className="px-3 py-2 text-start font-medium">Driver</th>
                  <th className="px-3 py-2 text-start font-medium">Status</th>
                  <th className="px-3 py-2 text-start font-medium">Checkout</th>
                  <th className="px-3 py-2 text-start font-medium">Check-in</th>
                  <th className="px-3 py-2 text-end font-medium">Rent / mo</th>
                </tr>
              </thead>
              <tbody>
                {contract.leaseAgreements.map((la) => (
                  <tr key={la.id} className="border-t border-slate-100 cursor-pointer hover:bg-brand-50/40" onClick={() => router.push(`/leases/${la.id}`)}>
                    <td className="px-3 py-2 font-mono font-semibold text-brand-700">{la.leaseNumber}</td>
                    <td className="px-3 py-2">{la.vehicleMakeModel}</td>
                    <td className="px-3 py-2 font-mono text-slate-600">{la.vehiclePlate}</td>
                    <td className="px-3 py-2 text-slate-600">{la.primaryDriverName || '—'}</td>
                    <td className="px-3 py-2"><Badge tone={LA_STATUS_TONES[la.status] ?? 'slate'}>{la.status}</Badge></td>
                    <td className="px-3 py-2 text-slate-600">{safeDate(la.contractStartUtc)}</td>
                    <td className="px-3 py-2 text-slate-600">{safeDate(la.contractEndUtc)}</td>
                    <td className="px-3 py-2 text-end font-mono">SAR {fmtMoney(la.rentAmountSar)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
      )}
    </div>
  )
}
