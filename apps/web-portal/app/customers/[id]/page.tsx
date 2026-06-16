'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type CustomerDetail } from '../../../lib/bff-client'
import { Badge, Card, ErrorBox, PageHeader, PrimaryButton, SecondaryButton, Spinner } from '../../../components/ui'

function Field({ label, value }: { label: string; value: string | number | boolean | null | undefined }) {
  return (
    <div>
      <div className="text-xs text-slate-500">{label}</div>
      <div className="mt-0.5 text-sm font-medium text-slate-900">
        {value === null || value === undefined || value === '' ? '—' : String(value)}
      </div>
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-400">{title}</h3>
      <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">{children}</div>
    </div>
  )
}

export default function CustomerDetailPage() {
  const { t } = useLocale()
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const c = t.crudCustomers
  const [data, setData] = useState<CustomerDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [actionBusy, setActionBusy] = useState(false)
  const [actionMsg, setActionMsg] = useState<string | null>(null)

  async function load() {
    setLoading(true); setError(null)
    try { setData(await bff.getCustomerById(id)) }
    catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => { load() }, [id])

  async function handleStatusAction(action: string) {
    setActionBusy(true); setActionMsg(null)
    try {
      const res = await bff.updateCustomerStatus(id, action, crypto.randomUUID())
      if (!res.success) throw new Error(res.errorMessage ?? res.errorCode ?? 'Failed')
      setActionMsg(t.common.successCreated)
      await load()
    } catch (e) {
      setActionMsg((e as Error).message)
    } finally {
      setActionBusy(false)
    }
  }

  if (loading) return <Spinner label={t.common.loading} />
  if (error) return <ErrorBox message={error} onRetry={load} retryLabel={t.common.retry} />
  if (!data) return <p className="text-sm text-slate-500">{t.common.notFound}</p>

  const isB2B = data.type === 'B2B'
  const statusTone = data.status === 'Active' ? 'green' : data.status === 'Suspended' ? 'amber' : 'slate'

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <PageHeader
        title={data.displayName}
        subtitle={`${data.type} · ${data.id}`}
        action={
          <SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>
        }
      />

      <div className="flex items-center gap-3">
        <Badge tone={statusTone}>{c.statuses[data.status as keyof typeof c.statuses] ?? data.status}</Badge>
        <Badge tone={isB2B ? 'blue' : 'slate'}>{isB2B ? t.customers.type.b2b : t.customers.type.b2c}</Badge>
        {data.kycVerified && <Badge tone="green">{c.kycBadge}</Badge>}
      </div>

      <Card className="divide-y divide-slate-100 p-6">
        {isB2B ? (
          <Section title={c.sections.identity}>
            <Field label={c.fields.legalName} value={data.legalName} />
            <Field label={c.fields.legalNameAr} value={data.legalNameAr} />
            <Field label={c.fields.commercialReg} value={data.commercialRegistration} />
            <Field label={c.fields.vatNumber} value={data.vatNumber} />
            <Field label={c.fields.creditLimit} value={data.creditLimit != null ? `${data.creditLimit} ${data.creditCurrency ?? ''}` : undefined} />
          </Section>
        ) : (
          <Section title={c.sections.identity}>
            <Field label={c.fields.personNameEn} value={data.personNameEn} />
            <Field label={c.fields.personNameAr} value={data.personNameAr} />
            <Field label={c.fields.idTypeCode} value={c.idTypes[data.idTypeCode as keyof typeof c.idTypes] ?? data.idTypeCode} />
            <Field label={c.fields.personIdNumber} value={data.personIdNumber} />
            <Field label={c.fields.dateOfBirth} value={data.dateOfBirth} />
            <Field label={c.fields.nationalityCode} value={data.nationalityCode} />
          </Section>
        )}

        <div className="pt-4">
          <Section title={c.sections.contact}>
            <Field label={c.fields.email} value={data.email} />
            <Field label={c.fields.mobile} value={data.mobile} />
            <Field label={c.fields.nationalAddress} value={data.nationalAddress} />
            {isB2B && <Field label={c.fields.billingAddress} value={data.billingAddress} />}
          </Section>
        </div>

        <div className="pt-4">
          <Section title={t.common.details}>
            <Field label={t.common.id} value={data.id} />
            <Field label={t.common.createdAt} value={data.createdAtUtc?.substring(0, 10)} />
            <Field label={t.common.updatedAt} value={data.updatedAtUtc?.substring(0, 10)} />
          </Section>
        </div>
      </Card>

      {/* Status actions */}
      <Card className="p-4">
        <h3 className="mb-3 text-sm font-semibold text-slate-700">{t.common.actions}</h3>
        {actionMsg && (
          <p className={`mb-3 rounded px-3 py-1.5 text-sm ${actionMsg.includes('success') || actionMsg.includes('بنجاح') ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
            {actionMsg}
          </p>
        )}
        <div className="flex flex-wrap gap-2">
          {data.status === 'Active' && (
            <SecondaryButton onClick={() => handleStatusAction('suspend')} disabled={actionBusy}>
              {c.actions.suspend}
            </SecondaryButton>
          )}
          {data.status === 'Suspended' && (
            <PrimaryButton onClick={() => handleStatusAction('reactivate')} disabled={actionBusy}>
              {c.actions.reactivate}
            </PrimaryButton>
          )}
          {data.status !== 'Closed' && (
            <SecondaryButton onClick={() => handleStatusAction('close')} disabled={actionBusy}>
              {c.actions.close}
            </SecondaryButton>
          )}
        </div>
      </Card>
    </div>
  )
}
