'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type PagedResult, type VehicleSummary } from '../../lib/bff-client'
import {
  Badge,
  DataTableMeta,
  ErrorBox,
  PageHeader,
  PrimaryButton,
  SearchInput,
  SecondaryButton,
  Spinner,
  Toolbar,
  ToolbarGroup,
} from '../../components/ui'

const STATUS_TONES: Record<number, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  1: 'green', 2: 'blue', 3: 'amber', 4: 'slate', 5: 'red',
}

const STATUS_LABELS: Record<number, string> = {
  1: 'Available', 2: 'Reserved', 3: 'On Lease', 4: 'In Service', 5: 'Retired',
}

const FUEL_ICONS: Record<string, string> = {
  Petrol91: '⛽', Petrol95: '⛽', Diesel: '🛢', Hybrid: '🔋', Electric: '⚡',
}

function VehicleCard({ v, onView, onDelete, deleting }: {
  v: VehicleSummary
  onView: () => void
  onDelete: (e: React.MouseEvent) => void
  deleting: boolean
}) {
  const [imgError, setImgError] = useState(false)
  const isOnLease = v.status === 3

  return (
    <div
      className={`group relative flex flex-col overflow-hidden rounded-xl border bg-white shadow-sm transition hover:shadow-md cursor-pointer
        ${isOnLease ? 'border-amber-200' : 'border-slate-200/80'}`}
      onClick={onView}
    >
      {/* Car image */}
      <div className="relative aspect-[16/9] w-full overflow-hidden bg-slate-100">
        {v.thumbnailUrl && !imgError ? (
          <img
            src={v.thumbnailUrl}
            alt={`${v.make} ${v.model}`}
            className="h-full w-full object-cover transition group-hover:scale-105"
            onError={() => setImgError(true)}
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center bg-gradient-to-br from-slate-100 to-slate-200">
            <span className="text-5xl opacity-40">🚗</span>
          </div>
        )}
        {/* Status badge overlay */}
        <div className="absolute top-2 right-2">
          <Badge tone={STATUS_TONES[v.status] ?? 'slate'}>
            {STATUS_LABELS[v.status] ?? v.status}
          </Badge>
        </div>
        {/* Color dot overlay */}
        {v.color && (
          <div className="absolute bottom-2 left-2">
            <span className="rounded-full bg-white/90 px-2 py-0.5 text-xs font-medium text-slate-700 shadow-sm">
              {v.color}
            </span>
          </div>
        )}
      </div>

      {/* Card body */}
      <div className="flex flex-1 flex-col gap-2 p-3">
        {/* Plate */}
        <div className="font-mono text-sm font-bold tracking-widest text-slate-800">
          {v.plateNumber}
        </div>

        {/* Make / Model / Year */}
        <div>
          <p className="text-base font-semibold leading-tight text-slate-900">
            {v.make} {v.model}
          </p>
          {v.modelYear && (
            <p className="text-xs text-slate-500">{v.modelYear}</p>
          )}
        </div>

        {/* Spec pills */}
        <div className="flex flex-wrap gap-1">
          {v.bodyType && (
            <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{v.bodyType}</span>
          )}
          {v.fuelType && (
            <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">
              {FUEL_ICONS[v.fuelType] ?? ''} {v.fuelType}
            </span>
          )}
          {v.transmissionType && (
            <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{v.transmissionType}</span>
          )}
          {v.seats != null && (
            <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{v.seats} seats</span>
          )}
        </div>

        {/* KM */}
        <p className="text-xs text-slate-500">
          {v.currentKm.toLocaleString()} km
        </p>

        {/* Actions */}
        <div className="mt-auto flex gap-1 pt-1">
          <SecondaryButton
            onClick={(e) => { e.stopPropagation(); onView() }}
            className="flex-1 px-2 py-1 text-xs"
          >
            View Details
          </SecondaryButton>
          <button
            type="button"
            disabled={deleting || isOnLease}
            onClick={onDelete}
            title={isOnLease ? 'Cannot delete — on active lease' : 'Delete'}
            className="rounded border border-red-200 bg-red-50 px-2 py-1 text-xs font-medium text-red-600 transition hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {deleting ? '…' : 'Del'}
          </button>
        </div>
      </div>
    </div>
  )
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
  const pageSize = 24

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

  function downloadCsv() {
    if (!data) return
    const rows = [
      ['Plate', 'Make', 'Model', 'Year', 'Color', 'Body Type', 'Fuel', 'Transmission', 'Seats', 'Status', 'KM'],
      ...data.items.map((v) => [
        v.plateNumber, v.make, v.model, v.modelYear ?? '', v.color ?? '', v.bodyType ?? '',
        v.fuelType ?? '', v.transmissionType ?? '', v.seats ?? '',
        STATUS_LABELS[v.status] ?? v.status, v.currentKm,
      ]),
    ]
    const csv = rows.map((r) => r.map(String).join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `vehicles-${new Date().toISOString().substring(0, 10)}.csv`; a.click()
  }

  const totalPages = data?.totalPages ?? 1

  return (
    <div className="space-y-4">
      <PageHeader
        title={t.vehicles.title}
        subtitle={t.vehicles.subtitle}
        action={
          <div className="flex gap-2">
            <SecondaryButton onClick={downloadCsv}>⬇ Export CSV</SecondaryButton>
            <SecondaryButton onClick={() => router.push('/vehicles/bulk-upload')}>{t.crudVehicles.actions.bulkUpload}</SecondaryButton>
            <PrimaryButton onClick={() => router.push('/vehicles/new')}>+ {t.crudVehicles.newTitle}</PrimaryButton>
          </div>
        }
      />

      <Toolbar>
        <ToolbarGroup>
          <SearchInput value={search} onChange={(value) => { setPage(1); setSearch(value) }} placeholder={t.vehicles.searchPlaceholder} />
          <select
            value={statusFilter}
            onChange={(e) => { setPage(1); setStatusFilter(e.target.value === '' ? '' : Number(e.target.value)) }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
          >
            <option value="">— Status —</option>
            {[1, 2, 3, 4, 5].map((s) => (
              <option key={s} value={s}>{STATUS_LABELS[s]}</option>
            ))}
          </select>
        </ToolbarGroup>
        <div className="text-xs text-slate-500">{t.table.total}: {data?.totalCount ?? 0}</div>
      </Toolbar>

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {loading && <Spinner label={t.common.loading} />}

      {!loading && data && (
        <>
          <DataTableMeta>{t.table.page} {page} {t.table.of} {totalPages} — {data.totalCount} vehicles</DataTableMeta>

          {data.items.length === 0 ? (
            <div className="rounded-xl border border-slate-200 bg-white py-16 text-center text-slate-500">
              {t.vehicles.empty}
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4">
              {data.items.map((v) => (
                <VehicleCard
                  key={v.id}
                  v={v}
                  onView={() => router.push(`/vehicles/${v.id}`)}
                  onDelete={(e) => handleDelete(e, v.id)}
                  deleting={deletingId === v.id}
                />
              ))}
            </div>
          )}

          <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50/70 px-3 py-2 text-xs text-slate-600">
            <div>{t.table.total}: {data.totalCount}</div>
            <div className="flex gap-2">
              <SecondaryButton onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="px-2 py-1 text-xs">{t.table.previous}</SecondaryButton>
              <SecondaryButton onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="px-2 py-1 text-xs">{t.table.next}</SecondaryButton>
            </div>
          </div>
        </>
      )}
    </div>
  )
}
