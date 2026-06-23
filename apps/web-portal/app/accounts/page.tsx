'use client'

import Link from 'next/link'
import { Suspense, useEffect, useState } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import { bff, type AccountSummary, type PagedResult } from '../../lib/bff-client'
import {
  Column,
  DataGrid,
  DetailPanel,
  DetailRow,
  DetailSection,
  FilterBar,
  PageShell,
  SearchBox,
  StatusBadge,
  type BadgeTone,
} from '../../components/data-grid'

const PAGE_SIZE = 30

const STATUS_TONES: Record<string, BadgeTone> = { Active: 'green', Inactive: 'slate' }

const COLUMNS: Column<AccountSummary>[] = [
  {
    key: 'customerDisplayName',
    header: 'Customer',
    render: (r) => <span className="font-medium text-slate-900">{r.customerDisplayName}</span>,
  },
  {
    key: 'natureOfBusiness',
    header: 'Nature of Business',
    render: (r) => <span className="text-slate-600">{r.natureOfBusiness || '—'}</span>,
  },
  {
    key: 'customerContactNameEn',
    header: 'Customer Contact',
    render: (r) => <span className="text-slate-700">{r.customerContactNameEn}</span>,
  },
  {
    key: 'accountHolderNameEn',
    header: 'Account Holder',
    render: (r) => <span className="text-slate-700">{r.accountHolderNameEn}</span>,
  },
  {
    key: 'city',
    header: 'City',
    width: '100px',
    render: (r) => <span className="text-slate-500">{r.city ?? '—'}</span>,
  },
  {
    key: 'status',
    header: 'Status',
    width: '90px',
    render: (r) => <StatusBadge tone={STATUS_TONES[r.status] ?? 'slate'}>{r.status}</StatusBadge>,
  },
]

export default function AccountsPageWrapper() {
  return (
    <Suspense fallback={<div className="p-6 text-sm text-slate-500">Loading accounts...</div>}>
      <AccountsPage />
    </Suspense>
  )
}

function AccountsPage() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const customerIdFilter = searchParams.get('customerId') ?? ''
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<AccountSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<AccountSummary | null>(null)

  useEffect(() => {
    let cancelled = false
    const h = setTimeout(async () => {
      setLoading(true)
      setError(null)
      try {
        const res = await bff.getAccounts(page, PAGE_SIZE, search || undefined, customerIdFilter || undefined)
        if (!cancelled) setData(res)
      } catch (e) {
        if (!cancelled) setError((e as Error).message)
      } finally {
        if (!cancelled) setLoading(false)
      }
    }, 200)
    return () => { cancelled = true; clearTimeout(h) }
  }, [page, search, customerIdFilter])

  async function handleDelete(id: string) {
    if (!window.confirm('Are you sure you want to delete this account?')) return
    try {
      await bff.deleteAccount(id, crypto.randomUUID())
      setSelected(null)
      const res = await bff.getAccounts(page, PAGE_SIZE, search || undefined, customerIdFilter || undefined)
      setData(res)
    } catch (e) {
      alert((e as Error).message)
    }
  }

  return (
    <PageShell
      title="Accounts"
      subtitle={customerIdFilter ? 'Accounts for selected customer.' : 'Business relationship accounts for all customers.'}
      actions={
        <Link
          href={customerIdFilter ? `/accounts/new?customerId=${customerIdFilter}` : '/accounts/new'}
          className="rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800"
        >
          + New Account
        </Link>
      }
    >
      <FilterBar>
        <SearchBox
          value={search}
          onChange={(v) => { setPage(1); setSearch(v) }}
          placeholder="Search by name, business, city..."
        />
        {customerIdFilter && (
          <button
            type="button"
            onClick={() => router.push('/accounts')}
            className="rounded-md border border-slate-300 bg-white px-2 py-1 text-xs text-slate-600 hover:bg-slate-50"
          >
            Clear customer filter
          </button>
        )}
      </FilterBar>

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-4 py-3 text-xs text-red-700">{error}</div>
      )}

      <div className="flex">
        <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
          <DataGrid<AccountSummary>
            columns={COLUMNS}
            rows={data?.items ?? []}
            totalCount={data?.totalCount ?? 0}
            page={page}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
            onRowClick={(row) => setSelected((prev) => (prev?.id === row.id ? null : row))}
            selectedId={selected?.id ?? null}
            emptyMessage="No accounts found."
            loading={loading}
          />
        </div>

        <DetailPanel
          open={!!selected}
          onClose={() => setSelected(null)}
          title={selected?.customerDisplayName ?? ''}
          {...(selected?.natureOfBusiness ? { subtitle: selected.natureOfBusiness } : {})}
          {...(selected ? { badge: <StatusBadge tone={STATUS_TONES[selected.status] ?? 'slate'}>{selected.status}</StatusBadge> } : {})}
        >
          {selected && (
            <>
              <DetailSection title="Customer Contact">
                <DetailRow label="Name" value={selected.customerContactNameEn} />
              </DetailSection>
              <DetailSection title="Account Holder">
                <DetailRow label="Name" value={selected.accountHolderNameEn} />
              </DetailSection>
              <DetailSection title="Location">
                <DetailRow label="City" value={selected.city ?? '—'} />
              </DetailSection>
              <DetailSection title="Actions">
                <div className="flex flex-col gap-2">
                  <Link href={`/accounts/${selected.id}`} className="block w-full rounded-md bg-brand-700 px-3 py-1.5 text-center text-xs font-medium text-white hover:bg-brand-800">
                    View / Edit
                  </Link>
                  <Link href={`/customers/${selected.customerId}`} className="block w-full rounded-md border border-slate-300 bg-white px-3 py-1.5 text-center text-xs font-medium text-slate-700 hover:bg-slate-50">
                    View Customer
                  </Link>
                  <button type="button" onClick={() => handleDelete(selected.id)} className="w-full rounded-md border border-red-300 bg-white px-3 py-1.5 text-xs font-medium text-red-700 hover:bg-red-50">
                    Delete Account
                  </button>
                </div>
              </DetailSection>
            </>
          )}
        </DetailPanel>
      </div>
    </PageShell>
  )
}
