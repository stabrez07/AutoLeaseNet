'use client'

import { useEffect, useMemo, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type CustomerSummary, type PagedResult } from '../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../components/ui'

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
          <button
            onClick={() => router.push('/customers/new')}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            + {t.customers.title.replace('Customers', 'Customer').replace('العملاء', 'عميل')}
          </button>
        }
      />
      <Card className="flex flex-col gap-3 p-3 md:flex-row md:items-center">
        <input
          type="search"
          value={search}
          onChange={(e) => { setPage(1); setSearch(e.target.value) }}
          placeholder={t.customers.searchPlaceholder}
          className="focus:border-brand-500 focus:ring-brand-500 w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-1 md:w-72"
        />
        <select
          value={typeFilter}
          onChange={(e) => setTypeFilter(e.target.value === '' ? '' : Number(e.target.value))}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        >
          <option value="">— {t.customers.columns.type} —</option>
          <option value={1}>{t.customers.type.b2b}</option>
          <option value={2}>{t.customers.type.b2c}</option>
        </select>
      </Card>

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {loading && <Spinner label={t.common.loading} />}

      {!loading && data && (
        <Card className="overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-slate-100 text-slate-700">
              <tr>
                <th className="px-3 py-2 text-start font-medium">{t.customers.columns.displayName}</th>
                <th className="px-3 py-2 text-start font-medium">{t.customers.columns.type}</th>
                <th className="px-3 py-2 text-start font-medium">{t.customers.columns.mobile}</th>
                <th className="px-3 py-2 text-start font-medium">{t.customers.columns.status}</th>
                <th className="px-3 py-2 text-start font-medium">{t.common.actions}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-3 py-6 text-center text-slate-500">{t.customers.empty}</td>
                </tr>
              )}
              {filtered.map((c) => (
                <tr key={c.id} className="cursor-pointer border-t border-slate-100 hover:bg-slate-50"
                  onClick={() => router.push(`/customers/${c.id}`)}>
                  <td className="px-3 py-2 font-medium">{c.displayName}</td>
                  <td className="px-3 py-2">
                    <Badge tone={c.type === 1 ? 'blue' : 'slate'}>
                      {c.type === 1 ? t.customers.type.b2b : t.customers.type.b2c}
                    </Badge>
                  </td>
                  <td className="px-3 py-2 font-mono text-xs">{c.mobile ?? '—'}</td>
                  <td className="px-3 py-2">
                    <Badge tone={c.isActive ? 'green' : 'slate'}>
                      {c.isActive ? t.customers.status.active : t.customers.status.inactive}
                    </Badge>
                  </td>
                  <td className="px-3 py-2">
                    <button
                      onClick={(e) => { e.stopPropagation(); router.push(`/customers/${c.id}`) }}
                      className="rounded border border-slate-200 bg-white px-2 py-0.5 text-xs hover:bg-slate-50"
                    >
                      {t.common.viewDetails}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="flex items-center justify-between border-t border-slate-100 bg-slate-50 px-3 py-2 text-xs text-slate-600">
            <div>{t.table.page} {page} {t.table.of} {totalPages} · {t.table.total}: {data.totalCount}</div>
            <div className="flex gap-2">
              <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1}
                className="rounded border border-slate-300 bg-white px-2 py-1 disabled:opacity-40">{t.table.previous}</button>
              <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages}
                className="rounded border border-slate-300 bg-white px-2 py-1 disabled:opacity-40">{t.table.next}</button>
            </div>
          </div>
        </Card>
      )}
    </div>
  )
}
