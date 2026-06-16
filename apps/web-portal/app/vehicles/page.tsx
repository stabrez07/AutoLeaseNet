'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type PagedResult, type VehicleSummary } from '../../lib/bff-client'
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

const STATUS_TONES: Record<number, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  1: 'green', 2: 'blue', 3: 'amber', 4: 'slate', 5: 'red',
}

export default function VehiclesPage() {
  const { t } = useLocale()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<number | ''>('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<VehicleSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const pageSize = 20

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await bff.getVehicles(page, pageSize, search || undefined, statusFilter === '' ? undefined : statusFilter))
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
  }, [page, search, statusFilter])

  const totalPages = data?.totalPages ?? 1

  return (
    <div className="space-y-4">
      <PageHeader
        title={t.vehicles.title}
        subtitle={t.vehicles.subtitle}
        action={
          <PrimaryButton onClick={() => router.push('/vehicles/new')}>+ {t.crudVehicles.newTitle}</PrimaryButton>
        }
      />
      <Toolbar>
        <ToolbarGroup>
          <SearchInput
            value={search}
            onChange={(value) => { setPage(1); setSearch(value) }}
            placeholder={t.vehicles.searchPlaceholder}
          />
          <FilterSelect
            value={statusFilter}
            onChange={(value) => { setPage(1); setStatusFilter(value) }}
          >
            <option value="">— {t.vehicles.columns.status} —</option>
            {[1, 2, 3, 4, 5].map((s) => (
              <option key={s} value={s}>{(t.vehicles.statuses as Record<number, string>)[s]}</option>
            ))}
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
                <TableHeadCell>{t.vehicles.columns.plate}</TableHeadCell>
                <TableHeadCell>{t.vehicles.columns.make}</TableHeadCell>
                <TableHeadCell>{t.vehicles.columns.model}</TableHeadCell>
                <TableHeadCell>{t.vehicles.columns.status}</TableHeadCell>
                <TableHeadCell align="end">{t.vehicles.columns.odometer}</TableHeadCell>
                <TableHeadCell>{t.common.actions}</TableHeadCell>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 && (
                <tr><td colSpan={6} className="px-3 py-8 text-center text-slate-500">{t.vehicles.empty}</td></tr>
              )}
              {data.items.map((v) => (
                <tr key={v.id} className="cursor-pointer border-t border-slate-100 transition hover:bg-brand-50/60"
                  onClick={() => router.push(`/vehicles/${v.id}`)}>
                  <TableCell className="font-mono text-xs">{v.plateNumber}</TableCell>
                  <TableCell>{v.make}</TableCell>
                  <TableCell>{v.model}</TableCell>
                  <TableCell>
                    <Badge tone={STATUS_TONES[v.status] ?? 'slate'}>
                      {(t.vehicles.statuses as Record<number, string>)[v.status] ?? v.status}
                    </Badge>
                  </TableCell>
                  <TableCell align="end" className="font-mono">{v.currentKm.toLocaleString()}</TableCell>
                  <TableCell>
                    <SecondaryButton
                      onClick={(e) => { e.stopPropagation(); router.push(`/vehicles/${v.id}`) }}
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
