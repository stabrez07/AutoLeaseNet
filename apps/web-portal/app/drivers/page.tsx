'use client'

import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type DriverSummary, type PagedResult } from '../../lib/bff-client'
import { ErrorBox } from '../../components/ui'
import {
  type Column,
  DataGrid,
  DateCell,
  DetailPanel,
  DetailRow,
  DetailSection,
  FilterBar,
  FilterPill,
  PageShell,
  SearchBox,
  StatusBadge,
  type BadgeTone,
} from '../../components/data-grid'

const STATUS_TONE: Record<number, BadgeTone> = { 1: 'green', 2: 'amber', 3: 'red' }
const STATUS_LABEL: Record<number, string> = { 1: 'Active', 2: 'Suspended', 3: 'Banned' }

const PAGE_SIZE = 25

export default function DriversPage() {
  const { locale } = useLocale()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<DriverSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<DriverSummary | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      setData(await bff.getDrivers(page, PAGE_SIZE, search || undefined))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const h = setTimeout(load, 200)
    return () => clearTimeout(h)
  }, [page, search]) // eslint-disable-line react-hooks/exhaustive-deps

  const rows = (data?.items ?? []).filter((d) => {
    if (statusFilter && String(d.status) !== statusFilter) return false
    return true
  })

  const driverName = (d: DriverSummary) =>
    (locale === 'ar' ? d.personNameAr : d.personNameEn) ?? d.personNameEn ?? d.personNameAr ?? '—'

  const columns: Column<DriverSummary>[] = [
    {
      key: 'name',
      header: 'Driver Name',
      render: (d) => <span className="font-medium text-slate-900">{driverName(d)}</span>,
    },
    {
      key: 'license',
      header: 'License #',
      render: (d) => <span className="font-mono">{d.driverLicenseNumber}</span>,
    },
    {
      key: 'nameAr',
      header: 'Arabic Name',
      render: (d) => <span className="text-slate-500">{d.personNameAr ?? '—'}</span>,
    },
    {
      key: 'expiry',
      header: 'License Expiry',
      render: (d) => {
        const soon = d.licenseExpiryDate ? new Date(d.licenseExpiryDate) < new Date(Date.now() + 30 * 86400000) : false
        return <span className={soon ? 'font-semibold text-red-600' : ''}><DateCell date={d.licenseExpiryDate} /></span>
      },
    },
    {
      key: 'status',
      header: 'Status',
      render: (d) => <StatusBadge tone={STATUS_TONE[d.status] ?? 'slate'}>{STATUS_LABEL[d.status] ?? d.status}</StatusBadge>,
    },
  ]

  return (
    <PageShell
      title="Drivers"
      subtitle={`${data?.totalCount ?? 0} drivers`}
      actions={
        <button onClick={() => router.push('/drivers/new')} className="rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800">
          + Add Driver
        </button>
      }
    >
      <FilterBar>
        <SearchBox value={search} onChange={(v) => { setPage(1); setSearch(v) }} placeholder="Search drivers..." />
        <FilterPill
          value={statusFilter}
          onChange={(v) => setStatusFilter(v)}
          options={[{ value: '1', label: 'Active' }, { value: '2', label: 'Suspended' }, { value: '3', label: 'Banned' }]}
          placeholder="All Statuses"
        />
      </FilterBar>

      {error && <div className="p-4"><ErrorBox message={error} onRetry={load} /></div>}

      <div className="flex">
        <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
          <DataGrid
            columns={columns}
            rows={rows}
            totalCount={data?.totalCount ?? 0}
            page={page}
            pageSize={PAGE_SIZE}
            onPageChange={setPage}
            onRowClick={setSelected}
            selectedId={selected?.id ?? null}
            loading={loading}
          />
        </div>

        <DetailPanel
          open={!!selected}
          onClose={() => setSelected(null)}
          title={selected ? driverName(selected) : ''}
          {...(selected ? { subtitle: selected.driverLicenseNumber } : {})}
          {...(selected ? { badge: <StatusBadge tone={STATUS_TONE[selected.status] ?? 'slate'}>{STATUS_LABEL[selected.status] ?? ''}</StatusBadge> } : {})}
        >
          {selected && (
            <>
              <DetailSection title="License">
                <DetailRow label="License Number" value={selected.driverLicenseNumber} />
                <DetailRow label="Expiry Date" value={<DateCell date={selected.licenseExpiryDate} />} />
                <DetailRow label="Status" value={STATUS_LABEL[selected.status] ?? String(selected.status)} />
              </DetailSection>
              <DetailSection title="Actions">
                <button
                  onClick={() => router.push(`/drivers/${selected.id}`)}
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
