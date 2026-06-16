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
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const pageSize = 20

  async function load() {
    setLoading(true); setError(null)
    try {
      setData(await bff.getVehicles(page, pageSize, search || undefined, statusFilter === '' ? undefined : statusFilter))
    } catch (e) {
      setError((e as Error).message)
    } finally { setLoading(false) }
  }

  useEffect(() => {
    const handle = setTimeout(load, 200)
    return () => clearTimeout(handle)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search, statusFilter])

  async function handleDelete(e: React.MouseEvent, id: string) {
    e.stopPropagation()
    if (!confirm(t.common.confirmDelete)) return
    setDeletingId(id)
    try {
      await bff.deleteVehicle(id, crypto.randomUUID())
      await load()
    } catch (err) {
      alert((err as Error).message)
    } finally { setDeletingId(null) }
  }

  const totalPages = data?.totalPages ?? 1

  return (
    <div className="space-y-4">
      <PageHeader
        title={t.vehicles.title}
        subtitle={t.vehicles.subtitle}
        action={
          <div className="flex gap-2">
            <SecondaryButton onClick={() => router.push('/vehicles/bulk-upload')}>{t.crudVehicles.actions.bulkUpload}</SecondaryButton>
            <PrimaryButton onClick={() => router.push('/vehicles/new')}>+ {t.crudVehicles.newTitle}</PrimaryButton>
          </div>
        }
      />
      <Toolbar>
        <ToolbarGroup>
          <SearchInput value={search} onChange={(value) => { setPage(1); setSearch(value) }} placeholder={t.vehicles.searchPlaceholder} />
          <FilterSelect value={statusFilter} onChange={(value) => { setPage(1); setStatusFilter(value) }}>
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
                <TableHeadCell>Plate</TableHeadCell>
                <TableHeadCell>{t.vehicles.columns.make}</TableHeadCell>
                <TableHeadCell>{t.vehicles.columns.model}</TableHeadCell>
                <TableHeadCell>Year</TableHeadCell>
                <TableHeadCell>{t.vehicles.columns.status}</TableHeadCell>
                <TableHeadCell align="end">{t.vehicles.columns.odometer}</TableHeadCell>
                <TableHeadCell>{t.common.actions}</TableHeadCell>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 && (
                <tr><td colSpan={7} className="px-3 py-8 text-center text-slate-500">{t.vehicles.empty}</td></tr>
              )}
              {data.items.map((v) => {
                const isOnLease = v.status === 3
                return (
                  <tr key={v.id}
                    className={`cursor-pointer border-t border-slate-100 transition hover:bg-brand-50/60 ${isOnLease ? 'bg-amber-50/30' : ''}`}
                    onClick={() => router.push(`/vehicles/${v.id}`)}>
                    <TableCell className="font-mono text-xs font-semibold">{v.plateNumber}</TableCell>
                    <TableCell>{v.make}</TableCell>
                    <TableCell>{v.model}</TableCell>
                    <TableCell className="text-slate-600">{v.modelYear ?? '—'}</TableCell>
                    <TableCell>
                      <Badge tone={STATUS_TONES[v.status] ?? 'slate'}>
                        {(t.vehicles.statuses as Record<number, string>)[v.status] ?? v.status}
                      </Badge>
                    </TableCell>
                    <TableCell align="end" className="font-mono text-xs">{v.currentKm.toLocaleString()}</TableCell>
                    <TableCell>
                      <div className="flex gap-1">
                        <SecondaryButton onClick={(e) => { e.stopPropagation(); router.push(`/vehicles/${v.id}`) }} className="px-2 py-1 text-xs">
                          {t.common.viewDetails}
                        </SecondaryButton>
                        <button
                          type="button"
                          disabled={deletingId === v.id || isOnLease}
                          onClick={(e) => handleDelete(e, v.id)}
                          title={isOnLease ? 'Cannot delete — vehicle is on active lease' : t.common.delete}
                          className="rounded border border-red-200 bg-red-50 px-2 py-1 text-xs font-medium text-red-600 transition hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-40"
                        >
                          {deletingId === v.id ? t.common.deleting : t.common.delete}
                        </button>
                      </div>
                    </TableCell>
                  </tr>
                )
              })}
            </tbody>
          </table>
          <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50/70 px-3 py-2 text-xs text-slate-600">
            <div>{t.table.total}: {data.totalCount}</div>
            <div className="flex gap-2">
              <SecondaryButton onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="px-2 py-1 text-xs">{t.table.previous}</SecondaryButton>
              <SecondaryButton onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="px-2 py-1 text-xs">{t.table.next}</SecondaryButton>
            </div>
          </div>
        </DataTable>
      )}
    </div>
  )
}
