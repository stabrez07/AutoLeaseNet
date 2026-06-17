'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import { useRouter } from 'next/navigation'
import { bff, type Invoice, type InvoiceStatus, type PagedResult } from '../../lib/bff-client'
import {
  Badge, Card, DataTable, DataTableMeta, ErrorBox, PageHeader, PrimaryButton,
  SearchInput, SecondaryButton, Spinner, TableCell, TableHeadCell,
  Toolbar, ToolbarGroup,
} from '../../components/ui'

// ─── Constants ───────────────────────────────────────────────────────────────

const PAGE_SIZE = 30

const STATUSES: InvoiceStatus[] = ['Draft', 'Issued', 'PartiallyPaid', 'Paid', 'Overdue', 'Cancelled']

const STATUS_TONES: Record<InvoiceStatus, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Paid: 'green',
  PartiallyPaid: 'amber',
  Issued: 'blue',
  Draft: 'slate',
  Overdue: 'red',
  Cancelled: 'slate',
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function fmt(n: number) {
  return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function matchesSearch(inv: Invoice, term: string): boolean {
  const hay = `${inv.invoiceNumber} ${inv.customerDisplayName} ${inv.vehiclePlate} ${inv.vehicleMakeModel}`.toLowerCase()
  return hay.includes(term.toLowerCase())
}

function matchesCustomer(inv: Invoice, term: string): boolean {
  return inv.customerDisplayName.toLowerCase().includes(term.toLowerCase())
}

function matchesVehicle(inv: Invoice, term: string): boolean {
  const hay = `${inv.vehiclePlate} ${inv.vehicleMakeModel}`.toLowerCase()
  return hay.includes(term.toLowerCase())
}

function inDateRange(inv: Invoice, from: string, to: string): boolean {
  if (from && inv.issuedDate < from) return false
  if (to && inv.issuedDate > to) return false
  return true
}

// ─── Page component ──────────────────────────────────────────────────────────

export default function InvoicesPage() {
  const router = useRouter()

  // Data state
  const [data, setData] = useState<PagedResult<Invoice> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [page, setPage] = useState(1)

  // Filter state
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<InvoiceStatus | ''>('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const [customerFilter, setCustomerFilter] = useState('')
  const [vehicleFilter, setVehicleFilter] = useState('')

  // Detail panel state
  const [selectedInvoice, setSelectedInvoice] = useState<Invoice | null>(null)

  // ─── Data loading ────────────────────────────────────────────────────────

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await bff.getInvoices(page, PAGE_SIZE, undefined, undefined, statusFilter || undefined)
      setData(res)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [page, statusFilter])

  useEffect(() => {
    const h = setTimeout(load, 200)
    return () => clearTimeout(h)
  }, [load])

  // ─── Client-side filtering ──────────────────────────────────────────────

  const filteredItems = useMemo(() => {
    if (!data) return []
    return data.items.filter((inv) => {
      if (search && !matchesSearch(inv, search)) return false
      if (customerFilter && !matchesCustomer(inv, customerFilter)) return false
      if (vehicleFilter && !matchesVehicle(inv, vehicleFilter)) return false
      if ((dateFrom || dateTo) && !inDateRange(inv, dateFrom, dateTo)) return false
      return true
    })
  }, [data, search, customerFilter, vehicleFilter, dateFrom, dateTo])

  // ─── Summary totals ────────────────────────────────────────────────────

  const totals = useMemo(() => {
    const count = filteredItems.length
    const totalAmount = filteredItems.reduce((s, i) => s + i.totalSar, 0)
    const totalPaid = filteredItems.reduce((s, i) => s + i.paidAmountSar, 0)
    const outstanding = filteredItems.reduce((s, i) => s + i.balanceSar, 0)
    return { count, totalAmount, totalPaid, outstanding }
  }, [filteredItems])

  const totalPages = data?.totalPages ?? 1

  // ─── CSV export ─────────────────────────────────────────────────────────

  function downloadCsv() {
    if (filteredItems.length === 0) return
    const headers = [
      'Invoice #', 'Customer', 'Vehicle', 'Plate', 'Period Start', 'Period End',
      'Issued', 'Due', 'Status', 'Sub-Total', 'VAT', 'Total', 'Paid', 'Balance',
    ]
    const rows = filteredItems.map((inv) => [
      inv.invoiceNumber,
      inv.customerDisplayName,
      inv.vehicleMakeModel,
      inv.vehiclePlate,
      inv.billingPeriodStart,
      inv.billingPeriodEnd,
      inv.issuedDate,
      inv.dueDate,
      inv.status,
      String(inv.subTotalSar),
      String(inv.vatAmountSar),
      String(inv.totalSar),
      String(inv.paidAmountSar),
      String(inv.balanceSar),
    ])

    const csvContent = [headers, ...rows]
      .map((r) => r.map((c) => `"${c.replace(/"/g, '""')}"`).join(','))
      .join('\n')

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = `invoices-${new Date().toISOString().substring(0, 10)}.csv`
    a.click()
  }

  // ─── Row click handler ──────────────────────────────────────────────────

  function handleRowClick(inv: Invoice) {
    setSelectedInvoice((prev) => (prev?.id === inv.id ? null : inv))
  }

  // ─── Render ─────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      <PageHeader
        title="Invoices"
        subtitle="Monthly rental invoices -- generate, track and collect."
        action={
          <div className="flex gap-2">
            <SecondaryButton onClick={downloadCsv}>Export CSV</SecondaryButton>
            <SecondaryButton onClick={() => router.push('/invoices/generate')}>Generate Invoices</SecondaryButton>
            <PrimaryButton onClick={() => router.push('/leases')}>View Contracts</PrimaryButton>
          </div>
        }
      />

      {/* ── Filters ─────────────────────────────────────────────────────────── */}
      <Toolbar>
        <ToolbarGroup>
          <SearchInput
            value={search}
            onChange={(v) => { setPage(1); setSearch(v) }}
            placeholder="Invoice #, customer, plate..."
          />
          <select
            value={statusFilter}
            onChange={(e) => { setPage(1); setStatusFilter(e.target.value as InvoiceStatus | '') }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
          >
            <option value="">All statuses</option>
            {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <input
            type="date"
            value={dateFrom}
            onChange={(e) => { setPage(1); setDateFrom(e.target.value) }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
            title="From date"
          />
          <input
            type="date"
            value={dateTo}
            onChange={(e) => { setPage(1); setDateTo(e.target.value) }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
            title="To date"
          />
        </ToolbarGroup>
        <ToolbarGroup>
          <input
            type="text"
            value={customerFilter}
            onChange={(e) => { setPage(1); setCustomerFilter(e.target.value) }}
            placeholder="Customer name..."
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
          />
          <input
            type="text"
            value={vehicleFilter}
            onChange={(e) => { setPage(1); setVehicleFilter(e.target.value) }}
            placeholder="Vehicle / plate..."
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
          />
        </ToolbarGroup>
      </Toolbar>

      {/* ── Summary bar ─────────────────────────────────────────────────────── */}
      {data && (
        <Card className="px-4 py-3">
          <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm">
            <div className="text-slate-600">
              <span className="font-medium text-slate-900">Total Invoices:</span>{' '}
              {totals.count}
            </div>
            <div className="text-slate-600">
              <span className="font-medium text-slate-900">Total Amount:</span>{' '}
              {fmt(totals.totalAmount)}
            </div>
            <div className="text-slate-600">
              <span className="font-medium text-slate-900">Total Paid:</span>{' '}
              {fmt(totals.totalPaid)}
            </div>
            <div className={totals.outstanding > 0 ? 'font-semibold text-red-700' : 'text-slate-600'}>
              <span className={totals.outstanding > 0 ? 'font-bold text-red-800' : 'font-medium text-slate-900'}>Outstanding:</span>{' '}
              {fmt(totals.outstanding)}
            </div>
          </div>
        </Card>
      )}

      {/* ── Error / loading ──────────────────────────────────────────────────── */}
      {error && <ErrorBox message={error} onRetry={load} retryLabel="Retry" />}
      {loading && <Spinner label="Loading invoices..." />}

      {/* ── Data table ───────────────────────────────────────────────────────── */}
      {!loading && data && (
        <>
          <DataTable>
            <DataTableMeta>
              Page {page} of {totalPages} ({data.totalCount} total records)
            </DataTableMeta>
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50/80">
                <tr>
                  <TableHeadCell>Invoice #</TableHeadCell>
                  <TableHeadCell>Customer</TableHeadCell>
                  <TableHeadCell>Vehicle</TableHeadCell>
                  <TableHeadCell>Plate</TableHeadCell>
                  <TableHeadCell>Period</TableHeadCell>
                  <TableHeadCell>Issued</TableHeadCell>
                  <TableHeadCell>Due</TableHeadCell>
                  <TableHeadCell align="end">Total (SAR)</TableHeadCell>
                  <TableHeadCell align="end">Paid (SAR)</TableHeadCell>
                  <TableHeadCell align="end">Balance (SAR)</TableHeadCell>
                  <TableHeadCell>Status</TableHeadCell>
                </tr>
              </thead>
              <tbody>
                {filteredItems.length === 0 && (
                  <tr>
                    <td colSpan={11} className="px-3 py-10 text-center text-slate-400">
                      No invoices found.
                    </td>
                  </tr>
                )}
                {filteredItems.map((inv) => (
                  <tr
                    key={inv.id}
                    className={`cursor-pointer border-t border-slate-100 transition hover:bg-brand-50/50 ${
                      selectedInvoice?.id === inv.id ? 'bg-brand-50' : ''
                    }`}
                    onClick={() => handleRowClick(inv)}
                  >
                    <TableCell className="font-mono text-xs font-bold">{inv.invoiceNumber}</TableCell>
                    <TableCell className="text-slate-800">{inv.customerDisplayName}</TableCell>
                    <TableCell className="text-xs text-slate-600">{inv.vehicleMakeModel}</TableCell>
                    <TableCell className="font-mono text-xs">{inv.vehiclePlate}</TableCell>
                    <TableCell className="text-xs text-slate-600">{inv.billingPeriodStart} - {inv.billingPeriodEnd}</TableCell>
                    <TableCell className="text-xs text-slate-600">{inv.issuedDate}</TableCell>
                    <TableCell className="text-xs text-slate-600">{inv.dueDate}</TableCell>
                    <TableCell align="end" className="font-mono text-xs">{fmt(inv.totalSar)}</TableCell>
                    <TableCell align="end" className="font-mono text-xs text-green-700">
                      {inv.paidAmountSar > 0 ? fmt(inv.paidAmountSar) : '--'}
                    </TableCell>
                    <TableCell
                      align="end"
                      className={`font-mono text-xs font-semibold ${inv.balanceSar > 0 ? 'text-red-700' : 'text-slate-400'}`}
                    >
                      {inv.balanceSar > 0 ? fmt(inv.balanceSar) : '--'}
                    </TableCell>
                    <TableCell>
                      <Badge tone={STATUS_TONES[inv.status]}>{inv.status}</Badge>
                    </TableCell>
                  </tr>
                ))}
              </tbody>
            </table>
          </DataTable>

          {/* ── Pagination ──────────────────────────────────────────────────── */}
          <div className="flex items-center justify-between text-xs text-slate-600">
            <span>
              Showing {filteredItems.length} of {data.totalCount} invoices
            </span>
            <div className="flex gap-2">
              <SecondaryButton
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="px-2 py-1 text-xs"
              >
                Previous
              </SecondaryButton>
              <SecondaryButton
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="px-2 py-1 text-xs"
              >
                Next
              </SecondaryButton>
            </div>
          </div>
        </>
      )}

      {/* ── Detail panel ─────────────────────────────────────────────────────── */}
      {selectedInvoice && (
        <Card className="p-5">
          <div className="mb-4 flex items-start justify-between">
            <div>
              <h3 className="text-lg font-semibold text-slate-900">
                {selectedInvoice.invoiceNumber}
              </h3>
              <div className="mt-1">
                <Badge tone={STATUS_TONES[selectedInvoice.status]}>{selectedInvoice.status}</Badge>
              </div>
            </div>
            <SecondaryButton onClick={() => setSelectedInvoice(null)} className="px-2 py-1 text-xs">
              Close
            </SecondaryButton>
          </div>

          <div className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Customer</div>
              <div className="mt-0.5 text-slate-800">{selectedInvoice.customerDisplayName}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Vehicle</div>
              <div className="mt-0.5 text-slate-800">{selectedInvoice.vehicleMakeModel}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Plate (EN / AR)</div>
              <div className="mt-0.5 text-slate-800">
                {selectedInvoice.vehiclePlate} / {selectedInvoice.vehiclePlateAr}
              </div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Billing Period</div>
              <div className="mt-0.5 text-slate-800">
                {selectedInvoice.billingPeriodStart} - {selectedInvoice.billingPeriodEnd}
              </div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Issued</div>
              <div className="mt-0.5 text-slate-800">{selectedInvoice.issuedDate}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Due</div>
              <div className="mt-0.5 text-slate-800">{selectedInvoice.dueDate}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Sub-Total</div>
              <div className="mt-0.5 font-mono text-slate-800">{fmt(selectedInvoice.subTotalSar)}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">VAT</div>
              <div className="mt-0.5 font-mono text-slate-800">{fmt(selectedInvoice.vatAmountSar)}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Total</div>
              <div className="mt-0.5 font-mono font-semibold text-slate-900">{fmt(selectedInvoice.totalSar)}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Paid</div>
              <div className="mt-0.5 font-mono text-green-700">{fmt(selectedInvoice.paidAmountSar)}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Balance</div>
              <div className={`mt-0.5 font-mono font-semibold ${selectedInvoice.balanceSar > 0 ? 'text-red-700' : 'text-slate-400'}`}>
                {fmt(selectedInvoice.balanceSar)}
              </div>
            </div>
          </div>

          <div className="mt-5 flex gap-2 border-t border-slate-200 pt-4">
            <PrimaryButton onClick={() => router.push(`/invoices/${selectedInvoice.id}`)}>
              Open Full Invoice
            </PrimaryButton>
            <SecondaryButton
              onClick={() => {
                const w = window.open(`/invoices/${selectedInvoice.id}`, '_blank')
                if (w) {
                  w.addEventListener('afterprint', () => w.close())
                  w.addEventListener('load', () => setTimeout(() => w.print(), 500))
                }
              }}
            >
              Print
            </SecondaryButton>
          </div>
        </Card>
      )}
    </div>
  )
}
