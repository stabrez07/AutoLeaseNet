'use client'

import Link from 'next/link'
import { useEffect, useMemo, useState } from 'react'
import { useLocale } from '../../lib/locale-provider'
import { bff, type PagedResult, type QuotationSummary } from '../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../components/ui'

const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Draft: 'slate',
  PendingApproval: 'amber',
  Approved: 'blue',
  SentToCustomer: 'blue',
  Accepted: 'green',
  Rejected: 'red',
  Expired: 'red',
  Withdrawn: 'slate',
}

export default function QuotationsPage() {
  const { t } = useLocale()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<QuotationSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const pageSize = 20

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await bff.getQuotations(page, pageSize, search || undefined))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const h = setTimeout(load, 200)
    return () => clearTimeout(h)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search])

  const totalPages = useMemo(() => data?.totalPages ?? 1, [data])
  const q = t.quotations

  return (
    <div className="space-y-4">
      <PageHeader
        title={q.title}
        subtitle={q.subtitle}
        action={
          <Link
            href="/quotations/new"
            className="bg-brand-600 hover:bg-brand-700 inline-flex items-center rounded-md px-4 py-2 text-sm font-medium text-white shadow-sm"
          >
            + {q.newButton}
          </Link>
        }
      />

      <Card className="p-3">
        <input
          type="search"
          value={search}
          onChange={(e) => { setPage(1); setSearch(e.target.value) }}
          placeholder={q.searchPlaceholder}
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
                <th className="px-3 py-2 text-start font-medium">{q.columns.quoteNumber}</th>
                <th className="px-3 py-2 text-start font-medium">{q.columns.contractType}</th>
                <th className="px-3 py-2 text-end font-medium">{q.columns.total}</th>
                <th className="px-3 py-2 text-start font-medium">{q.columns.status}</th>
                <th className="px-3 py-2 text-start font-medium">{q.columns.validUntil}</th>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-3 py-6 text-center text-slate-500">{q.empty}</td>
                </tr>
              )}
              {data.items.map((qt) => (
                <tr key={qt.id} className="border-t border-slate-100 hover:bg-slate-50">
                  <td className="px-3 py-2">
                    <Link href={`/quotations/${qt.id}`} className="font-mono text-xs font-medium text-blue-600 hover:underline">
                      {qt.quoteNumber}
                    </Link>
                  </td>
                  <td className="px-3 py-2 text-xs text-slate-600">
                    {q.contractTypes[qt.contractType as keyof typeof q.contractTypes] ?? qt.contractType}
                  </td>
                  <td className="px-3 py-2 text-end font-mono font-medium">
                    {qt.totalSar.toLocaleString('en-SA', { minimumFractionDigits: 2 })}
                  </td>
                  <td className="px-3 py-2">
                    <Badge tone={STATUS_TONES[qt.status] ?? 'slate'}>
                      {q.statuses[qt.status as keyof typeof q.statuses] ?? qt.status}
                    </Badge>
                  </td>
                  <td className="px-3 py-2 text-xs text-slate-600">{qt.validUntilDate}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Pagination */}
          <div className="flex items-center justify-between border-t border-slate-100 bg-slate-50 px-3 py-2 text-xs text-slate-600">
            <span>{t.table.page} {page} {t.table.of} {totalPages} · {t.table.total}: {data.totalCount}</span>
            <div className="flex gap-2">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page <= 1}
                className="rounded border border-slate-300 bg-white px-2 py-1 disabled:opacity-40">
                {t.table.previous}
              </button>
              <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page >= totalPages}
                className="rounded border border-slate-300 bg-white px-2 py-1 disabled:opacity-40">
                {t.table.next}
              </button>
            </div>
          </div>
        </Card>
      )}
    </div>
  )
}
