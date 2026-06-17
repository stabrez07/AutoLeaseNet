'use client'

import { useEffect, useMemo, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../lib/locale-provider'
import { bff, type LeaseSummary, type PagedResult } from '../../lib/bff-client'
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
  ToolbarGroup,
} from '../../components/ui'

const STATUS_TONES: Record<string, 'green' | 'amber' | 'blue' | 'slate' | 'red'> = {
  Active: 'green',
  Extended: 'blue',
  PendingIssuance: 'amber',
  Suspended: 'amber',
  Draft: 'slate',
  Closed: 'slate',
  Cancelled: 'red',
}

function fmt(iso: string) { return iso.substring(0, 10) }
function sar(n: number) { return n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }

export default function LeasesPage() {
  const { t } = useLocale()
  const router = useRouter()
  const tl = t.leases
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<LeaseSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const pageSize = 20

  async function load() {
    setLoading(true); setError(null)
    try {
      setData(await bff.getLeases(page, pageSize, search || undefined, statusFilter || undefined))
    } catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => {
    const h = setTimeout(load, 200)
    return () => clearTimeout(h)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search, statusFilter])

  const totalPages = useMemo(() => data?.totalPages ?? 1, [data])

  function downloadCsv() {
    if (!data) return
    const rows = [['Lease #', 'Customer', 'Vehicle', 'Plate', 'Driver', 'Status', 'Type', 'Start', 'End', 'Rent (SAR)', 'Branch']]
    data.items.forEach((l) => rows.push([l.leaseNumber, l.customerDisplayName, l.vehicleMakeModel, l.vehiclePlate, l.primaryDriverName ?? '', l.status, l.contractTypeCode, l.contractStartUtc.substring(0, 10), l.contractEndUtc.substring(0, 10), String(l.rentAmountSar), l.workingBranchName]))
    const csv = rows.map((r) => r.join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `leases-${new Date().toISOString().substring(0, 10)}.csv`; a.click()
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={tl.title}
        subtitle={tl.subtitle}
        action={
          <div className="flex gap-2">
            <SecondaryButton onClick={downloadCsv}>Export CSV</SecondaryButton>
            <PrimaryButton onClick={() => router.push('/leases/new')}>+ {t.newLease.title.split('—')[0]?.trim()}</PrimaryButton>
          </div>
        }
      />
      <Toolbar>
        <ToolbarGroup>
          <SearchInput value={search} onChange={(v) => { setPage(1); setSearch(v) }} placeholder={tl.searchPlaceholder} />
          <select
            value={statusFilter}
            onChange={(e) => { setPage(1); setStatusFilter(e.target.value) }}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm"
          >
            <option value="">— {tl.columns.status} —</option>
            {Object.entries(tl.statuses).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
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
                <TableHeadCell>{tl.columns.leaseNumber}</TableHeadCell>
                <TableHeadCell>{tl.columns.customer}</TableHeadCell>
                <TableHeadCell>{tl.columns.vehicle}</TableHeadCell>
                <TableHeadCell>{tl.columns.driver}</TableHeadCell>
                <TableHeadCell>{tl.columns.status}</TableHeadCell>
                <TableHeadCell>{tl.columns.contractType}</TableHeadCell>
                <TableHeadCell>{tl.columns.start}</TableHeadCell>
                <TableHeadCell>{tl.columns.end}</TableHeadCell>
                <TableHeadCell align="end">{tl.columns.rent}</TableHeadCell>
                <TableHeadCell>{t.common.actions}</TableHeadCell>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 && (
                <tr><td colSpan={10} className="px-3 py-8 text-center text-slate-500">{tl.empty}</td></tr>
              )}
              {data.items.map((l) => {
                const statusLabel = (tl.statuses as Record<string, string>)[l.status] ?? l.status
                const tone = STATUS_TONES[l.status] ?? 'slate'
                return (
                  <tr key={l.id}
                    className="cursor-pointer border-t border-slate-100 transition hover:bg-brand-50/60"
                    onClick={() => router.push(`/leases/${l.id}`)}>
                    <TableCell className="font-mono text-xs font-semibold text-slate-900">{l.leaseNumber}</TableCell>
                    <TableCell className="max-w-[140px] truncate font-medium text-slate-900">{l.customerDisplayName}</TableCell>
                    <TableCell className="max-w-[140px] truncate text-slate-700">{l.vehicleMakeModel}</TableCell>
                    <TableCell className="text-slate-600">{l.primaryDriverName ?? '—'}</TableCell>
                    <TableCell><Badge tone={tone}>{statusLabel}</Badge></TableCell>
                    <TableCell><Badge tone="slate">{(tl.contractTypes as Record<string, string>)[l.contractTypeCode] ?? l.contractTypeCode}</Badge></TableCell>
                    <TableCell className="text-xs text-slate-600">{fmt(l.contractStartUtc)}</TableCell>
                    <TableCell className="text-xs text-slate-600">{fmt(l.contractEndUtc)}</TableCell>
                    <TableCell align="end" className="font-mono text-xs">{sar(l.rentAmountSar)}</TableCell>
                    <TableCell>
                      <SecondaryButton onClick={(e) => { e.stopPropagation(); router.push(`/leases/${l.id}`) }} className="px-2 py-1 text-xs">
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
              <SecondaryButton onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="px-2 py-1 text-xs">{t.table.previous}</SecondaryButton>
              <SecondaryButton onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="px-2 py-1 text-xs">{t.table.next}</SecondaryButton>
            </div>
          </div>
        </DataTable>
      )}
    </div>
  )
}
