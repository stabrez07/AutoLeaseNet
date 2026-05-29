import type { Metadata } from 'next'
import type { ReactNode } from 'react'
import './globals.css'
import { LocaleProvider } from '../lib/locale-provider'
import { AppShell } from '../components/app-shell'

export const metadata: Metadata = {
  title: 'AutoLeaseNet — Customer Portal',
  description: 'Self-service portal for fleet admins, drivers, and individual lessees',
}

export default function RootLayout({ children }: { children: ReactNode }) {
  // <html dir> flips to rtl client-side via LocaleProvider once the cookie resolves;
  // first paint defaults to ltr so SSR stays deterministic.
  return (
    <html lang="en" dir="ltr">
      <body>
        <LocaleProvider>
          <AppShell>{children}</AppShell>
        </LocaleProvider>
      </body>
    </html>
  )
}
