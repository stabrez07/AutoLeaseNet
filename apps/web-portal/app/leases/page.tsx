'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { bff, type LeaseSummary, type PagedResult } from '../../lib/bff-client'
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

const PAGE_SIZE = 20

const STATUS_OPTIONS = [
  { value: 'Draft', label: 'Draft' },
  { value: 'PendingIssuance', label: 'Pending Issuance' },
  { value: 'Active', label: 'Active' },
  { value: 'Extended', label: 'Extended' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Closed', label: 'Closed' },
  { value: 'Cancelled', label: 'Cancelled' },
]

const STATUS_TONES: Record<string, BadgeTone> = {
  Draft: 'slate',
  PendingIssuance: 'amber',
  Active: 'green',
  Extended: 'blue',
  Suspended: 'red',
  Closed: 'slate',
  Cancelled: 'red',
}

// ─── Columns ─────────────────────────────────────────────────────────────────

const COLUMNS: Column<LeaseSummary>[] = [
  {
    key: 'leaseNumber',
    header: 'Contract #',
    width: '110px',
    render: (r) => <span className="font-mono text-xs font-semibold">{r.leaseNumber}</span>,
  },
  {
    key: 'customer',
    header: 'Customer',
    render: (r) => (
      <span className="max-w-[160px] truncate font-medium text-slate-900">{r.customerDisplayName}</span>
    ),
  },
  {
    key: 'vehicle',
    header: 'Vehicle',
    render: (r) => <span className="text-slate-700">{r.vehicleMakeModel}</span>,
  },
  {
    key: 'status',
    header: 'Status',
    width: '110px',
    render: (r) => (
      <StatusBadge tone={STATUS_TONES[r.status] ?? 'slate'}>
        {r.status}
      </StatusBadge>
    ),
  },
  {
    key: 'startDate',
    header: 'Start Date',
    width: '100px',
    render: (r) => <DateCell date={r.contractStartUtc} />,
  },
  {
    key: 'endDate',
    header: 'End Date',
    width: '100px',
    render: (r) => <DateCell date={r.contractEndUtc} />,
  },
  {
    key: 'rentAmount',
    header: 'Rent Amount',
    width: '120px',
    align: 'right',
    render: (r) => <MoneyCell amount={r.rentAmountSar} />,
  },
  {
    key: 'branch',
    header: 'Branch',
    width: '120px',
    render: (r) => <span className="text-slate-600">{r.workingBranchName}</span>,
  },
]

// ─── Page component ──────────────────────────────────────────────────────────

export default function LeasesPage() {
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<LeaseSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<LeaseSummary | null>(null)

  // ─── Data loading ────────────────────────────────────────────────────────

  useEffect(() => {
    let cancelled = false
    const h = setTimeout(async () => {
      setLoading(true)
      setError(null)
      try {
        const res = await bff.getLeases(page, PAGE_SIZE, search || undefined, statusFilter || undefined)
        if (!cancelled) setData(res)
      } catch (e) {
        if (!cancelled) setError((e as Error).message)
      } finally {
        if (!cancelled) setLoading(false)
      }
    }, 200)
    return () => { cancelled = true; clearTimeout(h) }
  }, [page, search, statusFilter])

  // ─── Render ────────────────────────────────────────────────────────────

  return (
    <PageShell
      title="Lease Agreements"
      subtitle="Vehicle checkout agreements under contracts — track vehicles, drivers, and billing."
      actions={
        <Link href="/contracts" className="rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800">
          + New LA (via Contract)
        </Link>
      }
    >
      <FilterBar>
        <SearchBox
          value={search}
          onChange={(v) => { setPage(1); setSearch(v) }}
          placeholder="Customer, vehicle, plate..."
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
          <button type="button" onClick={() => setPage(page)} className="ml-2 underline">Retry</button>
        </div>
      )}

      <div className="flex">
        <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
          <DataGrid<LeaseSummary>
            columns={COLUMNS}
            rows={data?.items ?? []}
            totalCount={data?.totalCount ?? 0}
            page={page}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
            onRowClick={(row) => setSelected((prev) => (prev?.id === row.id ? null : row))}
            selectedId={selected?.id ?? null}
            emptyMessage="No leases found."
            loading={loading}
          />
        </div>

        <DetailPanel
          open={!!selected}
          onClose={() => setSelected(null)}
          title={selected?.customerDisplayName ?? ''}
          {...(selected?.vehicleMakeModel ? { subtitle: selected.vehicleMakeModel } : {})}
          {...(selected ? { badge: <StatusBadge tone={STATUS_TONES[selected.status] ?? 'slate'}>{selected.status}</StatusBadge> } : {})}
        >
          {selected && (
            <>
              <DetailSection title="Contract">
                <DetailRow label="Contract #" value={selected.leaseNumber} />
                <DetailRow label="Status" value={<StatusBadge tone={STATUS_TONES[selected.status] ?? 'slate'}>{selected.status}</StatusBadge>} />
                <DetailRow label="Contract Type" value={selected.contractTypeCode} />
              </DetailSection>
              <DetailSection title="Customer & Vehicle">
                <DetailRow label="Customer" value={selected.customerDisplayName} />
                <DetailRow label="Vehicle" value={selected.vehicleMakeModel} />
                <DetailRow label="Plate" value={selected.vehiclePlate} />
                <DetailRow label="Driver" value={selected.primaryDriverName ?? '—'} />
              </DetailSection>
              <DetailSection title="Dates & Financials">
                <DetailRow label="Start Date" value={<DateCell date={selected.contractStartUtc} />} />
                <DetailRow label="End Date" value={<DateCell date={selected.contractEndUtc} />} />
                <DetailRow label="Rent Amount" value={<MoneyCell amount={selected.rentAmountSar} />} />
                <DetailRow label="Branch" value={selected.workingBranchName} />
              </DetailSection>
              <DetailSection title="Actions">
                <Link href={`/leases/${selected.id}`} className="block w-full rounded-md bg-brand-700 px-3 py-1.5 text-center text-xs font-medium text-white hover:bg-brand-800">
                  Open Contract Details
                </Link>
              </DetailSection>
            </>
          )}
        </DetailPanel>
      </div>
    </PageShell>
  )
}
