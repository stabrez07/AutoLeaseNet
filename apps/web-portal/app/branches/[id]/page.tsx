'use client'

import { useEffect, useState } from 'react'
import { useRouter, useParams } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type BranchDetail } from '../../../lib/bff-client'
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

export default function BranchDetailPage() {
  const { t, locale } = useLocale()
  const router = useRouter()
  const { id } = useParams<{ id: string }>()
  const f = t.crudBranches.fields
  const [data, setData] = useState<BranchDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [actionBusy, setActionBusy] = useState(false)
  const [actionMsg, setActionMsg] = useState<string | null>(null)

  async function load() {
    setLoading(true); setError(null)
    try { setData(await bff.getBranchById(id)) }
    catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }

  useEffect(() => { load() }, [id])

  async function toggleStatus(activate: boolean) {
    setActionBusy(true); setActionMsg(null)
    try {
      const res = await bff.updateBranchStatus(id, activate, crypto.randomUUID())
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
  if (error) return <ErrorBox message={error} retryLabel={t.common.retry} />
  if (!data) return <p className="text-sm text-slate-500">{t.common.notFound}</p>

  const displayName = locale === 'ar' ? data.nameAr : data.nameEn

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <PageHeader
        title={displayName}
        subtitle={`${data.code} · ${data.id}`}
        action={<SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>}
      />
      <div className="flex gap-2">
        <Badge tone={data.isActive ? 'green' : 'slate'}>{data.isActive ? t.common.yes : t.common.no}</Badge>
      </div>

      <Card className="divide-y divide-slate-100 p-6 space-y-4">
        <div>
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">{t.crudBranches.sections.identity}</h3>
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
            <Field label={f.code} value={data.code} />
            <Field label={f.nameEn} value={data.nameEn} />
            <Field label={f.nameAr} value={data.nameAr} />
            <Field label={f.licenseNumber} value={data.licenseNumber} />
            <Field label={f.phoneNumber} value={data.phoneNumber} />
          </div>
        </div>
        <div className="pt-4">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">{t.crudBranches.sections.location}</h3>
          <div className="grid grid-cols-2 gap-x-6 gap-y-3 md:grid-cols-3">
            <Field label={f.cityEn} value={data.cityEn} />
            <Field label={f.cityAr} value={data.cityAr} />
            <Field label={f.regionEn} value={data.regionEn} />
            <Field label={f.regionAr} value={data.regionAr} />
            <Field label={f.address} value={data.address} />
            <Field label={f.latitude} value={data.latitude} />
            <Field label={f.longitude} value={data.longitude} />
          </div>
        </div>
        <div className="pt-4">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">{t.crudBranches.sections.tajeer}</h3>
          <div className="grid grid-cols-2 gap-x-6 gap-y-3">
            <Field label={f.tajeerBranchId} value={data.tajeerBranchId} />
            <Field label={f.tajeerOperatorId} value={data.tajeerOperatorId} />
          </div>
        </div>
        <div className="pt-4">
          <div className="grid grid-cols-2 gap-x-6 gap-y-3">
            <Field label={t.common.createdAt} value={data.createdAtUtc?.substring(0, 10)} />
            <Field label={t.common.updatedAt} value={data.updatedAtUtc?.substring(0, 10)} />
          </div>
        </div>
      </Card>

      {/* Status action */}
      <Card className="p-4">
        <h3 className="mb-3 text-sm font-semibold text-slate-700">{t.common.actions}</h3>
        {actionMsg && (
          <p className={`mb-3 rounded px-3 py-1.5 text-sm ${actionMsg === t.common.successCreated ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
            {actionMsg}
          </p>
        )}
        <div className="flex gap-2">
          {!data.isActive ? (
            <PrimaryButton onClick={() => toggleStatus(true)} disabled={actionBusy}>
              {t.crudBranches.actions.activate}
            </PrimaryButton>
          ) : (
            <SecondaryButton onClick={() => toggleStatus(false)} disabled={actionBusy}>
              {t.crudBranches.actions.deactivate}
            </SecondaryButton>
          )}
        </div>
      </Card>
    </div>
  )
}
