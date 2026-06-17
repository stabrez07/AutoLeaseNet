'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { bff, type BulkGenerateResult } from '../../../lib/bff-client'
import { Card, PageHeader, PrimaryButton, SecondaryButton } from '../../../components/ui'

export default function BulkGenerateInvoicesPage() {
  const router = useRouter()
  const now = new Date()
  const defaultMonth = now.toISOString().substring(0, 7)
  const [month, setMonth] = useState(defaultMonth)
  const [generating, setGenerating] = useState(false)
  const [result, setResult] = useState<BulkGenerateResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  function getMonthRange(m: string) {
    const [y, mo] = m.split('-').map(Number)
    const start = `${m}-01`
    const lastDay = new Date(y!, mo!, 0).getDate()
    const end = `${m}-${String(lastDay).padStart(2, '0')}`
    return { start, end }
  }

  async function handleGenerate() {
    setGenerating(true); setError(null); setResult(null)
    try {
      const { start, end } = getMonthRange(month)
      const res = await bff.bulkGenerateInvoices(start, end, crypto.randomUUID())
      setResult(res)
    } catch (e) {
      setError((e as Error).message)
    } finally { setGenerating(false) }
  }

  const { start, end } = getMonthRange(month)

  return (
    <div className="space-y-4">
      <PageHeader
        title="Bulk Generate Invoices"
        subtitle="Generate monthly rental invoices for all active contracts in one click."
        action={<SecondaryButton onClick={() => router.push('/invoices')} className="px-3 py-1.5 text-xs">← Back to Invoices</SecondaryButton>}
      />

      <Card className="max-w-lg p-6">
        <h3 className="mb-4 font-semibold text-slate-800">Select Billing Month</h3>

        <div className="space-y-4">
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">Billing Month</label>
            <input
              type="month"
              value={month}
              onChange={(e) => setMonth(e.target.value)}
              className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
            />
          </div>

          <div className="rounded-lg bg-slate-50 p-3 text-sm text-slate-600">
            <p>Billing period: <span className="font-semibold">{start}</span> to <span className="font-semibold">{end}</span></p>
            <p className="mt-1 text-xs text-slate-400">Invoices will be generated for all Active and Extended contracts. Already-generated invoices for this period will be skipped.</p>
          </div>

          {error && <p className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

          <PrimaryButton onClick={handleGenerate} disabled={generating} className="w-full py-2.5 text-sm">
            {generating ? 'Generating invoices…' : `Generate Invoices for ${month}`}
          </PrimaryButton>
        </div>
      </Card>

      {result && (
        <div className="space-y-4">
          <div className="grid grid-cols-3 gap-4">
            <Card className="p-4 text-center">
              <p className="text-3xl font-bold text-green-700">{result.generated}</p>
              <p className="mt-1 text-sm text-slate-600">Generated</p>
            </Card>
            <Card className="p-4 text-center">
              <p className="text-3xl font-bold text-slate-500">{result.skipped}</p>
              <p className="mt-1 text-sm text-slate-600">Skipped (already exists)</p>
            </Card>
            <Card className="p-4 text-center">
              <p className="text-3xl font-bold text-red-600">{result.errors.length}</p>
              <p className="mt-1 text-sm text-slate-600">Errors</p>
            </Card>
          </div>

          {result.errors.length > 0 && (
            <Card className="p-4">
              <h4 className="mb-3 font-semibold text-slate-800">Errors</h4>
              <div className="space-y-2">
                {result.errors.map((e, i) => (
                  <div key={i} className="flex items-start gap-2 rounded-lg bg-red-50 px-3 py-2 text-sm">
                    <span className="font-mono font-semibold text-red-700">{e.leaseNumber}</span>
                    <span className="text-red-600">{e.error}</span>
                  </div>
                ))}
              </div>
            </Card>
          )}

          {result.generated > 0 && (
            <div className="flex gap-3">
              <PrimaryButton onClick={() => router.push('/invoices?status=Issued')}>View Generated Invoices</PrimaryButton>
              <SecondaryButton onClick={() => setResult(null)}>Generate Another Month</SecondaryButton>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
