'use client'

import Image from 'next/image'
import { useEffect, useState } from 'react'

const STORAGE_KEY = 'autolead_company_logo'
const DEFAULT_LOGO = '/company-logo.png'

export function getLogoUrl(): string {
  if (typeof window === 'undefined') return DEFAULT_LOGO
  return localStorage.getItem(STORAGE_KEY) || DEFAULT_LOGO
}

export function setLogoUrl(dataUrl: string) {
  localStorage.setItem(STORAGE_KEY, dataUrl)
  window.dispatchEvent(new Event('logo-changed'))
}

export function clearLogoUrl() {
  localStorage.removeItem(STORAGE_KEY)
  window.dispatchEvent(new Event('logo-changed'))
}

export function CompanyLogo({ className = '', width = 160, height = 48 }: {
  className?: string
  width?: number
  height?: number
}) {
  const [src, setSrc] = useState(DEFAULT_LOGO)

  useEffect(() => {
    setSrc(getLogoUrl())
    const handler = () => setSrc(getLogoUrl())
    window.addEventListener('logo-changed', handler)
    window.addEventListener('storage', handler)
    return () => {
      window.removeEventListener('logo-changed', handler)
      window.removeEventListener('storage', handler)
    }
  }, [])

  const isDataUrl = src.startsWith('data:')

  if (isDataUrl) {
    // eslint-disable-next-line @next/next/no-img-element
    return <img src={src} alt="Company Logo" width={width} height={height} className={`object-contain ${className}`} />
  }

  return (
    <Image
      src={src}
      alt="Company Logo"
      width={width}
      height={height}
      className={`object-contain ${className}`}
      priority
    />
  )
}

export function CompanyLogoUploader() {
  const [preview, setPreview] = useState<string | null>(null)

  useEffect(() => {
    const current = getLogoUrl()
    if (current !== DEFAULT_LOGO) setPreview(current)
  }, [])

  function handleFile(file: File) {
    if (!file.type.startsWith('image/')) return
    if (file.size > 2 * 1024 * 1024) {
      alert('Logo must be under 2 MB')
      return
    }
    const reader = new FileReader()
    reader.onload = () => {
      const dataUrl = reader.result as string
      setLogoUrl(dataUrl)
      setPreview(dataUrl)
    }
    reader.readAsDataURL(file)
  }

  function handleDrop(e: React.DragEvent) {
    e.preventDefault()
    const file = e.dataTransfer.files[0]
    if (file) handleFile(file)
  }

  function handleReset() {
    clearLogoUrl()
    setPreview(null)
  }

  return (
    <div className="space-y-3">
      <div
        onDrop={handleDrop}
        onDragOver={(e) => e.preventDefault()}
        className="flex flex-col items-center gap-3 rounded-lg border-2 border-dashed border-slate-300 bg-slate-50 p-6 transition-colors hover:border-brand-400"
      >
        {preview ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={preview} alt="Current logo" className="max-h-16 max-w-[240px] object-contain" />
        ) : (
          <CompanyLogo width={200} height={60} />
        )}
        <p className="text-xs text-slate-500">Drag & drop a logo here, or click to browse</p>
        <label className="cursor-pointer rounded-md bg-brand-700 px-4 py-1.5 text-xs font-medium text-white hover:bg-brand-800">
          Choose File
          <input
            type="file"
            accept="image/png,image/jpeg,image/svg+xml,image/webp"
            className="hidden"
            onChange={(e) => { const f = e.target.files?.[0]; if (f) handleFile(f) }}
          />
        </label>
      </div>
      {preview && (
        <button
          type="button"
          onClick={handleReset}
          className="text-xs text-red-600 hover:text-red-800"
        >
          Reset to default logo
        </button>
      )}
      <p className="text-[10px] text-slate-400">
        Recommended: PNG or SVG, transparent background, max 2 MB. Used in print headers and sidebar.
      </p>
    </div>
  )
}
