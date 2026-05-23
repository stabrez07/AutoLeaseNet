'use client'

import { useEffect, useMemo, useState } from 'react'
import { useLocale } from '../../lib/locale-provider'
import { bff, type CustomerSummary, type PagedResult } from '../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../components/ui'

export default function CustomersPage() {
  const { t } = useLocale()
  const [search, setSearch] = useState('')
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
    const handle = setTimeout(() => {
      load()
    }, 200)
    return () => clearTimeout(handle)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search])

  const totalPages = useMemo(() => data?.totalPages ?? 1, [data])

  return (
    <div className="space-y-4">
      <PageHeader title={t.customers.title} subtitle={t.customers.subtitle} />
      <Card className="p-3">
        <input
          type="search"
          value={search}
          onChange={(e) => {
            setPage(1)
            setSearch(e.target.value)
          }}
          placeholder={t.customers.searchPlaceholder}
          className="focus:border-brand-500 focus:ring-brand-500 w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-1 md:w-96"
        />
      </Card>

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {loading && <Spinner label={t.common.loading} />}

      {!loading && data && (
        <Card className="overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-slate-100 text-slate-700">
              <tr>
                <th className="px-3 py-2 text-start font-medium">
                  {t.customers.columns.displayName}
                </th>
                <th className="px-3 py-2 text-start font-medium">{t.customers.columns.type}</th>
                <th className="px-3 py-2 text-start font-medium">{t.customers.columns.mobile}</th>
                <th className="px-3 py-2 text-start font-medium">{t.customers.columns.status}</th>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-3 py-6 text-center text-slate-500">
                    {t.customers.empty}
                  </td>
                </tr>
              )}
              {data.items.map((c) => (
                <tr key={c.id} className="border-t border-slate-100">
                  <td className="px-3 py-2">{c.displayName}</td>
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
                </tr>
              ))}
            </tbody>
          </table>
          <Pagination
            page={page}
            totalPages={totalPages}
            total={data.totalCount}
            onPrev={() => setPage((p) => Math.max(1, p - 1))}
            onNext={() => setPage((p) => Math.min(totalPages, p + 1))}
          />
        </Card>
      )}
    </div>
  )
}

function Pagination({
  page,
  totalPages,
  total,
  onPrev,
  onNext,
}: {
  page: number
  totalPages: number
  total: number
  onPrev: () => void
  onNext: () => void
}) {
  const { t } = useLocale()
  return (
    <div className="flex items-center justify-between border-t border-slate-100 bg-slate-50 px-3 py-2 text-xs text-slate-600">
      <div>
        {t.table.page} {page} {t.table.of} {totalPages} · {t.table.total}: {total}
      </div>
      <div className="flex gap-2">
        <button
          onClick={onPrev}
          disabled={page <= 1}
          className="rounded border border-slate-300 bg-white px-2 py-1 disabled:opacity-40"
        >
          {t.table.previous}
        </button>
        <button
          onClick={onNext}
          disabled={page >= totalPages}
          className="rounded border border-slate-300 bg-white px-2 py-1 disabled:opacity-40"
        >
          {t.table.next}
        </button>
      </div>
    </div>
  )
}
