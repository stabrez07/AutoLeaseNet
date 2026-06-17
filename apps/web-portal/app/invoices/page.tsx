'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { bff, type Invoice, type InvoiceStatus, type PagedResult } from '../../lib/bff-client'
import {
  Badge, DataTableMeta, ErrorBox, PageHeader, PrimaryButton, SearchInput,
  SecondaryButton, Spinner, Toolbar, ToolbarGroup,
} from '../../components/ui'

const STATUS_TONES: Record<InvoiceStatus, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Paid: 'green', PartiallyPaid: 'amber', Issued: 'blue', Draft: 'slate', Overdue: 'red', Cancelled: 'slate',
}

function fmt(n: number) {
  return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

export default function InvoicesPage() {
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<InvoiceStatus | ''>('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<Invoice> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const pageSize = 25

  async function load() {
    setLoading(true); setError(null)
    try {
      const res = await bff.getInvoices(page, pageSize, undefined, undefined, statusFilter || undefined)
      if (search) {
        res.items = res.items.filter((inv) =>
          `${inv.invoiceNumber} ${inv.customerDisplayName} ${inv.leaseNumber} ${inv.vehiclePlate}`.toLowerCase().includes(search.toLowerCase())
        )
      }
      setData(res)
    } catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => {
    const h = setTimeout(load, 200); return () => clearTimeout(h)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search, statusFilter])

  function downloadCsv() {
    if (!data) return
    const rows = [['Invoice #', 'Lease #', 'Customer', 'Vehicle', 'Period Start', 'Period End', 'Issued', 'Due', 'Status', 'Sub-Total', 'VAT', 'Total', 'Paid', 'Balance']]
    data.items.forEach((inv) => rows.push([
      inv.invoiceNumber, inv.leaseNumber, inv.customerDisplayName, inv.vehiclePlate,
      inv.billingPeriodStart, inv.billingPeriodEnd, inv.issuedDate, inv.dueDate, inv.status,
      String(inv.subTotalSar), String(inv.vatAmountSar), String(inv.totalSar), String(inv.paidAmountSar), String(inv.balanceSar),
    ]))
    const csv = rows.map((r) => r.join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `invoices-${new Date().toISOString().substring(0, 10)}.csv`; a.click()
  }

  const STATUSES: InvoiceStatus[] = ['Draft', 'Issued', 'PartiallyPaid', 'Paid', 'Overdue', 'Cancelled']
  const totalPages = data?.totalPages ?? 1

  const totalOutstanding = data?.items.filter((i) => i.balanceSar > 0).reduce((s, i) => s + i.balanceSar, 0) ?? 0

  return (
    <div className="space-y-4">
      <PageHeader
        title="Invoices"
        subtitle="Monthly rental invoices — generate, track and collect."
        action={
          <div className="flex gap-2">
            <SecondaryButton onClick={downloadCsv}>⬇ Export CSV</SecondaryButton>
            <SecondaryButton onClick={() => router.push('/invoices/generate')}>⚡ Bulk Generate</SecondaryButton>
            <PrimaryButton onClick={() => router.push('/leases')}>View Contracts</PrimaryButton>
          </div>
        }
      />

      <Toolbar>
        <ToolbarGroup>
          <SearchInput value={search} onChange={(v) => { setPage(1); setSearch(v) }} placeholder="Invoice #, customer, lease…" />
          <select
            value={statusFilter}
            onChange={(e) => { setPage(1); setStatusFilter(e.target.value as InvoiceStatus | '') }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
          >
            <option value="">— All statuses —</option>
            {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </ToolbarGroup>
        <div className="flex items-center gap-3">
          {totalOutstanding > 0 && <Badge tone="red">Outstanding: {fmt(totalOutstanding)}</Badge>}
          <span className="text-xs text-slate-500">Total: {data?.totalCount ?? 0}</span>
        </div>
      </Toolbar>

      {error && <ErrorBox message={error} onRetry={load} retryLabel="Retry" />}
      {loading && <Spinner label="Loading invoices…" />}

      {!loading && data && (
        <>
          <DataTableMeta>Page {page} of {totalPages}</DataTableMeta>
          <div className="overflow-hidden rounded-xl border border-slate-200/80 bg-white shadow-sm">
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50/80">
                <tr>
                  {['Invoice #', 'Customer', 'Vehicle', 'Billing Period', 'Issued', 'Due', 'Status', 'Total', 'Paid', 'Balance', ''].map((h) => (
                    <th key={h} className="px-3 py-2.5 text-left text-xs font-semibold text-slate-600">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.items.length === 0 && (
                  <tr><td colSpan={11} className="px-3 py-10 text-center text-slate-400">No invoices found.</td></tr>
                )}
                {data.items.map((inv) => (
                  <tr
                    key={inv.id}
                    className="cursor-pointer border-t border-slate-100 transition hover:bg-brand-50/50"
                    onClick={() => router.push(`/invoices/${inv.id}`)}
                  >
                    <td className="px-3 py-2 font-mono text-xs font-bold">{inv.invoiceNumber}</td>
                    <td className="px-3 py-2 text-slate-800">{inv.customerDisplayName}</td>
                    <td className="px-3 py-2 font-mono text-xs">{inv.vehiclePlate}</td>
                    <td className="px-3 py-2 text-slate-600 text-xs">{inv.billingPeriodStart} – {inv.billingPeriodEnd}</td>
                    <td className="px-3 py-2 text-slate-600 text-xs">{inv.issuedDate}</td>
                    <td className="px-3 py-2 text-slate-600 text-xs">{inv.dueDate}</td>
                    <td className="px-3 py-2"><Badge tone={STATUS_TONES[inv.status]}>{inv.status}</Badge></td>
                    <td className="px-3 py-2 font-mono text-xs">{fmt(inv.totalSar)}</td>
                    <td className="px-3 py-2 font-mono text-xs text-green-700">{inv.paidAmountSar > 0 ? fmt(inv.paidAmountSar) : '—'}</td>
                    <td className={`px-3 py-2 font-mono text-xs font-semibold ${inv.balanceSar > 0 ? 'text-red-700' : 'text-slate-400'}`}>
                      {inv.balanceSar > 0 ? fmt(inv.balanceSar) : '—'}
                    </td>
                    <td className="px-3 py-2">
                      <SecondaryButton onClick={(e) => { e.stopPropagation(); router.push(`/invoices/${inv.id}`) }} className="px-2 py-1 text-xs">View</SecondaryButton>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50/70 px-3 py-2 text-xs text-slate-600">
            <span>Total: {data.totalCount}</span>
            <div className="flex gap-2">
              <SecondaryButton onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="px-2 py-1 text-xs">Previous</SecondaryButton>
              <SecondaryButton onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="px-2 py-1 text-xs">Next</SecondaryButton>
            </div>
          </div>
        </>
      )}
    </div>
  )
}
