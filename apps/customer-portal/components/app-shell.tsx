'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useState, type ReactNode } from 'react'
import { useLocale } from '../lib/locale-provider'
import { SUPPORTED_LOCALES } from '../lib/i18n'
import { DEV_DEMO_CUSTOMER } from '../lib/dev-customer'

const ICONS = {
  grid: <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}><path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6A2.25 2.25 0 016 3.75h2.25A2.25 2.25 0 0110.5 6v2.25a2.25 2.25 0 01-2.25 2.25H6a2.25 2.25 0 01-2.25-2.25V6zM3.75 15.75A2.25 2.25 0 016 13.5h2.25a2.25 2.25 0 012.25 2.25V18a2.25 2.25 0 01-2.25 2.25H6A2.25 2.25 0 013.75 18v-2.25zM13.5 6a2.25 2.25 0 012.25-2.25H18A2.25 2.25 0 0120.25 6v2.25A2.25 2.25 0 0118 10.5h-2.25a2.25 2.25 0 01-2.25-2.25V6zM13.5 15.75a2.25 2.25 0 012.25-2.25H18a2.25 2.25 0 012.25 2.25V18A2.25 2.25 0 0118 20.25h-2.25A2.25 2.25 0 0113.5 18v-2.25z" /></svg>,
  fileText: <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}><path strokeLinecap="round" strokeLinejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" /></svg>,
  truck: <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}><path strokeLinecap="round" strokeLinejoin="round" d="M8.25 18.75a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m3 0h6m-9 0H3.375a1.125 1.125 0 01-1.125-1.125V14.25m17.25 4.5a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m3 0h1.125c.621 0 1.129-.504 1.09-1.124a17.902 17.902 0 00-3.213-9.193 2.056 2.056 0 00-1.58-.86H14.25M16.5 18.75h-2.25m0-11.177v-.958c0-.568-.422-1.048-.987-1.106a48.554 48.554 0 00-10.026 0 1.106 1.106 0 00-.987 1.106v7.635m12-6.677v6.677m0 4.5v-4.5m0 0h-12" /></svg>,
  receipt: <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}><path strokeLinecap="round" strokeLinejoin="round" d="M9 14.25l6-6m4.5-3.493V21.75l-3.75-1.5-3.75 1.5-3.75-1.5-3.75 1.5V4.757c0-1.108.806-2.057 1.907-2.185a48.507 48.507 0 0111.186 0c1.1.128 1.907 1.077 1.907 2.185zM9.75 9h.008v.008H9.75V9zm.375 0a.375.375 0 11-.75 0 .375.375 0 01.75 0zm4.125 4.5h.008v.008h-.008V13.5zm.375 0a.375.375 0 11-.75 0 .375.375 0 01.75 0z" /></svg>,
  menu: <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}><path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" /></svg>,
  close: <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}><path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" /></svg>,
}

type IconKey = keyof typeof ICONS

interface NavItem { href: string; label: string; icon: IconKey }
interface NavGroup { title: string; items: NavItem[] }

const NAV_GROUPS: NavGroup[] = [
  {
    title: 'MY ACCOUNT',
    items: [
      { href: '/', label: 'dashboard', icon: 'grid' },
      { href: '/leases', label: 'myLeases', icon: 'fileText' },
      { href: '/vehicles', label: 'myVehicles', icon: 'truck' },
    ],
  },
  {
    title: 'BILLING',
    items: [
      { href: '/invoices', label: 'myInvoices', icon: 'receipt' },
    ],
  },
]

function isActive(pathname: string, href: string): boolean {
  if (href === '/') return pathname === '/'
  return pathname === href || pathname.startsWith(href + '/')
}

export function AppShell({ children }: { children: ReactNode }) {
  const { locale, setLocale, t } = useLocale()
  const pathname = usePathname() ?? '/'
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const navLabel = (key: string): string => {
    return (t.nav as Record<string, string>)[key] ?? key
  }

  const sidebar = (
    <nav className="flex h-full flex-col bg-brand-800">
      <div className="flex-1 overflow-y-auto py-4">
        {NAV_GROUPS.map((group) => (
          <div key={group.title} className="mb-4">
            <div className="px-4 py-2 text-[10px] font-semibold uppercase tracking-wider text-brand-300">
              {group.title}
            </div>
            {group.items.map((item) => {
              const active = isActive(pathname, item.href)
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  onClick={() => setSidebarOpen(false)}
                  className={[
                    'flex items-center gap-2.5 px-4 py-2 text-sm transition-colors',
                    active
                      ? 'border-l-2 border-white bg-brand-900/50 text-white'
                      : 'border-l-2 border-transparent text-brand-100 hover:bg-brand-900/30 hover:text-white',
                  ].join(' ')}
                >
                  {ICONS[item.icon]}
                  <span>{navLabel(item.label)}</span>
                </Link>
              )
            })}
          </div>
        ))}
      </div>
      <div className="border-t border-brand-700 px-4 py-3 text-[10px] text-brand-300">
        Customer Portal
      </div>
    </nav>
  )

  return (
    <div className="flex min-h-screen">
      {/* Desktop sidebar */}
      <aside className="hidden w-56 shrink-0 md:block">{sidebar}</aside>

      {/* Mobile overlay */}
      {sidebarOpen && (
        <div className="fixed inset-0 z-40 md:hidden">
          <div className="absolute inset-0 bg-black/50" onClick={() => setSidebarOpen(false)} />
          <div className="relative z-50 h-full w-56">
            <button
              type="button"
              onClick={() => setSidebarOpen(false)}
              className="absolute top-3 right-3 z-50 text-brand-200 hover:text-white"
              aria-label="Close menu"
            >
              {ICONS.close}
            </button>
            {sidebar}
          </div>
        </div>
      )}

      {/* Main content area */}
      <div className="flex min-w-0 flex-1 flex-col">
        {/* Top bar */}
        <header className="flex h-12 items-center justify-between border-b border-slate-200 bg-white px-4">
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={() => setSidebarOpen(true)}
              className="text-slate-600 hover:text-slate-900 md:hidden"
              aria-label="Open menu"
            >
              {ICONS.menu}
            </button>
            <div>
              <span className="font-semibold text-slate-900">Auto Lead Company</span>
              <span className="ms-2 text-xs text-slate-500">Customer Portal</span>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <span className="text-xs text-slate-500">
              {t.signedInAs}: <span className="font-semibold text-slate-700">{DEV_DEMO_CUSTOMER.displayName}</span>
            </span>
            <div className="flex items-center rounded-md border border-slate-200 p-0.5">
              {SUPPORTED_LOCALES.map((l) => (
                <button
                  key={l}
                  type="button"
                  onClick={() => setLocale(l)}
                  className={[
                    'rounded px-2 py-0.5 text-xs transition-colors',
                    locale === l
                      ? 'bg-brand-700 font-semibold text-white'
                      : 'text-slate-500 hover:text-slate-700',
                  ].join(' ')}
                  aria-pressed={locale === l}
                  aria-label={l === 'en' ? t.english : t.arabic}
                >
                  {l === 'en' ? 'EN' : 'ع'}
                </button>
              ))}
            </div>
          </div>
        </header>

        {/* Main */}
        <main className="flex-1 overflow-auto p-4 md:p-6">
          <div className="mx-auto max-w-7xl">
            <div className="mb-4 rounded-md border border-amber-200 bg-amber-50 px-4 py-2 text-xs text-amber-800">
              {t.devBanner}
            </div>
            {children}
          </div>
        </main>
      </div>
    </div>
  )
}
