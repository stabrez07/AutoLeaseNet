'use client'

import { useEffect, useMemo, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type CustomerSummary, type PagedResult } from '../../lib/bff-client'
import {
  Badge,
  Card,
  DataTable,
  DataTableMeta,
  ErrorBox,
  PageHeader,
  PrimaryButton,
  SearchInput,
  SecondaryButton,
  Spinner,
  TableCell,
  TableHeadCell,
  Toolbar,
  ToolbarGroup,
} from '../../components/ui'

function ColumnFilter({ column, values, active, onChange }: {
  column: string
  values: string[]
  active: string
  onChange: (v: string) => void
}) {
  return (
    <select
      value={active}
      onChange={(e) => onChange(e.target.value)}
      onClick={(e) => e.stopPropagation()}
      className="ms-1 inline-block w-auto rounded border border-slate-200 bg-slate-50 px-1 py-0.5 text-[10px] font-normal text-slate-600"
      aria-label={`Filter ${column}`}
    >
      <option value="">All</option>
      {values.map((v) => <option key={v} value={v}>{v}</option>)}
    </select>
  )
}

function DetailField({ label, value, mono }: { label: string; value?: string | null | undefined; mono?: boolean | undefined }) {
  return (
    <div>
      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
      <div className={`mt-0.5 text-slate-900 ${mono ? 'font-mono text-xs' : ''}`}>{value ?? '—'}</div>
    </div>
  )
}

function downloadCsv(rows: CustomerSummary[]) {
  const header = ['Name', 'Type', 'CR No', 'VAT No', 'Contact Person', 'Mobile', 'Email', 'City', 'Status']
  const lines = rows.map((c) => [
    c.displayName,
    c.type === 1 ? 'B2B' : 'B2C',
    c.commercialRegistration ?? '',
    c.vatNumber ?? '',
    c.contactPerson ?? '',
    c.mobile ?? '',
    c.email ?? '',
    c.city ?? '',
    c.isActive ? 'Active' : 'Inactive',
  ].map((v) => `"${String(v).replace(/"/g, '""')}"`).join(','))
  const csv = [header.join(','), ...lines].join('\n')
  const a = document.createElement('a')
  a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
  a.download = `customers-${new Date().toISOString().substring(0, 10)}.csv`
  a.click()
}

export default function CustomersPage() {
  const { t } = useLocale()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<CustomerSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<CustomerSummary | null>(null)
  const [columnFilters, setColumnFilters] = useState<Record<string, string>>({})
  const pageSize = 30

  async function load() {
    setLoading(true); setError(null)
    try { setData(await bff.getCustomers(page, pageSize, search || undefined)) }
    catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => {
    const handle = setTimeout(() => { load() }, 200)
    return () => clearTimeout(handle)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search])

  const totalPages = useMemo(() => data?.totalPages ?? 1, [data])

  const typeLabel = (c: CustomerSummary) => c.type === 1 ? t.customers.type.b2b : t.customers.type.b2c
  const statusLabel = (c: CustomerSummary) => c.isActive ? t.customers.status.active : t.customers.status.inactive

  const uniqueTypes = useMemo(() => [...new Set((data?.items ?? []).map(typeLabel))], [data]) // eslint-disable-line react-hooks/exhaustive-deps
  const uniqueCities = useMemo(() => [...new Set((data?.items ?? []).map((c) => c.city ?? '').filter(Boolean))].sort(), [data])
  const uniqueStatuses = useMemo(() => [...new Set((data?.items ?? []).map(statusLabel))], [data]) // eslint-disable-line react-hooks/exhaustive-deps

  const filtered = useMemo(() => {
    let items = data?.items ?? []
    if (columnFilters['type']) items = items.filter((c) => typeLabel(c) === columnFilters['type'])
    if (columnFilters['city']) items = items.filter((c) => (c.city ?? '') === columnFilters['city'])
    if (columnFilters['status']) items = items.filter((c) => statusLabel(c) === columnFilters['status'])
    return items
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data, columnFilters])

  function setFilter(col: string, val: string) {
    setColumnFilters((prev) => ({ ...prev, [col]: val }))
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={t.customers.title}
        subtitle={t.customers.subtitle}
        action={
          <PrimaryButton onClick={() => router.push('/customers/new')}>+ {t.crudCustomers.newTitle}</PrimaryButton>
        }
      />

      <Toolbar>
        <ToolbarGroup>
          <SearchInput value={search} onChange={(value) => { setPage(1); setSearch(value) }} placeholder={t.customers.searchPlaceholder} />
        </ToolbarGroup>
        <ToolbarGroup>
          <SecondaryButton onClick={() => downloadCsv(filtered)} className="px-2 py-1 text-xs">Export CSV</SecondaryButton>
          <div className="text-xs text-slate-500">{t.table.total}: {data?.totalCount ?? 0}</div>
        </ToolbarGroup>
      </Toolbar>

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {loading && <Spinner label={t.common.loading} />}

      {!loading && data && (
        <DataTable>
          <DataTableMeta>{t.table.page} {page} {t.table.of} {totalPages}</DataTableMeta>
          <div className="overflow-x-auto">
            <table className="w-full text-xs">
              <thead className="border-b border-slate-200 bg-white">
                <tr>
                  <TableHeadCell>{t.customers.columns.displayName}</TableHeadCell>
                  <TableHeadCell>
                    {t.customers.columns.type}
                    <ColumnFilter column="Type" values={uniqueTypes} active={columnFilters['type'] ?? ''} onChange={(v) => setFilter('type', v)} />
                  </TableHeadCell>
                  <TableHeadCell>CR No</TableHeadCell>
                  <TableHeadCell>VAT No</TableHeadCell>
                  <TableHeadCell>Contact Person</TableHeadCell>
                  <TableHeadCell>{t.customers.columns.mobile}</TableHeadCell>
                  <TableHeadCell>Email</TableHeadCell>
                  <TableHeadCell>
                    City
                    <ColumnFilter column="City" values={uniqueCities} active={columnFilters['city'] ?? ''} onChange={(v) => setFilter('city', v)} />
                  </TableHeadCell>
                  <TableHeadCell>
                    {t.customers.columns.status}
                    <ColumnFilter column="Status" values={uniqueStatuses} active={columnFilters['status'] ?? ''} onChange={(v) => setFilter('status', v)} />
                  </TableHeadCell>
                  <TableHeadCell>{t.common.actions}</TableHeadCell>
                </tr>
              </thead>
              <tbody>
                {filtered.length === 0 && (
                  <tr><td colSpan={10} className="px-3 py-8 text-center text-slate-500">{t.customers.empty}</td></tr>
                )}
                {filtered.map((c) => (
                  <tr key={c.id}
                    className={`cursor-pointer border-t border-slate-100 transition hover:bg-brand-50/60 ${selected?.id === c.id ? 'bg-brand-50 ring-1 ring-inset ring-brand-300' : ''}`}
                    onClick={() => setSelected(selected?.id === c.id ? null : c)}
                  >
                    <TableCell className="font-medium text-slate-900">{c.displayName}</TableCell>
                    <TableCell><Badge tone={c.type === 1 ? 'blue' : 'slate'}>{typeLabel(c)}</Badge></TableCell>
                    <TableCell className="font-mono">{c.type === 1 ? (c.commercialRegistration ?? '—') : '—'}</TableCell>
                    <TableCell className="font-mono">{c.type === 1 ? (c.vatNumber ?? '—') : '—'}</TableCell>
                    <TableCell>{c.contactPerson ?? '—'}</TableCell>
                    <TableCell className="font-mono">{c.mobile ?? '—'}</TableCell>
                    <TableCell>{c.email ?? '—'}</TableCell>
                    <TableCell>{c.city ?? '—'}</TableCell>
                    <TableCell><Badge tone={c.isActive ? 'green' : 'slate'}>{statusLabel(c)}</Badge></TableCell>
                    <TableCell>
                      <SecondaryButton onClick={(e) => { e.stopPropagation(); router.push(`/customers/${c.id}`) }} className="px-2 py-1 text-xs">
                        {t.common.viewDetails}
                      </SecondaryButton>
                    </TableCell>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50/70 px-3 py-2 text-xs text-slate-600">
            <div>{t.table.total}: {data.totalCount}</div>
            <div className="flex gap-2">
              <SecondaryButton onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="px-2 py-1 text-xs">{t.table.previous}</SecondaryButton>
              <SecondaryButton onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="px-2 py-1 text-xs">{t.table.next}</SecondaryButton>
            </div>
          </div>
        </DataTable>
      )}

      {/* Inline detail panel */}
      {selected && (
        <Card className="p-5">
          <div className="mb-4 flex flex-wrap items-center gap-3">
            <h2 className="text-lg font-semibold text-slate-900">{selected.displayName}</h2>
            <Badge tone={selected.type === 1 ? 'blue' : 'slate'}>{typeLabel(selected)}</Badge>
            <Badge tone={selected.isActive ? 'green' : 'slate'}>{statusLabel(selected)}</Badge>
          </div>
          <div className="grid grid-cols-1 gap-x-8 gap-y-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
            <DetailField label="CR No" value={selected.commercialRegistration} mono />
            <DetailField label="VAT No" value={selected.vatNumber} mono />
            <DetailField label="Contact Person" value={selected.contactPerson} />
            <DetailField label="Contact Mobile" value={selected.contactPersonMobile} mono />
            <DetailField label="Email" value={selected.email} />
            <DetailField label="Mobile" value={selected.mobile} mono />
            <DetailField label="City" value={selected.city} />
            <DetailField label="ID" value={selected.id} mono />
          </div>
          <div className="mt-5 flex gap-3">
            <PrimaryButton onClick={() => router.push(`/customers/${selected.id}`)}>Open Full Details</PrimaryButton>
            <SecondaryButton onClick={() => setSelected(null)}>Close</SecondaryButton>
          </div>
        </Card>
      )}
    </div>
  )
}
