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
    myVehicles: 'My Vehicles',
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
      currentlyDriving: 'Currently driving',
    },
    cta: 'View all my leases',
    ctaVehicles: 'See my vehicles',
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
  vehicles: {
    title: 'My Vehicles',
    subtitle: 'Vehicles you currently have under an active, extended, or suspended lease.',
    columns: {
      plate: 'Plate',
      makeModel: 'Make / Model',
      year: 'Year',
      color: 'Color',
      km: 'KM',
      licenseExpiry: 'License expiry',
      insuranceExpiry: 'Insurance expiry',
    },
    empty: 'You have no vehicles assigned to active leases right now.',
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
    myVehicles: 'سياراتي',
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
      currentlyDriving: 'تقودها حالياً',
    },
    cta: 'عرض كل عقودي',
    ctaVehicles: 'عرض سياراتي',
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
  vehicles: {
    title: 'سياراتي',
    subtitle: 'السيارات المسلّمة لك حالياً ضمن عقد نشط أو مُمدّد أو موقوف.',
    columns: {
      plate: 'اللوحة',
      makeModel: 'الصانع / الطراز',
      year: 'السنة',
      color: 'اللون',
      km: 'الكيلومترات',
      licenseExpiry: 'انتهاء الاستمارة',
      insuranceExpiry: 'انتهاء التأمين',
    },
    empty: 'لا توجد سيارات مسلّمة لك حالياً.',
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
