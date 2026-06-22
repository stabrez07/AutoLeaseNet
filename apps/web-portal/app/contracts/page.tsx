'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { bff, type PagedResult, type ContractSummary, type ContractDetail } from '../../lib/bff-client'
import {
  type BadgeTone, type Column, DataGrid, DateCell, DetailPanel, DetailRow, DetailSection,
  FilterBar, FilterPill, MoneyCell, PageShell, SearchBox, StatusBadge,
} from '../../components/data-grid'

// ─── Constants ───────────────────────────────────────────────────────────────

const PAGE_SIZE = 20

const STATUS_OPTIONS = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Active', label: 'Active' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Closed', label: 'Closed' },
]

const STATUS_TONES: Record<string, BadgeTone> = {
  Draft: 'slate',
  Active: 'green',
  Suspended: 'amber',
  Closed: 'slate',
  Cancelled: 'red',
}

const CONTRACT_TYPES: Record<string, string> = {
  '1': 'Long Term',
  '2': 'Short Term',
  '3': 'Daily',
  LongTermLease: 'Long Term',
  ShortTermLease: 'Short Term',
  Daily: 'Daily',
}

// ─── Columns ─────────────────────────────────────────────────────────────────

const COLUMNS: Column<ContractSummary>[] = [
  {
    key: 'contractNumber',
    header: 'Contract #',
    width: '130px',
    render: (r) => <span className="font-mono text-xs font-semibold text-slate-900">{r.contractNumber}</span>,
  },
  {
    key: 'customer',
    header: 'Customer',
    render: (r) => (
      <span className="max-w-[160px] truncate font-medium text-slate-900">{r.customerDisplayName}</span>
    ),
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
    key: 'totalVehicles',
    header: 'Vehicles',
    width: '80px',
    align: 'right',
    render: (r) => <span className="text-slate-700">{r.totalVehicles}</span>,
  },
  {
    key: 'monthlyRent',
    header: 'Monthly Rent',
    width: '130px',
    align: 'right',
    render: (r) => <MoneyCell amount={r.monthlyRentSar} />,
  },
  {
    key: 'duration',
    header: 'Duration',
    width: '90px',
    render: (r) => <span className="text-slate-600">{r.durationMonths} mo</span>,
  },
  {
    key: 'leaseAgreements',
    header: 'Lease Agreements',
    width: '130px',
    align: 'right',
    render: (r) => <span className="text-slate-700">{r.leaseAgreementCount}</span>,
  },
  {
    key: 'startDate',
    header: 'Start Date',
    width: '100px',
    render: (r) => <DateCell date={r.startDate} />,
  },
  {
    key: 'endDate',
    header: 'End Date',
    width: '100px',
    render: (r) => <DateCell date={r.endDate} />,
  },
]

// ─── Page component ──────────────────────────────────────────────────────────

export default function ContractsPage() {
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<ContractSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<ContractDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)

  // ─── Data loading ────────────────────────────────────────────────────────

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await bff.getContracts(page, PAGE_SIZE, search || undefined, statusFilter || undefined))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const h = setTimeout(load, 200)
    return () => clearTimeout(h)
  }, [page, search, statusFilter]) // eslint-disable-line react-hooks/exhaustive-deps

  async function openDetail(row: ContractSummary) {
    setDetailLoading(true)
    try {
      setSelected(await bff.getContractById(row.id))
    } catch { /* non-critical */ }
    finally {
      setDetailLoading(false)
    }
  }

  // ─── Render ────────────────────────────────────────────────────────────

  return (
    <PageShell
      title="Contracts"
      subtitle="Commercial agreements with customers — pricing, vehicle counts, and linked lease agreements."
    >
      <FilterBar>
        <SearchBox
          value={search}
          onChange={(v) => { setPage(1); setSearch(v) }}
          placeholder="Contract #, customer..."
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
          <DataGrid<ContractSummary>
            columns={COLUMNS}
            rows={data?.items ?? []}
            totalCount={data?.totalCount ?? 0}
            page={page}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
            onRowClick={openDetail}
            selectedId={selected?.id ?? null}
            emptyMessage="No contracts found."
            loading={loading}
          />
        </div>

        <DetailPanel
          open={!!selected}
          onClose={() => setSelected(null)}
          title={selected?.contractNumber ?? ''}
          {...(selected?.customerDisplayName ? { subtitle: selected.customerDisplayName } : {})}
          {...(selected ? { badge: <StatusBadge tone={STATUS_TONES[selected.status] ?? 'slate'}>{selected.status}</StatusBadge> } : {})}
        >
          {detailLoading && (
            <div className="flex items-center justify-center py-8 text-xs text-slate-400">
              <span className="border-t-brand-600 mr-2 inline-block h-3.5 w-3.5 animate-spin rounded-full border-2 border-slate-200" />
              Loading...
            </div>
          )}
          {selected && !detailLoading && (
            <>
              <DetailSection title="Contract Details">
                <DetailRow label="Contract #" value={selected.contractNumber} />
                <DetailRow label="Customer" value={selected.customerDisplayName} />
                <DetailRow label="Status" value={<StatusBadge tone={STATUS_TONES[selected.status] ?? 'slate'}>{selected.status}</StatusBadge>} />
                <DetailRow label="Type" value={CONTRACT_TYPES[selected.contractTypeCode] ?? selected.contractTypeCode} />
                <DetailRow label="Duration" value={`${selected.durationMonths} months`} />
                <DetailRow label="Start Date" value={<DateCell date={selected.startDate} />} />
                <DetailRow label="End Date" value={<DateCell date={selected.endDate} />} />
              </DetailSection>
              <DetailSection title="Financials">
                <DetailRow label="Monthly Rent" value={<MoneyCell amount={selected.monthlyRentSar} />} />
                <DetailRow label="Total Value" value={<MoneyCell amount={selected.totalContractValueSar} />} />
                <DetailRow label="Vehicles" value={String(selected.totalVehicles)} />
                <DetailRow label="Lease Agreements" value={String(selected.leaseAgreementCount)} />
              </DetailSection>
              {selected.lines.length > 0 && (
                <DetailSection title={`Vehicle Lines (${selected.lines.length})`}>
                  <div className="overflow-x-auto rounded border border-slate-200">
                    <table className="w-full text-[11px]">
                      <thead>
                        <tr className="border-b border-slate-100 bg-slate-50/80 text-slate-500">
                          <th className="px-2 py-1 text-left font-medium">Make</th>
                          <th className="px-2 py-1 text-left font-medium">Model</th>
                          <th className="px-2 py-1 text-right font-medium">Year</th>
                          <th className="px-2 py-1 text-right font-medium">Qty</th>
                          <th className="px-2 py-1 text-right font-medium">Unit Price</th>
                          <th className="px-2 py-1 text-right font-medium">Total</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {selected.lines.map((l) => (
                          <tr key={l.id}>
                            <td className="px-2 py-1">{l.make}</td>
                            <td className="px-2 py-1">{l.model}</td>
                            <td className="px-2 py-1 text-right">{l.year}</td>
                            <td className="px-2 py-1 text-right">{l.quantity}</td>
                            <td className="px-2 py-1 text-right"><MoneyCell amount={l.unitPriceSar} /></td>
                            <td className="px-2 py-1 text-right"><MoneyCell amount={l.lineTotalSar} /></td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </DetailSection>
              )}
              <DetailSection title="Actions">
                <Link href={`/contracts/${selected.id}`} className="block w-full rounded-md bg-brand-700 px-3 py-1.5 text-center text-xs font-medium text-white hover:bg-brand-800">
                  Open Full Details
                </Link>
              </DetailSection>
            </>
          )}
        </DetailPanel>
      </div>
    </PageShell>
  )
}
