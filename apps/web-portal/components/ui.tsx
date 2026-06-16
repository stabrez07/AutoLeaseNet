'use client'

import type { ReactNode } from 'react'
import type { MouseEventHandler } from 'react'

export function PageHeader({
  title,
  subtitle,
  action,
}: {
  title: string
  subtitle?: string
  action?: ReactNode
}) {
  return (
    <div className="mb-6 flex flex-col gap-2 md:flex-row md:items-end md:justify-between">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900">{title}</h1>
        {subtitle && <p className="mt-1 max-w-2xl text-sm text-slate-500">{subtitle}</p>}
      </div>
      {action && <div>{action}</div>}
    </div>
  )
}

export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`rounded-xl border border-slate-200/80 bg-white shadow-sm ${className}`}>
      {children}
    </div>
  )
}

export function PrimaryButton({
  children,
  onClick,
  disabled = false,
  type = 'button',
  className = '',
}: {
  children: ReactNode
  onClick?: MouseEventHandler<HTMLButtonElement>
  disabled?: boolean
  type?: 'button' | 'submit'
  className?: string
}) {
  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled}
      className={`inline-flex items-center rounded-lg bg-brand-700 px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-brand-800 disabled:pointer-events-none disabled:opacity-40 ${className}`}
    >
      {children}
    </button>
  )
}

export function SecondaryButton({
  children,
  onClick,
  disabled = false,
  className = '',
  type = 'button',
}: {
  children: ReactNode
  onClick?: MouseEventHandler<HTMLButtonElement>
  disabled?: boolean
  className?: string
  type?: 'button' | 'submit'
}) {
  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled}
      className={`inline-flex items-center rounded-lg border border-brand-200 bg-brand-50/40 px-3 py-2 text-sm font-medium text-brand-800 shadow-sm transition hover:bg-brand-100/70 disabled:pointer-events-none disabled:opacity-40 ${className}`}
    >
      {children}
    </button>
  )
}

export function Toolbar({ children }: { children: ReactNode }) {
  return (
    <Card className="p-3 md:p-4">
      <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">{children}</div>
    </Card>
  )
}

export function ToolbarGroup({ children }: { children: ReactNode }) {
  return <div className="flex flex-col gap-2 sm:flex-row sm:items-center">{children}</div>
}

export function SearchInput({
  value,
  placeholder,
  onChange,
  className = '',
}: {
  value: string
  placeholder: string
  onChange: (value: string) => void
  className?: string
}) {
  return (
    <input
      type="search"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      className={`focus:border-brand-500 focus:ring-brand-500 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-1 md:w-72 ${className}`}
    />
  )
}

export function FilterSelect({
  value,
  onChange,
  className = '',
  children,
}: {
  value: number | ''
  onChange: (value: number | '') => void
  className?: string
  children: ReactNode
}) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value === '' ? '' : Number(e.target.value))}
      className={`rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm ${className}`}
    >
      {children}
    </select>
  )
}

export function DataTable({ children }: { children: ReactNode }) {
  return <Card className="overflow-hidden">{children}</Card>
}

export function DataTableMeta({ children }: { children: ReactNode }) {
  return (
    <div className="border-b border-slate-200 bg-slate-50/70 px-3 py-2 text-xs font-medium text-slate-500">
      {children}
    </div>
  )
}

export function TableCell({
  children,
  align = 'start',
  className = '',
}: {
  children: ReactNode
  align?: 'start' | 'end' | 'center'
  className?: string
}) {
  const alignClass = align === 'end' ? 'text-end' : align === 'center' ? 'text-center' : 'text-start'
  return <td className={`px-3 py-2.5 ${alignClass} ${className}`}>{children}</td>
}

export function TableHeadCell({
  children,
  align = 'start',
  className = '',
}: {
  children: ReactNode
  align?: 'start' | 'end' | 'center'
  className?: string
}) {
  const alignClass = align === 'end' ? 'text-end' : align === 'center' ? 'text-center' : 'text-start'
  return <th className={`px-3 py-2.5 text-xs font-semibold uppercase tracking-wide text-slate-500 ${alignClass} ${className}`}>{children}</th>
}

export function StatCard({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Card className="p-4">
      <div className="text-xs uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-2 text-2xl font-semibold text-slate-900">{value}</div>
    </Card>
  )
}

export function Spinner({ label }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-3 py-10 text-sm text-slate-500">
      <span className="border-t-brand-600 inline-block h-4 w-4 animate-spin rounded-full border-2 border-slate-300" />
      {label}
    </div>
  )
}

export function ErrorBox({
  message,
  onRetry,
  retryLabel,
}: {
  message: string
  onRetry?: () => void
  retryLabel?: string
}) {
  return (
    <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-800">
      <div className="font-medium">{message}</div>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-2 inline-flex items-center rounded-md border border-red-300 bg-white px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-100"
        >
          {retryLabel ?? 'Retry'}
        </button>
      )}
    </div>
  )
}

export function Badge({
  tone = 'slate',
  children,
}: {
  tone?: 'green' | 'amber' | 'red' | 'slate' | 'blue'
  children: ReactNode
}) {
  const tones: Record<string, string> = {
    green: 'bg-green-100 text-green-800',
    amber: 'bg-amber-100 text-amber-800',
    red: 'bg-red-100 text-red-800',
    blue: 'bg-blue-100 text-blue-800',
    slate: 'bg-slate-100 text-slate-700',
  }
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${tones[tone]}`}
    >
      {children}
    </span>
  )
}
