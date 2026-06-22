'use client'

import { useCallback, useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import { bff, type AdvancePayment, type PaymentMethod, type PagedResult } from '../../lib/bff-client'
import { ErrorBox } from '../../components/ui'
import {
  type Column,
  DataGrid,
  DateCell,
  DetailPanel,
  DetailRow,
  DetailSection,
  FilterBar,
  FilterPill,
  MoneyCell,
  PageShell,
  SearchBox,
  StatusBadge,
  type BadgeTone,
} from '../../components/data-grid'

const PAGE_SIZE = 30
const METHODS: PaymentMethod[] = ['Cash', 'CreditCard', 'BankTransfer', 'Cheque', 'OnlineTransfer']
const METHOD_TONE: Record<PaymentMethod, BadgeTone> = { Cash: 'green', CreditCard: 'blue', BankTransfer: 'amber', Cheque: 'slate', OnlineTransfer: 'blue' }

function fmt(n: number) { return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2 })}` }

function downloadCsv(items: AdvancePayment[]) {
  if (!items.length) return
  const headers = ['Customer', 'Amount', 'Method', 'Date', 'Reference', 'Remaining', 'Notes']
  const rows = items.map((p) => [p.customerDisplayName, String(p.amount), p.paymentMethod, p.receivedDate, p.referenceNumber ?? '', String(p.remainingBalance), p.notes ?? ''])
  const csv = [headers, ...rows].map((r) => r.map((c) => `"${c.replace(/"/g, '""')}"`).join(',')).join('\n')
  const a = document.createElement('a')
  a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
  a.download = `payments-${new Date().toISOString().substring(0, 10)}.csv`
  a.click()
}

export default function PaymentsPage() {
  const [data, setData] = useState<PagedResult<AdvancePayment> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [methodFilter, setMethodFilter] = useState('')
  const [selected, setSelected] = useState<AdvancePayment | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try { setData(await bff.getAllPayments(page, PAGE_SIZE)) }
    catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }, [page])

  useEffect(() => { const h = setTimeout(load, 200); return () => clearTimeout(h) }, [load])

  const rows = useMemo(() => {
    if (!data) return []
    return data.items.filter((p) => {
      if (methodFilter && p.paymentMethod !== methodFilter) return false
      if (search) {
        const hay = `${p.customerDisplayName} ${p.referenceNumber ?? ''}`.toLowerCase()
        if (!hay.includes(search.toLowerCase())) return false
      }
      return true
    })
  }, [data, search, methodFilter])

  const totals = useMemo(() => ({
    amount: rows.reduce((s, p) => s + p.amount, 0),
    remaining: rows.reduce((s, p) => s + p.remainingBalance, 0),
  }), [rows])

  const columns: Column<AdvancePayment>[] = [
    { key: 'customer', header: 'Customer', render: (p) => <span className="font-medium text-slate-900">{p.customerDisplayName}</span> },
    { key: 'amount', header: 'Amount', align: 'right', render: (p) => <MoneyCell amount={p.amount} /> },
    { key: 'method', header: 'Method', render: (p) => <StatusBadge tone={METHOD_TONE[p.paymentMethod]}>{p.paymentMethod}</StatusBadge> },
    { key: 'date', header: 'Received', render: (p) => <DateCell date={p.receivedDate} /> },
    { key: 'ref', header: 'Reference', render: (p) => <span className="font-mono">{p.referenceNumber ?? '—'}</span> },
    { key: 'alloc', header: 'Allocated', align: 'center', render: (p) => p.allocations.length > 0 ? <span className="font-semibold text-green-700">{p.allocations.length}</span> : <span className="text-slate-300">—</span> },
    { key: 'remaining', header: 'Remaining', align: 'right', render: (p) => p.remainingBalance > 0 ? <span className="font-mono font-semibold text-amber-700">{fmt(p.remainingBalance)}</span> : <span className="text-slate-300">—</span> },
  ]

  return (
    <PageShell
      title="Payments"
      subtitle={`${rows.length} payments | Total: ${fmt(totals.amount)} | Remaining: ${fmt(totals.remaining)}`}
      actions={<button onClick={() => downloadCsv(rows)} className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Export CSV</button>}
    >
      <FilterBar>
        <SearchBox value={search} onChange={(v) => { setPage(1); setSearch(v) }} placeholder="Customer, reference..." />
        <FilterPill value={methodFilter} onChange={setMethodFilter} options={METHODS.map((m) => ({ value: m, label: m }))} placeholder="All Methods" />
      </FilterBar>

      {error && <div className="p-4"><ErrorBox message={error} onRetry={load} /></div>}

      <div className="flex">
        <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
          <DataGrid
            columns={columns}
            rows={rows}
            totalCount={data?.totalCount ?? 0}
            page={page}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
            onRowClick={setSelected}
            selectedId={selected?.id ?? null}
            loading={loading}
          />
        </div>

        <DetailPanel
          open={!!selected}
          onClose={() => setSelected(null)}
          title={selected?.customerDisplayName ?? ''}
          {...(selected?.referenceNumber ? { subtitle: selected.referenceNumber } : {})}
          {...(selected ? { badge: <StatusBadge tone={METHOD_TONE[selected.paymentMethod]}>{selected.paymentMethod}</StatusBadge> } : {})}
        >
          {selected && (
            <>
              <DetailSection title="Payment">
                <DetailRow label="Amount" value={<MoneyCell amount={selected.amount} />} />
                <DetailRow label="Date Received" value={<DateCell date={selected.receivedDate} />} />
                <DetailRow label="Reference" value={selected.referenceNumber ?? '—'} />
                <DetailRow label="Remaining" value={<span className={selected.remainingBalance > 0 ? 'font-semibold text-amber-700' : ''}>{fmt(selected.remainingBalance)}</span>} />
                <DetailRow label="Notes" value={selected.notes ?? '—'} />
              </DetailSection>
              {selected.allocations.length > 0 && (
                <DetailSection title={`Allocations (${selected.allocations.length})`}>
                  {selected.allocations.map((a) => (
                    <DetailRow key={a.id} label={a.invoiceNumber} value={<MoneyCell amount={a.allocatedAmountSar} />} />
                  ))}
                </DetailSection>
              )}
              <div className="px-4 pb-3 pt-1">
                <Link
                  href={`/payments/${selected.id}`}
                  className="inline-flex items-center rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                >
                  Print Receipt
                </Link>
              </div>
            </>
          )}
        </DetailPanel>
      </div>
    </PageShell>
  )
}
