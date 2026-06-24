'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useRouter } from 'next/navigation'
import type { ReactNode } from 'react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { bff, type NotificationItem } from '../lib/bff-client'
import { useLocale } from '../lib/locale-provider'
import { SUPPORTED_LOCALES } from '../lib/i18n'
import { CompanyLogo } from './company-logo'

/* ---------------------------------------------------------------------------
 * Inline SVG icons (16x16, stroke-current, strokeWidth 1.5)
 * No external icon library.
 * -------------------------------------------------------------------------*/

const ICONS = {
  grid: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6A2.25 2.25 0 016 3.75h2.25A2.25 2.25 0 0110.5 6v2.25a2.25 2.25 0 01-2.25 2.25H6a2.25 2.25 0 01-2.25-2.25V6zM3.75 15.75A2.25 2.25 0 016 13.5h2.25a2.25 2.25 0 012.25 2.25V18a2.25 2.25 0 01-2.25 2.25H6A2.25 2.25 0 013.75 18v-2.25zM13.5 6a2.25 2.25 0 012.25-2.25H18A2.25 2.25 0 0120.25 6v2.25A2.25 2.25 0 0118 10.5h-2.25a2.25 2.25 0 01-2.25-2.25V6zM13.5 15.75a2.25 2.25 0 012.25-2.25H18a2.25 2.25 0 012.25 2.25V18A2.25 2.25 0 0118 20.25h-2.25A2.25 2.25 0 0113.5 18v-2.25z" />
    </svg>
  ),
  fileText: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
    </svg>
  ),
  plus: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
    </svg>
  ),
  clipboard: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h3.75M9 15h3.75M9 18h3.75m3 .75H18a2.25 2.25 0 002.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 00-1.123-.08m-5.801 0c-.065.21-.1.433-.1.664 0 .414.336.75.75.75h4.5a.75.75 0 00.75-.75 2.25 2.25 0 00-.1-.664m-5.8 0A2.251 2.251 0 0113.5 2.25H15c1.012 0 1.867.668 2.15 1.586m-5.8 0c-.376.023-.75.05-1.124.08C9.095 4.01 8.25 4.973 8.25 6.108V8.25m0 0H4.875c-.621 0-1.125.504-1.125 1.125v11.25c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V9.375c0-.621-.504-1.125-1.125-1.125H8.25z" />
    </svg>
  ),
  truck: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M8.25 18.75a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m3 0h6m-9 0H3.375a1.125 1.125 0 01-1.125-1.125V14.25m17.25 4.5a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m3 0h1.125c.621 0 1.129-.504 1.09-1.124a17.902 17.902 0 00-3.213-9.193 2.056 2.056 0 00-1.58-.86H14.25M16.5 18.75h-2.25m0-11.177v-.958c0-.568-.422-1.048-.987-1.106a48.554 48.554 0 00-10.026 0 1.106 1.106 0 00-.987 1.106v7.635m12-6.677v6.677m0 4.5v-4.5m0 0h-12" />
    </svg>
  ),
  users: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M15 19.128a9.38 9.38 0 002.625.372 9.337 9.337 0 004.121-.952 4.125 4.125 0 00-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 018.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0111.964-3.07M12 6.375a3.375 3.375 0 11-6.75 0 3.375 3.375 0 016.75 0zm8.25 2.25a2.625 2.625 0 11-5.25 0 2.625 2.625 0 015.25 0z" />
    </svg>
  ),
  mapPin: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M15 10.5a3 3 0 11-6 0 3 3 0 016 0z" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 0115 0z" />
    </svg>
  ),
  receipt: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M9 14.25l6-6m4.5-3.493V21.75l-3.75-1.5-3.75 1.5-3.75-1.5-3.75 1.5V4.757c0-1.108.806-2.057 1.907-2.185a48.507 48.507 0 0111.186 0c1.1.128 1.907 1.077 1.907 2.185zM9.75 9h.008v.008H9.75V9zm.375 0a.375.375 0 11-.75 0 .375.375 0 01.75 0zm4.125 4.5h.008v.008h-.008V13.5zm.375 0a.375.375 0 11-.75 0 .375.375 0 01.75 0z" />
    </svg>
  ),
  refresh: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182M2.985 19.644l3.181-3.183" />
    </svg>
  ),
  creditCard: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M2.25 8.25h19.5M2.25 9h19.5m-16.5 5.25h6m-6 2.25h3m-3.75 3h15a2.25 2.25 0 002.25-2.25V6.75A2.25 2.25 0 0019.5 4.5h-15a2.25 2.25 0 00-2.25 2.25v10.5A2.25 2.25 0 004.5 19.5z" />
    </svg>
  ),
  building: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 21h16.5M4.5 3h15M5.25 3v18m13.5-18v18M9 6.75h1.5m-1.5 3h1.5m-1.5 3h1.5m3-6H15m-1.5 3H15m-1.5 3H15M9 21v-3.375c0-.621.504-1.125 1.125-1.125h3.75c.621 0 1.125.504 1.125 1.125V21" />
    </svg>
  ),
  cog: (
    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.324.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 011.37.49l1.296 2.247a1.125 1.125 0 01-.26 1.431l-1.003.827c-.293.24-.438.613-.431.992a6.759 6.759 0 010 .255c-.007.378.138.75.43.99l1.005.828c.424.35.534.954.26 1.43l-1.298 2.247a1.125 1.125 0 01-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.57 6.57 0 01-.22.128c-.331.183-.581.495-.644.869l-.213 1.28c-.09.543-.56.941-1.11.941h-2.594c-.55 0-1.02-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 01-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 01-1.369-.49l-1.297-2.247a1.125 1.125 0 01.26-1.431l1.004-.827c.292-.24.437-.613.43-.992a6.932 6.932 0 010-.255c.007-.378-.138-.75-.43-.99l-1.004-.828a1.125 1.125 0 01-.26-1.43l1.297-2.247a1.125 1.125 0 011.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.087.22-.128.332-.183.582-.495.644-.869l.214-1.281z" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
    </svg>
  ),
  menu: (
    <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
    </svg>
  ),
  close: (
    <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
    </svg>
  ),
}

/* ---------------------------------------------------------------------------
 * Navigation structure — grouped by category
 * -------------------------------------------------------------------------*/

type NavKey =
  | 'dashboard'
  | 'customers'
  | 'accounts'
  | 'leads'
  | 'quotations'
  | 'contracts'
  | 'approvals'
  | 'leaseAgreements'
  | 'vehicles'
  | 'drivers'
  | 'invoices'
  | 'generateInvoices'
  | 'payments'
  | 'branches'
  | 'setup'

interface NavItem {
  href: string
  key: NavKey
  icon: keyof typeof ICONS
}

interface NavGroup {
  labelEn: string
  labelAr: string
  items: NavItem[]
}

const NAV_GROUPS: NavGroup[] = [
  {
    labelEn: 'SALES',
    labelAr: 'المبيعات',
    items: [
      { href: '/customers', key: 'customers', icon: 'building' },
      { href: '/accounts', key: 'accounts', icon: 'users' },
      { href: '/rfqs', key: 'leads', icon: 'clipboard' },
      { href: '/quotations', key: 'quotations', icon: 'receipt' },
      { href: '/contracts', key: 'contracts', icon: 'fileText' },
      { href: '/approvals', key: 'approvals', icon: 'receipt' },
    ],
  },
  {
    labelEn: 'LEASE OPERATIONS',
    labelAr: 'عمليات الإيجار',
    items: [
      { href: '/', key: 'dashboard', icon: 'grid' },
      { href: '/leases', key: 'leaseAgreements', icon: 'fileText' },
      { href: '/vehicles', key: 'vehicles', icon: 'truck' },
      { href: '/drivers', key: 'drivers', icon: 'users' },
    ],
  },
  {
    labelEn: 'FINANCE',
    labelAr: 'المالية',
    items: [
      { href: '/invoices', key: 'invoices', icon: 'receipt' },
      { href: '/payments', key: 'payments', icon: 'creditCard' },
      { href: '/invoices/generate', key: 'generateInvoices', icon: 'refresh' },
    ],
  },
  {
    labelEn: 'ADMINISTRATION',
    labelAr: 'الإدارة',
    items: [
      { href: '/branches', key: 'branches', icon: 'mapPin' },
      { href: '/setup', key: 'setup', icon: 'cog' },
    ],
  },
]

/* ---------------------------------------------------------------------------
 * Active-state helper
 * -------------------------------------------------------------------------*/

function isActive(pathname: string, href: string): boolean {
  if (href === '/') return pathname === '/'
  return pathname === href || pathname.startsWith(href + '/')
}

/* ---------------------------------------------------------------------------
 * AppShell component — sidebar + top bar layout
 * -------------------------------------------------------------------------*/

function NotificationBell() {
  const router = useRouter()
  const [unread, setUnread] = useState(0)
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<NotificationItem[]>([])
  const ref = useRef<HTMLDivElement>(null)

  const poll = useCallback(() => {
    bff.getUnreadNotificationCount().then((r) => setUnread(r.unreadCount)).catch(() => {})
  }, [])

  useEffect(() => {
    poll()
    const h = setInterval(poll, 60000)
    return () => clearInterval(h)
  }, [poll])

  useEffect(() => {
    if (!open) return
    bff.getNotifications(1, 10).then((r) => setItems(r.items)).catch(() => {})
  }, [open])

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  function handleClick(n: NotificationItem) {
    bff.markNotificationRead(n.id).catch(() => {})
    setOpen(false)
    if (n.linkedEntityType && n.linkedEntityId) {
      const path = n.linkedEntityType === 'Quotation' ? `/quotations/${n.linkedEntityId}`
        : n.linkedEntityType === 'RFQ' ? `/rfqs/${n.linkedEntityId}`
        : n.linkedEntityType === 'Invoice' ? `/invoices/${n.linkedEntityId}`
        : n.linkedEntityType === 'Contract' ? `/leases/${n.linkedEntityId}`
        : null
      if (path) router.push(path)
    }
    poll()
  }

  async function handleMarkAllRead() {
    await bff.markAllNotificationsRead().catch(() => {})
    setUnread(0)
    setItems((prev) => prev.map((n) => ({ ...n, isRead: true })))
  }

  const typeIcon = (type: string) => {
    if (type.includes('approval')) return '🔔'
    if (type.includes('expir')) return '⚠️'
    if (type.includes('inactiv')) return '💤'
    if (type.includes('document')) return '📄'
    return '🔔'
  }

  function timeAgo(d: string) {
    const diff = Date.now() - new Date(d).getTime()
    const mins = Math.floor(diff / 60000)
    if (mins < 60) return `${mins}m`
    const hrs = Math.floor(mins / 60)
    if (hrs < 24) return `${hrs}h`
    return `${Math.floor(hrs / 24)}d`
  }

  return (
    <div ref={ref} className="relative me-3">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        className="relative rounded-md p-1.5 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
        aria-label="Notifications"
      >
        <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M14.857 17.082a23.848 23.848 0 005.454-1.31A8.967 8.967 0 0118 9.75v-.7V9A6 6 0 006 9v.75a8.967 8.967 0 01-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 01-5.714 0m5.714 0a3 3 0 11-5.714 0" />
        </svg>
        {unread > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-[16px] items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-bold text-white">
            {unread > 99 ? '99+' : unread}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 top-full z-50 mt-1 w-80 rounded-lg border border-slate-200 bg-white shadow-lg">
          <div className="flex items-center justify-between border-b border-slate-100 px-3 py-2">
            <span className="text-xs font-semibold text-slate-700">Notifications</span>
            {unread > 0 && (
              <button type="button" onClick={handleMarkAllRead} className="text-[10px] text-brand-600 hover:underline">
                Mark all read
              </button>
            )}
          </div>
          <div className="max-h-80 overflow-y-auto">
            {items.length === 0 && (
              <div className="py-6 text-center text-xs text-slate-400">No notifications</div>
            )}
            {items.map((n) => (
              <button
                key={n.id}
                type="button"
                onClick={() => handleClick(n)}
                className={`flex w-full gap-2 border-b border-slate-50 px-3 py-2 text-left transition-colors hover:bg-slate-50 last:border-b-0 ${!n.isRead ? 'bg-blue-50/50' : ''}`}
              >
                <span className="mt-0.5 text-sm">{typeIcon(n.type)}</span>
                <div className="min-w-0 flex-1">
                  <p className={`truncate text-xs ${!n.isRead ? 'font-semibold text-slate-900' : 'text-slate-700'}`}>{n.title}</p>
                  {n.body && <p className="mt-0.5 truncate text-[10px] text-slate-500">{n.body}</p>}
                </div>
                <span className="shrink-0 text-[10px] text-slate-400">{timeAgo(n.createdAtUtc)}</span>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

export function AppShell({ children }: { children: ReactNode }) {
  const { locale, setLocale, t } = useLocale()
  const pathname = usePathname() ?? '/'
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const navLabel = (key: NavKey): string => {
    return (t.nav as Record<string, string>)[key] ?? key
  }

  const groupLabel = (group: NavGroup): string => {
    return locale === 'ar' ? group.labelAr : group.labelEn
  }

  const sidebarContent = (
    <>
      {/* Navigation groups */}
      <nav className="flex-1 overflow-y-auto py-4">
        {NAV_GROUPS.map((group) => (
          <div key={group.labelEn} className="mb-4">
            <div className="px-4 py-2 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
              {groupLabel(group)}
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
                      ? 'border-brand-500 border-l-2 bg-slate-800 text-white'
                      : 'border-l-2 border-transparent text-slate-300 hover:bg-slate-800 hover:text-white',
                  ].join(' ')}
                >
                  {ICONS[item.icon]}
                  <span>{navLabel(item.key)}</span>
                </Link>
              )
            })}
          </div>
        ))}
      </nav>

      {/* Footer */}
      <div className="border-t border-slate-700 px-4 py-3 text-[10px] text-slate-500">
        Phase 1
      </div>
    </>
  )

  return (
    <div className="flex min-h-screen flex-col bg-slate-50 text-slate-900">
      {/* ---- Top bar ---- */}
      <header className="sticky top-0 z-30 flex h-12 items-center border-b border-slate-200 bg-white px-4 print:hidden">
        {/* Mobile menu button */}
        <button
          type="button"
          className="me-3 text-slate-600 hover:text-slate-900 md:hidden"
          onClick={() => setSidebarOpen(!sidebarOpen)}
          aria-label="Toggle sidebar"
        >
          {sidebarOpen ? ICONS.close : ICONS.menu}
        </button>

        {/* Logo area */}
        <div className="flex items-center gap-2">
          <CompanyLogo width={120} height={32} />
        </div>

        {/* Spacer */}
        <div className="flex-1" />

        {/* Notification bell */}
        <NotificationBell />

        {/* Language toggle */}
        <div className="flex items-center rounded-md bg-slate-100 p-0.5">
          {SUPPORTED_LOCALES.map((l) => (
            <button
              key={l}
              type="button"
              onClick={() => setLocale(l)}
              className={[
                'rounded-md px-2.5 py-1 text-xs transition-colors',
                locale === l
                  ? 'bg-white font-semibold text-slate-900 shadow-sm'
                  : 'text-slate-500 hover:text-slate-700',
              ].join(' ')}
              aria-pressed={locale === l}
              aria-label={l === 'en' ? t.english : t.arabic}
            >
              {l === 'en' ? 'EN' : 'ع'}
            </button>
          ))}
        </div>
      </header>

      <div className="flex flex-1">
        {/* ---- Sidebar (desktop: always visible, mobile: overlay) ---- */}

        {/* Mobile overlay backdrop */}
        {sidebarOpen && (
          <div
            className="fixed inset-0 z-40 bg-black/50 md:hidden"
            onClick={() => setSidebarOpen(false)}
            aria-hidden
          />
        )}

        {/* Sidebar panel */}
        <aside
          className={[
            'fixed inset-y-0 start-0 z-50 flex w-56 flex-col bg-slate-900 pt-12 text-white transition-transform md:static md:z-auto md:translate-x-0 md:pt-0 print:hidden',
            sidebarOpen ? 'translate-x-0' : '-translate-x-full rtl:translate-x-full',
          ].join(' ')}
        >
          {sidebarContent}
        </aside>

        {/* ---- Main content ---- */}
        <main className="flex-1 overflow-y-auto p-6 print:p-0">{children}</main>
      </div>
    </div>
  )
}
