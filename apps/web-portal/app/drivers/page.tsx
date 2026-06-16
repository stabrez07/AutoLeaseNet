'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type DriverSummary, type PagedResult } from '../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner } from '../../components/ui'

export default function DriversPage() {
  const { t, locale } = useLocale()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [data, setData] = useState<PagedResult<DriverSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await bff.getDrivers(1, 50, search || undefined))
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
  }, [search])

  return (
    <div className="space-y-4">
      <PageHeader
        title={t.drivers.title}
        subtitle={t.drivers.subtitle}
        action={
          <button onClick={() => router.push('/drivers/new')}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700">
            + {t.crudDrivers.newTitle}
          </button>
        }
      />
      <Card className="p-3">
        <input type="search" value={search} onChange={(e) => setSearch(e.target.value)}
          placeholder={t.drivers.searchPlaceholder}
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
                <th className="px-3 py-2 text-start font-medium">{t.drivers.columns.name}</th>
                <th className="px-3 py-2 text-start font-medium">{t.drivers.columns.license}</th>
                <th className="px-3 py-2 text-start font-medium">{t.drivers.columns.licenseExpiry}</th>
                <th className="px-3 py-2 text-start font-medium">{t.drivers.columns.status}</th>
                <th className="px-3 py-2 text-start font-medium">{t.common.actions}</th>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 && (
                <tr><td colSpan={5} className="px-3 py-6 text-center text-slate-500">{t.drivers.empty}</td></tr>
              )}
              {data.items.map((d) => {
                const name = (locale === 'ar' ? d.personNameAr : d.personNameEn) ?? d.personNameEn ?? d.personNameAr ?? '—'
                const expiry = d.licenseExpiryDate?.substring(0, 10) ?? '—'
                const isExpiringSoon = d.licenseExpiryDate
                  ? new Date(d.licenseExpiryDate) < new Date(Date.now() + 30 * 86400000)
                  : false
                return (
                  <tr key={d.id} className="cursor-pointer border-t border-slate-100 hover:bg-slate-50"
                    onClick={() => router.push(`/drivers/${d.id}`)}>
                    <td className="px-3 py-2 font-medium">{name}</td>
                    <td className="px-3 py-2 font-mono text-xs">{d.driverLicenseNumber}</td>
                    <td className="px-3 py-2 font-mono text-xs">
                      <span className={isExpiringSoon ? 'text-red-600 font-semibold' : ''}>{expiry}</span>
                    </td>
                    <td className="px-3 py-2">
                      <Badge tone={d.status === 1 ? 'green' : d.status === 2 ? 'amber' : 'slate'}>
                        {(t.drivers.statuses as Record<number, string>)[d.status] ?? d.status}
                      </Badge>
                    </td>
                    <td className="px-3 py-2">
                      <button onClick={(e) => { e.stopPropagation(); router.push(`/drivers/${d.id}`) }}
                        className="rounded border border-slate-200 bg-white px-2 py-0.5 text-xs hover:bg-slate-50">
                        {t.common.viewDetails}
                      </button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  )
}
