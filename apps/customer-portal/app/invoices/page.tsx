'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type InvoiceStatus, type MyInvoice } from '../../lib/bff-client'
import {
  Badge,
  Card,
  ErrorBox,
  PageHeader,
  SearchInput,
  SecondaryButton,
  Spinner,
} from '../../components/ui'

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatSar(n: number): string {
  return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function invoiceTone(status: InvoiceStatus): 'green' | 'amber' | 'red' | 'slate' | 'blue' {
  switch (status) {
    case 'Paid':
      return 'green'
    case 'Issued':
      return 'blue'
    case 'PartiallyPaid':
      return 'amber'
    case 'Overdue':
      return 'red'
    case 'Draft':
    case 'Cancelled':
    default:
      return 'slate'
  }
}

function downloadCsv(invoices: MyInvoice[]) {
  const headers = [
    'Invoice #',
    'Lease #',
    'Vehicle',
    'Plate',
    'Billing Period Start',
    'Billing Period End',
    'Issued Date',
    'Due Date',
    'Status',
    'Sub-Total (SAR)',
    'VAT (SAR)',
    'Total (SAR)',
    'Paid (SAR)',
    'Balance (SAR)',
    'ZATCA Invoice #',
  ]
  const rows = invoices.map((inv) => [
    inv.invoiceNumber,
    inv.leaseNumber,
    inv.vehicleMakeModel,
    inv.vehiclePlate,
    inv.billingPeriodStart,
    inv.billingPeriodEnd,
    inv.issuedDate,
    inv.dueDate,
    inv.status,
    inv.subTotalSar.toFixed(2),
    inv.vatAmountSar.toFixed(2),
    inv.totalSar.toFixed(2),
    inv.paidAmountSar.toFixed(2),
    inv.balanceSar.toFixed(2),
    inv.zatcaInvoiceNumber ?? '',
  ])
  const csv = [headers, ...rows]
    .map((r) => r.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(','))
    .join('\n')
  const blob = new Blob([csv], { type: 'text/csv' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'my-invoices.csv'
  a.click()
  URL.revokeObjectURL(url)
}

const ALL_STATUSES: InvoiceStatus[] = ['Draft', 'Issued', 'PartiallyPaid', 'Paid', 'Overdue', 'Cancelled']

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function MyInvoicesPage() {
  const { t } = useLocale()
  const router = useRouter()
  const [invoices, setInvoices] = useState<MyInvoice[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<InvoiceStatus | ''>('')

  const load = useCallback(async () => {
    setError(null)
    try {
      setInvoices(await bff.getMyInvoices())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : t.common.error)
    }
  }, [t.common.error])

  useEffect(() => {
    void load()
  }, [load])

  const filtered = useMemo(() => {
    if (!invoices) return []
    const q = search.trim().toLowerCase()
    return invoices.filter((inv) => {
      const matchesSearch =
        !q ||
        inv.invoiceNumber.toLowerCase().includes(q) ||
        inv.vehicleMakeModel.toLowerCase().includes(q)
      const matchesStatus = !statusFilter || inv.status === statusFilter
      return matchesSearch && matchesStatus
    })
  }, [invoices, search, statusFilter])

  const cols = t.invoices.columns
  const statuses = t.invoices.statuses

  return (
    <div>
      <PageHeader
        title={t.invoices.title}
        subtitle={t.invoices.subtitle}
        action={
          invoices && invoices.length > 0 ? (
            <SecondaryButton onClick={() => downloadCsv(filtered)}>
              {t.invoices.download}
            </SecondaryButton>
          ) : undefined
        }
      />

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {!error && !invoices && <Spinner label={t.common.loading} />}

      {invoices && (
        <>
          {/* Filters */}
          <div className="mb-4 flex flex-wrap items-center gap-3">
            <SearchInput
              value={search}
              onChange={setSearch}
              placeholder={`${cols.invoiceNumber} / ${cols.vehicle}`}
            />
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as InvoiceStatus | '')}
              className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-1"
            >
              <option value="">{cols.status} — All</option>
              {ALL_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {statuses[s]}
                </option>
              ))}
            </select>
          </div>

          {filtered.length === 0 && (
            <Card className="p-8 text-center text-sm text-slate-500">{t.invoices.empty}</Card>
          )}

          {filtered.length > 0 && (
            <Card className="overflow-hidden">
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                    <tr>
                      <th className="px-4 py-2.5 font-medium">{cols.invoiceNumber}</th>
                      <th className="px-4 py-2.5 font-medium">{cols.vehicle}</th>
                      <th className="px-4 py-2.5 font-medium">{cols.period}</th>
                      <th className="px-4 py-2.5 font-medium">{cols.issuedDate}</th>
                      <th className="px-4 py-2.5 font-medium">{cols.dueDate}</th>
                      <th className="px-4 py-2.5 text-end font-medium">{cols.total}</th>
                      <th className="px-4 py-2.5 text-end font-medium">{cols.paid}</th>
                      <th className="px-4 py-2.5 text-end font-medium">{cols.balance}</th>
                      <th className="px-4 py-2.5 font-medium">{cols.status}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {filtered.map((inv) => (
                      <tr
                        key={inv.id}
                        className="cursor-pointer hover:bg-slate-50"
                        onClick={() => router.push(`/invoices/${inv.id}`)}
                      >
                        <td className="px-4 py-2.5 font-mono text-xs text-brand-700">
                          {inv.invoiceNumber}
                        </td>
                        <td className="px-4 py-2.5">
                          <div className="font-medium text-slate-800">{inv.vehicleMakeModel}</div>
                          <div className="text-xs text-slate-500" dir="rtl">
                            {inv.vehiclePlateAr}
                          </div>
                        </td>
                        <td className="whitespace-nowrap px-4 py-2.5 text-slate-700">
                          {inv.billingPeriodStart} — {inv.billingPeriodEnd}
                        </td>
                        <td className="px-4 py-2.5 text-slate-700">{inv.issuedDate}</td>
                        <td className="px-4 py-2.5 text-slate-700">{inv.dueDate}</td>
                        <td className="px-4 py-2.5 text-end text-slate-800">
                          {formatSar(inv.totalSar)}
                        </td>
                        <td className="px-4 py-2.5 text-end text-slate-700">
                          {formatSar(inv.paidAmountSar)}
                        </td>
                        <td className="px-4 py-2.5 text-end font-medium text-slate-900">
                          {formatSar(inv.balanceSar)}
                        </td>
                        <td className="px-4 py-2.5">
                          <Badge tone={invoiceTone(inv.status)}>{statuses[inv.status]}</Badge>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>
          )}
        </>
      )}
    </div>
  )
}
