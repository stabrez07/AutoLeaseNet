'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type DriverSummary, type PagedResult } from '../../lib/bff-client'
import {
  Badge,
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
} from '../../components/ui'

export default function DriversPage() {
  const { t, locale } = useLocale()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<DriverSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const pageSize = 20

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await bff.getDrivers(page, pageSize, search || undefined))
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
  }, [page, search])

  const totalPages = data?.totalPages ?? 1

  return (
    <div className="space-y-4">
      <PageHeader
        title={t.drivers.title}
        subtitle={t.drivers.subtitle}
        action={
          <PrimaryButton onClick={() => router.push('/drivers/new')}>
            + {t.crudDrivers.newTitle}
          </PrimaryButton>
        }
      />
      <Toolbar>
        <SearchInput
          value={search}
          onChange={(value) => { setPage(1); setSearch(value) }}
          placeholder={t.drivers.searchPlaceholder}
          className="md:w-96"
        />
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
                <TableHeadCell>{t.drivers.columns.name}</TableHeadCell>
                <TableHeadCell>{t.drivers.columns.license}</TableHeadCell>
                <TableHeadCell>{t.drivers.columns.licenseExpiry}</TableHeadCell>
                <TableHeadCell>{t.drivers.columns.status}</TableHeadCell>
                <TableHeadCell>{t.common.actions}</TableHeadCell>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 && (
                <tr><td colSpan={5} className="px-3 py-8 text-center text-slate-500">{t.drivers.empty}</td></tr>
              )}
              {data.items.map((d) => {
                const name = (locale === 'ar' ? d.personNameAr : d.personNameEn) ?? d.personNameEn ?? d.personNameAr ?? '—'
                const expiry = d.licenseExpiryDate?.substring(0, 10) ?? '—'
                const isExpiringSoon = d.licenseExpiryDate
                  ? new Date(d.licenseExpiryDate) < new Date(Date.now() + 30 * 86400000)
                  : false
                return (
                  <tr key={d.id} className="cursor-pointer border-t border-slate-100 transition hover:bg-brand-50/60"
                    onClick={() => router.push(`/drivers/${d.id}`)}>
                    <TableCell className="font-medium">{name}</TableCell>
                    <TableCell className="font-mono text-xs">{d.driverLicenseNumber}</TableCell>
                    <TableCell className="font-mono text-xs">
                      <span className={isExpiringSoon ? 'text-red-600 font-semibold' : ''}>{expiry}</span>
                    </TableCell>
                    <TableCell>
                      <Badge tone={d.status === 1 ? 'green' : d.status === 2 ? 'amber' : 'slate'}>
                        {(t.drivers.statuses as Record<number, string>)[d.status] ?? d.status}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <SecondaryButton
                        onClick={(e) => { e.stopPropagation(); router.push(`/drivers/${d.id}`) }}
                        className="px-2 py-1 text-xs"
                      >
                        {t.common.viewDetails}
                      </SecondaryButton>
                    </TableCell>
                  </tr>
                )
              })}
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
