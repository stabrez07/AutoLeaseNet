'use client'

import { useCallback, useEffect, useState } from 'react'
import { useLocale } from '../../lib/locale-provider'
import { bff, type MyLease } from '../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, Spinner, statusTone } from '../../components/ui'

type StatusKey = keyof ReturnType<typeof useLocale>['t']['leases']['statuses']

function formatDate(iso: string): string {
  // Toggle to ISO date only — the table doesn't need time-of-day precision.
  return iso.slice(0, 10)
}

function formatMoney(amount: number): string {
  return amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

export default function MyLeasesPage() {
  const { t } = useLocale()
  const [leases, setLeases] = useState<MyLease[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setError(null)
    try {
      setLeases(await bff.getMyLeases())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : t.common.error)
    }
  }, [t.common.error])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <div>
      <PageHeader title={t.leases.title} subtitle={t.leases.subtitle} />

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {!error && !leases && <Spinner label={t.common.loading} />}

      {leases && leases.length === 0 && (
        <Card className="p-8 text-center text-sm text-slate-500">{t.leases.empty}</Card>
      )}

      {leases && leases.length > 0 && (
        <Card className="overflow-hidden">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-2.5 font-medium">{t.leases.columns.contractNumber}</th>
                <th className="px-4 py-2.5 font-medium">{t.leases.columns.status}</th>
                <th className="px-4 py-2.5 font-medium">{t.leases.columns.start}</th>
                <th className="px-4 py-2.5 font-medium">{t.leases.columns.end}</th>
                <th className="px-4 py-2.5 text-end font-medium">{t.leases.columns.rent}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {leases.map((l) => {
                const statusKey = l.status as StatusKey
                const statusLabel =
                  (t.leases.statuses as Record<number, string>)[statusKey] ?? `#${l.status}`
                return (
                  <tr key={l.id}>
                    <td className="px-4 py-2.5 font-mono text-xs text-slate-700">
                      {l.tajeerContractNumber ?? '—'}
                    </td>
                    <td className="px-4 py-2.5">
                      <Badge tone={statusTone(l.status)}>{statusLabel}</Badge>
                    </td>
                    <td className="px-4 py-2.5 text-slate-700">{formatDate(l.contractStartUtc)}</td>
                    <td className="px-4 py-2.5 text-slate-700">{formatDate(l.contractEndUtc)}</td>
                    <td className="px-4 py-2.5 text-end text-slate-700">
                      {formatMoney(l.rentAmount)}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  )
}
