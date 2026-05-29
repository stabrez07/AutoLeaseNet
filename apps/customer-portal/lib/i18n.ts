// Minimal AR/EN dictionary for the Customer Portal. Mirrors the web-portal's
// flat cookie-backed locale approach; migration to next-intl + [locale] segments
// happens together with the web-portal once design.md lands.

export type Locale = 'en' | 'ar'

export const SUPPORTED_LOCALES: Locale[] = ['en', 'ar']
export const DEFAULT_LOCALE: Locale = 'en'

export const directionFor = (locale: Locale): 'rtl' | 'ltr' => (locale === 'ar' ? 'rtl' : 'ltr')

export type Messages = typeof messagesEn

export const messagesEn = {
  appName: 'AutoLeaseNet',
  portalSubtitle: 'Customer self-service',
  language: 'Language',
  english: 'English',
  arabic: 'العربية',
  nav: {
    dashboard: 'Dashboard',
    myLeases: 'My Leases',
  },
  signedInAs: 'Signed in as',
  devBanner:
    'Phase 1 dev — real Entra External ID login lands in Phase 2. The portal is currently scoped to a single demo customer via X-Dev-Customer-Id.',
  dashboard: {
    title: 'Welcome',
    subtitle: 'Your leases at a glance.',
    cards: {
      total: 'Total leases',
      active: 'Active',
      closed: 'Closed',
    },
    cta: 'View all my leases',
  },
  leases: {
    title: 'My Leases',
    subtitle: 'All leases on your account, newest first.',
    columns: {
      contractNumber: 'Contract #',
      status: 'Status',
      start: 'Start',
      end: 'End',
      rent: 'Rent (SAR)',
    },
    statuses: {
      1: 'Pending issuance',
      2: 'Active',
      3: 'Extended',
      4: 'Suspended',
      5: 'Closed',
      6: 'Cancelled',
      7: 'Expired',
      99: 'Save failed',
    },
    empty: 'You have no leases on file yet.',
  },
  common: {
    loading: 'Loading…',
    error: 'Something went wrong',
    retry: 'Retry',
  },
}

export const messagesAr: Messages = {
  appName: 'أوتو ليس نت',
  portalSubtitle: 'خدمة العملاء الذاتية',
  language: 'اللغة',
  english: 'English',
  arabic: 'العربية',
  nav: {
    dashboard: 'لوحة المعلومات',
    myLeases: 'عقودي',
  },
  signedInAs: 'تم الدخول باسم',
  devBanner:
    'إصدار تجريبي — تسجيل الدخول الحقيقي عبر Entra External ID يأتي في المرحلة 2. حالياً البوابة محصورة بعميل واحد للعرض.',
  dashboard: {
    title: 'مرحباً',
    subtitle: 'لمحة سريعة عن عقودك.',
    cards: {
      total: 'إجمالي العقود',
      active: 'النشطة',
      closed: 'المغلقة',
    },
    cta: 'عرض كل عقودي',
  },
  leases: {
    title: 'عقودي',
    subtitle: 'جميع عقودك، الأحدث أولاً.',
    columns: {
      contractNumber: 'رقم العقد',
      status: 'الحالة',
      start: 'البداية',
      end: 'النهاية',
      rent: 'الإيجار (ر.س)',
    },
    statuses: {
      1: 'قيد الإصدار',
      2: 'نشط',
      3: 'مُمدّد',
      4: 'موقوف',
      5: 'مغلق',
      6: 'ملغى',
      7: 'منتهي',
      99: 'فشل الحفظ',
    },
    empty: 'لا توجد لديك عقود مسجلة بعد.',
  },
  common: {
    loading: 'جارٍ التحميل…',
    error: 'حدث خطأ ما',
    retry: 'إعادة المحاولة',
  },
}

export const dictionaries: Record<Locale, Messages> = {
  en: messagesEn,
  ar: messagesAr,
}
