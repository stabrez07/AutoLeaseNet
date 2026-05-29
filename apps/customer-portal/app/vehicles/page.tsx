'use client'

import Link from 'next/link'
import { useCallback, useEffect, useState } from 'react'
import { useLocale } from '../../lib/locale-provider'
import { bff, type MyVehicle } from '../../lib/bff-client'
import { Card, ErrorBox, PageHeader, Spinner } from '../../components/ui'

function formatDate(iso: string | null): string {
  return iso ? iso.slice(0, 10) : '—'
}

function formatKm(km: number): string {
  return km.toLocaleString()
}

export default function MyVehiclesPage() {
  const { t } = useLocale()
  const [vehicles, setVehicles] = useState<MyVehicle[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setError(null)
    try {
      setVehicles(await bff.getMyVehicles())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : t.common.error)
    }
  }, [t.common.error])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <div>
      <PageHeader title={t.vehicles.title} subtitle={t.vehicles.subtitle} />

      {error && <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />}
      {!error && !vehicles && <Spinner label={t.common.loading} />}

      {vehicles && vehicles.length === 0 && (
        <Card className="p-8 text-center text-sm text-slate-500">{t.vehicles.empty}</Card>
      )}

      {vehicles && vehicles.length > 0 && (
        <Card className="overflow-hidden">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-2.5 font-medium">{t.vehicles.columns.plate}</th>
                <th className="px-4 py-2.5 font-medium">{t.vehicles.columns.makeModel}</th>
                <th className="px-4 py-2.5 font-medium">{t.vehicles.columns.year}</th>
                <th className="px-4 py-2.5 font-medium">{t.vehicles.columns.color}</th>
                <th className="px-4 py-2.5 text-end font-medium">{t.vehicles.columns.km}</th>
                <th className="px-4 py-2.5 font-medium">{t.vehicles.columns.licenseExpiry}</th>
                <th className="px-4 py-2.5 font-medium">{t.vehicles.columns.insuranceExpiry}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {vehicles.map((v) => (
                <tr key={v.id}>
                  <td className="px-4 py-2.5">
                    <Link
                      href={`/vehicles/${v.id}`}
                      className="text-brand-700 hover:text-brand-900 underline-offset-4 hover:underline"
                    >
                      <span dir="rtl" className="font-mono text-xs">
                        {v.plateLetters}&nbsp;&nbsp;{v.plateNumber}
                      </span>
                    </Link>
                  </td>
                  <td className="px-4 py-2.5 text-slate-700">
                    {v.make} {v.model}
                  </td>
                  <td className="px-4 py-2.5 text-slate-700">{v.modelYear}</td>
                  <td className="px-4 py-2.5 text-slate-700">{v.color ?? '—'}</td>
                  <td className="px-4 py-2.5 text-end text-slate-700">{formatKm(v.currentKm)}</td>
                  <td className="px-4 py-2.5 text-slate-700">{formatDate(v.licenseExpiryDate)}</td>
                  <td className="px-4 py-2.5 text-slate-700">{formatDate(v.insuranceExpiryDate)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  )
}
