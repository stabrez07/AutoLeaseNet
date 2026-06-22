'use client'

import type { ReactNode } from 'react'

// ─── StatusBadge ─────────────────────────────────────────────────────────────

const TONE_CLASSES = {
  green: 'bg-emerald-100 text-emerald-800 border-emerald-200',
  amber: 'bg-amber-100 text-amber-800 border-amber-200',
  red: 'bg-red-100 text-red-800 border-red-200',
  blue: 'bg-blue-100 text-blue-800 border-blue-200',
  slate: 'bg-slate-100 text-slate-600 border-slate-200',
  purple: 'bg-purple-100 text-purple-800 border-purple-200',
} as const

export type BadgeTone = keyof typeof TONE_CLASSES

export function StatusBadge({ tone = 'slate', children }: { tone?: BadgeTone; children: ReactNode }) {
  return (
    <span className={`inline-flex items-center rounded px-2 py-0.5 text-[11px] font-semibold border ${TONE_CLASSES[tone]}`}>
      {children}
    </span>
  )
}

// ─── FilterBar ───────────────────────────────────────────────────────────────

export function FilterBar({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center gap-2 border-b border-slate-200 bg-white px-4 py-2">
      {children}
    </div>
  )
}

export function SearchBox({ value, onChange, placeholder = 'Search...' }: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
}) {
  return (
    <div className="relative">
      <svg className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
      </svg>
      <input
        type="search"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="h-7 w-56 rounded border border-slate-200 bg-white pl-8 pr-3 text-xs text-slate-700 placeholder:text-slate-400 focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400"
      />
    </div>
  )
}

export function FilterPill({ value, onChange, options, placeholder }: {
  value: string
  onChange: (v: string) => void
  options: { value: string; label: string }[]
  placeholder: string
}) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="h-7 rounded border border-slate-200 bg-white px-2 text-xs text-slate-600 focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400"
    >
      <option value="">{placeholder}</option>
      {options.map((o) => (
        <option key={o.value} value={o.value}>{o.label}</option>
      ))}
    </select>
  )
}

// ─── DataGrid (CRM-style) ───────────────────────────────────────────────────

export interface Column<T> {
  key: string
  header: string
  width?: string
  align?: 'left' | 'right' | 'center'
  render: (row: T) => ReactNode
  sortable?: boolean
}

export function DataGrid<T extends { id: string }>({
  columns,
  rows,
  totalCount,
  page,
  pageSize,
  onPageChange,
  onRowClick,
  selectedId,
  emptyMessage = 'No data found',
  loading = false,
}: {
  columns: Column<T>[]
  rows: T[]
  totalCount: number
  page: number
  pageSize: number
  onPageChange: (page: number) => void
  onRowClick?: ((row: T) => void) | undefined
  selectedId?: string | null | undefined
  emptyMessage?: string | undefined
  loading?: boolean | undefined
}) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const startIdx = (page - 1) * pageSize

  return (
    <div className="flex flex-col border border-slate-200 rounded-md bg-white overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-[13px]">
          <thead>
            <tr className="border-b border-slate-200 bg-slate-50">
              <th className="w-10 border-r border-slate-200 px-2 py-2 text-center text-[11px] font-medium text-slate-400">#</th>
              {columns.map((col) => (
                <th
                  key={col.key}
                  className={`border-r border-slate-100 px-3 py-2 text-[11px] font-semibold text-slate-500 last:border-r-0 ${
                    col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left'
                  }`}
                  style={col.width ? { width: col.width } : undefined}
                >
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {loading && (
              <tr>
                <td colSpan={columns.length + 1} className="py-12 text-center text-slate-400">
                  <span className="border-t-brand-600 mr-2 inline-block h-4 w-4 animate-spin rounded-full border-2 border-slate-200" />
                  Loading...
                </td>
              </tr>
            )}
            {!loading && rows.length === 0 && (
              <tr>
                <td colSpan={columns.length + 1} className="py-12 text-center text-slate-400">
                  <div className="flex flex-col items-center gap-1">
                    <svg className="h-8 w-8 text-slate-300" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" />
                    </svg>
                    <span className="text-sm">{emptyMessage}</span>
                  </div>
                </td>
              </tr>
            )}
            {!loading && rows.map((row, idx) => (
              <tr
                key={row.id}
                onClick={() => onRowClick?.(row)}
                className={`border-b border-slate-100 last:border-b-0 transition-colors ${
                  onRowClick ? 'cursor-pointer' : ''
                } ${selectedId === row.id ? 'bg-brand-50' : 'hover:bg-slate-50/70'}`}
              >
                <td className="w-10 border-r border-slate-100 px-2 py-[7px] text-center text-[11px] text-slate-400 tabular-nums">{startIdx + idx + 1}</td>
                {columns.map((col) => (
                  <td
                    key={col.key}
                    className={`border-r border-slate-50 px-3 py-[7px] last:border-r-0 ${
                      col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left'
                    }`}
                  >
                    {col.render(row)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Footer — record count + pagination */}
      <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50/80 px-4 py-1.5">
        <span className="text-[11px] text-slate-500">
          {totalCount > 0 ? `${totalCount} records` : '0 records'}
        </span>
        <div className="flex items-center gap-1">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
            className="rounded p-1 text-slate-500 hover:bg-slate-200 disabled:opacity-30"
          >
            <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
            </svg>
          </button>
          <span className="px-2 text-[11px] font-medium text-slate-600 tabular-nums">{page} / {totalPages}</span>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() => onPageChange(page + 1)}
            className="rounded p-1 text-slate-500 hover:bg-slate-200 disabled:opacity-30"
          >
            <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── DetailPanel ─────────────────────────────────────────────────────────────

export function DetailPanel({
  open,
  onClose,
  title,
  subtitle,
  badge,
  children,
}: {
  open: boolean
  onClose: () => void
  title: string
  subtitle?: string | undefined
  badge?: ReactNode | undefined
  children: ReactNode
}) {
  if (!open) return null

  return (
    <div className="sticky top-0 flex h-[calc(100vh-100px)] w-full max-w-md flex-col border-l border-slate-200 bg-white shadow-sm">
      {/* Header */}
      <div className="flex items-start justify-between border-b border-slate-200 px-4 py-3">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <h2 className="truncate text-sm font-semibold text-slate-900">{title}</h2>
            {badge}
          </div>
          {subtitle && <p className="mt-0.5 truncate text-xs text-slate-500">{subtitle}</p>}
        </div>
        <button
          type="button"
          onClick={onClose}
          className="ml-3 rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-4">
        {children}
      </div>
    </div>
  )
}

export function DetailSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="mb-4">
      <h3 className="mb-2 text-[11px] font-semibold uppercase tracking-wider text-slate-400">{title}</h3>
      {children}
    </div>
  )
}

export function DetailRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex justify-between py-1 text-xs">
      <span className="text-slate-500">{label}</span>
      <span className="font-medium text-slate-900">{value}</span>
    </div>
  )
}

// ─── PageShell ───────────────────────────────────────────────────────────────

export function PageShell({
  title,
  subtitle,
  actions,
  children,
}: {
  title: string
  subtitle?: string
  actions?: ReactNode
  children: ReactNode
}) {
  return (
    <div className="space-y-0">
      <div className="flex items-center justify-between border-b border-slate-200 bg-white px-4 py-3">
        <div>
          <h1 className="text-base font-semibold text-slate-900">{title}</h1>
          {subtitle && <p className="text-xs text-slate-500">{subtitle}</p>}
        </div>
        {actions && <div className="flex gap-2">{actions}</div>}
      </div>
      {children}
    </div>
  )
}

// ─── MoneyCell helper ────────────────────────────────────────────────────────

export function MoneyCell({ amount, currency = 'SAR' }: { amount: number; currency?: string }) {
  return (
    <span className="font-mono text-xs tabular-nums">
      {amount.toLocaleString('en-SA', { minimumFractionDigits: 2 })} {currency}
    </span>
  )
}

export function DateCell({ date }: { date: string | null | undefined }) {
  if (!date) return <span className="text-slate-300">—</span>
  return <span>{new Date(date).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}</span>
}
