'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { useLocale } from '../../lib/locale-provider'
import { bff, type PagedResult, type QuotationDetail, type QuotationSummary } from '../../lib/bff-client'
import { ErrorBox } from '../../components/ui'
import {
  type BadgeTone, type Column, DataGrid, DateCell, DetailPanel, DetailRow, DetailSection,
  FilterBar, FilterPill, MoneyCell, PageShell, SearchBox, StatusBadge,
} from '../../components/data-grid'

const STATUS_TONES: Record<string, BadgeTone> = { Draft: 'slate', PendingApproval: 'amber', Approved: 'blue', SentToCustomer: 'blue', Accepted: 'green', Rejected: 'red', Expired: 'red', Withdrawn: 'slate' }
const APPROVAL_TONES: Record<string, BadgeTone> = { Pending: 'amber', Approved: 'green', Rejected: 'red' }
const STATUS_OPTIONS = ['Draft', 'PendingApproval', 'Approved', 'SentToCustomer', 'Accepted', 'Rejected'] as const
const PAGE_SIZE = 20

export default function QuotationsPage() {
  const { t } = useLocale()
  const q = t.quotations
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<PagedResult<QuotationSummary> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<QuotationDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)

  async function load() {
    setLoading(true); setError(null)
    try { setData(await bff.getQuotations(page, PAGE_SIZE, search || undefined)) }
    catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => { const h = setTimeout(load, 200); return () => clearTimeout(h) }, [page, search]) // eslint-disable-line react-hooks/exhaustive-deps

  async function openDetail(row: QuotationSummary) {
    setDetailLoading(true)
    try { setSelected(await bff.getQuotation(row.id)) } catch { /* non-critical */ }
    finally { setDetailLoading(false) }
  }

  const rows = (data?.items ?? []).filter((r) => !statusFilter || r.status === statusFilter)

  const columns: Column<QuotationSummary>[] = [
    { key: 'quoteNumber', header: 'Quote #', render: (r) => <span className="font-mono text-xs font-semibold text-slate-900">{r.quoteNumber}</span> },
    { key: 'customer', header: 'Customer', render: (r) => r.customerDisplayName ?? '—' },
    { key: 'type', header: 'Type', render: (r) => q.contractTypes[r.contractType as keyof typeof q.contractTypes] ?? r.contractType },
    { key: 'duration', header: 'Duration', render: (r) => `${r.estimatedDurationMonths} mo` },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge tone={STATUS_TONES[r.status] ?? 'slate'}>{q.statuses[r.status as keyof typeof q.statuses] ?? r.status}</StatusBadge> },
    { key: 'total', header: 'Total', align: 'right', render: (r) => <MoneyCell amount={r.totalSar} /> },
    { key: 'quoteDate', header: 'Quote Date', render: (r) => <DateCell date={r.quoteDate} /> },
    { key: 'validUntil', header: 'Valid Until', render: (r) => <DateCell date={r.validUntilDate} /> },
  ]

  return (
    <PageShell title={q.title} subtitle={q.subtitle} actions={
      <Link href="/quotations/new" className="rounded-md bg-brand-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-800">+ {q.newButton}</Link>
    }>
      <FilterBar>
        <SearchBox value={search} onChange={(v) => { setPage(1); setSearch(v) }} placeholder={q.searchPlaceholder} />
        <FilterPill value={statusFilter} onChange={setStatusFilter} options={STATUS_OPTIONS.map((s) => ({ value: s, label: q.statuses[s as keyof typeof q.statuses] ?? s }))} placeholder="All Statuses" />
      </FilterBar>

      {error && <div className="p-4"><ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} /></div>}

      <div className="flex">
        <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
          <DataGrid columns={columns} rows={rows} totalCount={data?.totalCount ?? 0} page={page} pageSize={PAGE_SIZE} onPageChange={setPage} onRowClick={openDetail} selectedId={selected?.id ?? null} loading={loading} />
        </div>

        <DetailPanel open={!!selected} onClose={() => setSelected(null)} title={selected?.quoteNumber ?? ''} {...(selected?.customerDisplayName ? { subtitle: selected.customerDisplayName } : {})} {...(selected ? { badge: <StatusBadge tone={STATUS_TONES[selected.status] ?? 'slate'}>{q.statuses[selected.status as keyof typeof q.statuses] ?? selected.status}</StatusBadge> } : {})}>
          {detailLoading && <div className="flex items-center justify-center py-8 text-xs text-slate-400"><span className="border-t-brand-600 mr-2 inline-block h-3.5 w-3.5 animate-spin rounded-full border-2 border-slate-200" />Loading...</div>}
          {selected && !detailLoading && (
            <>
              <DetailSection title="Quote Details">
                <DetailRow label="Quote #" value={selected.quoteNumber} />
                <DetailRow label="Customer" value={selected.customerDisplayName ?? '—'} />
                <DetailRow label="Type" value={q.contractTypes[selected.contractType as keyof typeof q.contractTypes] ?? selected.contractType} />
                <DetailRow label="Duration" value={`${selected.estimatedDurationMonths} months`} />
                <DetailRow label="Quote Date" value={<DateCell date={selected.quoteDate} />} />
                <DetailRow label="Valid Until" value={<DateCell date={selected.validUntilDate} />} />
              </DetailSection>
              {selected.lines.length > 0 && (
                <DetailSection title={`Line Items (${selected.lines.length})`}>
                  <div className="overflow-x-auto rounded border border-slate-200">
                    <table className="w-full text-[11px]">
                      <thead><tr className="border-b border-slate-100 bg-slate-50/80 text-slate-500"><th className="px-2 py-1 text-left font-medium">Item</th><th className="px-2 py-1 text-right font-medium">Qty</th><th className="px-2 py-1 text-right font-medium">Price</th><th className="px-2 py-1 text-right font-medium">Total</th></tr></thead>
                      <tbody className="divide-y divide-slate-100">
                        {selected.lines.map((l) => <tr key={l.id}><td className="px-2 py-1">{l.description}</td><td className="px-2 py-1 text-right">{l.quantity}</td><td className="px-2 py-1 text-right"><MoneyCell amount={l.unitPriceSar} /></td><td className="px-2 py-1 text-right"><MoneyCell amount={l.lineTotalSar} /></td></tr>)}
                      </tbody>
                    </table>
                  </div>
                </DetailSection>
              )}
              <DetailSection title="Pricing">
                <DetailRow label="Subtotal" value={<MoneyCell amount={selected.subTotalSar} />} />
                <DetailRow label="Discount" value={`${selected.discountPercent}%`} />
                <DetailRow label="VAT (15%)" value={<MoneyCell amount={selected.vatSar} />} />
                <div className="border-t border-slate-200 pt-1"><DetailRow label="Total" value={<span className="font-semibold"><MoneyCell amount={selected.totalSar} /></span>} /></div>
              </DetailSection>
              {selected.approvals.length > 0 && (
                <DetailSection title="Approvals">
                  {selected.approvals.map((a, i) => <div key={i} className="mb-1 rounded border border-slate-200 px-3 py-2 text-xs"><div className="flex items-center justify-between"><span className="font-medium">Tier {a.tierLevel} — {a.requiredRoleCode}</span><StatusBadge tone={APPROVAL_TONES[a.status] ?? 'slate'}>{a.status}</StatusBadge></div>{a.comment && <p className="mt-1 text-slate-500">{a.comment}</p>}</div>)}
                </DetailSection>
              )}
              <DetailSection title="Actions">
                <Link href={`/quotations/${selected.id}`} className="block w-full rounded-md bg-brand-700 px-3 py-1.5 text-center text-xs font-medium text-white hover:bg-brand-800">Open Full View</Link>
              </DetailSection>
            </>
          )}
        </DetailPanel>
      </div>
    </PageShell>
  )
}
