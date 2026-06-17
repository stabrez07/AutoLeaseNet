'use client'

import Link from 'next/link'
import { useCallback, useEffect, useState } from 'react'
import { useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type InvoiceStatus, type MyInvoice } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, SecondaryButton, Spinner } from '../../../components/ui'

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatSar(n: number): string {
  return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function invoiceTone(status: InvoiceStatus): 'green' | 'amber' | 'red' | 'slate' | 'blue' {
  switch (status) {
    case 'Paid':
      return 'green'
    case 'Issued':
      return 'blue'
    case 'PartiallyPaid':
      return 'amber'
    case 'Overdue':
      return 'red'
    case 'Draft':
    case 'Cancelled':
    default:
      return 'slate'
  }
}

function downloadCsv(inv: MyInvoice) {
  const headers = [
    'Line #',
    'Description',
    'Plate (EN)',
    'Plate (AR)',
    'Qty',
    'Unit Price (SAR)',
    'VAT %',
    'VAT Amount (SAR)',
    'Line Total (SAR)',
  ]
  const rows = inv.lines.map((l) => [
    l.lineNumber,
    l.description,
    l.plateNumberEn ?? '',
    l.plateNumberAr ?? '',
    l.quantity,
    l.unitPriceSar.toFixed(2),
    l.vatPercent,
    l.vatAmountSar.toFixed(2),
    l.lineTotalSar.toFixed(2),
  ])
  const csv = [headers, ...rows]
    .map((r) => r.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(','))
    .join('\n')
  const blob = new Blob([csv], { type: 'text/csv' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${inv.invoiceNumber}.csv`
  a.click()
  URL.revokeObjectURL(url)
}

// ─── Sub-components ──────────────────────────────────────────────────────────

function SectionHeading({ children }: { children: React.ReactNode }) {
  return (
    <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
      {children}
    </h2>
  )
}

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-slate-100 py-1.5 last:border-b-0">
      <dt className="text-xs uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className="text-end text-sm font-medium text-slate-800">{value}</dd>
    </div>
  )
}

function TotalsRow({
  label,
  value,
  highlight = false,
}: {
  label: string
  value: string
  highlight?: boolean
}) {
  return (
    <div
      className={[
        'flex justify-between gap-4 border-b border-slate-100 py-1.5 last:border-b-0',
        highlight ? 'font-semibold text-slate-900' : 'text-slate-700',
      ].join(' ')}
    >
      <span className="text-sm">{label}</span>
      <span className="text-sm">{value}</span>
    </div>
  )
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function InvoiceDetailPage() {
  const { t } = useLocale()
  const params = useParams<{ id: string }>()
  const id = params?.id
  const [invoice, setInvoice] = useState<MyInvoice | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notFound, setNotFound] = useState(false)

  const load = useCallback(async () => {
    if (!id) return
    setError(null)
    setNotFound(false)
    try {
      setInvoice(await bff.getMyInvoiceById(id))
    } catch (e: unknown) {
      const status = (e as { status?: number })?.status
      if (status === 404) {
        setNotFound(true)
        return
      }
      setError(e instanceof Error ? e.message : t.common.error)
    }
  }, [id, t.common.error])

  useEffect(() => {
    void load()
  }, [load])

  const backLink = (
    <Link
      href="/invoices"
      className="print:hidden text-brand-700 hover:text-brand-900 text-sm font-medium underline-offset-4 hover:underline"
    >
      ← {t.invoices.backToList}
    </Link>
  )

  if (notFound) {
    return (
      <div>
        <PageHeader title={t.invoices.title} action={backLink} />
        <Card className="p-8 text-center text-sm text-slate-500">{t.invoices.notFound}</Card>
      </div>
    )
  }

  if (error) {
    return (
      <div>
        <PageHeader title={t.invoices.title} action={backLink} />
        <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />
      </div>
    )
  }

  if (!invoice) {
    return (
      <div>
        <PageHeader title={t.invoices.title} action={backLink} />
        <Spinner label={t.common.loading} />
      </div>
    )
  }

  const sec = t.invoices.sections
  const statuses = t.invoices.statuses

  return (
    <div className="space-y-6">
      {/* Actions bar — hidden on print */}
      <div className="print:hidden flex items-center gap-3">
        {backLink}
        <div className="ms-auto flex gap-2">
          <SecondaryButton onClick={() => downloadCsv(invoice)}>
            {t.invoices.download}
          </SecondaryButton>
          <SecondaryButton onClick={() => window.print()}>
            {t.invoices.print}
          </SecondaryButton>
        </div>
      </div>

      {/* ── Invoice header (print-first layout) ── */}
      <Card className="p-6">
        <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
          {/* Supplier block */}
          <div>
            <div className="text-2xl font-bold text-slate-900">{invoice.supplierName}</div>
            <div className="mt-1 text-sm text-slate-600">
              CR No: <span className="font-medium">{invoice.supplierCrNo}</span>
            </div>
            <div className="text-sm text-slate-600">
              VAT No: <span className="font-medium">{invoice.supplierVatNo}</span>
            </div>
          </div>

          {/* Invoice identity block */}
          <div className="text-end">
            <div className="text-lg font-semibold uppercase tracking-widest text-slate-700">
              TAX INVOICE
            </div>
            <div className="mt-1 font-mono text-xl font-bold text-slate-900">
              {invoice.invoiceNumber}
            </div>
            <div className="mt-2">
              <Badge tone={invoiceTone(invoice.status)}>{statuses[invoice.status]}</Badge>
            </div>
            {invoice.zatcaInvoiceNumber && (
              <div className="mt-1 text-xs text-slate-500">
                ZATCA: <span className="font-mono">{invoice.zatcaInvoiceNumber}</span>
              </div>
            )}
          </div>
        </div>
      </Card>

      {/* ── Bill To + Contract details ── */}
      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
        <Card className="p-5">
          <SectionHeading>{sec.billTo}</SectionHeading>
          <dl>
            <InfoRow label={t.leaseDetail.contract.number} value={invoice.leaseNumber} />
            <InfoRow label={t.vehicles.columns.makeModel} value={invoice.vehicleMakeModel} />
            <InfoRow
              label={t.vehicles.columns.plate}
              value={
                <span dir="rtl" className="font-mono">
                  {invoice.vehiclePlateAr}
                </span>
              }
            />
          </dl>
        </Card>

        <Card className="p-5">
          <SectionHeading>{sec.contract}</SectionHeading>
          <dl>
            <InfoRow label={t.invoices.columns.period} value={`${invoice.billingPeriodStart} — ${invoice.billingPeriodEnd}`} />
            <InfoRow label={t.invoices.columns.issuedDate} value={invoice.issuedDate} />
            <InfoRow label={t.invoices.columns.dueDate} value={invoice.dueDate} />
            {invoice.quotationNumber !== null && (
              <InfoRow label="Quotation #" value={invoice.quotationNumber} />
            )}
            {invoice.poNumber !== null && (
              <InfoRow label="PO #" value={invoice.poNumber} />
            )}
          </dl>
        </Card>
      </div>

      {/* ── Line items ── */}
      <Card className="overflow-hidden">
        <div className="border-b border-slate-200 px-5 py-3">
          <SectionHeading>{sec.lines}</SectionHeading>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-2.5 text-start font-medium">Line #</th>
                <th className="px-4 py-2.5 text-start font-medium">Plate (EN)</th>
                <th className="px-4 py-2.5 text-start font-medium">
                  <span dir="rtl">Plate (AR)</span>
                </th>
                <th className="px-4 py-2.5 text-start font-medium">Description</th>
                <th className="px-4 py-2.5 text-end font-medium">Qty</th>
                <th className="px-4 py-2.5 text-end font-medium">Unit Price</th>
                <th className="px-4 py-2.5 text-end font-medium">VAT %</th>
                <th className="px-4 py-2.5 text-end font-medium">VAT Amount</th>
                <th className="px-4 py-2.5 text-end font-medium">Line Total</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {invoice.lines.map((line) => (
                <tr key={line.id}>
                  <td className="px-4 py-2.5 text-slate-700">{line.lineNumber}</td>
                  <td className="px-4 py-2.5 font-mono text-xs text-slate-700">
                    {line.plateNumberEn ?? '—'}
                  </td>
                  <td className="px-4 py-2.5 font-mono text-xs text-slate-700" dir="rtl">
                    {line.plateNumberAr ?? '—'}
                  </td>
                  <td className="px-4 py-2.5 text-slate-800">{line.description}</td>
                  <td className="px-4 py-2.5 text-end text-slate-700">{line.quantity}</td>
                  <td className="px-4 py-2.5 text-end text-slate-700">
                    {formatSar(line.unitPriceSar)}
                  </td>
                  <td className="px-4 py-2.5 text-end text-slate-700">{line.vatPercent}%</td>
                  <td className="px-4 py-2.5 text-end text-slate-700">
                    {formatSar(line.vatAmountSar)}
                  </td>
                  <td className="px-4 py-2.5 text-end font-medium text-slate-900">
                    {formatSar(line.lineTotalSar)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      {/* ── Totals ── */}
      <Card className="p-5">
        <SectionHeading>{sec.totals}</SectionHeading>
        <div className="ms-auto max-w-sm">
          <TotalsRow label={t.leaseDetail.payment.rent} value={formatSar(invoice.subTotalSar)} />
          <TotalsRow label={`${t.leaseDetail.payment.vat} (15%)`} value={formatSar(invoice.vatAmountSar)} />
          <TotalsRow label={t.leaseDetail.payment.total} value={formatSar(invoice.totalSar)} highlight />
          <TotalsRow label={t.leaseDetail.payment.paid} value={formatSar(invoice.paidAmountSar)} />
          <TotalsRow
            label={t.invoices.columns.balance}
            value={formatSar(invoice.balanceSar)}
            highlight={invoice.balanceSar > 0}
          />
        </div>
      </Card>

      {/* Notes */}
      {invoice.notes !== null && (
        <Card className="p-5">
          <SectionHeading>Notes</SectionHeading>
          <p className="text-sm text-slate-700">{invoice.notes}</p>
        </Card>
      )}
    </div>
  )
}
