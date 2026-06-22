'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import {
  bff,
  type PagedResult,
  type RfqSummary,
  type RfqPipelineStage,
} from '../../lib/bff-client'
import { ErrorBox } from '../../components/ui'
import {
  type BadgeTone,
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
} from '../../components/data-grid'

/* ---------------------------------------------------------------------------
 * Stage colour mapping
 * -------------------------------------------------------------------------*/

const STAGE_TONES: Record<string, BadgeTone> = {
  Draft: 'slate',
  Qualified: 'blue',
  Proposal: 'amber',
  Negotiation: 'purple',
  Won: 'green',
  Lost: 'red',
}

const STAGE_OPTIONS = ['Draft', 'Qualified', 'Proposal', 'Negotiation', 'Won', 'Lost'] as const

const STAGE_COLORS: Record<string, { header: string; border: string; bg: string }> = {
  Draft: { header: 'bg-slate-100 text-slate-700', border: 'border-slate-200', bg: 'bg-slate-50/40' },
  Qualified: { header: 'bg-blue-100 text-blue-700', border: 'border-blue-200', bg: 'bg-blue-50/40' },
  Proposal: { header: 'bg-amber-100 text-amber-700', border: 'border-amber-200', bg: 'bg-amber-50/40' },
  Negotiation: { header: 'bg-purple-100 text-purple-700', border: 'border-purple-200', bg: 'bg-purple-50/40' },
  Won: { header: 'bg-emerald-100 text-emerald-700', border: 'border-emerald-200', bg: 'bg-emerald-50/40' },
  Lost: { header: 'bg-red-100 text-red-700', border: 'border-red-200', bg: 'bg-red-50/40' },
}

const PAGE_SIZE = 20

/* ---------------------------------------------------------------------------
 * View toggle icon
 * -------------------------------------------------------------------------*/

function KanbanIcon() {
  return (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M9 4.5v15m6-15v15M4.5 4.5h15" />
    </svg>
  )
}

function ListIcon() {
  return (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 12h16.5m-16.5 5.25h16.5m-16.5-10.5h16.5" />
    </svg>
  )
}

/* ---------------------------------------------------------------------------
 * Kanban card
 * -------------------------------------------------------------------------*/

function KanbanCard({ rfq }: { rfq: RfqSummary }) {
  return (
    <Link
      href={`/rfqs/${rfq.id}`}
      className="block rounded-lg border border-slate-200 bg-white p-3 shadow-sm transition hover:border-brand-300 hover:shadow"
    >
      <div className="mb-1.5 flex items-center justify-between">
        <span className="font-mono text-[11px] font-semibold text-slate-900">{rfq.rfqNumber}</span>
        <StatusBadge tone={STAGE_TONES[rfq.stage] ?? 'slate'}>{rfq.probability}%</StatusBadge>
      </div>
      <p className="mb-1 truncate text-xs font-medium text-slate-700">{rfq.customerDisplayName}</p>
      <p className="text-[11px] text-slate-500">
        {rfq.vehicleQty} vehicle{rfq.vehicleQty !== 1 ? 's' : ''} &times; {rfq.tenureMonths} mo
      </p>
      {rfq.expectedCloseDate && (
        <p className="mt-1 text-[10px] text-slate-400">
          Close: {new Date(rfq.expectedCloseDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}
        </p>
      )}
    </Link>
  )
}

/* ---------------------------------------------------------------------------
 * Main page
 * -------------------------------------------------------------------------*/

export default function RfqPipelinePage() {
  const [view, setView] = useState<'kanban' | 'list'>('kanban')
  const [search, setSearch] = useState('')
  const [stageFilter, setStageFilter] = useState('')
  const [page, setPage] = useState(1)

  // Kanban data
  const [pipeline, setPipeline] = useState<RfqPipelineStage[] | null>(null)
  const [kanbanLoading, setKanbanLoading] = useState(false)
  const [kanbanError, setKanbanError] = useState<string | null>(null)

  // List data
  const [listData, setListData] = useState<PagedResult<RfqSummary> | null>(null)
  const [listLoading, setListLoading] = useState(false)
  const [listError, setListError] = useState<string | null>(null)

  // Detail panel (list view)
  const [selected, setSelected] = useState<RfqSummary | null>(null)

  /* ─── Kanban loader ───────────────────────────────────────────────────────*/
  async function loadKanban() {
    setKanbanLoading(true)
    setKanbanError(null)
    try {
      setPipeline(await bff.getRfqPipeline())
    } catch (e) {
      setKanbanError((e as Error).message)
    } finally {
      setKanbanLoading(false)
    }
  }

  /* ─── List loader ─────────────────────────────────────────────────────────*/
  async function loadList() {
    setListLoading(true)
    setListError(null)
    try {
      setListData(
        await bff.getRfqs(page, PAGE_SIZE, search || undefined, stageFilter || undefined),
      )
    } catch (e) {
      setListError((e as Error).message)
    } finally {
      setListLoading(false)
    }
  }

  useEffect(() => {
    if (view === 'kanban') {
      loadKanban()
    }
  }, [view]) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (view === 'list') {
      const h = setTimeout(loadList, 200)
      return () => clearTimeout(h)
    }
    return undefined
  }, [view, page, search, stageFilter]) // eslint-disable-line react-hooks/exhaustive-deps

  /* ─── Filter kanban by search ─────────────────────────────────────────────*/
  const filteredPipeline = pipeline?.map((col) => {
    if (!search) return col
    const lc = search.toLowerCase()
    const items = col.items.filter(
      (r) =>
        r.rfqNumber.toLowerCase().includes(lc) ||
        r.customerDisplayName.toLowerCase().includes(lc),
    )
    return { ...col, count: items.length, items }
  })

  /* ─── List columns ────────────────────────────────────────────────────────*/
  const columns: Column<RfqSummary>[] = [
    {
      key: 'rfqNumber',
      header: 'RFQ #',
      render: (r) => (
        <span className="font-mono text-xs font-semibold text-slate-900">{r.rfqNumber}</span>
      ),
    },
    { key: 'customer', header: 'Customer', render: (r) => r.customerDisplayName },
    {
      key: 'stage',
      header: 'Stage',
      render: (r) => (
        <StatusBadge tone={STAGE_TONES[r.stage] ?? 'slate'}>{r.stage}</StatusBadge>
      ),
    },
    { key: 'vehicles', header: 'Vehicles', align: 'right', render: (r) => String(r.vehicleQty) },
    { key: 'tenure', header: 'Tenure', render: (r) => `${r.tenureMonths} mo` },
    {
      key: 'probability',
      header: 'Probability',
      align: 'right',
      render: (r) => `${r.probability}%`,
    },
    {
      key: 'expectedClose',
      header: 'Expected Close',
      render: (r) => (r.expectedCloseDate ? <DateCell date={r.expectedCloseDate} /> : '—'),
    },
    { key: 'created', header: 'Created', render: (r) => <DateCell date={r.createdAtUtc} /> },
  ]

  const listRows = listData?.items ?? []

  /* ─── Render ──────────────────────────────────────────────────────────────*/
  return (
    <PageShell
      title="Sales Pipeline"
      subtitle="Track opportunities from enquiry to conversion. Click a deal to manage its lifecycle."
      actions={
        <div className="flex items-center gap-2">
          {/* View toggle */}
          <div className="flex rounded-md border border-slate-200 bg-slate-50">
            <button
              onClick={() => setView('kanban')}
              className={`flex items-center gap-1.5 rounded-l-md px-3 py-1.5 text-xs font-medium transition ${view === 'kanban' ? 'bg-white text-brand-700 shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}
            >
              <KanbanIcon /> Kanban
            </button>
            <button
              onClick={() => setView('list')}
              className={`flex items-center gap-1.5 rounded-r-md px-3 py-1.5 text-xs font-medium transition ${view === 'list' ? 'bg-white text-brand-700 shadow-sm' : 'text-slate-500 hover:text-slate-700'}`}
            >
              <ListIcon /> List
            </button>
          </div>
          <Link
            href="/rfqs/new"
            className="rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800"
          >
            + New Lead
          </Link>
        </div>
      }
    >
      {/* Filter bar */}
      <FilterBar>
        <SearchBox
          value={search}
          onChange={(v) => {
            setPage(1)
            setSearch(v)
          }}
          placeholder="Search by RFQ #, customer..."
        />
        {view === 'list' && (
          <FilterPill
            value={stageFilter}
            onChange={setStageFilter}
            options={STAGE_OPTIONS.map((s) => ({ value: s, label: s }))}
            placeholder="All Stages"
          />
        )}
      </FilterBar>

      {/* ─── Kanban view ──────────────────────────────────────────────────────*/}
      {view === 'kanban' && (
        <>
          {kanbanError && (
            <div className="p-4">
              <ErrorBox message={kanbanError} onRetry={loadKanban} retryLabel="Retry" />
            </div>
          )}

          {kanbanLoading && (
            <div className="flex items-center justify-center py-16 text-sm text-slate-400">
              <span className="border-t-brand-600 mr-2 inline-block h-4 w-4 animate-spin rounded-full border-2 border-slate-200" />
              Loading pipeline...
            </div>
          )}

          {!kanbanLoading && filteredPipeline && (
            <div className="overflow-x-auto px-4 py-4">
              <div className="flex gap-3" style={{ minWidth: '1200px' }}>
                {filteredPipeline.map((col) => {
                  const colors = STAGE_COLORS[col.stage] ?? STAGE_COLORS.Draft!
                  return (
                    <div
                      key={col.stage}
                      className={`flex w-[200px] flex-shrink-0 flex-col rounded-lg border ${colors.border} ${colors.bg}`}
                    >
                      {/* Column header */}
                      <div
                        className={`flex items-center justify-between rounded-t-lg px-3 py-2 ${colors.header}`}
                      >
                        <span className="text-xs font-semibold">{col.stage}</span>
                        <span className="inline-flex h-5 min-w-[20px] items-center justify-center rounded-full bg-white/70 px-1.5 text-[10px] font-bold">
                          {col.count}
                        </span>
                      </div>

                      {/* Cards */}
                      <div className="flex flex-1 flex-col gap-2 overflow-y-auto p-2" style={{ maxHeight: '70vh' }}>
                        {col.items.length === 0 && (
                          <p className="py-4 text-center text-[11px] text-slate-400">
                            No RFQs in this stage
                          </p>
                        )}
                        {col.items.map((rfq) => (
                          <KanbanCard key={rfq.id} rfq={rfq} />
                        ))}
                      </div>
                    </div>
                  )
                })}
              </div>
            </div>
          )}
        </>
      )}

      {/* ─── List view ────────────────────────────────────────────────────────*/}
      {view === 'list' && (
        <>
          {listError && (
            <div className="p-4">
              <ErrorBox message={listError} onRetry={loadList} retryLabel="Retry" />
            </div>
          )}

          <div className="flex">
            <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
              <DataGrid
                columns={columns}
                rows={listRows}
                totalCount={listData?.totalCount ?? 0}
                page={page}
                pageSize={PAGE_SIZE}
                onPageChange={setPage}
                onRowClick={setSelected}
                selectedId={selected?.id ?? null}
                loading={listLoading}
                emptyMessage="No RFQs found."
              />
            </div>

            <DetailPanel
              open={!!selected}
              onClose={() => setSelected(null)}
              title={selected?.rfqNumber ?? ''}
              {...(selected?.customerDisplayName
                ? { subtitle: selected.customerDisplayName }
                : {})}
              {...(selected
                ? {
                    badge: (
                      <StatusBadge tone={STAGE_TONES[selected.stage] ?? 'slate'}>
                        {selected.stage}
                      </StatusBadge>
                    ),
                  }
                : {})}
            >
              {selected && (
                <>
                  <DetailSection title="Summary">
                    <DetailRow label="RFQ #" value={selected.rfqNumber} />
                    <DetailRow label="Customer" value={selected.customerDisplayName} />
                    <DetailRow label="Source" value={selected.source} />
                    <DetailRow
                      label="Vehicles"
                      value={`${selected.vehicleQty} vehicle${selected.vehicleQty !== 1 ? 's' : ''}`}
                    />
                    <DetailRow label="Tenure" value={`${selected.tenureMonths} months`} />
                    <DetailRow label="Probability" value={`${selected.probability}%`} />
                    <DetailRow
                      label="Expected Close"
                      value={
                        selected.expectedCloseDate ? (
                          <DateCell date={selected.expectedCloseDate} />
                        ) : (
                          '—'
                        )
                      }
                    />
                    <DetailRow
                      label="Created"
                      value={<DateCell date={selected.createdAtUtc} />}
                    />
                  </DetailSection>
                  <DetailSection title="Actions">
                    <Link
                      href={`/rfqs/${selected.id}`}
                      className="block w-full rounded-md bg-brand-700 px-3 py-1.5 text-center text-xs font-medium text-white hover:bg-brand-800"
                    >
                      Open Details
                    </Link>
                  </DetailSection>
                </>
              )}
            </DetailPanel>
          </div>
        </>
      )}
    </PageShell>
  )
}
