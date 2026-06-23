'use client'

import Link from 'next/link'
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

const PAGE_SIZE = 30

const STATUS_OPTIONS = [
  { value: 'active', label: 'Active' },
  { value: 'inactive', label: 'Inactive' },
]

function statusTone(c: CustomerSummary): BadgeTone {
  return c.isActive ? 'green' : 'slate'
}

const COLUMNS: Column<CustomerSummary>[] = [
  {
    key: 'displayName',
    header: 'Company Name',
    render: (r) => <span className="font-medium text-slate-900">{r.displayName}</span>,
  },
  {
    key: 'email',
    header: 'Email',
    render: (r) => <span className="text-slate-500">{r.email ?? '—'}</span>,
  },
  {
    key: 'mobile',
    header: 'Mobile',
    width: '130px',
    render: (r) => <span className="font-mono text-xs">{r.mobile ?? '—'}</span>,
  },
  {
    key: 'crNumber',
    header: 'CR Number',
    width: '130px',
    render: (r) => <span className="font-mono text-xs">{r.commercialRegistration ?? '—'}</span>,
  },
  {
    key: 'vatNumber',
    header: 'VAT Number',
    width: '130px',
    render: (r) => <span className="font-mono text-xs">{r.vatNumber ?? '—'}</span>,
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

export default function CustomersPage() {
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<CustomerSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<CustomerSummary | null>(null)

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

  const filteredItems = useMemo(() => {
    if (!data) return []
    let items = data.items.filter((c) => c.type === 1)
    if (statusFilter === 'active') items = items.filter((c) => c.isActive)
    if (statusFilter === 'inactive') items = items.filter((c) => !c.isActive)
    return items
  }, [data, statusFilter])

  async function handleDelete(id: string) {
    if (!window.confirm('Are you sure you want to delete this customer?')) return
    try {
      await bff.deleteCustomer(id, crypto.randomUUID())
      setSelected(null)
      setPage(page)
      const res = await bff.getCustomers(page, PAGE_SIZE, search || undefined)
      setData(res)
    } catch (e) {
      alert((e as Error).message)
    }
  }

  return (
    <PageShell
      title="Customers"
      subtitle="B2B corporate fleet accounts."
      actions={
        <Link href="/customers/new" className="rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800">
          + New Customer
        </Link>
      }
    >
      <FilterBar>
        <SearchBox
          value={search}
          onChange={(v) => { setPage(1); setSearch(v) }}
          placeholder="Search by company name, CR, or mobile..."
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
                <DetailRow label="CR Number" value={selected.commercialRegistration ?? '—'} />
                <DetailRow label="VAT Number" value={selected.vatNumber ?? '—'} />
                <DetailRow label="City" value={selected.city ?? '—'} />
              </DetailSection>
              <DetailSection title="Actions">
                <div className="flex flex-col gap-2">
                  <Link href={`/customers/${selected.id}`} className="block w-full rounded-md bg-brand-700 px-3 py-1.5 text-center text-xs font-medium text-white hover:bg-brand-800">
                    View Details
                  </Link>
                  <Link href={`/customers/${selected.id}?edit=true`} className="block w-full rounded-md border border-brand-300 bg-white px-3 py-1.5 text-center text-xs font-medium text-brand-700 hover:bg-brand-50">
                    Edit Customer
                  </Link>
                  <button type="button" onClick={() => handleDelete(selected.id)} className="w-full rounded-md border border-red-300 bg-white px-3 py-1.5 text-xs font-medium text-red-700 hover:bg-red-50">
                    Delete Customer
                  </button>
                  <Link href={`/accounts?customerId=${selected.id}`} className="block w-full rounded-md border border-slate-300 bg-white px-3 py-1.5 text-center text-xs font-medium text-slate-700 hover:bg-slate-50">
                    View Accounts
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
