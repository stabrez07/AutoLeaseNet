'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import type { ReactNode } from 'react'
import { useLocale } from '../lib/locale-provider'
import { SUPPORTED_LOCALES } from '../lib/i18n'

const navItems = [
  { href: '/', key: 'dashboard' as const },
  { href: '/customers', key: 'customers' as const },
  { href: '/vehicles', key: 'vehicles' as const },
  { href: '/drivers', key: 'drivers' as const },
  { href: '/branches', key: 'branches' as const },
  { href: '/quotations', key: 'quotations' as const },
  { href: '/leases/new', key: 'newLease' as const },
]

export function AppShell({ children }: { children: ReactNode }) {
  const { locale, setLocale, t } = useLocale()
  const pathname = usePathname() ?? '/'

  return (
    <div className="flex min-h-screen flex-col bg-slate-50 text-slate-900">
      <header className="bg-brand-800 text-white shadow">
        <div className="mx-auto flex max-w-7xl items-center gap-6 px-4 py-3">
          <Link href="/" className="text-lg font-semibold tracking-tight">
            {t.appName}
          </Link>
          <span className="text-brand-100 hidden text-xs md:inline">{t.portalSubtitle}</span>
          <nav className="ms-auto flex items-center gap-1 text-sm">
            {navItems.map((item) => {
              const active = item.href === '/' ? pathname === '/' : pathname.startsWith(item.href)
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={[
                    'rounded-md px-3 py-1.5 transition-colors',
                    active
                      ? 'bg-white/20 text-white'
                      : 'text-brand-100 hover:bg-white/10 hover:text-white',
                  ].join(' ')}
                >
                  {t.nav[item.key]}
                </Link>
              )
            })}
            <div className="ms-3 flex items-center rounded-md bg-white/10 p-0.5">
              {SUPPORTED_LOCALES.map((l) => (
                <button
                  key={l}
                  type="button"
                  onClick={() => setLocale(l)}
                  className={[
                    'rounded-md px-2.5 py-1 text-xs transition-colors',
                    locale === l
                      ? 'text-brand-800 bg-white font-semibold'
                      : 'text-brand-50 hover:text-white',
                  ].join(' ')}
                  aria-pressed={locale === l}
                  aria-label={l === 'en' ? t.english : t.arabic}
                >
                  {l === 'en' ? 'EN' : 'ع'}
                </button>
              ))}
            </div>
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6">{children}</main>
      <footer className="border-t border-slate-200 py-3 text-center text-xs text-slate-500">
        AutoLeaseNet · Phase 1 preview · BFF:{' '}
        <code>{process.env.NEXT_PUBLIC_BFF_BASE_URL ?? 'http://localhost:5000'}</code>
      </footer>
    </div>
  )
}
