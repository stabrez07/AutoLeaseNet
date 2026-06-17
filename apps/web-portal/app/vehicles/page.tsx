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
  1: 'green', 2: 'blue', 3: 'amber', 4: 'slate', 5: 'slate',
}

const STATUS_LABELS: Record<number, string> = {
  1: 'Available', 2: 'Reserved', 3: 'On Rent', 4: 'In Service', 5: 'Retired',
}

// bodyType values from mock use 'Suv' (mixed case), not 'SUV'
const BODY_TYPE_OPTIONS: { label: string; value: string }[] = [
  { label: 'Sedan', value: 'Sedan' },
  { label: 'SUV', value: 'Suv' },
  { label: 'Pickup', value: 'Pickup' },
  { label: 'Van', value: 'Van' },
  { label: 'Bus', value: 'Bus' },
  { label: 'Hatchback', value: 'Hatchback' },
  { label: 'Coupe', value: 'Coupe' },
]

const FUEL_TYPE_OPTIONS: { label: string; value: string }[] = [
  { label: 'Petrol 91', value: 'Petrol91' },
  { label: 'Petrol 95', value: 'Petrol95' },
  { label: 'Diesel', value: 'Diesel' },
  { label: 'Hybrid', value: 'Hybrid' },
  { label: 'Electric', value: 'Electric' },
]

const PAGE_SIZE = 30

function ThumbnailCell({
  v,
  imgErrors,
  onError,
}: {
  v: VehicleSummary
  imgErrors: Record<string, boolean>
  onError: (id: string) => void
}) {
  const hasError = imgErrors[v.id] === true
  if (v.thumbnailUrl && !hasError) {
    return (
      <img
        src={v.thumbnailUrl}
        alt={`${v.make} ${v.model}`}
        width={56}
        height={40}
        className="object-cover rounded"
        style={{ width: 56, height: 40 }}
        onError={() => onError(v.id)}
      />
    )
  }
  return (
    <span
      className="inline-flex items-center justify-center bg-slate-100 rounded text-xl"
      style={{ width: 56, height: 40 }}
      aria-label="No image"
    >
      🚗
    </span>
  )
}

export default function VehiclesPage() {
  const { t } = useLocale()
  const router = useRouter()

  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<number | ''>('')
  const [bodyTypeFilter, setBodyTypeFilter] = useState('')
  const [fuelTypeFilter, setFuelTypeFilter] = useState('')
  const [page, setPage] = useState(1)

  const [data, setData] = useState<PagedResult<VehicleSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [imgErrors, setImgErrors] = useState<Record<string, boolean>>({})

  function handleImgError(id: string) {
    setImgErrors((prev) => ({ ...prev, [id]: true }))
  }

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const statusArg = statusFilter === '' ? undefined : statusFilter
      const raw = await bff.getVehicles(page, PAGE_SIZE, search || undefined, statusArg)
      // Client-side filter for bodyType and fuelType (mock returns all, no server param)
      const filtered = raw.items.filter((v) => {
        if (bodyTypeFilter && v.bodyType !== bodyTypeFilter) return false
        if (fuelTypeFilter && v.fuelType !== fuelTypeFilter) return false
        return true
      })
      setData({ ...raw, items: filtered, totalCount: filtered.length })
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
  }, [page, search, statusFilter, bodyTypeFilter, fuelTypeFilter])

  async function handleDelete(e: React.MouseEvent, id: string) {
    e.stopPropagation()
    if (!confirm(t.common.confirmDelete)) return
    setDeletingId(id)
    try {
      await bff.deleteVehicle(id, crypto.randomUUID())
      await load()
    } catch (err) {
      alert((err as Error).message)
    } finally {
      setDeletingId(null)
    }
  }

  function downloadCsv() {
    if (!data) return
    const rows = [
      ['Plate', 'Make', 'Model', 'Year', 'Body Type', 'Fuel Type', 'Trans', 'Seats', 'Status', 'KM'],
      ...data.items.map((v) => [
        v.plateNumber,
        v.make,
        v.model,
        v.modelYear ?? '',
        v.bodyType ?? '',
        v.fuelType ?? '',
        v.transmissionType ?? '',
        v.seats ?? '',
        STATUS_LABELS[v.status] ?? String(v.status),
        v.currentKm,
      ]),
    ]
    const csv = rows.map((r) => r.map(String).join(',')).join('\n')
    const a = document.createElement('a')
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `vehicles-${new Date().toISOString().substring(0, 10)}.csv`
    a.click()
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1

  return (
    <div className="space-y-4">
      <PageHeader
        title={t.vehicles.title}
        subtitle={t.vehicles.subtitle}
        action={
          <div className="flex gap-2">
            <SecondaryButton onClick={downloadCsv}>Export CSV</SecondaryButton>
            <SecondaryButton onClick={() => router.push('/vehicles/bulk-upload')}>
              {t.crudVehicles.actions.bulkUpload}
            </SecondaryButton>
            <PrimaryButton onClick={() => router.push('/vehicles/new')}>
              + {t.crudVehicles.newTitle}
            </PrimaryButton>
          </div>
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
            <option value="">— Status —</option>
            <option value={1}>Available</option>
            <option value={2}>Reserved</option>
            <option value={3}>On Rent</option>
            <option value={4}>In Service</option>
            <option value={5}>Retired</option>
          </FilterSelect>
          <select
            value={bodyTypeFilter}
            onChange={(e) => { setPage(1); setBodyTypeFilter(e.target.value) }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
          >
            <option value="">— Body Type —</option>
            {BODY_TYPE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
          <select
            value={fuelTypeFilter}
            onChange={(e) => { setPage(1); setFuelTypeFilter(e.target.value) }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
          >
            <option value="">— Fuel Type —</option>
            {FUEL_TYPE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </ToolbarGroup>
        <div className="text-xs text-slate-500">
          {t.table.total}: {data?.totalCount ?? 0}
        </div>
      </Toolbar>

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {loading && <Spinner label={t.common.loading} />}

      {!loading && data && (
        <>
          <DataTable>
            <DataTableMeta>
              {t.table.page} {page} {t.table.of} {totalPages} — {data.totalCount} vehicles
            </DataTableMeta>

            {data.items.length === 0 ? (
              <div className="py-16 text-center text-slate-500">{t.vehicles.empty}</div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm text-slate-700">
                  <thead className="border-b border-slate-200 bg-slate-50/70">
                    <tr>
                      <TableHeadCell>Thumbnail</TableHeadCell>
                      <TableHeadCell>Plate</TableHeadCell>
                      <TableHeadCell>Make / Model</TableHeadCell>
                      <TableHeadCell>Year</TableHeadCell>
                      <TableHeadCell>Body Type</TableHeadCell>
                      <TableHeadCell>Fuel</TableHeadCell>
                      <TableHeadCell>Trans</TableHeadCell>
                      <TableHeadCell align="center">Seats</TableHeadCell>
                      <TableHeadCell>Status</TableHeadCell>
                      <TableHeadCell align="end">KM</TableHeadCell>
                      <TableHeadCell align="center">Actions</TableHeadCell>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {data.items.map((v) => {
                      const isOnRent = v.status === 3
                      const isDeleting = deletingId === v.id
                      return (
                        <tr
                          key={v.id}
                          onClick={() => router.push(`/vehicles/${v.id}`)}
                          className="cursor-pointer hover:bg-slate-50 transition-colors"
                        >
                          <TableCell>
                            <ThumbnailCell
                              v={v}
                              imgErrors={imgErrors}
                              onError={handleImgError}
                            />
                          </TableCell>
                          <TableCell>
                            <span className="font-mono font-semibold tracking-widest text-slate-800">
                              {v.plateNumber}
                            </span>
                          </TableCell>
                          <TableCell>
                            <div className="font-medium text-slate-900">{v.make} {v.model}</div>
                          </TableCell>
                          <TableCell>
                            {v.modelYear ?? '—'}
                          </TableCell>
                          <TableCell>
                            {v.bodyType ?? '—'}
                          </TableCell>
                          <TableCell>
                            {v.fuelType ?? '—'}
                          </TableCell>
                          <TableCell>
                            {v.transmissionType ?? '—'}
                          </TableCell>
                          <TableCell align="center">
                            {v.seats ?? '—'}
                          </TableCell>
                          <TableCell>
                            <Badge tone={STATUS_TONES[v.status] ?? 'slate'}>
                              {STATUS_LABELS[v.status] ?? String(v.status)}
                            </Badge>
                          </TableCell>
                          <TableCell align="end">
                            {v.currentKm.toLocaleString()}
                          </TableCell>
                          <TableCell align="center">
                            <div
                              className="flex items-center justify-center gap-1"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <SecondaryButton
                                onClick={() => router.push(`/vehicles/${v.id}`)}
                                className="px-2 py-1 text-xs"
                              >
                                {t.common.viewDetails}
                              </SecondaryButton>
                              {!isOnRent && (
                                <button
                                  type="button"
                                  disabled={isDeleting}
                                  onClick={(e) => handleDelete(e, v.id)}
                                  className="rounded border border-red-200 bg-red-50 px-2 py-1 text-xs font-medium text-red-600 transition hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-40"
                                >
                                  {isDeleting ? '…' : t.common.delete}
                                </button>
                              )}
                            </div>
                          </TableCell>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </DataTable>

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
        </>
      )}
    </div>
  )
}
