'use client'

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import {
  DEFAULT_LOCALE,
  SUPPORTED_LOCALES,
  dictionaries,
  directionFor,
  type Locale,
  type Messages,
} from './i18n'

interface LocaleContextValue {
  locale: Locale
  setLocale: (l: Locale) => void
  t: Messages
}

const LocaleContext = createContext<LocaleContextValue | null>(null)

const COOKIE_NAME = 'aln_locale'

function readCookie(): Locale | null {
  if (typeof document === 'undefined') return null
  const match = document.cookie.split('; ').find((row) => row.startsWith(`${COOKIE_NAME}=`))
  const value = match?.split('=')[1] as Locale | undefined
  return value && (SUPPORTED_LOCALES as string[]).includes(value) ? value : null
}

function writeCookie(locale: Locale) {
  if (typeof document === 'undefined') return
  document.cookie = `${COOKIE_NAME}=${locale}; path=/; max-age=${60 * 60 * 24 * 365}; SameSite=Lax`
}

export function LocaleProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(DEFAULT_LOCALE)

  useEffect(() => {
    const fromCookie = readCookie()
    if (fromCookie && fromCookie !== locale) {
      setLocaleState(fromCookie)
    }
    // sync dir + lang on html for first paint
    document.documentElement.lang = fromCookie ?? DEFAULT_LOCALE
    document.documentElement.dir = directionFor(fromCookie ?? DEFAULT_LOCALE)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const setLocale = useCallback((next: Locale) => {
    setLocaleState(next)
    writeCookie(next)
    document.documentElement.lang = next
    document.documentElement.dir = directionFor(next)
  }, [])

  const value = useMemo<LocaleContextValue>(
    () => ({ locale, setLocale, t: dictionaries[locale] }),
    [locale, setLocale],
  )

  return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>
}

export function useLocale() {
  const ctx = useContext(LocaleContext)
  if (!ctx) throw new Error('useLocale must be used inside <LocaleProvider>')
  return ctx
}
