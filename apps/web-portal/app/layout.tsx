import type { Metadata } from 'next'
import type { ReactNode } from 'react'
import './globals.css'
import { LocaleProvider } from '../lib/locale-provider'
import { AppShell } from '../components/app-shell'

export const metadata: Metadata = {
  title: 'AutoLeaseNet — Web Portal',
  description: 'Internal portal for sales and operations',
}

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    // The <html dir> is set client-side by LocaleProvider once the locale cookie
    // resolves; we render LTR by default so the SSR pass is deterministic.
    <html lang="en" dir="ltr">
      <body>
        <LocaleProvider>
          <AppShell>{children}</AppShell>
        </LocaleProvider>
      </body>
    </html>
  )
}
