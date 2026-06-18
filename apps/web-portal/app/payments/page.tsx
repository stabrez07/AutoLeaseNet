'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  bff,
  type AdvancePayment,
  type PaymentMethod,
  type PagedResult,
} from '../../lib/bff-client'
import {
  Badge,
  Card,
  DataTable,
  DataTableMeta,
  ErrorBox,
  PageHeader,
  SecondaryButton,
  SearchInput,
  Spinner,
  TableCell,
  TableHeadCell,
  Toolbar,
  ToolbarGroup,
} from '../../components/ui'

// ─── Constants ───────────────────────────────────────────────────────────────

const PAGE_SIZE = 30

const PAYMENT_METHODS: PaymentMethod[] = ['Cash', 'CreditCard', 'BankTransfer', 'Cheque', 'OnlineTransfer']

const METHOD_TONES: Record<PaymentMethod, 'green' | 'blue' | 'amber' | 'slate'> = {
  Cash: 'green',
  CreditCard: 'blue',
  BankTransfer: 'amber',
  Cheque: 'slate',
  OnlineTransfer: 'blue',
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function fmt(n: number) {
  return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function matchesSearch(pmt: AdvancePayment, term: string): boolean {
  const hay = `${pmt.customerDisplayName} ${pmt.referenceNumber ?? ''} ${pmt.id}`.toLowerCase()
  return hay.includes(term.toLowerCase())
}

function inDateRange(pmt: AdvancePayment, from: string, to: string): boolean {
  if (from && pmt.receivedDate < from) return false
  if (to && pmt.receivedDate > to) return false
  return true
}

function downloadCsv(items: AdvancePayment[]) {
  if (items.length === 0) return
  const headers = [
    'Receipt #', 'Customer', 'Amount (SAR)', 'Method', 'Date Received',
    'Reference #', 'Allocations', 'Remaining Balance', 'Notes',
  ]
  const rows = items.map((p) => [
    p.id,
    p.customerDisplayName,
    String(p.amount),
    p.paymentMethod,
    p.receivedDate,
    p.referenceNumber ?? '',
    String(p.allocations.length),
    String(p.remainingBalance),
    p.notes ?? '',
  ])
  const csvContent = [headers, ...rows]
    .map((r) => r.map((c) => `"${c.replace(/"/g, '""')}"`).join(','))
    .join('\n')
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `payments-${new Date().toISOString().substring(0, 10)}.csv`
  a.click()
}

// ─── Page component ──────────────────────────────────────────────────────────

export default function PaymentsPage() {
  // Data state
  const [data, setData] = useState<PagedResult<AdvancePayment> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [page, setPage] = useState(1)

  // Filter state
  const [search, setSearch] = useState('')
  const [methodFilter, setMethodFilter] = useState<PaymentMethod | ''>('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')

  // Detail panel state
  const [selected, setSelected] = useState<AdvancePayment | null>(null)

  // ─── Data loading ────────────────────────────────────────────────────────

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await bff.getAllPayments(page, PAGE_SIZE)
      setData(res)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => {
    const h = setTimeout(load, 200)
    return () => clearTimeout(h)
  }, [load])

  // ─── Client-side filtering ──────────────────────────────────────────────

  const filteredItems = useMemo(() => {
    if (!data) return []
    return data.items.filter((pmt) => {
      if (search && !matchesSearch(pmt, search)) return false
      if (methodFilter && pmt.paymentMethod !== methodFilter) return false
      if ((dateFrom || dateTo) && !inDateRange(pmt, dateFrom, dateTo)) return false
      return true
    })
  }, [data, search, methodFilter, dateFrom, dateTo])

  // ─── Summary totals ────────────────────────────────────────────────────

  const totals = useMemo(() => {
    const count = filteredItems.length
    const totalAmount = filteredItems.reduce((s, p) => s + p.amount, 0)
    const totalApplied = filteredItems.reduce((s, p) => s + (p.amount - p.remainingBalance), 0)
    const totalRemaining = filteredItems.reduce((s, p) => s + p.remainingBalance, 0)
    return { count, totalAmount, totalApplied, totalRemaining }
  }, [filteredItems])

  const totalPages = data?.totalPages ?? 1

  // ─── Render ─────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      <PageHeader
        title="Payments"
        subtitle="All advance payments across customers -- track receipts, allocations and balances."
        action={
          <SecondaryButton onClick={() => downloadCsv(filteredItems)}>Export CSV</SecondaryButton>
        }
      />

      {/* ── Filters ─────────────────────────────────────────────────────────── */}
      <Toolbar>
        <ToolbarGroup>
          <SearchInput
            value={search}
            onChange={(v) => { setPage(1); setSearch(v) }}
            placeholder="Customer name, reference #..."
          />
          <select
            value={methodFilter}
            onChange={(e) => { setPage(1); setMethodFilter(e.target.value as PaymentMethod | '') }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
          >
            <option value="">All methods</option>
            {PAYMENT_METHODS.map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
        </ToolbarGroup>
        <ToolbarGroup>
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
      </Toolbar>

      {/* ── Summary bar ─────────────────────────────────────────────────────── */}
      {data && (
        <Card className="px-4 py-3">
          <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm">
            <div className="text-slate-600">
              <span className="font-medium text-slate-900">Total Payments:</span>{' '}
              {totals.count}
            </div>
            <div className="text-slate-600">
              <span className="font-medium text-slate-900">Total Amount:</span>{' '}
              {fmt(totals.totalAmount)}
            </div>
            <div className="text-slate-600">
              <span className="font-medium text-slate-900">Total Applied:</span>{' '}
              {fmt(totals.totalApplied)}
            </div>
            <div className={totals.totalRemaining > 0 ? 'font-semibold text-amber-700' : 'text-slate-600'}>
              <span className={totals.totalRemaining > 0 ? 'font-bold text-amber-800' : 'font-medium text-slate-900'}>Total Remaining:</span>{' '}
              {fmt(totals.totalRemaining)}
            </div>
          </div>
        </Card>
      )}

      {/* ── Error / loading ──────────────────────────────────────────────────── */}
      {error && <ErrorBox message={error} onRetry={load} retryLabel="Retry" />}
      {loading && <Spinner label="Loading payments..." />}

      {/* ── Data table ───────────────────────────────────────────────────────── */}
      {!loading && data && (
        <>
          <DataTable>
            <DataTableMeta>
              Page {page} of {totalPages} ({data.totalCount} total records)
            </DataTableMeta>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="border-b border-slate-200 bg-slate-50/80">
                  <tr>
                    <TableHeadCell>Receipt #</TableHeadCell>
                    <TableHeadCell>Customer</TableHeadCell>
                    <TableHeadCell align="end">Amount (SAR)</TableHeadCell>
                    <TableHeadCell>Method</TableHeadCell>
                    <TableHeadCell>Date Received</TableHeadCell>
                    <TableHeadCell>Reference #</TableHeadCell>
                    <TableHeadCell align="center">Applied To</TableHeadCell>
                    <TableHeadCell align="end">Remaining Balance</TableHeadCell>
                    <TableHeadCell>Notes</TableHeadCell>
                  </tr>
                </thead>
                <tbody>
                  {filteredItems.length === 0 && (
                    <tr>
                      <td colSpan={9} className="px-3 py-10 text-center text-slate-400">
                        No payments found.
                      </td>
                    </tr>
                  )}
                  {filteredItems.map((pmt) => (
                    <tr
                      key={pmt.id}
                      className={`cursor-pointer border-t border-slate-100 transition hover:bg-brand-50/50 ${
                        selected?.id === pmt.id ? 'bg-brand-50 ring-1 ring-inset ring-brand-300' : ''
                      }`}
                      onClick={() => setSelected(selected?.id === pmt.id ? null : pmt)}
                    >
                      <TableCell className="font-mono text-xs font-bold">{pmt.id}</TableCell>
                      <TableCell className="text-slate-800">{pmt.customerDisplayName}</TableCell>
                      <TableCell align="end" className="font-mono text-xs font-semibold">{fmt(pmt.amount)}</TableCell>
                      <TableCell>
                        <Badge tone={METHOD_TONES[pmt.paymentMethod]}>{pmt.paymentMethod}</Badge>
                      </TableCell>
                      <TableCell className="text-xs text-slate-600">{pmt.receivedDate}</TableCell>
                      <TableCell className="font-mono text-xs">{pmt.referenceNumber ?? '--'}</TableCell>
                      <TableCell align="center" className="text-xs">
                        {pmt.allocations.length > 0 ? (
                          <span className="font-semibold text-green-700">{pmt.allocations.length} invoice{pmt.allocations.length !== 1 ? 's' : ''}</span>
                        ) : (
                          <span className="text-slate-400">--</span>
                        )}
                      </TableCell>
                      <TableCell
                        align="end"
                        className={`font-mono text-xs font-semibold ${pmt.remainingBalance > 0 ? 'text-amber-700' : 'text-slate-400'}`}
                      >
                        {pmt.remainingBalance > 0 ? fmt(pmt.remainingBalance) : '--'}
                      </TableCell>
                      <TableCell className="max-w-[200px] truncate text-xs text-slate-500">{pmt.notes ?? '--'}</TableCell>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </DataTable>

          {/* ── Pagination ──────────────────────────────────────────────────── */}
          <div className="flex items-center justify-between text-xs text-slate-600">
            <span>Showing {filteredItems.length} of {data.totalCount} payments</span>
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
      {selected && (
        <Card className="p-5">
          <div className="mb-4 flex items-start justify-between">
            <div>
              <h3 className="text-lg font-semibold text-slate-900">
                Payment {selected.id}
              </h3>
              <div className="mt-1">
                <Badge tone={METHOD_TONES[selected.paymentMethod]}>{selected.paymentMethod}</Badge>
              </div>
            </div>
            <SecondaryButton onClick={() => setSelected(null)} className="px-2 py-1 text-xs">
              Close
            </SecondaryButton>
          </div>

          <div className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Customer</div>
              <div className="mt-0.5 text-slate-800">{selected.customerDisplayName}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Amount</div>
              <div className="mt-0.5 font-mono font-semibold text-slate-900">{fmt(selected.amount)}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Date Received</div>
              <div className="mt-0.5 text-slate-800">{selected.receivedDate}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Reference #</div>
              <div className="mt-0.5 font-mono text-slate-800">{selected.referenceNumber ?? '--'}</div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Remaining Balance</div>
              <div className={`mt-0.5 font-mono font-semibold ${selected.remainingBalance > 0 ? 'text-amber-700' : 'text-slate-400'}`}>
                {fmt(selected.remainingBalance)}
              </div>
            </div>
            <div>
              <div className="text-xs font-medium uppercase tracking-wide text-slate-500">Notes</div>
              <div className="mt-0.5 text-slate-800">{selected.notes ?? '--'}</div>
            </div>
          </div>

          {/* ── Allocations list ──────────────────────────────────────────────── */}
          {selected.allocations.length > 0 && (
            <div className="mt-5 border-t border-slate-200 pt-4">
              <h4 className="mb-3 text-sm font-semibold text-slate-800">Allocations ({selected.allocations.length})</h4>
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="border-b border-slate-200 bg-slate-50/60">
                    <tr>
                      <TableHeadCell>Invoice #</TableHeadCell>
                      <TableHeadCell align="end">Amount Allocated</TableHeadCell>
                      <TableHeadCell>Date Allocated</TableHeadCell>
                    </tr>
                  </thead>
                  <tbody>
                    {selected.allocations.map((alloc) => (
                      <tr key={alloc.id} className="border-t border-slate-100">
                        <TableCell className="font-mono text-xs font-semibold text-brand-700">{alloc.invoiceNumber}</TableCell>
                        <TableCell align="end" className="font-mono text-xs text-green-700">{fmt(alloc.allocatedAmountSar)}</TableCell>
                        <TableCell className="text-xs text-slate-600">{alloc.allocatedAtUtc.substring(0, 10)}</TableCell>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {selected.allocations.length === 0 && (
            <div className="mt-5 border-t border-slate-200 pt-4">
              <p className="text-sm text-slate-500">No allocations yet. This payment has not been applied to any invoices.</p>
            </div>
          )}
        </Card>
      )}
    </div>
  )
}
