'use client'

import { useEffect, useMemo, useState } from 'react'
import { bff, type CustomerSummary, type PagedResult } from '../../lib/bff-client'
import {
  Column,
  DataGrid,
  DetailPanel,
  DetailRow,
  DetailSection,
  FilterBar,
  FilterPill,
  PageShell,
  SearchBox,
  StatusBadge,
  type BadgeTone,
} from '../../components/data-grid'

// ─── Constants ───────────────────────────────────────────────────────────────

const PAGE_SIZE = 30

const TYPE_OPTIONS = [
  { value: 'b2b', label: 'Business (B2B)' },
  { value: 'b2c', label: 'Individual (B2C)' },
]

const STATUS_OPTIONS = [
  { value: 'active', label: 'Active' },
  { value: 'inactive', label: 'Inactive' },
]

// ─── Helpers ─────────────────────────────────────────────────────────────────

function typeLabel(c: CustomerSummary): string {
  return c.type === 1 ? 'B2B' : 'B2C'
}

function typeTone(c: CustomerSummary): BadgeTone {
  return c.type === 1 ? 'blue' : 'slate'
}

function statusTone(c: CustomerSummary): BadgeTone {
  return c.isActive ? 'green' : 'slate'
}

// ─── Columns ─────────────────────────────────────────────────────────────────

const COLUMNS: Column<CustomerSummary>[] = [
  {
    key: 'displayName',
    header: 'Name',
    render: (r) => <span className="font-medium text-slate-900">{r.displayName}</span>,
  },
  {
    key: 'email',
    header: 'Email',
    render: (r) => <span className="text-slate-500">{r.email ?? '—'}</span>,
  },
  {
    key: 'type',
    header: 'Type',
    width: '90px',
    render: (r) => <StatusBadge tone={typeTone(r)}>{typeLabel(r)}</StatusBadge>,
  },
  {
    key: 'mobile',
    header: 'Mobile',
    width: '130px',
    render: (r) => <span className="font-mono">{r.mobile ?? '—'}</span>,
  },
  {
    key: 'crNumber',
    header: 'CR Number',
    width: '120px',
    render: (r) => <span className="font-mono">{r.type === 1 ? (r.commercialRegistration ?? '—') : '—'}</span>,
  },
  {
    key: 'status',
    header: 'Status',
    width: '90px',
    render: (r) => (
      <StatusBadge tone={statusTone(r)}>
        {r.isActive ? 'Active' : 'Inactive'}
      </StatusBadge>
    ),
  },
]

// ─── Page component ──────────────────────────────────────────────────────────

export default function CustomersPage() {
  const [search, setSearch] = useState('')
  const [typeFilter, setTypeFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<CustomerSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<CustomerSummary | null>(null)

  // ─── Data loading ────────────────────────────────────────────────────────

  useEffect(() => {
    let cancelled = false
    const h = setTimeout(async () => {
      setLoading(true)
      setError(null)
      try {
        const res = await bff.getCustomers(page, PAGE_SIZE, search || undefined)
        if (!cancelled) setData(res)
      } catch (e) {
        if (!cancelled) setError((e as Error).message)
      } finally {
        if (!cancelled) setLoading(false)
      }
    }, 200)
    return () => { cancelled = true; clearTimeout(h) }
  }, [page, search])

  // ─── Client-side filtering ─────────────────────────────────────────────

  const filteredItems = useMemo(() => {
    if (!data) return []
    let items = data.items
    if (typeFilter === 'b2b') items = items.filter((c) => c.type === 1)
    if (typeFilter === 'b2c') items = items.filter((c) => c.type === 2)
    if (statusFilter === 'active') items = items.filter((c) => c.isActive)
    if (statusFilter === 'inactive') items = items.filter((c) => !c.isActive)
    return items
  }, [data, typeFilter, statusFilter])

  // ─── Render ────────────────────────────────────────────────────────────

  return (
    <PageShell
      title="Customers"
      subtitle="Tenant customers (B2B + B2C)."
    >
      <FilterBar>
        <SearchBox
          value={search}
          onChange={(v) => { setPage(1); setSearch(v) }}
          placeholder="Search by name or mobile..."
        />
        <FilterPill
          value={typeFilter}
          onChange={(v) => { setPage(1); setTypeFilter(v) }}
          options={TYPE_OPTIONS}
          placeholder="All types"
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
        </div>
      )}

      <div className="flex">
        <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
          <DataGrid<CustomerSummary>
            columns={COLUMNS}
            rows={filteredItems}
            totalCount={data?.totalCount ?? 0}
            page={page}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
            onRowClick={(row) => setSelected((prev) => (prev?.id === row.id ? null : row))}
            selectedId={selected?.id ?? null}
            emptyMessage="No customers found."
            loading={loading}
          />
        </div>

        <DetailPanel
          open={!!selected}
          onClose={() => setSelected(null)}
          title={selected?.displayName ?? ''}
          {...(selected?.email ? { subtitle: selected.email } : {})}
          {...(selected ? { badge: <StatusBadge tone={statusTone(selected)}>{selected.isActive ? 'Active' : 'Inactive'}</StatusBadge> } : {})}
        >
          {selected && (
            <>
              <DetailSection title="Contact">
                <DetailRow label="Email" value={selected.email ?? '—'} />
                <DetailRow label="Mobile" value={selected.mobile ?? '—'} />
                <DetailRow label="Contact Person" value={selected.contactPerson ?? '—'} />
                <DetailRow label="Contact Mobile" value={selected.contactPersonMobile ?? '—'} />
              </DetailSection>
              <DetailSection title="Business">
                <DetailRow label="Type" value={<StatusBadge tone={typeTone(selected)}>{typeLabel(selected)}</StatusBadge>} />
                <DetailRow label="CR Number" value={selected.commercialRegistration ?? '—'} />
                <DetailRow label="VAT Number" value={selected.vatNumber ?? '—'} />
                <DetailRow label="City" value={selected.city ?? '—'} />
              </DetailSection>
              <DetailSection title="KYC">
                <DetailRow label="Status" value={<StatusBadge tone={statusTone(selected)}>{selected.isActive ? 'Active' : 'Inactive'}</StatusBadge>} />
              </DetailSection>
            </>
          )}
        </DetailPanel>
      </div>
    </PageShell>
  )
}
