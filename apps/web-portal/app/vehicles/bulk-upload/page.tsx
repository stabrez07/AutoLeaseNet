'use client'

import { useRef, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useLocale } from '../../../lib/locale-provider'
import { bff, type BulkImportResult } from '../../../lib/bff-client'
import { Card, PageHeader, PrimaryButton, SecondaryButton } from '../../../components/ui'

const CSV_HEADER = 'plateNumber,plateLetters,plateTypeCode,vin,make,model,modelYear,color,fuelType,transmissionType,bodyType,seats,ownerBranchId,currentKm'
const CSV_EXAMPLE = '1234,أ ب ج,1,VIN000000000001,Toyota,Camry,2024,White,1,1,1,5,00000000-0000-0000-0000-000000000001,12500'

function downloadTemplate() {
  const content = `${CSV_HEADER}\n${CSV_EXAMPLE}\n`
  const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'vehicle-import-template.csv'
  a.click()
  URL.revokeObjectURL(url)
}

export default function BulkUploadPage() {
  const { t } = useLocale()
  const router = useRouter()
  const bu = t.crudVehicles.bulkUpload
  const fileRef = useRef<HTMLInputElement>(null)

  const [file, setFile] = useState<File | null>(null)
  const [dragging, setDragging] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [result, setResult] = useState<BulkImportResult | null>(null)
  const [uploadError, setUploadError] = useState<string | null>(null)

  function handleFile(f: File) {
    setFile(f)
    setResult(null)
    setUploadError(null)
  }

  function handleDrop(e: React.DragEvent) {
    e.preventDefault(); setDragging(false)
    const f = e.dataTransfer.files[0]
    if (f) handleFile(f)
  }

  async function handleUpload() {
    if (!file) return
    setUploading(true); setUploadError(null); setResult(null)
    try {
      const res = await bff.bulkImportVehicles(file, crypto.randomUUID())
      setResult(res)
    } catch (e) {
      setUploadError((e as Error).message)
    } finally {
      setUploading(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <PageHeader
        title={bu.title}
        subtitle={bu.subtitle}
        action={
          <div className="flex gap-2">
            <SecondaryButton onClick={downloadTemplate}>{t.crudVehicles.actions.downloadTemplate}</SecondaryButton>
            <SecondaryButton onClick={() => router.back()}>{t.common.back}</SecondaryButton>
          </div>
        }
      />

      {/* Template hint */}
      <Card className="p-4">
        <p className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-2">CSV columns (in order)</p>
        <code className="block rounded bg-slate-100 p-2 text-xs text-slate-600 break-all">{bu.templateCols}</code>
        <p className="mt-2 text-xs text-slate-500">
          fuelType: 1=Petrol91 2=Petrol95 3=Diesel 4=Hybrid 5=Electric · transmissionType: 1=Automatic 2=Manual 3=CVT · bodyType: 1=Sedan 2=SUV 3=Hatchback 4=Pickup 5=Van 6=Bus 7=Coupe
        </p>
      </Card>

      {/* Drop zone */}
      <Card className="p-0 overflow-hidden">
        <div
          onDragOver={(e) => { e.preventDefault(); setDragging(true) }}
          onDragLeave={() => setDragging(false)}
          onDrop={handleDrop}
          onClick={() => fileRef.current?.click()}
          className={`flex cursor-pointer flex-col items-center justify-center gap-3 rounded-xl p-10 transition-colors ${
            dragging ? 'bg-brand-50 border-2 border-dashed border-brand-400' : 'bg-slate-50 border-2 border-dashed border-slate-200 hover:border-slate-300 hover:bg-white'
          }`}
        >
          <svg xmlns="http://www.w3.org/2000/svg" className="h-10 w-10 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
          </svg>
          <p className="text-sm font-medium text-slate-600">{bu.dropzone}</p>
          {file && <p className="rounded bg-brand-100 px-3 py-1 text-xs font-semibold text-brand-700">{file.name}</p>}
          <input ref={fileRef} type="file" accept=".csv" className="sr-only" onChange={(e) => { const f = e.target.files?.[0]; if (f) handleFile(f) }} />
        </div>

        {file && (
          <div className="flex justify-end border-t border-slate-200 bg-white px-4 py-3">
            <PrimaryButton onClick={handleUpload} disabled={uploading}>
              {uploading ? bu.uploading : t.crudVehicles.actions.bulkUpload}
            </PrimaryButton>
          </div>
        )}
      </Card>

      {/* Error */}
      {uploadError && (
        <Card className="border-red-200 bg-red-50 p-4">
          <p className="text-sm text-red-700">{uploadError}</p>
        </Card>
      )}

      {/* Result */}
      {result && (
        <div className="space-y-3">
          <Card className={`p-4 ${result.success ? 'border-green-200 bg-green-50' : 'border-amber-200 bg-amber-50'}`}>
            <div className="grid grid-cols-3 gap-4 text-center">
              <div>
                <p className="text-2xl font-bold text-green-700">{result.createdCount}</p>
                <p className="text-xs text-green-600 font-medium">{bu.result.created}</p>
              </div>
              <div>
                <p className="text-2xl font-bold text-slate-600">{result.skippedCount}</p>
                <p className="text-xs text-slate-500 font-medium">{bu.result.skipped}</p>
              </div>
              <div>
                <p className="text-2xl font-bold text-red-600">{result.errors.length}</p>
                <p className="text-xs text-red-500 font-medium">{bu.result.errors}</p>
              </div>
            </div>
          </Card>

          {result.errors.length > 0 && (
            <Card className="overflow-hidden">
              <div className="border-b border-slate-200 bg-slate-50 px-4 py-3">
                <h3 className="text-sm font-semibold text-slate-700">{bu.result.errors}</h3>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead className="border-b border-slate-200 bg-white text-left">
                    <tr>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{bu.errorTable.row}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{bu.errorTable.code}</th>
                      <th className="px-3 py-2 font-medium uppercase tracking-wide text-slate-500">{bu.errorTable.message}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.errors.map((err, i) => (
                      <tr key={i} className="border-t border-slate-100">
                        <td className="px-3 py-2 font-mono text-slate-600">{err.rowIndex}</td>
                        <td className="px-3 py-2 font-mono text-red-600">{err.errorCode}</td>
                        <td className="px-3 py-2 text-slate-700">{err.errorMessage}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>
          )}

          {result.createdCount > 0 && (
            <div className="flex justify-end">
              <PrimaryButton onClick={() => router.push('/vehicles')}>
                View Fleet →
              </PrimaryButton>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
