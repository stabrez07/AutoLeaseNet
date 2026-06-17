'use client'

import { useEffect, useState } from 'react'
import { useParams, useRouter } from 'next/navigation'
import {
  bff, type AdvancePayment, type StatementOfAccount,
  type PagedResult, type RecordAdvancePaymentRequest, type PaymentMethod,
} from '../../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, PrimaryButton, SecondaryButton, Spinner } from '../../../../components/ui'

function fmt(n: number) {
  return `SAR ${n.toLocaleString('en', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

type Tab = 'payments' | 'soa'

const PAY_METHODS: PaymentMethod[] = ['Cash', 'CreditCard', 'BankTransfer', 'Cheque', 'OnlineTransfer']

export default function CustomerAccountPage() {
  const params = useParams()
  const router = useRouter()
  const customerId = params?.id as string

  const [tab, setTab] = useState<Tab>('payments')
  const [customerName, setCustomerName] = useState('')
  const [payments, setPayments] = useState<PagedResult<AdvancePayment> | null>(null)
  const [soa, setSoa] = useState<StatementOfAccount | null>(null)
  const [soaFrom, setSoaFrom] = useState(new Date(Date.now() - 90 * 86400000).toISOString().substring(0, 10))
  const [soaTo, setSoaTo] = useState(new Date().toISOString().substring(0, 10))
  const [loading, setLoading] = useState(true)
  const [soaLoading, setSoaLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [saving, setSaving] = useState(false)
  const [applyingFifo, setApplyingFifo] = useState(false)
  const [form, setForm] = useState<Partial<RecordAdvancePaymentRequest>>({
    customerId, paymentMethod: 'BankTransfer',
    receivedDate: new Date().toISOString().substring(0, 10), autoApplyFifo: true,
  })

  useEffect(() => {
    bff.getCustomerById(customerId).then((c) => setCustomerName(c.displayName)).catch(() => {})
    bff.getCustomerAdvancePayments(customerId).then(setPayments).catch((e: Error) => setError(e.message)).finally(() => setLoading(false))
  }, [customerId])

  async function loadSoa() {
    setSoaLoading(true)
    try { setSoa(await bff.getStatementOfAccount(customerId, soaFrom, soaTo)) }
    catch (e) { setError((e as Error).message) }
    finally { setSoaLoading(false) }
  }

  useEffect(() => { if (tab === 'soa') loadSoa() }, [tab]) // eslint-disable-line react-hooks/exhaustive-deps

  async function handleRecord() {
    if (!form.amount || !form.paymentMethod || !form.receivedDate) { alert('Fill all required fields'); return }
    setSaving(true)
    try {
      const pmt = await bff.recordAdvancePayment(form as RecordAdvancePaymentRequest, crypto.randomUUID())
      setPayments((prev) => prev ? { ...prev, items: [pmt, ...prev.items], totalCount: prev.totalCount + 1 } : null)
      setShowForm(false)
      setForm({ customerId, paymentMethod: 'BankTransfer', receivedDate: new Date().toISOString().substring(0, 10), autoApplyFifo: true })
    } catch (e) { alert((e as Error).message) }
    finally { setSaving(false) }
  }

  async function handleApplyFifo() {
    if (!confirm('Auto-apply all available credit balances to oldest outstanding invoices (FIFO)?')) return
    setApplyingFifo(true)
    try {
      const res = await bff.applyFifoPayments(customerId, crypto.randomUUID())
      alert(`FIFO applied: ${res.allocations} allocations, total ${fmt(res.totalAllocatedSar)}`)
      bff.getCustomerAdvancePayments(customerId).then(setPayments)
    } catch (e) { alert((e as Error).message) }
    finally { setApplyingFifo(false) }
  }

  function downloadPaymentsCsv() {
    if (!payments) return
    const rows = [['ID', 'Date', 'Amount', 'Method', 'Ref #', 'Remaining Balance', 'Allocations']]
    payments.items.forEach((p) => rows.push([p.id, p.receivedDate, String(p.amount), p.paymentMethod, p.referenceNumber ?? '', String(p.remainingBalance), String(p.allocations.length)]))
    const csv = rows.map((r) => r.join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `payments-${customerId}.csv`; a.click()
  }

  function downloadSoaCsv() {
    if (!soa) return
    const rows = [['Date', 'Type', 'Reference', 'Description', 'Debit (SAR)', 'Credit (SAR)', 'Balance (SAR)']]
    soa.transactions.forEach((t) => rows.push([t.date, t.type, t.reference, t.description, String(t.debitSar), String(t.creditSar), String(t.balanceSar)]))
    const csv = rows.map((r) => r.join(',')).join('\n')
    const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `soa-${customerId}-${soaFrom}-${soaTo}.csv`; a.click()
  }

  const totalCredit = payments?.items.reduce((s, p) => s + p.remainingBalance, 0) ?? 0

  return (
    <div className="space-y-4">
      <PageHeader
        title={`Account — ${customerName}`}
        subtitle="Advance payments, FIFO allocation, and statement of account."
        action={
          <div className="flex gap-2">
            <SecondaryButton onClick={() => router.push(`/customers/${customerId}`)} className="px-3 py-1.5 text-xs">← Customer Profile</SecondaryButton>
          </div>
        }
      />

      {/* Summary cards */}
      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        <Card className="p-4">
          <p className="text-xs text-slate-500">Total Payments</p>
          <p className="mt-1 text-xl font-bold text-slate-900">{payments?.totalCount ?? '—'}</p>
        </Card>
        <Card className="p-4">
          <p className="text-xs text-slate-500">Available Credit</p>
          <p className={`mt-1 text-xl font-bold ${totalCredit > 0 ? 'text-green-700' : 'text-slate-400'}`}>{fmt(totalCredit)}</p>
        </Card>
        <Card className="p-4 md:col-span-2">
          <p className="text-xs text-slate-500">FIFO Auto-Apply</p>
          <p className="mt-1 text-sm text-slate-600">Allocates available credit to oldest outstanding invoices first.</p>
        </Card>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 border-b border-slate-200">
        {([['payments', 'Advance Payments'], ['soa', 'Statement of Account']] as [Tab, string][]).map(([key, label]) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key)}
            className={`px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px
              ${tab === key ? 'border-brand-600 text-brand-700' : 'border-transparent text-slate-500 hover:text-slate-700'}`}
          >
            {label}
          </button>
        ))}
      </div>

      {/* ── Advance Payments Tab ── */}
      {tab === 'payments' && (
        <div className="space-y-4">
          {error && <ErrorBox message={error} onRetry={() => bff.getCustomerAdvancePayments(customerId).then(setPayments)} retryLabel="Retry" />}
          {loading && <Spinner label="Loading payments…" />}

          {!loading && (
            <>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm text-slate-500">{payments?.totalCount ?? 0} payment{(payments?.totalCount ?? 0) !== 1 ? 's' : ''}</p>
                <div className="flex gap-2">
                  <SecondaryButton onClick={downloadPaymentsCsv} className="px-3 py-1.5 text-xs">Export CSV</SecondaryButton>
                  <SecondaryButton onClick={handleApplyFifo} disabled={applyingFifo || totalCredit === 0} className="px-3 py-1.5 text-xs text-blue-700 border-blue-300">
                    {applyingFifo ? 'Applying…' : '⚡ Apply FIFO'}
                  </SecondaryButton>
                  <PrimaryButton onClick={() => setShowForm((f) => !f)} className="px-3 py-1.5 text-xs">{showForm ? 'Cancel' : '+ Record Payment'}</PrimaryButton>
                </div>
              </div>

              {showForm && (
                <Card className="p-4 space-y-4">
                  <h4 className="font-semibold text-slate-800">Record Advance Payment</h4>
                  <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
                    <div>
                      <label className="mb-1 block text-xs font-medium text-slate-600">Amount (SAR) *</label>
                      <input type="number" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={form.amount ?? ''} onChange={(e) => { const v = parseFloat(e.target.value); setForm((f) => ({ ...f, ...(v ? { amount: v } : {}) })) }} placeholder="0.00" />
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-medium text-slate-600">Payment Method *</label>
                      <select className="w-full rounded-lg border border-slate-300 px-2 py-2 text-sm" value={form.paymentMethod ?? 'BankTransfer'} onChange={(e) => setForm((f) => ({ ...f, paymentMethod: e.target.value as PaymentMethod }))}>
                        {PAY_METHODS.map((m) => <option key={m} value={m}>{m}</option>)}
                      </select>
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-medium text-slate-600">Received Date *</label>
                      <input type="date" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={form.receivedDate ?? ''} onChange={(e) => setForm((f) => ({ ...f, receivedDate: e.target.value }))} />
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-medium text-slate-600">Reference Number</label>
                      <input type="text" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={form.referenceNumber ?? ''} onChange={(e) => { const v = e.target.value; setForm((f) => ({ ...f, ...(v ? { referenceNumber: v } : {}) })) }} placeholder="REF-..." />
                    </div>
                    <div className="flex items-center gap-2 pt-5">
                      <input type="checkbox" id="fifoAuto" checked={form.autoApplyFifo ?? true} onChange={(e) => setForm((f) => ({ ...f, autoApplyFifo: e.target.checked }))} />
                      <label htmlFor="fifoAuto" className="text-sm text-slate-700">Auto-apply FIFO on save</label>
                    </div>
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-medium text-slate-600">Notes</label>
                    <input type="text" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm" value={form.notes ?? ''} onChange={(e) => { const v = e.target.value; setForm((f) => ({ ...f, ...(v ? { notes: v } : {}) })) }} />
                  </div>
                  <div className="flex gap-2">
                    <PrimaryButton onClick={handleRecord} disabled={saving} className="px-4 py-2 text-sm">{saving ? 'Saving…' : 'Record Payment'}</PrimaryButton>
                    <SecondaryButton onClick={() => setShowForm(false)} className="px-4 py-2 text-sm">Cancel</SecondaryButton>
                  </div>
                </Card>
              )}

              {(payments?.items.length ?? 0) === 0 ? (
                <div className="rounded-xl border border-dashed border-slate-300 py-12 text-center text-sm text-slate-400">No advance payments recorded yet.</div>
              ) : (
                <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
                  <table className="w-full text-sm">
                    <thead className="border-b border-slate-200 bg-slate-50/80">
                      <tr>
                        {['Date', 'Method', 'Amount', 'Ref #', 'Remaining', 'Allocations', 'Applied To'].map((h) => (
                          <th key={h} className="px-3 py-2.5 text-left text-xs font-semibold text-slate-600">{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {payments?.items.map((p) => (
                        <tr key={p.id} className="border-t border-slate-100 hover:bg-slate-50/60">
                          <td className="px-3 py-2">{p.receivedDate}</td>
                          <td className="px-3 py-2 text-slate-600">{p.paymentMethod}</td>
                          <td className="px-3 py-2 font-mono text-xs font-semibold">{fmt(p.amount)}</td>
                          <td className="px-3 py-2 font-mono text-xs text-slate-500">{p.referenceNumber ?? '—'}</td>
                          <td className={`px-3 py-2 font-mono text-xs font-semibold ${p.remainingBalance > 0 ? 'text-green-700' : 'text-slate-400'}`}>{fmt(p.remainingBalance)}</td>
                          <td className="px-3 py-2 text-center">
                            <Badge tone={p.allocations.length > 0 ? 'green' : 'slate'}>{p.allocations.length}</Badge>
                          </td>
                          <td className="px-3 py-2 text-xs text-slate-500">
                            {p.allocations.slice(0, 2).map((a) => a.invoiceNumber).join(', ')}
                            {p.allocations.length > 2 && ` +${p.allocations.length - 2} more`}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          )}
        </div>
      )}

      {/* ── Statement of Account Tab ── */}
      {tab === 'soa' && (
        <div className="space-y-4">
          <div className="flex flex-wrap items-end gap-3">
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">From</label>
              <input type="date" className="rounded-lg border border-slate-300 px-3 py-2 text-sm" value={soaFrom} onChange={(e) => setSoaFrom(e.target.value)} />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">To</label>
              <input type="date" className="rounded-lg border border-slate-300 px-3 py-2 text-sm" value={soaTo} onChange={(e) => setSoaTo(e.target.value)} />
            </div>
            <PrimaryButton onClick={loadSoa} disabled={soaLoading} className="px-4 py-2 text-sm">
              {soaLoading ? 'Loading…' : 'Generate SOA'}
            </PrimaryButton>
            {soa && <SecondaryButton onClick={downloadSoaCsv} className="px-3 py-2 text-sm">Download CSV</SecondaryButton>}
          </div>

          {soaLoading && <Spinner label="Generating statement…" />}

          {soa && !soaLoading && (
            <>
              {/* SOA summary */}
              <div className="grid grid-cols-3 gap-4">
                <Card className="p-4">
                  <p className="text-xs text-slate-500">Total Invoiced</p>
                  <p className="mt-1 text-xl font-bold text-slate-900">{fmt(soa.totalInvoiced)}</p>
                </Card>
                <Card className="p-4">
                  <p className="text-xs text-slate-500">Total Paid</p>
                  <p className="mt-1 text-xl font-bold text-green-700">{fmt(soa.totalPaid)}</p>
                </Card>
                <Card className={`p-4 ${soa.closingBalance > 0 ? 'border-red-200 bg-red-50' : 'border-green-200 bg-green-50'}`}>
                  <p className="text-xs text-slate-500">Closing Balance</p>
                  <p className={`mt-1 text-xl font-bold ${soa.closingBalance > 0 ? 'text-red-700' : 'text-green-700'}`}>{fmt(soa.closingBalance)}</p>
                </Card>
              </div>

              {/* Print header */}
              <Card className="p-4 print:shadow-none">
                <div className="mb-3 flex items-center justify-between">
                  <div>
                    <p className="text-lg font-bold text-slate-900">Statement of Account</p>
                    <p className="text-sm text-slate-600">{soa.customerDisplayName}</p>
                    <p className="text-xs text-slate-400">Period: {soa.periodFrom} to {soa.periodTo} · Generated: {new Date(soa.generatedAtUtc).toLocaleString('en-GB')}</p>
                  </div>
                </div>

                {soa.transactions.length === 0 ? (
                  <div className="py-8 text-center text-sm text-slate-400">No transactions in this period.</div>
                ) : (
                  <div className="overflow-hidden rounded-lg border border-slate-200">
                    <table className="w-full text-sm">
                      <thead className="border-b border-slate-200 bg-slate-50/80">
                        <tr>
                          {['Date', 'Type', 'Reference', 'Description', 'Debit (SAR)', 'Credit (SAR)', 'Balance (SAR)'].map((h) => (
                            <th key={h} className={`px-3 py-2.5 text-xs font-semibold text-slate-600 ${h.includes('SAR') ? 'text-right' : 'text-left'}`}>{h}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {soa.transactions.map((t, i) => (
                          <tr key={i} className={`border-t border-slate-100 ${t.type === 'Invoice' ? 'hover:bg-red-50/30' : 'hover:bg-green-50/30'}`}>
                            <td className="px-3 py-2 text-slate-600 text-xs">{t.date}</td>
                            <td className="px-3 py-2"><Badge tone={t.type === 'Invoice' ? 'amber' : 'green'}>{t.type}</Badge></td>
                            <td className="px-3 py-2 font-mono text-xs">{t.reference}</td>
                            <td className="px-3 py-2 text-slate-700 text-xs">{t.description}</td>
                            <td className="px-3 py-2 text-right font-mono text-xs text-red-700">{t.debitSar > 0 ? fmt(t.debitSar) : '—'}</td>
                            <td className="px-3 py-2 text-right font-mono text-xs text-green-700">{t.creditSar > 0 ? fmt(t.creditSar) : '—'}</td>
                            <td className={`px-3 py-2 text-right font-mono text-xs font-semibold ${t.balanceSar > 0 ? 'text-red-700' : t.balanceSar < 0 ? 'text-green-700' : 'text-slate-500'}`}>
                              {fmt(Math.abs(t.balanceSar))} {t.balanceSar < 0 ? 'CR' : t.balanceSar > 0 ? 'DR' : ''}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                      <tfoot className="border-t-2 border-slate-300 bg-slate-50/80 font-semibold">
                        <tr>
                          <td colSpan={4} className="px-3 py-2 text-sm font-semibold text-slate-800">Closing Balance</td>
                          <td className="px-3 py-2 text-right font-mono text-xs text-red-700">{fmt(soa.totalInvoiced)}</td>
                          <td className="px-3 py-2 text-right font-mono text-xs text-green-700">{fmt(soa.totalPaid)}</td>
                          <td className={`px-3 py-2 text-right font-mono text-xs font-bold ${soa.closingBalance > 0 ? 'text-red-700' : 'text-green-700'}`}>
                            {fmt(Math.abs(soa.closingBalance))} {soa.closingBalance > 0 ? 'DR' : 'CR'}
                          </td>
                        </tr>
                      </tfoot>
                    </table>
                  </div>
                )}
              </Card>
            </>
          )}
        </div>
      )}
    </div>
  )
}
