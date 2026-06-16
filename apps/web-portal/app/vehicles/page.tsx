'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type PagedResult, type VehicleSummary } from '../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../components/ui'

const STATUS_TONES: Record<number, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  1: 'green', 2: 'blue', 3: 'amber', 4: 'slate', 5: 'red',
}

export default function VehiclesPage() {
  const { t } = useLocale()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<number | ''>('')
  const [data, setData] = useState<PagedResult<VehicleSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await bff.getVehicles(1, 50, search || undefined, statusFilter === '' ? undefined : statusFilter))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const handle = setTimeout(load, 200)
    return () => clearTimeout(handle)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search, statusFilter])

  return (
    <div className="space-y-4">
      <PageHeader
        title={t.vehicles.title}
        subtitle={t.vehicles.subtitle}
        action={
          <button
            onClick={() => router.push('/vehicles/new')}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            + {t.crudVehicles.newTitle}
          </button>
        }
      />
      <Card className="flex flex-col gap-3 p-3 md:flex-row md:items-center">
        <input
          type="search" value={search} onChange={(e) => setSearch(e.target.value)}
          placeholder={t.vehicles.searchPlaceholder}
          className="focus:border-brand-500 focus:ring-brand-500 w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-1 md:w-72"
        />
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value === '' ? '' : Number(e.target.value))}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        >
          <option value="">— {t.vehicles.columns.status} —</option>
          {[1, 2, 3, 4, 5].map((s) => (
            <option key={s} value={s}>{(t.vehicles.statuses as Record<number, string>)[s]}</option>
          ))}
        </select>
      </Card>

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {loading && <Spinner label={t.common.loading} />}

      {!loading && data && (
        <Card className="overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-slate-100 text-slate-700">
              <tr>
                <th className="px-3 py-2 text-start font-medium">{t.vehicles.columns.plate}</th>
                <th className="px-3 py-2 text-start font-medium">{t.vehicles.columns.make}</th>
                <th className="px-3 py-2 text-start font-medium">{t.vehicles.columns.model}</th>
                <th className="px-3 py-2 text-start font-medium">{t.vehicles.columns.status}</th>
                <th className="px-3 py-2 text-end font-medium">{t.vehicles.columns.odometer}</th>
                <th className="px-3 py-2 text-start font-medium">{t.common.actions}</th>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 && (
                <tr><td colSpan={6} className="px-3 py-6 text-center text-slate-500">{t.vehicles.empty}</td></tr>
              )}
              {data.items.map((v) => (
                <tr key={v.id} className="cursor-pointer border-t border-slate-100 hover:bg-slate-50"
                  onClick={() => router.push(`/vehicles/${v.id}`)}>
                  <td className="px-3 py-2 font-mono text-xs">{v.plateNumber}</td>
                  <td className="px-3 py-2">{v.make}</td>
                  <td className="px-3 py-2">{v.model}</td>
                  <td className="px-3 py-2">
                    <Badge tone={STATUS_TONES[v.status] ?? 'slate'}>
                      {(t.vehicles.statuses as Record<number, string>)[v.status] ?? v.status}
                    </Badge>
                  </td>
                  <td className="px-3 py-2 text-end font-mono">{v.currentKm.toLocaleString()}</td>
                  <td className="px-3 py-2">
                    <button onClick={(e) => { e.stopPropagation(); router.push(`/vehicles/${v.id}`) }}
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
