'use client'

import Link from 'next/link'
import { useCallback, useEffect, useState } from 'react'
import { useLocale } from '../lib/locale-provider'
import { bff, type MyLease } from '../lib/bff-client'
import { PageHeader, StatCard, Spinner, ErrorBox } from '../components/ui'

export default function DashboardPage() {
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

  if (error) {
    return (
      <div>
        <PageHeader title={t.dashboard.title} subtitle={t.dashboard.subtitle} />
        <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />
      </div>
    )
  }

  if (!leases) {
    return (
      <div>
        <PageHeader title={t.dashboard.title} subtitle={t.dashboard.subtitle} />
        <Spinner label={t.common.loading} />
      </div>
    )
  }

  const total = leases.length
  const active = leases.filter((l) => l.status === 2 || l.status === 3).length
  const closed = leases.filter((l) => l.status === 5).length

  return (
    <div>
      <PageHeader title={t.dashboard.title} subtitle={t.dashboard.subtitle} />
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <StatCard label={t.dashboard.cards.total} value={total} />
        <StatCard label={t.dashboard.cards.active} value={active} />
        <StatCard label={t.dashboard.cards.closed} value={closed} />
      </div>
      <div className="mt-6">
        <Link
          href="/leases"
          className="text-brand-700 hover:text-brand-900 inline-block text-sm font-medium underline-offset-4 hover:underline"
        >
          {t.dashboard.cta} →
        </Link>
      </div>
    </div>
  )
}
