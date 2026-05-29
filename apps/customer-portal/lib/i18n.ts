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
    // Codes match the LeaseStatus enum verbatim (Domain/Leases/LeaseStatus.cs).
    statuses: {
      0: 'Draft',
      1: 'Save failed',
      2: 'Pending issuance',
      3: 'Active',
      4: 'Extended',
      5: 'Suspended',
      6: 'Closed',
      7: 'Cancelled',
      8: 'Expired draft',
    },
    empty: 'You have no leases on file yet.',
  },
  leaseDetail: {
    notFound: 'Lease not found, or no longer visible to your account.',
    backToList: 'Back to all leases',
    sections: {
      contract: 'Contract terms',
      vehicle: 'Vehicle',
      payment: 'Payment',
      timeline: 'Timeline',
    },
    contract: {
      number: 'Contract #',
      typeCode: 'Type code',
      start: 'Start',
      end: 'End',
      actualReturn: 'Actual return',
      allowedKmPerDay: 'Allowed KM / day',
      allowedKmPerHour: 'Allowed KM / hour',
      unlimitedKm: 'Unlimited KM',
      allowedLateHours: 'Grace late hours',
      extensionCount: 'Extensions',
      status: 'Status',
    },
    vehicle: {
      unassigned: 'No vehicle assigned to this lease yet.',
    },
    payment: {
      rent: 'Rent',
      paid: 'Paid',
      remaining: 'Remaining',
      vat: 'VAT',
      total: 'Total',
      methodCode: 'Method code',
      discountType: 'Discount type',
      discountValue: 'Discount value',
    },
    timeline: {
      saved: 'Saved',
      issued: 'Issued',
      suspended: 'Suspended',
      resumed: 'Resumed',
      closed: 'Closed',
      cancelled: 'Cancelled',
      expired: 'Expired',
    },
    yes: 'Yes',
    no: 'No',
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
      0: 'مسودة',
      1: 'فشل الحفظ',
      2: 'قيد الإصدار',
      3: 'نشط',
      4: 'مُمدّد',
      5: 'موقوف',
      6: 'مغلق',
      7: 'ملغى',
      8: 'مسودة منتهية',
    },
    empty: 'لا توجد لديك عقود مسجلة بعد.',
  },
  leaseDetail: {
    notFound: 'العقد غير موجود أو لم يعد ظاهراً لحسابك.',
    backToList: 'العودة إلى جميع العقود',
    sections: {
      contract: 'شروط العقد',
      vehicle: 'السيارة',
      payment: 'الدفع',
      timeline: 'الخط الزمني',
    },
    contract: {
      number: 'رقم العقد',
      typeCode: 'رمز النوع',
      start: 'البداية',
      end: 'النهاية',
      actualReturn: 'الإرجاع الفعلي',
      allowedKmPerDay: 'الكيلومترات المسموحة / يوم',
      allowedKmPerHour: 'الكيلومترات المسموحة / ساعة',
      unlimitedKm: 'كيلومترات غير محدودة',
      allowedLateHours: 'ساعات السماح للتأخير',
      extensionCount: 'عدد التمديدات',
      status: 'الحالة',
    },
    vehicle: {
      unassigned: 'لم تُسلَّم سيارة لهذا العقد بعد.',
    },
    payment: {
      rent: 'الإيجار',
      paid: 'المدفوع',
      remaining: 'المتبقي',
      vat: 'ضريبة القيمة المضافة',
      total: 'الإجمالي',
      methodCode: 'رمز الطريقة',
      discountType: 'نوع الخصم',
      discountValue: 'قيمة الخصم',
    },
    timeline: {
      saved: 'مُحفوظ',
      issued: 'مُصدَر',
      suspended: 'موقوف',
      resumed: 'مُستأنف',
      closed: 'مغلق',
      cancelled: 'ملغى',
      expired: 'منتهي',
    },
    yes: 'نعم',
    no: 'لا',
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
