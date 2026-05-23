'use client'

import { useEffect, useState } from 'react'
import { useLocale } from '../lib/locale-provider'
import { bff } from '../lib/bff-client'
import { Card, ErrorBox, PageHeader, Spinner, StatCard } from '../components/ui'

interface Stats {
  vehiclesAvailable: number
  vehiclesOnLease: number
  driversCount: number
  customersCount: number
}

export default function HomePage() {
  const { t } = useLocale()
  const [stats, setStats] = useState<Stats | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setError(null)
    setStats(null)
    try {
      const [available, onLease, drivers, customers] = await Promise.all([
        bff.getVehicles(1, 1, undefined, 1),
        bff.getVehicles(1, 1, undefined, 3),
        bff.getDrivers(1, 1),
        bff.getCustomers(1, 1),
      ])
      setStats({
        vehiclesAvailable: available.totalCount,
        vehiclesOnLease: onLease.totalCount,
        driversCount: drivers.totalCount,
        customersCount: customers.totalCount,
      })
    } catch (e) {
      setError((e as Error).message)
    }
  }

  useEffect(() => {
    load()
  }, [])

  return (
    <div className="space-y-6">
      <PageHeader title={t.dashboard.title} subtitle={t.dashboard.subtitle} />

      <Card className="border-amber-200 bg-amber-50 p-4">
        <p className="text-sm text-amber-900">{t.dashboard.seedBanner}</p>
      </Card>

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {!error && !stats && <Spinner label={t.common.loading} />}

      {stats && (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
          <StatCard label={t.dashboard.cards.activeLeases} value={stats.vehiclesOnLease} />
          <StatCard label={t.dashboard.cards.pendingIssuance} value={'—'} />
          <StatCard label={t.dashboard.cards.vehiclesAvailable} value={stats.vehiclesAvailable} />
          <StatCard label={t.dashboard.cards.driversValid} value={stats.driversCount} />
        </div>
      )}
    </div>
  )
}
