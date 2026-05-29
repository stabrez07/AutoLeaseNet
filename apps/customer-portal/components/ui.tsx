'use client'

import type { ReactNode } from 'react'

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
    <div className={`rounded-lg border border-slate-200 bg-white shadow-sm ${className}`}>
      {children}
    </div>
  )
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
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${tones[tone]}`}
    >
      {children}
    </span>
  )
}

/**
 * Maps a LeaseStatus enum value to the right badge tone. Codes match the
 * Domain/Leases/LeaseStatus.cs enum: 0=Draft, 1=SaveFailed, 2=PendingIssuance,
 * 3=Active, 4=Extended, 5=Suspended, 6=Closed, 7=Cancelled, 8=ExpiredDraft.
 * Kept in one place so the dashboard + leases table agree visually.
 */
export function statusTone(status: number): 'green' | 'amber' | 'red' | 'slate' | 'blue' {
  switch (status) {
    case 3: // Active
      return 'green'
    case 4: // Extended
      return 'blue'
    case 5: // Suspended
      return 'amber'
    case 6: // Closed
      return 'slate'
    case 1: // SaveFailed
    case 7: // Cancelled
    case 8: // ExpiredDraft
      return 'red'
    case 0: // Draft
    case 2: // PendingIssuance
    default:
      return 'amber'
  }
}
