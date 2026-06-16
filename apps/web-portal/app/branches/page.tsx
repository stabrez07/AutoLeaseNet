'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type BranchDto } from '../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../components/ui'

export default function BranchesPage() {
  const { t, locale } = useLocale()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [data, setData] = useState<BranchDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function load() {
    setError(null)
    setLoading(true)
    try {
      setData(await bff.getBranches())
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  const filtered = data?.filter((b) => {
    if (!search) return true
    const s = search.toLowerCase()
    return b.code.toLowerCase().includes(s) || b.nameEn.toLowerCase().includes(s) || b.nameAr.toLowerCase().includes(s)
  }) ?? []

  return (
    <div className="space-y-4">
      <PageHeader
        title={t.branches.title}
        subtitle={t.branches.subtitle}
        action={
          <button onClick={() => router.push('/branches/new')}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700">
            + {t.crudBranches.newTitle}
          </button>
        }
      />
      <Card className="p-3">
        <input type="search" value={search} onChange={(e) => setSearch(e.target.value)}
          placeholder="Search branches…"
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
                <th className="px-3 py-2 text-start font-medium">{t.branches.columns.code}</th>
                <th className="px-3 py-2 text-start font-medium">{t.branches.columns.name}</th>
                <th className="px-3 py-2 text-start font-medium">{t.branches.columns.city}</th>
                <th className="px-3 py-2 text-start font-medium">{t.branches.columns.active}</th>
                <th className="px-3 py-2 text-start font-medium">{t.common.actions}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((b) => (
                <tr key={b.id} className="cursor-pointer border-t border-slate-100 hover:bg-slate-50"
                  onClick={() => router.push(`/branches/${b.id}`)}>
                  <td className="px-3 py-2 font-mono text-xs font-semibold">{b.code}</td>
                  <td className="px-3 py-2">{locale === 'ar' ? b.nameAr : b.nameEn}</td>
                  <td className="px-3 py-2">{b.city ?? '—'}</td>
                  <td className="px-3 py-2">
                    <Badge tone={b.isActive ? 'green' : 'slate'}>
                      {b.isActive ? t.branches.yes : t.branches.no}
                    </Badge>
                  </td>
                  <td className="px-3 py-2">
                    <button onClick={(e) => { e.stopPropagation(); router.push(`/branches/${b.id}`) }}
                      className="rounded border border-slate-200 bg-white px-2 py-0.5 text-xs hover:bg-slate-50">
                      {t.common.viewDetails}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  )
}
