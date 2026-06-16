'use client'

import { useEffect, useMemo, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type CustomerSummary, type PagedResult } from '../../lib/bff-client'
import {
  Badge,
  DataTable,
  DataTableMeta,
  ErrorBox,
  FilterSelect,
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

export default function CustomersPage() {
  const { t } = useLocale()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [typeFilter, setTypeFilter] = useState<number | ''>('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<CustomerSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const pageSize = 20

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await bff.getCustomers(page, pageSize, search || undefined))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const handle = setTimeout(() => { load() }, 200)
    return () => clearTimeout(handle)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search, typeFilter])

  const totalPages = useMemo(() => data?.totalPages ?? 1, [data])
  const filtered = useMemo(
    () => (typeFilter === '' ? data?.items : data?.items.filter((c) => c.type === typeFilter)) ?? [],
    [data, typeFilter],
  )

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
          <SearchInput
            value={search}
            onChange={(value) => { setPage(1); setSearch(value) }}
            placeholder={t.customers.searchPlaceholder}
          />
          <FilterSelect
            value={typeFilter}
            onChange={(value) => { setPage(1); setTypeFilter(value) }}
          >
            <option value="">— {t.customers.columns.type} —</option>
            <option value={1}>{t.customers.type.b2b}</option>
            <option value={2}>{t.customers.type.b2c}</option>
          </FilterSelect>
        </ToolbarGroup>
        <div className="text-xs text-slate-500">{t.table.total}: {data?.totalCount ?? 0}</div>
      </Toolbar>

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {loading && <Spinner label={t.common.loading} />}

      {!loading && data && (
        <DataTable>
          <DataTableMeta>{t.table.page} {page} {t.table.of} {totalPages}</DataTableMeta>
          <table className="w-full text-sm">
            <thead className="border-b border-slate-200 bg-white">
              <tr>
                <TableHeadCell>{t.customers.columns.displayName}</TableHeadCell>
                <TableHeadCell>{t.customers.columns.type}</TableHeadCell>
                <TableHeadCell>{t.customers.columns.mobile}</TableHeadCell>
                <TableHeadCell>{t.customers.columns.status}</TableHeadCell>
                <TableHeadCell>{t.common.actions}</TableHeadCell>
              </tr>
            </thead>
            <tbody>
              {filtered.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-3 py-8 text-center text-slate-500">{t.customers.empty}</td>
                </tr>
              )}
              {filtered.map((c) => (
                <tr key={c.id} className="cursor-pointer border-t border-slate-100 transition hover:bg-brand-50/60"
                  onClick={() => router.push(`/customers/${c.id}`)}>
                  <TableCell className="font-medium text-slate-900">{c.displayName}</TableCell>
                  <TableCell>
                    <Badge tone={c.type === 1 ? 'blue' : 'slate'}>
                      {c.type === 1 ? t.customers.type.b2b : t.customers.type.b2c}
                    </Badge>
                  </TableCell>
                  <TableCell className="font-mono text-xs">{c.mobile ?? '—'}</TableCell>
                  <TableCell>
                    <Badge tone={c.isActive ? 'green' : 'slate'}>
                      {c.isActive ? t.customers.status.active : t.customers.status.inactive}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <SecondaryButton
                      onClick={(e) => { e.stopPropagation(); router.push(`/customers/${c.id}`) }}
                      className="px-2 py-1 text-xs"
                    >
                      {t.common.viewDetails}
                    </SecondaryButton>
                  </TableCell>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50/70 px-3 py-2 text-xs text-slate-600">
            <div>{t.table.total}: {data.totalCount}</div>
            <div className="flex gap-2">
              <SecondaryButton
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="px-2 py-1 text-xs"
              >
                {t.table.previous}
              </SecondaryButton>
              <SecondaryButton
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="px-2 py-1 text-xs"
              >
                {t.table.next}
              </SecondaryButton>
            </div>
          </div>
        </DataTable>
      )}
    </div>
  )
}
