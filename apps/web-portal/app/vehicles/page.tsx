'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type PagedResult, type VehicleSummary } from '../../lib/bff-client'
import { ErrorBox } from '../../components/ui'
import {
  type BadgeTone,
  type Column,
  DataGrid,
  DetailPanel,
  DetailRow,
  DetailSection,
  FilterBar,
  FilterPill,
  PageShell,
  SearchBox,
  StatusBadge,
} from '../../components/data-grid'

/* ─── Status mapping ──────────────────────────────────────────────────────── */

const STATUS_TONE: Record<number, BadgeTone> = {
  1: 'green',   // Available
  2: 'blue',    // Reserved
  3: 'amber',   // On Rent
  4: 'slate',   // In Service
  5: 'red',     // Retired
}

const STATUS_LABEL: Record<number, string> = {
  1: 'Available',
  2: 'Reserved',
  3: 'On Rent',
  4: 'In Service',
  5: 'Retired',
}

/* ─── Filter options ──────────────────────────────────────────────────────── */

const STATUS_OPTIONS = [
  { value: '1', label: 'Available' },
  { value: '2', label: 'Reserved' },
  { value: '3', label: 'On Rent' },
  { value: '4', label: 'In Service' },
  { value: '5', label: 'Retired' },
]

// bodyType values from mock use 'Suv' (mixed case), not 'SUV'
const BODY_TYPE_OPTIONS = [
  { value: 'Sedan', label: 'Sedan' },
  { value: 'Suv', label: 'SUV' },
  { value: 'Pickup', label: 'Pickup' },
  { value: 'Van', label: 'Van' },
  { value: 'Bus', label: 'Bus' },
  { value: 'Hatchback', label: 'Hatchback' },
  { value: 'Coupe', label: 'Coupe' },
]

const FUEL_TYPE_OPTIONS = [
  { value: 'Petrol91', label: 'Petrol 91' },
  { value: 'Petrol95', label: 'Petrol 95' },
  { value: 'Diesel', label: 'Diesel' },
  { value: 'Hybrid', label: 'Hybrid' },
  { value: 'Electric', label: 'Electric' },
]

const PAGE_SIZE = 30

/* ─── Page ────────────────────────────────────────────────────────────────── */

export default function VehiclesPage() {
  const { t } = useLocale()
  const router = useRouter()

  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [bodyTypeFilter, setBodyTypeFilter] = useState('')
  const [fuelTypeFilter, setFuelTypeFilter] = useState('')
  const [page, setPage] = useState(1)

  const [data, setData] = useState<PagedResult<VehicleSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<VehicleSummary | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const statusArg = statusFilter === '' ? undefined : Number(statusFilter)
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

  /* ─── Columns ─────────────────────────────────────────────────────────── */

  const columns: Column<VehicleSummary>[] = [
    {
      key: 'plate',
      header: 'Plate',
      render: (v) => (
        <span className="font-mono font-semibold tracking-widest text-slate-800">
          {v.plateNumber}
        </span>
      ),
    },
    {
      key: 'makeModel',
      header: 'Make / Model / Year',
      render: (v) => (
        <span className="font-medium text-slate-900">
          {v.make} {v.model}{v.modelYear ? ` (${v.modelYear})` : ''}
        </span>
      ),
    },
    {
      key: 'color',
      header: 'Color',
      render: (v) => v.color ?? '—',
    },
    {
      key: 'body',
      header: 'Body',
      render: (v) => v.bodyType ?? '—',
    },
    {
      key: 'status',
      header: 'Status',
      render: (v) => (
        <StatusBadge tone={STATUS_TONE[v.status] ?? 'slate'}>
          {STATUS_LABEL[v.status] ?? String(v.status)}
        </StatusBadge>
      ),
    },
    {
      key: 'km',
      header: 'KM',
      align: 'right',
      render: (v) => <span className="font-mono tabular-nums">{v.currentKm.toLocaleString()}</span>,
    },
  ]

  /* ─── Render ──────────────────────────────────────────────────────────── */

  return (
    <PageShell
      title={t.vehicles.title}
      subtitle={`${data?.totalCount ?? 0} vehicles`}
      actions={
        <button
          onClick={() => router.push('/vehicles/new')}
          className="rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800"
        >
          + Add Vehicle
        </button>
      }
    >
      <FilterBar>
        <SearchBox
          value={search}
          onChange={(v) => { setPage(1); setSearch(v) }}
          placeholder={t.vehicles.searchPlaceholder}
        />
        <FilterPill
          value={statusFilter}
          onChange={(v) => { setPage(1); setStatusFilter(v) }}
          options={STATUS_OPTIONS}
          placeholder="All Statuses"
        />
        <FilterPill
          value={bodyTypeFilter}
          onChange={(v) => { setPage(1); setBodyTypeFilter(v) }}
          options={BODY_TYPE_OPTIONS}
          placeholder="All Body Types"
        />
        <FilterPill
          value={fuelTypeFilter}
          onChange={(v) => { setPage(1); setFuelTypeFilter(v) }}
          options={FUEL_TYPE_OPTIONS}
          placeholder="All Fuel Types"
        />
      </FilterBar>

      {error && <div className="p-4"><ErrorBox message={error} onRetry={load} /></div>}

      <div className="flex">
        <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
          <DataGrid
            columns={columns}
            rows={data?.items ?? []}
            totalCount={data?.totalCount ?? 0}
            page={page}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
            onRowClick={setSelected}
            selectedId={selected?.id ?? null}
            loading={loading}
            emptyMessage={t.vehicles.empty}
          />
        </div>

        <DetailPanel
          open={!!selected}
          onClose={() => setSelected(null)}
          title={selected ? `${selected.make} ${selected.model}` : ''}
          subtitle={selected?.plateNumber ?? ''}
          badge={
            selected
              ? <StatusBadge tone={STATUS_TONE[selected.status] ?? 'slate'}>{STATUS_LABEL[selected.status] ?? ''}</StatusBadge>
              : undefined
          }
        >
          {selected && (
            <>
              <DetailSection title="Vehicle Identity">
                <DetailRow label="Plate" value={selected.plateNumber} />
                <DetailRow label="Make" value={selected.make} />
                <DetailRow label="Model" value={selected.model} />
                <DetailRow label="Year" value={selected.modelYear ?? '—'} />
              </DetailSection>

              <DetailSection title="Specifications">
                <DetailRow label="Body Type" value={selected.bodyType ?? '—'} />
                <DetailRow label="Color" value={selected.color ?? '—'} />
                <DetailRow label="Fuel Type" value={selected.fuelType ?? '—'} />
                <DetailRow label="Transmission" value={selected.transmissionType ?? '—'} />
                <DetailRow label="Seats" value={selected.seats ?? '—'} />
              </DetailSection>

              <DetailSection title="Odometer">
                <DetailRow label="Current KM" value={selected.currentKm.toLocaleString()} />
              </DetailSection>

              <DetailSection title="Actions">
                <button
                  onClick={() => router.push(`/vehicles/${selected.id}`)}
                  className="w-full rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800"
                >
                  View Full Details
                </button>
              </DetailSection>
            </>
          )}
        </DetailPanel>
      </div>
    </PageShell>
  )
}
