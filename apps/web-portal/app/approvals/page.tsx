'use client'

import Link from 'next/link'
import { useEffect, useState } from 'react'
import { bff, type PendingApprovalItem } from '../../lib/bff-client'
import {
  DataGrid,
  DetailPanel,
  DetailRow,
  DetailSection,
  FilterBar,
  MoneyCell,
  PageShell,
  SearchBox,
  StatusBadge,
  type BadgeTone,
  type Column,
} from '../../components/data-grid'

const APPROVAL_TONES: Record<string, BadgeTone> = { Pending: 'amber', Approved: 'green', Rejected: 'red' }

function relativeTime(dateStr: string | null): string {
  if (!dateStr) return '—'
  const diff = Date.now() - new Date(dateStr).getTime()
  const days = Math.floor(diff / 86400000)
  if (days < 1) return 'Today'
  if (days === 1) return '1 day ago'
  return `${days}d ago`
}

function discountTone(pct: number): BadgeTone {
  if (pct <= 5) return 'green'
  if (pct <= 15) return 'amber'
  return 'red'
}

export default function ApprovalsPage() {
  const [data, setData] = useState<PendingApprovalItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<PendingApprovalItem | null>(null)
  const [search, setSearch] = useState('')
  const [actionBusy, setActionBusy] = useState(false)
  const [actionMsg, setActionMsg] = useState<{ ok: boolean; text: string } | null>(null)
  const [rejectComment, setRejectComment] = useState('')

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const res = await bff.getPendingApprovals()
      setData(res.items)
    } catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => { load() }, [])

  const filtered = data.filter((item) => {
    if (!search) return true
    const hay = `${item.quoteNumber} ${item.customerDisplayName}`.toLowerCase()
    return hay.includes(search.toLowerCase())
  })

  async function handleDecision(item: PendingApprovalItem, approved: boolean, comment?: string) {
    const pendingTier = item.approvals.find((a) => a.status === 'Pending')
    if (!pendingTier) return
    setActionBusy(true)
    setActionMsg(null)
    try {
      await bff.recordApprovalDecision(item.quotationId, pendingTier.tierLevel, approved, comment, crypto.randomUUID())
      setActionMsg({ ok: true, text: `${item.quoteNumber} ${approved ? 'approved' : 'rejected'} successfully.` })
      setSelected(null)
      setRejectComment('')
      await load()
    } catch (e) {
      setActionMsg({ ok: false, text: (e as Error).message })
    } finally { setActionBusy(false) }
  }

  type ApprovalRow = PendingApprovalItem & { id: string }
  const rows: ApprovalRow[] = filtered.map((item) => ({ ...item, id: item.quotationId }))

  const columns: Column<ApprovalRow>[] = [
    {
      key: 'quoteNumber',
      header: 'Quote #',
      width: '120px',
      render: (r) => <Link href={`/quotations/${r.quotationId}`} className="font-mono text-xs font-semibold text-brand-700 hover:underline">{r.quoteNumber}</Link>,
    },
    {
      key: 'customer',
      header: 'Customer',
      render: (r) => <span className="font-medium text-slate-800">{r.customerDisplayName}</span>,
    },
    {
      key: 'total',
      header: 'Total SAR',
      align: 'right',
      width: '120px',
      render: (r) => <MoneyCell amount={r.totalSar} />,
    },
    {
      key: 'discount',
      header: 'Discount',
      width: '90px',
      render: (r) => <StatusBadge tone={discountTone(r.discountPercent)}>{r.discountPercent}%</StatusBadge>,
    },
    {
      key: 'duration',
      header: 'Duration',
      width: '80px',
      render: (r) => <span>{r.estimatedDurationMonths} mo</span>,
    },
    {
      key: 'submitted',
      header: 'Submitted',
      width: '90px',
      render: (r) => <span className="text-slate-500">{relativeTime(r.submittedAtUtc)}</span>,
    },
    {
      key: 'pendingTier',
      header: 'Pending Tier',
      width: '130px',
      render: (r) => {
        const pending = r.approvals.find((a) => a.status === 'Pending')
        return pending
          ? <StatusBadge tone="amber">Tier {pending.tierLevel} — {pending.requiredRoleCode}</StatusBadge>
          : <span className="text-slate-300">—</span>
      },
    },
  ]

  return (
    <PageShell
      title="Approval Inbox"
      subtitle="Review and approve pending quotations. Items listed here require your approval before they can be sent to customers."
    >
      <FilterBar>
        <SearchBox value={search} onChange={setSearch} placeholder="Search by quote # or customer..." />
        <span className="ml-auto text-[11px] text-slate-500">{filtered.length} pending</span>
      </FilterBar>

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-4 py-3 text-xs text-red-700">
          {error}
          <button type="button" onClick={load} className="ml-2 underline">Retry</button>
        </div>
      )}

      {actionMsg && (
        <div className={`border-b px-4 py-2 text-xs ${actionMsg.ok ? 'border-green-200 bg-green-50 text-green-800' : 'border-red-200 bg-red-50 text-red-800'}`}>
          {actionMsg.text}
        </div>
      )}

      <div className="flex">
        <div className={`flex-1 ${selected ? 'max-w-[calc(100%-400px)]' : ''}`}>
          <DataGrid<ApprovalRow>
            columns={columns}
            rows={rows}
            totalCount={rows.length}
            page={1}
            pageSize={rows.length || 1}
            onPageChange={() => {}}
            onRowClick={(row) => setSelected((prev) => (prev?.quotationId === row.quotationId ? null : row))}
            selectedId={selected?.quotationId ?? null}
            emptyMessage="No pending approvals. All quotations are up to date."
            loading={loading}
          />
        </div>

        <DetailPanel
          open={!!selected}
          onClose={() => { setSelected(null); setRejectComment('') }}
          title={selected?.quoteNumber ?? ''}
          {...(selected?.customerDisplayName ? { subtitle: selected.customerDisplayName } : {})}
          {...(selected ? { badge: <StatusBadge tone="amber">Pending Approval</StatusBadge> } : {})}
        >
          {selected && (
            <>
              <DetailSection title="Quotation Summary">
                <DetailRow label="Quote #" value={selected.quoteNumber} />
                <DetailRow label="Customer" value={selected.customerDisplayName} />
                <DetailRow label="Total" value={<MoneyCell amount={selected.totalSar} />} />
                <DetailRow label="Discount" value={<StatusBadge tone={discountTone(selected.discountPercent)}>{selected.discountPercent}%</StatusBadge>} />
                <DetailRow label="Duration" value={`${selected.estimatedDurationMonths} months`} />
                <DetailRow label="Lines" value={selected.lineCount} />
              </DetailSection>

              <DetailSection title="Approval Chain">
                {selected.approvals.map((a) => (
                  <div key={a.tierLevel} className="mb-1 flex items-center justify-between rounded border border-slate-200 px-3 py-2 text-xs">
                    <span className="font-medium">Tier {a.tierLevel} — {a.requiredRoleCode}</span>
                    <StatusBadge tone={APPROVAL_TONES[a.status] ?? 'slate'}>{a.status}</StatusBadge>
                  </div>
                ))}
              </DetailSection>

              <DetailSection title="Actions">
                <div className="space-y-2">
                  <button
                    type="button"
                    disabled={actionBusy}
                    onClick={() => handleDecision(selected, true)}
                    className="w-full rounded-md bg-green-600 px-3 py-2 text-xs font-semibold text-white hover:bg-green-700 disabled:opacity-50"
                  >
                    {actionBusy ? 'Processing...' : 'Approve'}
                  </button>
                  <div className="space-y-1">
                    <textarea
                      value={rejectComment}
                      onChange={(e) => setRejectComment(e.target.value)}
                      placeholder="Rejection reason (required)..."
                      rows={2}
                      className="w-full rounded-md border border-slate-200 px-3 py-2 text-xs text-slate-700 placeholder:text-slate-400 focus:border-red-400 focus:outline-none focus:ring-1 focus:ring-red-400"
                    />
                    <button
                      type="button"
                      disabled={actionBusy || !rejectComment.trim()}
                      onClick={() => handleDecision(selected, false, rejectComment)}
                      className="w-full rounded-md border border-red-300 bg-white px-3 py-2 text-xs font-semibold text-red-700 hover:bg-red-50 disabled:opacity-50"
                    >
                      {actionBusy ? 'Processing...' : 'Reject'}
                    </button>
                    {!rejectComment.trim() && (
                      <p className="text-[10px] text-slate-400">Enter a reason above to enable rejection.</p>
                    )}
                  </div>
                  <Link
                    href={`/quotations/${selected.quotationId}`}
                    className="block w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-center text-xs font-medium text-slate-700 hover:bg-slate-50"
                  >
                    View Full Quotation
                  </Link>
                </div>
              </DetailSection>
            </>
          )}
        </DetailPanel>
      </div>
    </PageShell>
  )
}
