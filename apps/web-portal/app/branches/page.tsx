'use client'

import { useEffect, useState } from 'react'
import { useLocale } from '../../lib/locale-provider'
import { bff, type BranchDto } from '../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../components/ui'

export default function BranchesPage() {
  const { t, locale } = useLocale()
  const [data, setData] = useState<BranchDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setError(null)
    setData(null)
    try {
      setData(await bff.getBranches())
    } catch (e) {
      setError((e as Error).message)
    }
  }

  useEffect(() => {
    load()
  }, [])

  return (
    <div className="space-y-4">
      <PageHeader title={t.branches.title} subtitle={t.branches.subtitle} />

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {!error && !data && <Spinner label={t.common.loading} />}

      {data && (
        <Card className="overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-slate-100 text-slate-700">
              <tr>
                <th className="px-3 py-2 text-start font-medium">{t.branches.columns.code}</th>
                <th className="px-3 py-2 text-start font-medium">{t.branches.columns.name}</th>
                <th className="px-3 py-2 text-start font-medium">{t.branches.columns.city}</th>
                <th className="px-3 py-2 text-start font-medium">{t.branches.columns.active}</th>
              </tr>
            </thead>
            <tbody>
              {data.map((b) => (
                <tr key={b.id} className="border-t border-slate-100">
                  <td className="px-3 py-2 font-mono text-xs">{b.code}</td>
                  <td className="px-3 py-2">{locale === 'ar' ? b.nameAr : b.nameEn}</td>
                  <td className="px-3 py-2">{b.city ?? '—'}</td>
                  <td className="px-3 py-2">
                    <Badge tone={b.isActive ? 'green' : 'slate'}>
                      {b.isActive ? t.branches.yes : t.branches.no}
                    </Badge>
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
