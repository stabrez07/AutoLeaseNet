// Minimal AR/EN dictionary system. We deliberately avoid pulling in next-intl
// route segments to keep the App Router structure flat until design.md lands.
// Locale state is persisted client-side in a cookie ("locale=ar|en").
//
// When design.md arrives and we move to next-intl + [locale] segments, replace
// `useLocale()` + `useT()` with `useTranslations()` and the message files below
// migrate 1:1 to `messages/<locale>.json`.

export type Locale = 'en' | 'ar'

export const SUPPORTED_LOCALES: Locale[] = ['en', 'ar']
export const DEFAULT_LOCALE: Locale = 'en'

export const directionFor = (locale: Locale): 'rtl' | 'ltr' => (locale === 'ar' ? 'rtl' : 'ltr')

export type Messages = typeof messagesEn

export const messagesEn = {
  appName: 'AutoLeaseNet',
  portalSubtitle: 'Internal portal — sales + operations',
  language: 'Language',
  english: 'English',
  arabic: 'العربية',
  nav: {
    dashboard: 'Dashboard',
    customers: 'Customers',
    vehicles: 'Vehicles',
    drivers: 'Drivers',
    branches: 'Branches',
    newLease: 'New Lease',
  },
  dashboard: {
    title: 'Dashboard',
    subtitle: 'Pipeline snapshot — placeholder until design.md arrives.',
    cards: {
      activeLeases: 'Active leases',
      pendingIssuance: 'Pending issuance',
      vehiclesAvailable: 'Vehicles available',
      driversValid: 'Drivers with valid licenses',
    },
    seedBanner:
      'This UI is reading from the BFF lookup endpoints against your local seed data. Real production data and design system land later.',
  },
  customers: {
    title: 'Customers',
    subtitle: 'Tenants’ customers (B2B + B2C).',
    columns: {
      displayName: 'Display name',
      type: 'Type',
      mobile: 'Mobile',
      status: 'Status',
    },
    type: { b2b: 'Business', b2c: 'Individual' },
    status: { active: 'Active', inactive: 'Inactive' },
    searchPlaceholder: 'Search by name or id…',
    empty: 'No customers found.',
  },
  vehicles: {
    title: 'Vehicles',
    subtitle: 'Fleet inventory and availability.',
    columns: {
      plate: 'Plate',
      make: 'Make',
      model: 'Model',
      status: 'Status',
      odometer: 'Odometer (km)',
    },
    statuses: {
      1: 'Available',
      2: 'Reserved',
      3: 'OnLease',
      4: 'InService',
      5: 'Retired',
    },
    searchPlaceholder: 'Search by plate, make, model…',
    empty: 'No vehicles found.',
  },
  drivers: {
    title: 'Drivers',
    subtitle: 'Authorized drivers and license validity.',
    columns: {
      name: 'Name',
      license: 'License number',
      licenseExpiry: 'License expiry',
      status: 'Status',
    },
    statuses: { 1: 'Active', 2: 'Suspended', 3: 'Retired' },
    searchPlaceholder: 'Search by name or license…',
    empty: 'No drivers found.',
  },
  branches: {
    title: 'Branches',
    subtitle: 'Operating branches (working / receive / return).',
    columns: { code: 'Code', name: 'Name', city: 'City', active: 'Active' },
    yes: 'Yes',
    no: 'No',
  },
  newLease: {
    title: 'New Lease — Save Contract',
    subtitle:
      'Mirrors the Tajeer V9.7 Save Contract flow. Submits to /api/v1/dev/save-contract. ' +
      'When real Tajeer staging credentials are configured the request hits Rabet; ' +
      'until then InMemory mode returns a synthetic issuance URL.',
    fields: {
      customer: 'Customer',
      vehicle: 'Vehicle',
      primaryDriver: 'Primary driver',
      rentPolicy: 'Rent policy',
      workingBranch: 'Working branch',
      receiveBranch: 'Receive branch',
      returnBranch: 'Return branch',
      contractStart: 'Contract start (UTC)',
      contractEnd: 'Contract end (UTC)',
      contractType: 'Contract type (code)',
      allowedKmPerDay: 'Allowed km / day',
      rentAmount: 'Rent amount (SAR)',
      paidAmount: 'Paid amount (SAR)',
      paymentMethod: 'Payment method (code)',
    },
    submit: 'Submit Save Contract',
    submitting: 'Submitting…',
    successTitle: 'Save Contract accepted',
    successLeaseId: 'Lease id',
    successContractNumber: 'Tajeer contract number',
    successIssuanceUrl: 'Issuance URL',
    pickFirst: 'Pick first seeded value',
    error: 'Save Contract failed',
    devHint:
      'Tip: run scripts/local-smoke.ps1 first to seed the database and verify the ' +
      'full save → webhook → Active flow against InMemory Tajeer.',
  },
  table: {
    page: 'Page',
    of: 'of',
    next: 'Next',
    previous: 'Previous',
    total: 'Total',
  },
  common: {
    loading: 'Loading…',
    error: 'Something went wrong',
    retry: 'Retry',
  },
} as const

export const messagesAr: Messages = {
  appName: 'أوتو ليس نت',
  portalSubtitle: 'البوابة الداخلية — المبيعات والعمليات',
  language: 'اللغة',
  english: 'English',
  arabic: 'العربية',
  nav: {
    dashboard: 'لوحة المعلومات',
    customers: 'العملاء',
    vehicles: 'المركبات',
    drivers: 'السائقون',
    branches: 'الفروع',
    newLease: 'عقد جديد',
  },
  dashboard: {
    title: 'لوحة المعلومات',
    subtitle: 'لقطة سريعة — مؤقتة إلى حين وصول ملف التصميم.',
    cards: {
      activeLeases: 'العقود النشطة',
      pendingIssuance: 'قيد الإصدار',
      vehiclesAvailable: 'المركبات المتاحة',
      driversValid: 'سائقون برخص سارية',
    },
    seedBanner:
      'هذه الواجهة تقرأ من واجهات BFF للبيانات المرجعية على بياناتك المحلية. النظام التصميمي والبيانات الحقيقية تأتي لاحقاً.',
  },
  customers: {
    title: 'العملاء',
    subtitle: 'عملاء المستأجر (شركات وأفراد).',
    columns: {
      displayName: 'الاسم',
      type: 'النوع',
      mobile: 'الجوال',
      status: 'الحالة',
    },
    type: { b2b: 'شركة', b2c: 'فرد' },
    status: { active: 'نشط', inactive: 'غير نشط' },
    searchPlaceholder: 'ابحث بالاسم أو الهوية…',
    empty: 'لا يوجد عملاء.',
  },
  vehicles: {
    title: 'المركبات',
    subtitle: 'مخزون الأسطول والتوفر.',
    columns: {
      plate: 'اللوحة',
      make: 'الصانع',
      model: 'الطراز',
      status: 'الحالة',
      odometer: 'العداد (كم)',
    },
    statuses: {
      1: 'متاحة',
      2: 'محجوزة',
      3: 'مؤجرة',
      4: 'صيانة',
      5: 'مسحوبة',
    },
    searchPlaceholder: 'ابحث بلوحة أو صانع أو طراز…',
    empty: 'لا توجد مركبات.',
  },
  drivers: {
    title: 'السائقون',
    subtitle: 'السائقون المعتمدون وصلاحية الرخص.',
    columns: {
      name: 'الاسم',
      license: 'رقم الرخصة',
      licenseExpiry: 'انتهاء الرخصة',
      status: 'الحالة',
    },
    statuses: { 1: 'نشط', 2: 'موقوف', 3: 'متقاعد' },
    searchPlaceholder: 'ابحث بالاسم أو الرخصة…',
    empty: 'لا يوجد سائقون.',
  },
  branches: {
    title: 'الفروع',
    subtitle: 'الفروع التشغيلية (العمل / الاستلام / الإعادة).',
    columns: { code: 'الرمز', name: 'الاسم', city: 'المدينة', active: 'نشط' },
    yes: 'نعم',
    no: 'لا',
  },
  newLease: {
    title: 'عقد جديد — حفظ العقد',
    subtitle:
      'يحاكي تدفق Tajeer V9.7. يرسل إلى /api/v1/dev/save-contract. ' +
      'عند توفر بيانات Rabet الحقيقية يضرب التطبيق المباشر؛ ' +
      'حالياً يعود وضع InMemory برابط إصدار صناعي.',
    fields: {
      customer: 'العميل',
      vehicle: 'المركبة',
      primaryDriver: 'السائق الأساسي',
      rentPolicy: 'سياسة الإيجار',
      workingBranch: 'فرع العمل',
      receiveBranch: 'فرع الاستلام',
      returnBranch: 'فرع الإعادة',
      contractStart: 'بداية العقد (UTC)',
      contractEnd: 'نهاية العقد (UTC)',
      contractType: 'نوع العقد (رمز)',
      allowedKmPerDay: 'الكيلومترات المسموحة / يوم',
      rentAmount: 'مبلغ الإيجار (ر.س)',
      paidAmount: 'المبلغ المدفوع (ر.س)',
      paymentMethod: 'طريقة الدفع (رمز)',
    },
    submit: 'إرسال حفظ العقد',
    submitting: 'جارٍ الإرسال…',
    successTitle: 'تم قبول حفظ العقد',
    successLeaseId: 'معرّف العقد',
    successContractNumber: 'رقم عقد تأجير',
    successIssuanceUrl: 'رابط الإصدار',
    pickFirst: 'اختر أول قيمة بذرة',
    error: 'فشل حفظ العقد',
    devHint:
      'نصيحة: شغّل scripts/local-smoke.ps1 أولاً لزرع قاعدة البيانات والتحقق من التدفق كاملاً.',
  },
  table: {
    page: 'صفحة',
    of: 'من',
    next: 'التالي',
    previous: 'السابق',
    total: 'الإجمالي',
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
