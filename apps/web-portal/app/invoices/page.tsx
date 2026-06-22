'use client'

import Link from 'next/link'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { bff, type Invoice, type InvoiceStatus, type PagedResult } from '../../lib/bff-client'
import {
  Column,
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

// ─── Constants ───────────────────────────────────────────────────────────────

const PAGE_SIZE = 30

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Submitted', label: 'Submitted' },
  { value: 'Cleared', label: 'Cleared' },
  { value: 'Finalized', label: 'Finalized' },
  { value: 'SubmissionFailed', label: 'Failed' },
  { value: 'Voided', label: 'Voided' },
]

const STATUS_TONES: Record<string, BadgeTone> = {
  Draft: 'slate',
  Submitted: 'amber',
  Cleared: 'green',
  Finalized: 'blue',
  SubmissionFailed: 'red',
  ClearanceFailed: 'red',
  Voided: 'red',
}

// ─── Columns ─────────────────────────────────────────────────────────────────

const COLUMNS: Column<Invoice>[] = [
  {
    key: 'invoiceNumber',
    header: 'Invoice #',
    width: '110px',
    render: (r) => <span className="font-mono text-xs font-semibold">{r.invoiceNumber}</span>,
  },
  {
    key: 'customer',
    header: 'Customer',
    render: (r) => <span className="font-medium text-slate-800">{r.customerDisplayName}</span>,
  },
  {
    key: 'vehicle',
    header: 'Vehicle',
    render: (r) => <span className="text-slate-700">{r.vehicleMakeModel}</span>,
  },
  {
    key: 'status',
    header: 'Status',
    width: '100px',
    render: (r) => <StatusBadge tone={STATUS_TONES[r.status] ?? 'slate'}>{r.status}</StatusBadge>,
  },
  {
    key: 'issuedDate',
    header: 'Issue Date',
    width: '100px',
    render: (r) => <DateCell date={r.issuedDate} />,
  },
  {
    key: 'dueDate',
    header: 'Due Date',
    width: '100px',
    render: (r) => <DateCell date={r.dueDate} />,
  },
  {
    key: 'subTotal',
    header: 'Base Amount',
    width: '120px',
    align: 'right',
    render: (r) => <MoneyCell amount={r.subTotalSar} />,
  },
  {
    key: 'vat',
    header: 'VAT',
    width: '100px',
    align: 'right',
    render: (r) => <MoneyCell amount={r.vatAmountSar} />,
  },
  {
    key: 'total',
    header: 'Total',
    width: '120px',
    align: 'right',
    render: (r) => <MoneyCell amount={r.totalSar} />,
  },
]

// ─── Page component ──────────────────────────────────────────────────────────

export default function InvoicesPage() {
  // Data state
  const [data, setData] = useState<PagedResult<Invoice> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [page, setPage] = useState(1)

  // Filter state
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')

  // Detail panel state
  const [selected, setSelected] = useState<Invoice | null>(null)

  // ─── Data loading ────────────────────────────────────────────────────────

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await bff.getInvoices(page, PAGE_SIZE, undefined, undefined, (statusFilter || undefined) as InvoiceStatus | undefined)
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

  // ─── Client-side search filter ─────────────────────────────────────────

  const filteredItems = useMemo(() => {
    if (!data) return []
    if (!search) return data.items
    const term = search.toLowerCase()
    return data.items.filter((inv) => {
      const hay = `${inv.invoiceNumber} ${inv.customerDisplayName}`.toLowerCase()
      return hay.includes(term)
    })
  }, [data, search])

  // ─── Render ────────────────────────────────────────────────────────────

  return (
    <PageShell
      title="Invoices"
      subtitle="Monthly rental invoices -- generate, track and collect."
    >
      <FilterBar>
        <SearchBox
          value={search}
          onChange={(v) => { setPage(1); setSearch(v) }}
          placeholder="Invoice #, customer..."
        />
        <FilterPill
          value={statusFilter}
          onChange={(v) => { setPage(1); setStatusFilter(v) }}
          options={STATUS_OPTIONS}
          placeholder="All statuses"
        />
      </FilterBar>

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-4 py-3 text-xs text-red-700">
          {error}
          <button type="button" onClick={load} className="ml-2 underline">Retry</button>
        </div>
      )}

      <div className="flex">
        <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
          <DataGrid<Invoice>
            columns={COLUMNS}
            rows={filteredItems}
            totalCount={data?.totalCount ?? 0}
            page={page}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
            onRowClick={(row) => setSelected((prev) => (prev?.id === row.id ? null : row))}
            selectedId={selected?.id ?? null}
            emptyMessage="No invoices found."
            loading={loading}
          />
        </div>

        <DetailPanel
          open={!!selected}
          onClose={() => setSelected(null)}
          title={selected?.invoiceNumber ?? ''}
          {...(selected?.customerDisplayName ? { subtitle: selected.customerDisplayName } : {})}
          {...(selected ? { badge: <StatusBadge tone={STATUS_TONES[selected.status] ?? 'slate'}>{selected.status}</StatusBadge> } : {})}
        >
          {selected && (
            <>
              <DetailSection title="Invoice">
                <DetailRow label="Invoice #" value={selected.invoiceNumber} />
                <DetailRow label="Status" value={<StatusBadge tone={STATUS_TONES[selected.status] ?? 'slate'}>{selected.status}</StatusBadge>} />
                <DetailRow label="Issue Date" value={<DateCell date={selected.issuedDate} />} />
                <DetailRow label="Due Date" value={<DateCell date={selected.dueDate} />} />
              </DetailSection>
              <DetailSection title="Customer & Vehicle">
                <DetailRow label="Customer" value={selected.customerDisplayName} />
                <DetailRow label="Vehicle" value={selected.vehicleMakeModel} />
                <DetailRow label="Plate (EN)" value={selected.vehiclePlate} />
                <DetailRow label="Plate (AR)" value={selected.vehiclePlateAr} />
              </DetailSection>
              <DetailSection title="Amounts">
                <DetailRow label="Sub-Total" value={<MoneyCell amount={selected.subTotalSar} />} />
                <DetailRow label="VAT" value={<MoneyCell amount={selected.vatAmountSar} />} />
                <DetailRow label="Total" value={<MoneyCell amount={selected.totalSar} />} />
                <DetailRow label="Paid" value={<MoneyCell amount={selected.paidAmountSar} />} />
                <DetailRow
                  label="Balance"
                  value={
                    <span className={selected.balanceSar > 0 ? 'font-semibold text-red-700' : ''}>
                      <MoneyCell amount={selected.balanceSar} />
                    </span>
                  }
                />
              </DetailSection>
              {(selected.allocations?.length ?? 0) > 0 && (
                <DetailSection title={`Payments Applied (${selected.allocations!.length})`}>
                  <div className="space-y-1">
                    {selected.allocations!.map((a, i) => (
                      <div key={i} className="flex items-center justify-between rounded border border-slate-200 px-2 py-1.5 text-[11px]">
                        <span className="font-mono font-medium text-slate-700">{a.referenceNumber}</span>
                        <span className="font-mono tabular-nums"><MoneyCell amount={a.amount} /></span>
                      </div>
                    ))}
                  </div>
                </DetailSection>
              )}
              {selected.notes && (
                <DetailSection title="Notes">
                  <p className="text-xs text-slate-600">{selected.notes}</p>
                </DetailSection>
              )}
              <DetailSection title="Actions">
                <div className="flex flex-col gap-2">
                  <Link href={`/invoices/${selected.id}`} className="block w-full rounded-md bg-brand-700 px-3 py-1.5 text-center text-xs font-medium text-white hover:bg-brand-800">
                    Open Full View
                  </Link>
                  <Link href={`/leases/${selected.leaseId}`} className="block w-full rounded-md border border-slate-300 bg-white px-3 py-1.5 text-center text-xs font-medium text-slate-700 hover:bg-slate-50">
                    View Contract
                  </Link>
                </div>
              </DetailSection>
            </>
          )}
        </DetailPanel>
      </div>
    </PageShell>
  )
}
