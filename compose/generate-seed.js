#!/usr/bin/env node
/**
 * generate-seed.js
 * Generates compose/full-seed.sql with realistic KSA vehicle leasing seed data.
 * Run: node compose/generate-seed.js
 */
'use strict';

const fs = require('fs');
const path = require('path');

// ─── Constants ────────────────────────────────────────────────────────────────
const TENANT_ID = 'a1a1a1a1-0001-0000-0000-000000000001';
const NOW = '2026-06-22T10:00:00+03:00';

// ─── Helper: deterministic GUID generator ─────────────────────────────────────
function guid(prefix, index) {
  const hex = index.toString(16).padStart(12, '0');
  return `${prefix}-0000-0000-${hex}`;
}

function branchGuid(i) { return guid('b0b0b0b0-0001', i); }
function customerGuid(i) { return guid('c0c0c0c0-0001', i); }
function vehicleGuid(i) { return guid('d0d0d0d0-0001', i); }
function driverGuid(i) { return guid('e0e0e0e0-0001', i); }
function rentPolicyGuid(i) { return guid('f0f0f0f0-0001', i); }
function extCoverageGuid(i) { return guid('a2a2a2a2-0001', i); }
function approvalTierGuid(i) { return guid('a3a3a3a3-0001', i); }
function leaseGuid(i) { return guid('b1b1b1b1-0001', i); }
function quotationGuid(i) { return guid('c1c1c1c1-0001', i); }
function quotationLineGuid(qi, li) { return guid(`c2c2c2c2-${qi.toString(16).padStart(4, '0')}`, li); }
function quotationApprovalGuid(qi, ai) { return guid(`c3c3c3c3-${qi.toString(16).padStart(4, '0')}`, ai); }
function invoiceGuid(i) { return guid('d1d1d1d1-0001', i); }
function pricingVersionGuid(i) { return guid('e1e1e1e1-0001', i); }
function pricingDiscountGuid(i) { return guid('e2e2e2e2-0001', i); }
function pricingFormulaGuid(i) { return guid('e3e3e3e3-0001', i); }
function accountManagerGuid(i) { return guid('f1f1f1f1-0001', i); }
function paymentGuid(i) { return guid('f2f2f2f2-0001', i); }
function rfqGuid(i) { return guid('f4f4f4f4-0001', i); }
function rfqHistoryGuid(ri, hi) { return guid(`f5f5f5f5-${ri.toString(16).padStart(4, '0')}`, hi); }
function paymentAllocationGuid(pi, ai) { return guid(`f3f3f3f3-${pi.toString(16).padStart(4, '0')}`, ai); }
function contractGuid(i) { return guid('a5a5a5a5-0001', i); }
function contractLineGuid(ci, li) { return guid(`a6a6a6a6-${ci.toString(16).padStart(4, '0')}`, li); }
function accountRecordGuid(i) { return guid('a7a7a7a7-0001', i); }

// ─── SQL escape helper ────────────────────────────────────────────────────────
function esc(val) {
  if (val === null || val === undefined) return 'NULL';
  if (typeof val === 'boolean') return val ? '1' : '0';
  if (typeof val === 'number') return val.toString();
  // string
  return `N'${val.toString().replace(/'/g, "''")}'`;
}

function bit(val) { return val ? '1' : '0'; }

// ─── Data Arrays ──────────────────────────────────────────────────────────────

const KSA_CITIES = [
  { en: 'Riyadh', ar: 'الرياض', regionEn: 'Riyadh Region', regionAr: 'منطقة الرياض', lat: 24.7136, lng: 46.6753 },
  { en: 'Jeddah', ar: 'جدة', regionEn: 'Makkah Region', regionAr: 'منطقة مكة المكرمة', lat: 21.4858, lng: 39.1925 },
  { en: 'Dammam', ar: 'الدمام', regionEn: 'Eastern Province', regionAr: 'المنطقة الشرقية', lat: 26.3927, lng: 49.9777 },
  { en: 'Makkah', ar: 'مكة المكرمة', regionEn: 'Makkah Region', regionAr: 'منطقة مكة المكرمة', lat: 21.3891, lng: 39.8579 },
  { en: 'Madinah', ar: 'المدينة المنورة', regionEn: 'Madinah Region', regionAr: 'منطقة المدينة المنورة', lat: 24.5247, lng: 39.5692 },
  { en: 'Khobar', ar: 'الخبر', regionEn: 'Eastern Province', regionAr: 'المنطقة الشرقية', lat: 26.2172, lng: 50.1971 },
  { en: 'Tabuk', ar: 'تبوك', regionEn: 'Tabuk Region', regionAr: 'منطقة تبوك', lat: 28.3838, lng: 36.5550 },
  { en: 'Abha', ar: 'أبها', regionEn: 'Asir Region', regionAr: 'منطقة عسير', lat: 18.2164, lng: 42.5053 },
  { en: 'Jubail', ar: 'الجبيل', regionEn: 'Eastern Province', regionAr: 'المنطقة الشرقية', lat: 27.0046, lng: 49.6225 },
  { en: 'Yanbu', ar: 'ينبع', regionEn: 'Madinah Region', regionAr: 'منطقة المدينة المنورة', lat: 24.0895, lng: 38.0618 },
  { en: 'Hail', ar: 'حائل', regionEn: 'Hail Region', regionAr: 'منطقة حائل', lat: 27.5219, lng: 41.6907 },
  { en: 'Buraidah', ar: 'بريدة', regionEn: 'Qassim Region', regionAr: 'منطقة القصيم', lat: 26.3260, lng: 43.9750 },
  { en: 'Najran', ar: 'نجران', regionEn: 'Najran Region', regionAr: 'منطقة نجران', lat: 17.4924, lng: 44.1277 },
  { en: 'Jazan', ar: 'جازان', regionEn: 'Jazan Region', regionAr: 'منطقة جازان', lat: 16.8892, lng: 42.5611 },
  { en: 'Taif', ar: 'الطائف', regionEn: 'Makkah Region', regionAr: 'منطقة مكة المكرمة', lat: 21.2703, lng: 40.4158 },
  { en: 'Al Ahsa', ar: 'الأحساء', regionEn: 'Eastern Province', regionAr: 'المنطقة الشرقية', lat: 25.3498, lng: 49.5876 },
  { en: 'Qatif', ar: 'القطيف', regionEn: 'Eastern Province', regionAr: 'المنطقة الشرقية', lat: 26.5196, lng: 50.0115 },
  { en: 'Sakaka', ar: 'سكاكا', regionEn: 'Al Jawf Region', regionAr: 'منطقة الجوف', lat: 29.9697, lng: 40.2064 },
  { en: 'Arar', ar: 'عرعر', regionEn: 'Northern Borders', regionAr: 'منطقة الحدود الشمالية', lat: 30.9753, lng: 41.0382 },
  { en: 'Bisha', ar: 'بيشة', regionEn: 'Asir Region', regionAr: 'منطقة عسير', lat: 19.9888, lng: 42.6010 },
  { en: 'Dhahran', ar: 'الظهران', regionEn: 'Eastern Province', regionAr: 'المنطقة الشرقية', lat: 26.2361, lng: 50.0393 },
  { en: 'Khamis Mushait', ar: 'خميس مشيط', regionEn: 'Asir Region', regionAr: 'منطقة عسير', lat: 18.3065, lng: 42.7290 },
  { en: 'Hafar Al Batin', ar: 'حفر الباطن', regionEn: 'Eastern Province', regionAr: 'المنطقة الشرقية', lat: 28.4328, lng: 45.9708 },
  { en: 'Unaizah', ar: 'عنيزة', regionEn: 'Qassim Region', regionAr: 'منطقة القصيم', lat: 26.0841, lng: 43.9938 },
  { en: 'Al Kharj', ar: 'الخرج', regionEn: 'Riyadh Region', regionAr: 'منطقة الرياض', lat: 24.1556, lng: 47.3122 },
  { en: 'Wadi Al Dawasir', ar: 'وادي الدواسر', regionEn: 'Riyadh Region', regionAr: 'منطقة الرياض', lat: 20.4429, lng: 44.7240 },
  { en: 'Al Baha', ar: 'الباحة', regionEn: 'Al Baha Region', regionAr: 'منطقة الباحة', lat: 20.0000, lng: 41.4667 },
  { en: 'Al Majmaah', ar: 'المجمعة', regionEn: 'Riyadh Region', regionAr: 'منطقة الرياض', lat: 25.8881, lng: 45.3432 },
  { en: 'Rabigh', ar: 'رابغ', regionEn: 'Makkah Region', regionAr: 'منطقة مكة المكرمة', lat: 22.7981, lng: 39.0341 },
  { en: 'Dawadmi', ar: 'الدوادمي', regionEn: 'Riyadh Region', regionAr: 'منطقة الرياض', lat: 24.5071, lng: 44.3932 },
];

const SAUDI_FIRST_NAMES_EN = [
  'Mohammed', 'Abdullah', 'Khalid', 'Fahad', 'Sultan', 'Turki', 'Faisal', 'Saud',
  'Abdulrahman', 'Omar', 'Ali', 'Hassan', 'Nasser', 'Bandar', 'Saleh', 'Ibrahim',
  'Ahmad', 'Yousef', 'Waleed', 'Saeed', 'Majed', 'Mansour', 'Hamad', 'Badr',
  'Rakan', 'Nawaf', 'Mishari', 'Abdulaziz', 'Talal', 'Thamer',
];

const SAUDI_FIRST_NAMES_AR = [
  'محمد', 'عبدالله', 'خالد', 'فهد', 'سلطان', 'تركي', 'فيصل', 'سعود',
  'عبدالرحمن', 'عمر', 'علي', 'حسن', 'ناصر', 'بندر', 'صالح', 'إبراهيم',
  'أحمد', 'يوسف', 'وليد', 'سعيد', 'ماجد', 'منصور', 'حمد', 'بدر',
  'راكان', 'نواف', 'مشاري', 'عبدالعزيز', 'طلال', 'ثامر',
];

const SAUDI_LAST_NAMES_EN = [
  'Al-Otaibi', 'Al-Ghamdi', 'Al-Qahtani', 'Al-Dosari', 'Al-Shehri', 'Al-Zahrani',
  'Al-Harbi', 'Al-Mutairi', 'Al-Subaie', 'Al-Shamrani', 'Al-Malki', 'Al-Tamimi',
  'Al-Anazi', 'Al-Rashidi', 'Al-Bishi', 'Al-Yami', 'Al-Shammari', 'Al-Juhani',
  'Al-Dossary', 'Al-Fahad', 'Al-Saud', 'Al-Faisal', 'Al-Turki', 'Al-Salem',
  'Al-Nasser', 'Al-Omar', 'Al-Hassan', 'Al-Ibrahim', 'Al-Ahmad', 'Al-Khalid',
];

const SAUDI_LAST_NAMES_AR = [
  'العتيبي', 'الغامدي', 'القحطاني', 'الدوسري', 'الشهري', 'الزهراني',
  'الحربي', 'المطيري', 'السبيعي', 'الشمراني', 'المالكي', 'التميمي',
  'العنزي', 'الرشيدي', 'البشري', 'اليامي', 'الشمري', 'الجهني',
  'الدوسري', 'الفهد', 'آل سعود', 'الفيصل', 'التركي', 'السالم',
  'الناصر', 'العمر', 'الحسن', 'الإبراهيم', 'الأحمد', 'الخالد',
];

const COMPANY_NAMES = [
  { en: 'Saudi Construction Group', ar: 'مجموعة البناء السعودية' },
  { en: 'Al Rajhi Trading Co.', ar: 'شركة الراجحي للتجارة' },
  { en: 'National Logistics Solutions', ar: 'حلول اللوجستيات الوطنية' },
  { en: 'Gulf Engineering Services', ar: 'خدمات هندسة الخليج' },
  { en: 'Riyadh Facilities Management', ar: 'إدارة مرافق الرياض' },
  { en: 'Al Marai Distribution', ar: 'توزيع المراعي' },
  { en: 'Eastern Petrochemicals', ar: 'بتروكيماويات الشرقية' },
  { en: 'Jeddah Port Services', ar: 'خدمات ميناء جدة' },
  { en: 'Tabuk Mining Corp.', ar: 'شركة تبوك للتعدين' },
  { en: 'Saudi Telecom Solutions', ar: 'حلول الاتصالات السعودية' },
  { en: 'Neom Contractors Ltd.', ar: 'مقاولو نيوم المحدودة' },
  { en: 'Al Tamimi Real Estate', ar: 'التميمي للعقارات' },
  { en: 'Dammam Steel Industries', ar: 'صناعات الدمام للحديد' },
  { en: 'Al Watania Catering', ar: 'الوطنية للتموين' },
  { en: 'Haramain Transport', ar: 'نقل الحرمين' },
  { en: 'Al Faisaliah Group', ar: 'مجموعة الفيصلية' },
  { en: 'Saudi Cement Company', ar: 'الشركة السعودية للإسمنت' },
  { en: 'Medina Healthcare', ar: 'المدينة للرعاية الصحية' },
  { en: 'Kingdom Agri Trading', ar: 'المملكة للتجارة الزراعية' },
  { en: 'Al Jazeera Printing', ar: 'الجزيرة للطباعة' },
  { en: 'Saudi Power Generation', ar: 'السعودية لتوليد الطاقة' },
  { en: 'Red Sea Developments', ar: 'تطويرات البحر الأحمر' },
  { en: 'Al Othaim Markets', ar: 'أسواق العثيم' },
  { en: 'Saudi Water Authority', ar: 'هيئة المياه السعودية' },
  { en: 'Al Subaie Contracting', ar: 'السبيعي للمقاولات' },
  { en: 'Yanbu Industrial Services', ar: 'خدمات ينبع الصناعية' },
  { en: 'Al Khaleej IT Solutions', ar: 'حلول الخليج لتقنية المعلومات' },
  { en: 'Saudi Electric Works', ar: 'الأعمال الكهربائية السعودية' },
  { en: 'Abha Tourism Group', ar: 'مجموعة أبها للسياحة' },
  { en: 'Al Rashid Automotive', ar: 'الرشيد للسيارات' },
  { en: 'Hail Agricultural Co.', ar: 'شركة حائل الزراعية' },
  { en: 'Al Shoula Group', ar: 'مجموعة الشعلة' },
  { en: 'SABIC Transport Division', ar: 'قسم نقل سابك' },
  { en: 'Al Zamil Industries', ar: 'صناعات الزامل' },
  { en: 'Madinah Dates Export', ar: 'المدينة لتصدير التمور' },
  { en: 'Saudi Paper Manufacturing', ar: 'السعودية لصناعة الورق' },
  { en: 'Al Murabba Properties', ar: 'عقارات المربع' },
  { en: 'Jubail Chemical Corp.', ar: 'شركة الجبيل للكيماويات' },
  { en: 'Al Hinai Shipping', ar: 'الهنائي للشحن' },
  { en: 'Saudi Oil Services', ar: 'الخدمات النفطية السعودية' },
  { en: 'Qassim Food Industries', ar: 'صناعات القصيم الغذائية' },
  { en: 'Al Baha Contracting', ar: 'الباحة للمقاولات' },
  { en: 'Eastern Steel Fabricators', ar: 'مصنع الشرقية للحديد' },
  { en: 'Saudi Glass Industries', ar: 'السعودية لصناعة الزجاج' },
  { en: 'Al Bawani Construction', ar: 'البواني للبناء' },
  { en: 'Saudi Arabian Mining Co.', ar: 'شركة التعدين العربية السعودية' },
  { en: 'Al Khozama Management', ar: 'إدارة الخزامى' },
  { en: 'Saudi Dairy & Foodstuff', ar: 'السعودية للألبان والأغذية' },
  { en: 'Al Tawfiq Holding', ar: 'التوفيق القابضة' },
  { en: 'Jubail Port Logistics', ar: 'لوجستيات ميناء الجبيل' },
  { en: 'Saudi Furniture Factory', ar: 'مصنع الأثاث السعودي' },
  { en: 'Al Khobar Marine Services', ar: 'خدمات الخبر البحرية' },
  { en: 'Saudi Aviation Support', ar: 'دعم الطيران السعودي' },
  { en: 'Al Harbi Electronics', ar: 'الحربي للإلكترونيات' },
  { en: 'Taif Honey Co.', ar: 'شركة الطائف للعسل' },
  { en: 'Saudi Plastic Products', ar: 'المنتجات البلاستيكية السعودية' },
  { en: 'Al Nafie Trading', ar: 'النافع للتجارة' },
  { en: 'Saudi Technical Services', ar: 'الخدمات التقنية السعودية' },
  { en: 'Al Jouf Olive Oil', ar: 'زيتون الجوف' },
  { en: 'Al Madinah Printing Press', ar: 'مطبعة المدينة' },
  { en: 'Saudi Poultry Company', ar: 'الشركة السعودية للدواجن' },
  { en: 'Al Inma Properties', ar: 'الإنماء للعقارات' },
  { en: 'Saudi Cable Company', ar: 'الشركة السعودية للكابلات' },
  { en: 'Al Murjan Trading', ar: 'المرجان للتجارة' },
  { en: 'Najran Cement Company', ar: 'شركة نجران للإسمنت' },
  { en: 'Hail Frozen Foods', ar: 'حائل للأغذية المجمدة' },
  { en: 'Saudi Recycling Corp.', ar: 'الشركة السعودية لإعادة التدوير' },
  { en: 'Al Essa Trading', ar: 'العيسى للتجارة' },
  { en: 'Saudi Event Management', ar: 'إدارة الفعاليات السعودية' },
  { en: 'Al Fahad Security Services', ar: 'خدمات الفهد الأمنية' },
  { en: 'Saudi Packaging Co.', ar: 'السعودية للتغليف' },
  { en: 'Al Bilad Investments', ar: 'استثمارات البلاد' },
  { en: 'Saudi Medical Supplies', ar: 'المستلزمات الطبية السعودية' },
];

const VEHICLE_MODELS = [
  { make: 'Toyota', model: 'Camry', body: 1, fuel: 1, seats: 5, prefix: 'JTD' },
  { make: 'Toyota', model: 'Corolla', body: 1, fuel: 1, seats: 5, prefix: 'JTC' },
  { make: 'Toyota', model: 'Hilux', body: 3, fuel: 2, seats: 5, prefix: 'MR0' },
  { make: 'Toyota', model: 'Land Cruiser', body: 2, fuel: 3, seats: 7, prefix: 'JTM' },
  { make: 'Hyundai', model: 'Sonata', body: 1, fuel: 1, seats: 5, prefix: 'KMH' },
  { make: 'Hyundai', model: 'Tucson', body: 2, fuel: 1, seats: 5, prefix: 'KM8' },
  { make: 'Hyundai', model: 'Elantra', body: 1, fuel: 1, seats: 5, prefix: 'KME' },
  { make: 'Nissan', model: 'Patrol', body: 2, fuel: 3, seats: 7, prefix: 'JN1' },
  { make: 'Nissan', model: 'Altima', body: 1, fuel: 1, seats: 5, prefix: 'JNA' },
  { make: 'Nissan', model: 'Sunny', body: 1, fuel: 1, seats: 5, prefix: 'JNS' },
  { make: 'Kia', model: 'K5', body: 1, fuel: 1, seats: 5, prefix: 'KNA' },
  { make: 'Kia', model: 'Sportage', body: 2, fuel: 1, seats: 5, prefix: 'KNB' },
  { make: 'Chevrolet', model: 'Tahoe', body: 2, fuel: 3, seats: 7, prefix: '1GN' },
  { make: 'GMC', model: 'Yukon', body: 2, fuel: 3, seats: 7, prefix: '1GK' },
];

const INSURANCE_COMPANIES = [
  'Tawuniya', 'Bupa Arabia', 'Al Rajhi Takaful', 'Malath Insurance',
  'Salama Insurance', 'Walaa Insurance', 'Gulf Union Cooperative',
];

const COLORS = ['White', 'Silver', 'Black', 'Gray', 'Blue', 'Red', 'Pearl White', 'Beige', 'Brown', 'Dark Blue'];

const PLATE_LETTERS = ['ABD', 'SHR', 'KHA', 'NWR', 'HMD', 'RSH', 'FHD', 'SLT', 'TRK', 'MHD', 'BSD', 'ALM', 'SKR', 'HBR', 'JBL'];

// ─── Generate data ────────────────────────────────────────────────────────────

function generateBranches() {
  const branches = [];
  for (let i = 1; i <= 30; i++) {
    const city = KSA_CITIES[i - 1];
    branches.push({
      id: branchGuid(i),
      code: `BR-${i.toString().padStart(3, '0')}`,
      nameEn: `${city.en} Branch`,
      nameAr: `فرع ${city.ar}`,
      cityEn: city.en,
      cityAr: city.ar,
      regionEn: city.regionEn,
      regionAr: city.regionAr,
      licenseNumber: `LIC-${(700000 + i * 111).toString()}`,
      address: `King Fahd Road, ${city.en}, Saudi Arabia`,
      latitude: city.lat,
      longitude: city.lng,
      phoneNumber: `+9661${(2000000 + i * 10000).toString()}`,
      tajeerBranchId: 1000 + i,
      tajeerOperatorId: 5000000 + i,
      isActive: true,
    });
  }
  return branches;
}

function generateCustomers() {
  const customers = [];
  // 70 B2B
  for (let i = 1; i <= 70; i++) {
    const company = COMPANY_NAMES[i - 1];
    const crNum = `10${(10000000 + i * 1234).toString()}`;
    const vatNum = `3${(10000000000 + i * 12345).toString().substring(0, 10)}3`;
    customers.push({
      id: customerGuid(i),
      type: 1,
      status: 1,
      displayName: company.en,
      displayNameAr: company.ar,
      email: `contact@${company.en.toLowerCase().replace(/[^a-z0-9]/g, '')}.com.sa`,
      mobile: `+9665${(50000000 + i * 100000).toString().substring(0, 8)}`,
      nationalAddress: `${1000 + i} Industrial Area, ${KSA_CITIES[i % 30].en}`,
      preferredLanguage: i % 3 === 0 ? 1 : 0,
      legalName: company.en,
      legalNameAr: company.ar,
      commercialRegistration: crNum,
      vatNumber: vatNum,
      billingAddress: `PO Box ${1000 + i}, ${KSA_CITIES[i % 30].en}, Saudi Arabia`,
      creditLimit: 50000 + (i * 5000),
      creditCurrency: 'SAR',
      personNameEn: null,
      personNameAr: null,
      idTypeCode: null,
      personIdNumber: null,
      dateOfBirth: null,
      nationalityCode: 'SA',
      kycVerified: true,
      piiOptedOut: false,
    });
  }
  // B2C removed — B2B only
  return customers;
}

function generateVehicles(branches) {
  const vehicles = [];
  for (let i = 1; i <= 120; i++) {
    const modelInfo = VEHICLE_MODELS[(i - 1) % VEHICLE_MODELS.length];
    const year = 2022 + (i % 4);
    const vin = `${modelInfo.prefix}${year.toString().substring(2)}SA${i.toString().padStart(8, '0')}${(10 + (i % 90)).toString()}`;
    const plateNum = (1000 + i * 7).toString();
    const plateLetterIdx = i % PLATE_LETTERS.length;
    const branchIdx = (i % 30) + 1;
    const ownerBranch = branchGuid(branchIdx);
    const currentBranch = branchGuid(((i + 3) % 30) + 1);
    const km = 5000 + (i * 500);
    const purchasePrice = modelInfo.body === 2 ? 120000 + (i * 300) : modelInfo.body === 3 ? 95000 + (i * 200) : 75000 + (i * 250);
    const depreciationPerMonth = Math.round(purchasePrice / 60);
    const monthsOwned = 6 + (i % 24);
    const bookValue = purchasePrice - (depreciationPerMonth * monthsOwned);

    // status: Available=1, Reserved=2, OnRent=3, InService=4
    let status = 1; // Available
    if (i > 80 && i <= 110) status = 3; // OnRent
    if (i > 110) status = 4; // InService

    vehicles.push({
      id: vehicleGuid(i),
      status,
      plateNumber: plateNum,
      plateLetters: PLATE_LETTERS[plateLetterIdx],
      plateTypeCode: 1,
      vin: vin.substring(0, 17),
      make: modelInfo.make,
      model: modelInfo.model,
      modelYear: year,
      color: COLORS[i % COLORS.length],
      fuelType: modelInfo.fuel,
      transmissionType: 1,
      bodyType: modelInfo.body,
      seats: modelInfo.seats,
      licenseExpiryDate: `2027-${(1 + i % 12).toString().padStart(2, '0')}-${(1 + i % 28).toString().padStart(2, '0')}`,
      insuranceExpiryDate: `2027-${(1 + (i + 3) % 12).toString().padStart(2, '0')}-15`,
      insuranceCompany: INSURANCE_COMPANIES[i % INSURANCE_COMPANIES.length],
      insurancePolicyNumber: `POL-${year}-${i.toString().padStart(6, '0')}`,
      ownerBranchId: ownerBranch,
      currentBranchId: currentBranch,
      currentKm: km,
      purchasePrice,
      purchaseDate: `${year}-01-15`,
      depreciationPerMonth,
      currentBookValue: bookValue > 0 ? bookValue : 10000,
      telematicsProvider: i % 3 === 0 ? 'Wialon' : i % 3 === 1 ? 'Geotab' : 'Teltonika',
      deviceImei: `86${(1000000000000 + i * 111111111).toString().substring(0, 13)}`,
      notes: null,
    });
  }
  return vehicles;
}

function generateDrivers(customers) {
  const drivers = [];
  for (let i = 1; i <= 200; i++) {
    const firstIdx = (i - 1) % 30;
    const lastIdx = (i + 7) % 30;
    const firstEn = SAUDI_FIRST_NAMES_EN[firstIdx];
    const lastEn = SAUDI_LAST_NAMES_EN[lastIdx];
    const firstAr = SAUDI_FIRST_NAMES_AR[firstIdx];
    const lastAr = SAUDI_LAST_NAMES_AR[lastIdx];
    const custIdx = ((i - 1) % 70) + 1;

    drivers.push({
      id: driverGuid(i),
      status: i <= 180 ? 1 : i <= 195 ? 2 : 3, // Active=1, Suspended=2, Banned=3
      customerId: customerGuid(custIdx),
      personNameEn: `${firstEn} ${lastEn}`,
      personNameAr: `${firstAr} ${lastAr}`,
      idTypeCode: i % 5 === 0 ? 2 : 1,
      personIdNumber: `${i % 5 === 0 ? '2' : '1'}${(100000000 + i * 1234567).toString().substring(0, 9)}`,
      dateOfBirth: `19${80 + (i % 15)}-${(1 + i % 12).toString().padStart(2, '0')}-${(1 + i % 28).toString().padStart(2, '0')}`,
      nationalityCode: i % 5 === 0 ? 'PK' : i % 7 === 0 ? 'EG' : i % 11 === 0 ? 'IN' : 'SA',
      driverLicenseNumber: `DL${(3000000000 + i * 1111111).toString().substring(0, 10)}`,
      licenseClass: i % 10 === 0 ? 3 : 1,
      licenseExpiryDate: `2027-${(1 + i % 12).toString().padStart(2, '0')}-${(10 + i % 18).toString().padStart(2, '0')}`,
      mobile: `+9665${(10000000 + i * 100000).toString().substring(0, 8)}`,
      email: i % 3 === 0 ? `${firstEn.toLowerCase()}${i}@email.com` : null,
      tammAuthorizationStatus: 0,
      defensiveDrivingCertHeld: i % 4 === 0,
      accidentCountLast3Yrs: i % 5 === 0 ? 1 : 0,
      piiOptedOut: false,
    });
  }
  return drivers;
}

function generateRentPolicies() {
  const policies = [
    { code: 'ECON', nameEn: 'Economy', nameAr: 'اقتصادي', descEn: 'Economy daily rental policy for compact and sedan vehicles', descAr: 'سياسة إيجار يومية اقتصادية', baseDaily: 120, baseHourly: 15, kmDay: 200, kmHour: 25, unlimited: false, lateFee: 50, extraKm: 0.50, minDays: 1, maxDays: 30, deposit: 500, tajId: 101 },
    { code: 'MDSZ', nameEn: 'Midsize', nameAr: 'متوسط', descEn: 'Midsize sedan rental policy with standard features', descAr: 'سياسة إيجار سيارات سيدان متوسطة', baseDaily: 180, baseHourly: 22, kmDay: 250, kmHour: 30, unlimited: false, lateFee: 75, extraKm: 0.75, minDays: 1, maxDays: 60, deposit: 1000, tajId: 102 },
    { code: 'SUV', nameEn: 'SUV', nameAr: 'دفع رباعي', descEn: 'SUV rental policy for family and off-road vehicles', descAr: 'سياسة إيجار سيارات الدفع الرباعي', baseDaily: 280, baseHourly: 35, kmDay: 300, kmHour: 35, unlimited: false, lateFee: 100, extraKm: 1.00, minDays: 1, maxDays: 90, deposit: 2000, tajId: 103 },
    { code: 'PKUP', nameEn: 'Pickup', nameAr: 'بيك أب', descEn: 'Pickup truck rental for commercial and utility use', descAr: 'سياسة إيجار الشاحنات الخفيفة', baseDaily: 200, baseHourly: 25, kmDay: 300, kmHour: 35, unlimited: false, lateFee: 80, extraKm: 0.60, minDays: 1, maxDays: 90, deposit: 1500, tajId: 104 },
    { code: 'LUX', nameEn: 'Luxury', nameAr: 'فاخر', descEn: 'Luxury vehicle rental with premium services', descAr: 'سياسة إيجار السيارات الفاخرة', baseDaily: 500, baseHourly: 65, kmDay: 200, kmHour: 25, unlimited: false, lateFee: 200, extraKm: 2.00, minDays: 1, maxDays: 30, deposit: 5000, tajId: 105 },
    { code: 'CORP', nameEn: 'Corporate', nameAr: 'شركات', descEn: 'Corporate fleet rental with volume discounts and unlimited km', descAr: 'سياسة إيجار أساطيل الشركات', baseDaily: 150, baseHourly: null, kmDay: 0, kmHour: 0, unlimited: true, lateFee: 60, extraKm: 0, minDays: 30, maxDays: 365, deposit: 3000, tajId: 106 },
    { code: 'SHRT', nameEn: 'Short-Term', nameAr: 'قصير المدة', descEn: 'Short-term hourly rental for quick trips', descAr: 'إيجار قصير المدة بالساعة', baseDaily: 100, baseHourly: 12, kmDay: 150, kmHour: 20, unlimited: false, lateFee: 40, extraKm: 0.40, minDays: 1, maxDays: 7, deposit: 300, tajId: 107 },
    { code: 'LONG', nameEn: 'Long-Term', nameAr: 'طويل المدة', descEn: 'Long-term lease with reduced daily rates and unlimited km', descAr: 'إيجار طويل المدة بأسعار مخفضة', baseDaily: 130, baseHourly: null, kmDay: 0, kmHour: 0, unlimited: true, lateFee: 50, extraKm: 0, minDays: 90, maxDays: null, deposit: 2000, tajId: 108 },
  ];
  return policies.map((p, idx) => ({
    id: rentPolicyGuid(idx + 1),
    ...p,
    isActive: true,
  }));
}

function generateExtendedCoverages() {
  const coverages = [
    { code: 'CDW', nameEn: 'Collision Damage Waiver', nameAr: 'تنازل عن أضرار التصادم', descEn: 'Covers collision damage with reduced deductible', descAr: 'يغطي أضرار التصادم مع تخفيض التحمل', coverageType: 1, dailyRate: 35, deductible: 500, tajId: 201 },
    { code: 'PAI', nameEn: 'Personal Accident Insurance', nameAr: 'تأمين الحوادث الشخصية', descEn: 'Personal injury coverage for driver and passengers', descAr: 'تغطية الإصابات الشخصية للسائق والركاب', coverageType: 2, dailyRate: 20, deductible: 0, tajId: 202 },
    { code: 'TIRE', nameEn: 'Tire & Glass Protection', nameAr: 'حماية الإطارات والزجاج', descEn: 'Coverage for tire punctures and windshield damage', descAr: 'تغطية ثقوب الإطارات وأضرار الزجاج الأمامي', coverageType: 3, dailyRate: 15, deductible: 200, tajId: 203 },
    { code: 'ROAD', nameEn: 'Roadside Assistance', nameAr: 'المساعدة على الطريق', descEn: '24/7 roadside assistance including towing and battery jump', descAr: 'مساعدة على الطريق على مدار الساعة', coverageType: 4, dailyRate: 10, deductible: 0, tajId: 204 },
    { code: 'FULL', nameEn: 'Full Coverage Package', nameAr: 'باقة التغطية الشاملة', descEn: 'Complete coverage bundle including CDW, PAI, tire, and roadside', descAr: 'باقة تغطية شاملة تشمل جميع الأنواع', coverageType: 1, dailyRate: 65, deductible: 300, tajId: 205 },
  ];
  return coverages.map((c, idx) => ({
    id: extCoverageGuid(idx + 1),
    ...c,
    isActive: true,
  }));
}

function generateApprovalTiers() {
  return [
    { id: approvalTierGuid(1), tierLevel: 1, requiredRoleCode: 'SalesManager', minAmountSar: 0, isActive: true },
    { id: approvalTierGuid(2), tierLevel: 2, requiredRoleCode: 'RegionalManager', minAmountSar: 50000, isActive: true },
    { id: approvalTierGuid(3), tierLevel: 3, requiredRoleCode: 'GeneralManager', minAmountSar: 200000, isActive: true },
  ];
}

function generateLeases(customers, vehicles, drivers, rentPolicies, extCoverages, branches) {
  const leases = [];
  // 40 active, 10 closed, 5 suspended, 5 draft = 60
  const statusDist = [];
  for (let i = 0; i < 40; i++) statusDist.push(2); // Active
  for (let i = 0; i < 10; i++) statusDist.push(5); // Closed
  for (let i = 0; i < 5; i++) statusDist.push(7);  // Suspended
  for (let i = 0; i < 5; i++) statusDist.push(0);  // Draft

  for (let i = 1; i <= 60; i++) {
    const status = statusDist[i - 1];
    const custIdx = ((i - 1) % 70) + 1;
    const vehIdx = ((i - 1) % 120) + 1;
    const drvIdx = ((i - 1) % 200) + 1;
    const policyIdx = ((i - 1) % 8) + 1;
    const coverageIdx = i % 3 === 0 ? ((i % 5) + 1) : null;
    const branchIdx = ((i - 1) % 30) + 1;
    const branchIdx2 = ((i + 5) % 30) + 1;
    const branchIdx3 = ((i + 10) % 30) + 1;

    const startMonth = 1 + (i % 6);
    const durationDays = 30 + (i % 330);
    const startDate = `2026-${startMonth.toString().padStart(2, '0')}-${(1 + i % 28).toString().padStart(2, '0')}T08:00:00+03:00`;
    const endDate = new Date(2026, startMonth - 1, 1 + (i % 28) + durationDays);
    const endDateStr = `${endDate.getFullYear()}-${(endDate.getMonth() + 1).toString().padStart(2, '0')}-${endDate.getDate().toString().padStart(2, '0')}T08:00:00+03:00`;

    const dailyRate = 120 + (i * 10);
    const totalDays = durationDays;
    const rentAmount = dailyRate * totalDays;
    const vatAmount = Math.round(rentAmount * 0.15 * 100) / 100;
    const totalAmount = rentAmount + vatAmount;
    const paidAmount = status === 5 ? totalAmount : status === 2 ? Math.round(totalAmount * 0.4) : 0;
    const remainingAmount = totalAmount - paidAmount;

    leases.push({
      id: leaseGuid(i),
      customerId: customerGuid(custIdx),
      vehicleId: vehicleGuid(vehIdx),
      primaryDriverId: driverGuid(drvIdx),
      rentPolicyId: rentPolicyGuid(policyIdx),
      extendedCoverageId: coverageIdx ? extCoverageGuid(coverageIdx) : null,
      workingBranchId: branchGuid(branchIdx),
      receiveBranchId: branchGuid(branchIdx2),
      returnBranchId: branchGuid(branchIdx3),
      tajeerContractNumber: 9000000 + i,
      status,
      contractStartUtc: startDate,
      contractEndUtc: endDateStr,
      contractTypeCode: 1,
      allowedKmPerDay: 200 + (i % 100),
      allowedKmPerHour: 25 + (i % 10),
      allowedLateHours: 2,
      unlimitedKm: i % 8 === 0,
      rentAmount,
      paidAmount,
      remainingAmount,
      totalAmount,
      vatAmount,
      paymentMethodCode: i % 2 === 0 ? 1 : 2,
      extensionCount: status === 2 && i % 5 === 0 ? 1 : 0,
      piiOptedOut: false,
    });
  }
  return leases;
}

function generateQuotations(customers) {
  const quotations = [];
  const statuses = [];
  // 10 Draft, 8 PendingApproval, 5 Approved, 4 SentToCustomer, 3 Accepted
  for (let i = 0; i < 10; i++) statuses.push('Draft');
  for (let i = 0; i < 8; i++) statuses.push('PendingApproval');
  for (let i = 0; i < 5; i++) statuses.push('Approved');
  for (let i = 0; i < 4; i++) statuses.push('SentToCustomer');
  for (let i = 0; i < 3; i++) statuses.push('Accepted');

  for (let i = 1; i <= 30; i++) {
    const status = statuses[i - 1];
    const custIdx = ((i - 1) % 70) + 1;
    const amIdx = ((i - 1) % 5) + 1;
    const quoteDate = `2026-${(3 + (i % 4)).toString().padStart(2, '0')}-${(1 + i % 28).toString().padStart(2, '0')}`;
    const validDate = `2026-${(4 + (i % 4)).toString().padStart(2, '0')}-${(1 + i % 28).toString().padStart(2, '0')}`;
    const durationMonths = 6 + (i % 18);
    const subTotal = 5000 + (i * 2000);
    const discountPercent = i % 5 === 0 ? 5 : i % 3 === 0 ? 10 : 0;
    const discountedSubTotal = subTotal * (1 - discountPercent / 100);
    const vatSar = Math.round(discountedSubTotal * 0.15 * 100) / 100;
    const totalSar = Math.round((discountedSubTotal + vatSar) * 100) / 100;

    const submittedAt = status !== 'Draft' ? `${quoteDate}T10:00:00+03:00` : null;
    const approvedAt = ['Approved', 'SentToCustomer', 'Accepted'].includes(status) ? `${quoteDate}T14:00:00+03:00` : null;
    const sentAt = ['SentToCustomer', 'Accepted'].includes(status) ? `${quoteDate}T16:00:00+03:00` : null;
    const acceptedAt = status === 'Accepted' ? `${validDate}T09:00:00+03:00` : null;

    quotations.push({
      id: quotationGuid(i),
      quoteNumber: `QT-2026-${i.toString().padStart(4, '0')}`,
      customerId: customerGuid(custIdx),
      accountManagerId: accountManagerGuid(amIdx),
      status,
      quoteDate,
      validUntilDate: validDate,
      contractType: i % 3 === 0 ? 'Daily' : 'LongTermLease',
      estimatedDurationMonths: durationMonths,
      termsAndConditionsMd: null,
      subTotalSar: subTotal,
      discountPercent,
      vatSar,
      totalSar,
      submittedAtUtc: submittedAt,
      approvedAtUtc: approvedAt,
      sentAtUtc: sentAt,
      acceptedAtUtc: acceptedAt,
      closedAtUtc: null,
    });
  }
  return quotations;
}

function generateQuotationLines(quotations) {
  const lines = [];
  const itemTypes = ['VehicleRental', 'Insurance', 'AdditionalDriver', 'Gps', 'Other'];

  for (let qi = 1; qi <= quotations.length; qi++) {
    const lineCount = 1 + (qi % 4); // 1-4 lines
    for (let li = 1; li <= lineCount; li++) {
      const itemType = itemTypes[(li - 1) % itemTypes.length];
      let desc, unitPrice, qty, specRef;
      switch (itemType) {
        case 'VehicleRental':
          const vm = VEHICLE_MODELS[(qi + li) % VEHICLE_MODELS.length];
          desc = `Monthly rental - ${vm.make} ${vm.model}`;
          unitPrice = 3000 + (qi * 100);
          qty = 1 + (qi % 3);
          specRef = `${vm.make}/${vm.model}/2025`;
          break;
        case 'Insurance':
          desc = 'Comprehensive insurance coverage';
          unitPrice = 500 + (qi * 20);
          qty = 1;
          specRef = null;
          break;
        case 'AdditionalDriver':
          desc = 'Additional driver registration';
          unitPrice = 300;
          qty = 1 + (qi % 2);
          specRef = null;
          break;
        case 'Gps':
          desc = 'GPS tracking device monthly fee';
          unitPrice = 150;
          qty = 1;
          specRef = null;
          break;
        default:
          desc = 'Miscellaneous service fee';
          unitPrice = 200;
          qty = 1;
          specRef = null;
      }
      const discountPct = li === 1 && qi % 4 === 0 ? 5 : 0;
      const lineTotal = Math.round(unitPrice * qty * (1 - discountPct / 100) * 100) / 100;

      lines.push({
        id: quotationLineGuid(qi, li),
        quotationId: quotationGuid(qi),
        lineNumber: li,
        itemType,
        description: desc,
        vehicleSpecRef: specRef,
        quantity: qty,
        unitPriceSar: unitPrice,
        discountPercent: discountPct,
        lineTotalSar: lineTotal,
      });
    }
  }
  return lines;
}

function generateQuotationApprovals(quotations) {
  const approvals = [];
  const needsApproval = quotations.filter(q =>
    ['PendingApproval', 'Approved', 'SentToCustomer', 'Accepted'].includes(q.status)
  );

  for (const q of needsApproval) {
    const qi = quotations.indexOf(q) + 1;
    const isApproved = ['Approved', 'SentToCustomer', 'Accepted'].includes(q.status);
    // Tier 1 always
    approvals.push({
      id: quotationApprovalGuid(qi, 1),
      quotationId: q.id,
      tierLevel: 1,
      requiredRoleCode: 'SalesManager',
      assignedUserId: accountManagerGuid(1),
      status: isApproved ? 'Approved' : 'Pending',
      decisionAtUtc: isApproved ? q.approvedAtUtc : null,
      decidedByUserId: isApproved ? accountManagerGuid(1) : null,
      comment: isApproved ? 'Approved - within policy limits' : null,
    });
    // Tier 2 for larger quotes
    if (q.totalSar > 50000) {
      approvals.push({
        id: quotationApprovalGuid(qi, 2),
        quotationId: q.id,
        tierLevel: 2,
        requiredRoleCode: 'RegionalManager',
        assignedUserId: accountManagerGuid(2),
        status: isApproved ? 'Approved' : 'Pending',
        decisionAtUtc: isApproved ? q.approvedAtUtc : null,
        decidedByUserId: isApproved ? accountManagerGuid(2) : null,
        comment: isApproved ? 'Regional approval granted' : null,
      });
    }
  }
  return approvals;
}

function generateRfqs(customers) {
  const rfqs = [];
  const rfqHistories = [];
  const stages = ['Draft', 'Qualified', 'Proposal', 'Negotiation', 'Won', 'Lost'];
  const stageNums = { Draft: 1, Qualified: 2, Proposal: 3, Negotiation: 4, Won: 5, Lost: 6 };
  const sourceNums = { Direct: 1, CrmSync: 2, Website: 3, Referral: 4 };
  const sources = ['Direct', 'CrmSync', 'Website', 'Referral'];
  const categories = ['Sedan', 'SUV', 'Pickup', 'Van', 'Bus'];

  for (let i = 1; i <= 25; i++) {
    const custIdx = ((i - 1) % customers.length) + 1;
    const stage = stages[(i - 1) % 6];
    const source = sources[(i - 1) % 4];
    const vehicleQty = 1 + (i % 20);
    const tenure = [12, 24, 36, 48, 60][(i - 1) % 5];
    const prob = stage === 'Won' ? 100 : stage === 'Lost' ? 0 : 10 + (i * 3) % 80;
    const month = 1 + ((i - 1) % 6);
    const closeMonth = Math.min(month + 2, 12);

    rfqs.push({
      id: rfqGuid(i),
      rfqNumber: `RFQ-2026-${i.toString().padStart(6, '0')}`,
      customerId: customerGuid(custIdx),
      crmOpportunityId: source === 'CrmSync' ? `OPP-${(10000 + i).toString()}` : null,
      source: sourceNums[source],
      stage: stageNums[stage],
      probability: prob,
      vehicleCategories: JSON.stringify([categories[(i - 1) % 5]]),
      vehicleQty,
      tenureMonths: tenure,
      annualMileageCapKm: 20000 + (i * 1000) % 30000,
      services: JSON.stringify(['Maintenance', 'Insurance'].slice(0, 1 + (i % 2))),
      expectedCloseDate: `2026-${closeMonth.toString().padStart(2, '0')}-15`,
      ownerUserId: accountManagerGuid(1),
      lostReason: stage === 'Lost' ? 'Price too high' : null,
      notes: i % 3 === 0 ? 'Priority fleet deal' : null,
      quotationId: null,
    });

    // Stage history: always has initial "Created" entry
    rfqHistories.push({
      id: rfqHistoryGuid(i, 1),
      rfqId: rfqGuid(i),
      fromStage: null,
      toStage: stageNums.Draft,
      changedByUserId: accountManagerGuid(1),
      comment: 'Created',
    });

    // Add transition history for non-Draft stages
    const stageOrder = ['Draft', 'Qualified', 'Proposal', 'Negotiation', 'Won', 'Lost'];
    const currentIdx = stageOrder.indexOf(stage);
    if (stage === 'Lost') {
      rfqHistories.push({
        id: rfqHistoryGuid(i, 2),
        rfqId: rfqGuid(i),
        fromStage: stageNums.Draft,
        toStage: stageNums.Lost,
        changedByUserId: accountManagerGuid(1),
        comment: 'Price too high',
      });
    } else {
      for (let s = 1; s <= Math.min(currentIdx, 4); s++) {
        rfqHistories.push({
          id: rfqHistoryGuid(i, s + 1),
          rfqId: rfqGuid(i),
          fromStage: stageNums[stageOrder[s - 1]],
          toStage: stageNums[stageOrder[s]],
          changedByUserId: accountManagerGuid(1),
          comment: `Moved to ${stageOrder[s]}`,
        });
      }
    }
  }
  return { rfqs, rfqHistories };
}

function generateInvoices(leases) {
  const invoices = [];
  // 40 invoices linked to leases
  const activeLeases = leases.filter(l => l.status === 2 || l.status === 5);
  for (let i = 1; i <= 40; i++) {
    const lease = activeLeases[(i - 1) % activeLeases.length];
    const month = 1 + ((i - 1) % 6);
    const issueDate = `2026-${month.toString().padStart(2, '0')}-01`;
    const dueDate = `2026-${month.toString().padStart(2, '0')}-15`;
    const baseAmount = Math.round(lease.rentAmount / 12); // monthly portion
    const vatSar = Math.round(baseAmount * 0.15 * 100) / 100;
    const totalSar = baseAmount + vatSar;

    // status mix: Draft=0, Submitted=1, Cleared=2, Finalized=3
    let status;
    if (i <= 10) status = 3; // Finalized
    else if (i <= 20) status = 2; // Cleared
    else if (i <= 30) status = 1; // Submitted
    else status = 0; // Draft

    invoices.push({
      id: invoiceGuid(i),
      invoiceNumber: `INV-2026-${i.toString().padStart(5, '0')}`,
      leaseId: lease.id,
      customerId: lease.customerId,
      status,
      issueDateUtc: issueDate,
      dueDateUtc: dueDate,
      baseAmountSar: baseAmount,
      vatSar,
      totalSar,
      submissionAttempts: 0,
    });
  }
  return invoices;
}

function generatePayments(customers, invoices) {
  const payments = [];
  const allocations = [];
  const methods = ['Cash', 'CreditCard', 'BankTransfer', 'Cheque', 'OnlineTransfer'];

  // 35 payments from various customers
  const uniqueCustomerIds = [...new Set(invoices.map(inv => inv.customerId))];
  for (let i = 1; i <= 35; i++) {
    const custId = uniqueCustomerIds[(i - 1) % uniqueCustomerIds.length];
    const amount = 1500 + (i * 137) % 4000;
    const method = methods[(i - 1) % methods.length];
    const month = 1 + ((i - 1) % 6);
    const day = 1 + ((i * 3) % 25);
    const receivedDate = `2026-${month.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`;
    const refNum = `REF-${(20260000 + i * 17).toString()}`;

    // Some payments fully allocated, some partially, some unallocated
    let remaining = amount;
    const payAllocations = [];
    if (i <= 20) {
      // Allocate against invoices for this customer
      const custInvoices = invoices.filter(inv => inv.customerId === custId);
      if (custInvoices.length > 0) {
        const inv = custInvoices[(i - 1) % custInvoices.length];
        const allocAmount = Math.min(remaining, inv.totalSar);
        payAllocations.push({
          id: paymentAllocationGuid(i, 1),
          advancePaymentId: paymentGuid(i),
          invoiceId: inv.id,
          invoiceNumber: inv.invoiceNumber,
          allocatedAmountSar: allocAmount,
        });
        remaining -= allocAmount;
      }
    }

    payments.push({
      id: paymentGuid(i),
      customerId: custId,
      amount,
      paymentMethod: method,
      receivedDate,
      referenceNumber: refNum,
      notes: i % 5 === 0 ? 'Advance payment for fleet' : null,
      remainingBalance: Math.max(0, Math.round(remaining * 100) / 100),
    });

    allocations.push(...payAllocations);
  }
  return { payments, allocations };
}

function generateContracts(quotations, quotationLines, leases) {
  const contracts = [];
  const contractLines = [];
  // Create contracts from Accepted and some Approved quotations
  const eligibleQuotations = quotations.filter(q =>
    q.status === 'Accepted' || q.status === 'Approved' || q.status === 'SentToCustomer'
  );

  for (let ci = 0; ci < eligibleQuotations.length; ci++) {
    const q = eligibleQuotations[ci];
    const i = ci + 1;
    const isAccepted = q.status === 'Accepted';
    const status = isAccepted ? 2 : 1; // Active or Draft
    const startDate = `2026-${(4 + (ci % 4)).toString().padStart(2, '0')}-01T08:00:00+03:00`;
    const endMonth = 4 + (ci % 4) + q.estimatedDurationMonths;
    const endYear = 2026 + Math.floor((endMonth - 1) / 12);
    const endMo = ((endMonth - 1) % 12) + 1;
    const endDate = `${endYear}-${endMo.toString().padStart(2, '0')}-01T08:00:00+03:00`;

    const contractNumber = `CNT-2026-${i.toString().padStart(5, '0')}`;

    // Get quotation lines for this quote to build contract lines
    const qLines = quotationLines.filter(l => l.quotationId === q.id);
    let totalVehicles = 0;
    let monthlyRent = 0;

    for (let li = 0; li < qLines.length; li++) {
      const ql = qLines[li];
      // Only vehicle rental lines become contract lines
      if (ql.itemType === 'VehicleRental' && ql.vehicleSpecRef) {
        const parts = ql.vehicleSpecRef.split('/');
        const make = parts[0] || 'Toyota';
        const model = parts[1] || 'Camry';
        const year = parseInt(parts[2] || '2025', 10);
        const lineTotal = ql.lineTotalSar;
        contractLines.push({
          id: contractLineGuid(i, li + 1),
          contractId: contractGuid(i),
          make, model, year,
          description: `${make} ${model} ${year}`,
          quantity: ql.quantity,
          unitPriceSar: ql.unitPriceSar,
          lineTotalSar: lineTotal,
        });
        totalVehicles += ql.quantity;
        monthlyRent += lineTotal;
      }
    }
    // If no vehicle lines found, create a default line
    if (totalVehicles === 0) {
      const vm = VEHICLE_MODELS[ci % VEHICLE_MODELS.length];
      const qty = 2 + (ci % 3);
      const unitPrice = 3000 + (ci * 200);
      const lineTotal = qty * unitPrice;
      contractLines.push({
        id: contractLineGuid(i, 1),
        contractId: contractGuid(i),
        make: vm.make, model: vm.model, year: 2025,
        description: `${vm.make} ${vm.model} 2025`,
        quantity: qty, unitPriceSar: unitPrice, lineTotalSar: lineTotal,
      });
      totalVehicles = qty;
      monthlyRent = lineTotal;
    }

    const totalContractValue = monthlyRent * q.estimatedDurationMonths;

    contracts.push({
      id: contractGuid(i),
      contractNumber,
      customerId: q.customerId,
      quotationId: q.id,
      status,
      contractTypeCode: 1,
      startDate, endDate,
      durationMonths: q.estimatedDurationMonths,
      totalVehicles,
      monthlyRentSar: monthlyRent,
      totalContractValueSar: totalContractValue,
      paymentTermsDays: 30,
      notes: null,
    });
  }

  // Assign first N leases to contracts (3 leases per contract)
  for (let ci = 0; ci < contracts.length; ci++) {
    const c = contracts[ci];
    if (c.status !== 2) continue; // Only active contracts get lease assignments
    const startIdx = ci * 3;
    for (let j = 0; j < 3 && startIdx + j < leases.length; j++) {
      leases[startIdx + j].contractId = c.id;
    }
  }

  return { contracts, contractLines };
}

function generatePricingVersions() {
  return [
    { id: pricingVersionGuid(1), name: 'FY2025 Q4 Pricing', status: 'Expired', effectiveFrom: '2025-10-01T00:00:00+03:00', effectiveTo: '2025-12-31T23:59:59+03:00' },
    { id: pricingVersionGuid(2), name: 'FY2026 H1 Pricing', status: 'Active', effectiveFrom: '2026-01-01T00:00:00+03:00', effectiveTo: '2026-06-30T23:59:59+03:00' },
    { id: pricingVersionGuid(3), name: 'FY2026 H2 Pricing', status: 'Active', effectiveFrom: '2026-07-01T00:00:00+03:00', effectiveTo: null },
  ];
}

function generatePricingDiscountPolicies() {
  return [{
    id: pricingDiscountGuid(1),
    maxDiscountPercent: 25,
    allowedPresetsCsv: '0,5,10,15,20,25',
  }];
}

function generatePricingFormulas() {
  const formulas = [
    { code: 'DAILY_RATE', expr: 'baseDaily * (1 + seasonalMultiplier)', output: 'dailyRate', precision: 2, rounding: 'HalfUp' },
    { code: 'MONTHLY_RATE', expr: 'dailyRate * 30 * 0.85', output: 'monthlyRate', precision: 2, rounding: 'HalfUp' },
    { code: 'VAT_CALC', expr: 'subtotal * 0.15', output: 'vatAmount', precision: 2, rounding: 'HalfUp' },
    { code: 'EXTRA_KM', expr: 'excessKm * extraKmRate', output: 'excessKmCharge', precision: 2, rounding: 'HalfUp' },
    { code: 'LATE_HOUR', expr: 'lateHours * lateHourFee', output: 'lateHourCharge', precision: 2, rounding: 'HalfUp' },
    { code: 'DEPOSIT', expr: 'dailyRate * depositDaysMultiplier', output: 'securityDeposit', precision: 0, rounding: 'Up' },
    { code: 'DISCOUNT', expr: 'subtotal * (discountPercent / 100)', output: 'discountAmount', precision: 2, rounding: 'HalfUp' },
    { code: 'INSURANCE_DAILY', expr: 'coverageDailyRate * durationDays', output: 'insuranceTotal', precision: 2, rounding: 'HalfUp' },
    { code: 'DEPRECIATION', expr: 'purchasePrice / usefulLifeMonths', output: 'monthlyDepreciation', precision: 2, rounding: 'HalfUp' },
    { code: 'TOTAL_LEASE', expr: '(monthlyRate * durationMonths) + insuranceTotal + vatAmount - discountAmount', output: 'totalLeaseValue', precision: 2, rounding: 'HalfUp' },
  ];
  return formulas.map((f, idx) => ({
    id: pricingFormulaGuid(idx + 1),
    ...f,
    isActive: true,
  }));
}

// ─── SQL Generation ───────────────────────────────────────────────────────────

function sqlHeader() {
  return `-- ============================================================================
-- AutoLeaseNet Full Seed Data
-- Generated: ${new Date().toISOString()}
-- Target: Azure SQL Edge (SQL Server compatible)
-- TenantId: ${TENANT_ID}
-- ============================================================================

USE AutoLeaseNet;
GO

`;
}

function batchHeader() {
  return `SET QUOTED_IDENTIFIER ON;
DECLARE @TenantId UNIQUEIDENTIFIER = '${TENANT_ID}';
DECLARE @Now DATETIMEOFFSET = '${NOW}';
`;
}

function insertBranches(branches) {
  let sql = `-- =============================================================================
-- BRANCHES (${branches.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const b of branches) {
    sql += `INSERT INTO Branches (Id, Code, NameEn, NameAr, CityEn, CityAr, RegionEn, RegionAr, LicenseNumber, Address, Latitude, Longitude, PhoneNumber, TajeerBranchId, TajeerOperatorId, IsActive, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${b.id}', ${esc(b.code)}, ${esc(b.nameEn)}, ${esc(b.nameAr)}, ${esc(b.cityEn)}, ${esc(b.cityAr)}, ${esc(b.regionEn)}, ${esc(b.regionAr)}, ${esc(b.licenseNumber)}, ${esc(b.address)}, ${b.latitude}, ${b.longitude}, ${esc(b.phoneNumber)}, ${b.tajeerBranchId}, ${b.tajeerOperatorId}, ${bit(b.isActive)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function generateAccounts(customers) {
  const BUSINESS_TYPES = [
    'Construction', 'Transportation', 'Oil & Gas', 'Real Estate', 'Retail',
    'Healthcare', 'Education', 'Government', 'Technology', 'Manufacturing',
    'Hospitality', 'Agriculture', 'Financial Services', 'Telecommunications', 'Logistics',
  ];
  const POSITIONS_CUSTOMER = ['Fleet Manager', 'Operations Director', 'Procurement Manager', 'VP Operations', 'CEO', 'CFO', 'Transport Manager'];
  const POSITIONS_OURS = ['Account Manager', 'Senior Account Executive', 'Regional Manager', 'Sales Manager', 'Business Development Mgr'];
  const OUR_NAMES_EN = ['Fahad Al-Otaibi', 'Sultan Al-Shehri', 'Mohammed Al-Ghamdi', 'Abdulrahman Al-Qahtani', 'Nasser Al-Dosari',
    'Khalid Al-Zahrani', 'Ahmad Al-Maliki', 'Omar Al-Turki', 'Saad Al-Harbi', 'Faisal Al-Juhani'];
  const OUR_NAMES_AR = ['فهد العتيبي', 'سلطان الشهري', 'محمد الغامدي', 'عبدالرحمن القحطاني', 'ناصر الدوسري',
    'خالد الزهراني', 'أحمد المالكي', 'عمر التركي', 'سعد الحربي', 'فيصل الجهني'];
  const REGIONS = ['Riyadh', 'Makkah', 'Eastern Province', 'Madinah', 'Qassim', 'Asir', 'Tabuk', 'Hail'];

  const b2bCustomers = customers.filter(c => c.type === 1);
  const accounts = [];
  for (let i = 0; i < Math.min(b2bCustomers.length, 40); i++) {
    const cust = b2bCustomers[i];
    accounts.push({
      id: accountRecordGuid(i + 1),
      customerId: cust.id,
      natureOfBusiness: BUSINESS_TYPES[i % BUSINESS_TYPES.length],
      customerContactNameEn: cust.displayName.split(' ')[0] + ' (Contact)',
      customerContactNameAr: null,
      customerContactPosition: POSITIONS_CUSTOMER[i % POSITIONS_CUSTOMER.length],
      customerContactMobile: cust.mobile,
      customerContactEmail: cust.email,
      accountHolderNameEn: OUR_NAMES_EN[i % OUR_NAMES_EN.length],
      accountHolderNameAr: OUR_NAMES_AR[i % OUR_NAMES_AR.length],
      accountHolderPosition: POSITIONS_OURS[i % POSITIONS_OURS.length],
      accountHolderMobile: `+9665${(50000000 + i * 111111).toString().substring(0, 8)}`,
      accountHolderEmail: OUR_NAMES_EN[i % OUR_NAMES_EN.length].split(' ')[0].toLowerCase() + '@autoleasenet.sa',
      street: `${100 + i * 10} Industrial Rd`,
      city: KSA_CITIES[i % KSA_CITIES.length].en,
      region: REGIONS[i % REGIONS.length],
      postalCode: (11000 + i * 100).toString(),
      country: 'Saudi Arabia',
      status: 'Active',
    });
  }
  return accounts;
}

function insertAccounts(accounts) {
  let sql = `-- =============================================================================
-- ACCOUNTS (${accounts.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const a of accounts) {
    sql += `INSERT INTO Accounts (Id, CustomerId, NatureOfBusiness, CustomerContactNameEn, CustomerContactNameAr, CustomerContactPosition, CustomerContactMobile, CustomerContactEmail, AccountHolderNameEn, AccountHolderNameAr, AccountHolderPosition, AccountHolderMobile, AccountHolderEmail, Street, City, Region, PostalCode, Country, Status, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${a.id}', '${a.customerId}', ${esc(a.natureOfBusiness)}, ${esc(a.customerContactNameEn)}, ${esc(a.customerContactNameAr)}, ${esc(a.customerContactPosition)}, ${esc(a.customerContactMobile)}, ${esc(a.customerContactEmail)}, ${esc(a.accountHolderNameEn)}, ${esc(a.accountHolderNameAr)}, ${esc(a.accountHolderPosition)}, ${esc(a.accountHolderMobile)}, ${esc(a.accountHolderEmail)}, ${esc(a.street)}, ${esc(a.city)}, ${esc(a.region)}, ${esc(a.postalCode)}, ${esc(a.country)}, ${esc(a.status)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertCustomers(customers) {
  let sql = `-- =============================================================================
-- CUSTOMERS (${customers.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const c of customers) {
    sql += `INSERT INTO Customers (Id, Type, Status, DisplayName, DisplayNameAr, Email, Mobile, NationalAddress, PreferredLanguage, LegalName, LegalNameAr, CommercialRegistration, VatNumber, BillingAddress, CreditLimit, CreditCurrency, PersonNameEn, PersonNameAr, IdTypeCode, PersonIdNumber, DateOfBirth, NationalityCode, KycVerified, PiiOptedOut, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${c.id}', ${c.type}, ${c.status}, ${esc(c.displayName)}, ${esc(c.displayNameAr)}, ${esc(c.email)}, ${esc(c.mobile)}, ${esc(c.nationalAddress)}, ${c.preferredLanguage}, ${esc(c.legalName)}, ${esc(c.legalNameAr)}, ${esc(c.commercialRegistration)}, ${esc(c.vatNumber)}, ${esc(c.billingAddress)}, ${c.creditLimit !== null ? c.creditLimit : 'NULL'}, ${esc(c.creditCurrency)}, ${esc(c.personNameEn)}, ${esc(c.personNameAr)}, ${c.idTypeCode !== null ? c.idTypeCode : 'NULL'}, ${esc(c.personIdNumber)}, ${c.dateOfBirth ? `'${c.dateOfBirth}'` : 'NULL'}, ${esc(c.nationalityCode)}, ${bit(c.kycVerified)}, ${bit(c.piiOptedOut)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertVehicles(vehicles) {
  let sql = `-- =============================================================================
-- VEHICLES (${vehicles.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const v of vehicles) {
    sql += `INSERT INTO Vehicles (Id, Status, PlateNumber, PlateLetters, PlateTypeCode, Vin, Make, Model, ModelYear, Color, FuelType, TransmissionType, BodyType, Seats, LicenseExpiryDate, InsuranceExpiryDate, InsuranceCompany, InsurancePolicyNumber, OwnerBranchId, CurrentBranchId, CurrentKm, PurchasePrice, PurchaseDate, DepreciationPerMonth, CurrentBookValue, TelematicsProvider, DeviceImei, Notes, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${v.id}', ${v.status}, ${esc(v.plateNumber)}, ${esc(v.plateLetters)}, ${v.plateTypeCode}, ${esc(v.vin)}, ${esc(v.make)}, ${esc(v.model)}, ${v.modelYear}, ${esc(v.color)}, ${v.fuelType}, ${v.transmissionType}, ${v.bodyType}, ${v.seats}, '${v.licenseExpiryDate}', '${v.insuranceExpiryDate}', ${esc(v.insuranceCompany)}, ${esc(v.insurancePolicyNumber)}, '${v.ownerBranchId}', '${v.currentBranchId}', ${v.currentKm}, ${v.purchasePrice}, '${v.purchaseDate}', ${v.depreciationPerMonth}, ${v.currentBookValue}, ${esc(v.telematicsProvider)}, ${esc(v.deviceImei)}, ${esc(v.notes)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertDrivers(drivers) {
  let sql = `-- =============================================================================
-- DRIVERS (${drivers.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const d of drivers) {
    sql += `INSERT INTO Drivers (Id, Status, CustomerId, PersonNameEn, PersonNameAr, IdTypeCode, PersonIdNumber, DateOfBirth, NationalityCode, DriverLicenseNumber, LicenseClass, LicenseExpiryDate, Mobile, Email, TammAuthorizationStatus, DefensiveDrivingCertHeld, AccidentCountLast3Yrs, PiiOptedOut, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${d.id}', ${d.status}, ${d.customerId ? `'${d.customerId}'` : 'NULL'}, ${esc(d.personNameEn)}, ${esc(d.personNameAr)}, ${d.idTypeCode}, ${esc(d.personIdNumber)}, '${d.dateOfBirth}', ${esc(d.nationalityCode)}, ${esc(d.driverLicenseNumber)}, ${d.licenseClass}, '${d.licenseExpiryDate}', ${esc(d.mobile)}, ${esc(d.email)}, ${d.tammAuthorizationStatus}, ${bit(d.defensiveDrivingCertHeld)}, ${d.accidentCountLast3Yrs}, ${bit(d.piiOptedOut)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertRentPolicies(policies) {
  let sql = `-- =============================================================================
-- RENT POLICIES (${policies.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const p of policies) {
    sql += `INSERT INTO RentPolicies (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, BaseDailyRate, BaseHourlyRate, AllowedKmPerDay, AllowedKmPerHour, UnlimitedKm, LateHourFee, ExtraKmFee, MinRentalDays, MaxRentalDays, SecurityDeposit, TajeerRentPolicyId, IsActive, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${p.id}', ${esc(p.code)}, ${esc(p.nameEn)}, ${esc(p.nameAr)}, ${esc(p.descEn)}, ${esc(p.descAr)}, ${p.baseDaily}, ${p.baseHourly !== null ? p.baseHourly : 'NULL'}, ${p.kmDay}, ${p.kmHour}, ${bit(p.unlimited)}, ${p.lateFee}, ${p.extraKm}, ${p.minDays}, ${p.maxDays !== null ? p.maxDays : 'NULL'}, ${p.deposit !== null ? p.deposit : 'NULL'}, ${p.tajId}, ${bit(p.isActive)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertExtendedCoverages(coverages) {
  let sql = `-- =============================================================================
-- EXTENDED COVERAGES (${coverages.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const c of coverages) {
    sql += `INSERT INTO ExtendedCoverages (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, CoverageType, DailyRate, DeductibleAmount, TajeerExtendedCoverageId, IsActive, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${c.id}', ${esc(c.code)}, ${esc(c.nameEn)}, ${esc(c.nameAr)}, ${esc(c.descEn)}, ${esc(c.descAr)}, ${c.coverageType}, ${c.dailyRate}, ${c.deductible}, ${c.tajId}, ${bit(c.isActive)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertRfqs(rfqs) {
  let sql = `-- =============================================================================
-- RFQS (${rfqs.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const r of rfqs) {
    sql += `INSERT INTO Rfqs (Id, RfqNumber, CustomerId, CrmOpportunityId, Source, Stage, Probability, VehicleCategories, VehicleQty, TenureMonths, AnnualMileageCapKm, Services, ExpectedCloseDate, OwnerUserId, LostReason, Notes, QuotationId, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${r.id}', ${esc(r.rfqNumber)}, '${r.customerId}', ${esc(r.crmOpportunityId)}, ${r.source}, ${r.stage}, ${r.probability}, ${esc(r.vehicleCategories)}, ${r.vehicleQty}, ${r.tenureMonths}, ${r.annualMileageCapKm !== null ? r.annualMileageCapKm : 'NULL'}, ${esc(r.services)}, ${r.expectedCloseDate ? `'${r.expectedCloseDate}'` : 'NULL'}, '${r.ownerUserId}', ${esc(r.lostReason)}, ${esc(r.notes)}, ${r.quotationId ? `'${r.quotationId}'` : 'NULL'}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertRfqStageHistories(histories) {
  if (histories.length === 0) return '';
  let sql = `-- =============================================================================
-- RFQ STAGE HISTORIES (${histories.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const h of histories) {
    sql += `INSERT INTO RfqStageHistories (Id, RfqId, FromStage, ToStage, ChangedByUserId, Comment, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${h.id}', '${h.rfqId}', ${h.fromStage !== null ? h.fromStage : 'NULL'}, ${h.toStage}, '${h.changedByUserId}', ${esc(h.comment)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertApprovalTiers(tiers) {
  let sql = `-- =============================================================================
-- APPROVAL TIERS (${tiers.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const t of tiers) {
    sql += `INSERT INTO ApprovalTiers (Id, TierLevel, RequiredRoleCode, MinAmountSar, IsActive, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${t.id}', ${t.tierLevel}, ${esc(t.requiredRoleCode)}, ${t.minAmountSar}, ${bit(t.isActive)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertLeases(leases) {
  let sql = `-- =============================================================================
-- LEASES (${leases.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const l of leases) {
    sql += `INSERT INTO Leases (Id, ContractId, CustomerId, VehicleId, PrimaryDriverId, RentPolicyId, ExtendedCoverageId, WorkingBranchId, ReceiveBranchId, ReturnBranchId, TajeerContractNumber, Status, ContractStartUtc, ContractEndUtc, ContractTypeCode, AllowedKmPerDay, AllowedKmPerHour, AllowedLateHours, UnlimitedKm, RentAmount, PaidAmount, RemainingAmount, TotalAmount, VatAmount, PaymentMethodCode, ExtensionCount, PiiOptedOut, TenantId, CreatedAtUtc, UpdatedAtUtc, SavedAtUtc)
VALUES ('${l.id}', ${l.contractId ? `'${l.contractId}'` : 'NULL'}, '${l.customerId}', '${l.vehicleId}', '${l.primaryDriverId}', '${l.rentPolicyId}', ${l.extendedCoverageId ? `'${l.extendedCoverageId}'` : 'NULL'}, '${l.workingBranchId}', '${l.receiveBranchId}', '${l.returnBranchId}', ${l.tajeerContractNumber}, ${l.status}, '${l.contractStartUtc}', '${l.contractEndUtc}', ${l.contractTypeCode}, ${l.allowedKmPerDay}, ${l.allowedKmPerHour}, ${l.allowedLateHours}, ${bit(l.unlimitedKm)}, ${l.rentAmount}, ${l.paidAmount}, ${l.remainingAmount}, ${l.totalAmount}, ${l.vatAmount}, ${l.paymentMethodCode}, ${l.extensionCount}, ${bit(l.piiOptedOut)}, @TenantId, @Now, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertContracts(contracts) {
  let sql = `-- =============================================================================
-- CONTRACTS (${contracts.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const c of contracts) {
    const discPct = 0;
    const discAmt = Math.round(c.monthlyRentSar * discPct / 100 * 100) / 100;
    const netAmt = c.monthlyRentSar - discAmt;
    const vatPct = 15;
    const vatAmt = Math.round(netAmt * vatPct / 100 * 100) / 100;
    const totalAmt = Math.round((netAmt + vatAmt) * 100) / 100;
    const monthlyRent = c.durationMonths > 0 ? Math.round(totalAmt / c.durationMonths * 100) / 100 : totalAmt;
    sql += `INSERT INTO Contracts (Id, ContractNumber, CustomerId, QuotationId, Status, ContractTypeCode, StartDate, EndDate, DurationMonths, TotalVehicles, CheckedOutVehicles, BaseAmountSar, DiscountPercent, DiscountAmountSar, NetAmountSar, VatPercent, VatAmountSar, TotalAmountSar, MonthlyRentSar, TotalContractValueSar, PaymentTermsDays, Notes, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${c.id}', ${esc(c.contractNumber)}, '${c.customerId}', ${c.quotationId ? `'${c.quotationId}'` : 'NULL'}, ${esc(c.status === 2 ? 'Active' : c.status === 3 ? 'Suspended' : c.status === 4 ? 'Closed' : 'Draft')}, ${c.contractTypeCode}, '${c.startDate}', '${c.endDate}', ${c.durationMonths}, ${c.totalVehicles}, 0, ${c.monthlyRentSar}, ${discPct}, ${discAmt}, ${netAmt}, ${vatPct}, ${vatAmt}, ${totalAmt}, ${monthlyRent}, ${totalAmt}, ${c.paymentTermsDays}, ${esc(c.notes)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertContractLines(lines) {
  if (lines.length === 0) return '';
  let sql = `-- =============================================================================
-- CONTRACT LINES (${lines.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const l of lines) {
    sql += `INSERT INTO ContractLines (Id, ContractId, Make, Model, Year, Description, Quantity, UnitPriceSar, LineTotalSar, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${l.id}', '${l.contractId}', ${esc(l.make)}, ${esc(l.model)}, ${l.year}, ${esc(l.description)}, ${l.quantity}, ${l.unitPriceSar}, ${l.lineTotalSar}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertQuotations(quotations) {
  let sql = `-- =============================================================================
-- QUOTATIONS (${quotations.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const q of quotations) {
    sql += `INSERT INTO Quotations (Id, QuoteNumber, CustomerId, AccountManagerId, Status, QuoteDate, ValidUntilDate, ContractType, EstimatedDurationMonths, TermsAndConditionsMd, SubTotalSar, DiscountPercent, VatSar, TotalSar, SubmittedAtUtc, ApprovedAtUtc, SentAtUtc, AcceptedAtUtc, ClosedAtUtc, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${q.id}', ${esc(q.quoteNumber)}, '${q.customerId}', '${q.accountManagerId}', ${esc(q.status)}, '${q.quoteDate}', '${q.validUntilDate}', ${esc(q.contractType)}, ${q.estimatedDurationMonths}, ${esc(q.termsAndConditionsMd)}, ${q.subTotalSar}, ${q.discountPercent}, ${q.vatSar}, ${q.totalSar}, ${q.submittedAtUtc ? `'${q.submittedAtUtc}'` : 'NULL'}, ${q.approvedAtUtc ? `'${q.approvedAtUtc}'` : 'NULL'}, ${q.sentAtUtc ? `'${q.sentAtUtc}'` : 'NULL'}, ${q.acceptedAtUtc ? `'${q.acceptedAtUtc}'` : 'NULL'}, ${q.closedAtUtc ? `'${q.closedAtUtc}'` : 'NULL'}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertQuotationLines(lines) {
  let sql = `-- =============================================================================
-- QUOTATION LINES (${lines.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const l of lines) {
    sql += `INSERT INTO QuotationLines (Id, QuotationId, LineNumber, ItemType, Description, VehicleSpecRef, Quantity, UnitPriceSar, DiscountPercent, LineTotalSar, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${l.id}', '${l.quotationId}', ${l.lineNumber}, ${esc(l.itemType)}, ${esc(l.description)}, ${esc(l.vehicleSpecRef)}, ${l.quantity}, ${l.unitPriceSar}, ${l.discountPercent}, ${l.lineTotalSar}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertQuotationApprovals(approvals) {
  let sql = `-- =============================================================================
-- QUOTATION APPROVALS (${approvals.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const a of approvals) {
    sql += `INSERT INTO QuotationApprovals (Id, QuotationId, TierLevel, RequiredRoleCode, AssignedUserId, Status, DecisionAtUtc, DecidedByUserId, Comment, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${a.id}', '${a.quotationId}', ${a.tierLevel}, ${esc(a.requiredRoleCode)}, ${a.assignedUserId ? `'${a.assignedUserId}'` : 'NULL'}, ${esc(a.status)}, ${a.decisionAtUtc ? `'${a.decisionAtUtc}'` : 'NULL'}, ${a.decidedByUserId ? `'${a.decidedByUserId}'` : 'NULL'}, ${esc(a.comment)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertInvoices(invoices) {
  let sql = `-- =============================================================================
-- INVOICES (${invoices.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const inv of invoices) {
    sql += `INSERT INTO Invoices (Id, InvoiceNumber, LeaseId, CustomerId, Status, IssueDateUtc, DueDateUtc, BaseAmountSar, VatSar, TotalSar, SubmissionAttempts, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${inv.id}', ${esc(inv.invoiceNumber)}, '${inv.leaseId}', '${inv.customerId}', ${inv.status}, '${inv.issueDateUtc}', '${inv.dueDateUtc}', ${inv.baseAmountSar}, ${inv.vatSar}, ${inv.totalSar}, ${inv.submissionAttempts}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertPayments(payments) {
  let sql = `-- =============================================================================
-- ADVANCE PAYMENTS (${payments.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const p of payments) {
    sql += `INSERT INTO AdvancePayments (Id, CustomerId, Amount, PaymentMethod, ReceivedDate, ReferenceNumber, Notes, RemainingBalance, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${p.id}', '${p.customerId}', ${p.amount}, ${esc(p.paymentMethod)}, '${p.receivedDate}', ${esc(p.referenceNumber)}, ${esc(p.notes)}, ${p.remainingBalance}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertPaymentAllocations(allocations) {
  if (allocations.length === 0) return '';
  let sql = `-- =============================================================================
-- PAYMENT ALLOCATIONS (${allocations.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const a of allocations) {
    sql += `INSERT INTO PaymentAllocations (Id, AdvancePaymentId, InvoiceId, InvoiceNumber, AllocatedAmountSar, AllocatedAtUtc, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${a.id}', '${a.advancePaymentId}', '${a.invoiceId}', ${esc(a.invoiceNumber)}, ${a.allocatedAmountSar}, @Now, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertPricingVersions(versions) {
  let sql = `-- =============================================================================
-- PRICING VERSIONS (${versions.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const v of versions) {
    sql += `INSERT INTO PricingVersions (Id, Name, Status, EffectiveFromUtc, EffectiveToUtc, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${v.id}', ${esc(v.name)}, ${esc(v.status)}, '${v.effectiveFrom}', ${v.effectiveTo ? `'${v.effectiveTo}'` : 'NULL'}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertPricingDiscountPolicies(policies) {
  let sql = `-- =============================================================================
-- PRICING DISCOUNT POLICIES (${policies.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const p of policies) {
    sql += `INSERT INTO PricingDiscountPolicies (Id, MaxDiscountPercent, AllowedPresetsCsv, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${p.id}', ${p.maxDiscountPercent}, ${esc(p.allowedPresetsCsv)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

function insertPricingFormulas(formulas) {
  let sql = `-- =============================================================================
-- PRICING FORMULA DEFINITIONS (${formulas.length} rows)
-- =============================================================================
${batchHeader()}
`;
  for (const f of formulas) {
    sql += `INSERT INTO PricingFormulaDefinitions (Id, Code, Expression, OutputField, Precision, RoundingMode, IsActive, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES ('${f.id}', ${esc(f.code)}, ${esc(f.expr)}, ${esc(f.output)}, ${f.precision}, ${esc(f.rounding)}, ${bit(f.isActive)}, @TenantId, @Now, @Now);
`;
  }
  sql += '\nGO\n\n';
  return sql;
}

// ─── Main ─────────────────────────────────────────────────────────────────────

function main() {
  console.log('Generating seed data...');

  const branches = generateBranches();
  const customers = generateCustomers();
  const vehicles = generateVehicles(branches);
  const drivers = generateDrivers(customers);
  const rentPolicies = generateRentPolicies();
  const extCoverages = generateExtendedCoverages();
  const approvalTiers = generateApprovalTiers();
  const leases = generateLeases(customers, vehicles, drivers, rentPolicies, extCoverages, branches);
  const quotations = generateQuotations(customers);
  const quotationLines = generateQuotationLines(quotations);
  const quotationApprovals = generateQuotationApprovals(quotations);
  const { contracts, contractLines } = generateContracts(quotations, quotationLines, leases);
  const invoices = generateInvoices(leases);
  const { rfqs, rfqHistories } = generateRfqs(customers);
  const pricingVersions = generatePricingVersions();
  const pricingDiscountPolicies = generatePricingDiscountPolicies();
  const pricingFormulas = generatePricingFormulas();

  const accounts = generateAccounts(customers);

  let sql = sqlHeader();
  sql += insertBranches(branches);
  sql += insertCustomers(customers);
  sql += insertAccounts(accounts);
  sql += insertVehicles(vehicles);
  sql += insertDrivers(drivers);
  sql += insertRentPolicies(rentPolicies);
  sql += insertExtendedCoverages(extCoverages);
  sql += insertApprovalTiers(approvalTiers);
  sql += insertContracts(contracts);
  sql += insertContractLines(contractLines);
  sql += insertLeases(leases);
  sql += insertQuotations(quotations);
  sql += insertQuotationLines(quotationLines);
  sql += insertQuotationApprovals(quotationApprovals);
  sql += insertInvoices(invoices);
  sql += insertRfqs(rfqs);
  sql += insertRfqStageHistories(rfqHistories);
  const { payments, allocations: payAllocations } = generatePayments(customers, invoices);
  sql += insertPayments(payments);
  sql += insertPaymentAllocations(payAllocations);
  sql += insertPricingVersions(pricingVersions);
  sql += insertPricingDiscountPolicies(pricingDiscountPolicies);
  sql += insertPricingFormulas(pricingFormulas);

  sql += `-- =============================================================================
-- SEED COMPLETE
-- =============================================================================
PRINT 'Seed data inserted successfully.';
GO
`;

  const outputPath = path.join(__dirname, 'full-seed.sql');
  fs.writeFileSync(outputPath, sql, 'utf8');

  console.log(`Done! Output: ${outputPath}`);
  console.log(`  Branches:          ${branches.length}`);
  console.log(`  Customers:         ${customers.length} (B2B only)`);
  console.log(`  Accounts:          ${accounts.length}`);
  console.log(`  Vehicles:          ${vehicles.length}`);
  console.log(`  Drivers:           ${drivers.length}`);
  console.log(`  RentPolicies:      ${rentPolicies.length}`);
  console.log(`  ExtendedCoverages: ${extCoverages.length}`);
  console.log(`  ApprovalTiers:     ${approvalTiers.length}`);
  console.log(`  Contracts:         ${contracts.length}`);
  console.log(`  ContractLines:     ${contractLines.length}`);
  console.log(`  Leases:            ${leases.length} (${leases.filter(l => l.contractId).length} linked to contracts)`);
  console.log(`  Quotations:        ${quotations.length}`);
  console.log(`  QuotationLines:    ${quotationLines.length}`);
  console.log(`  QuotationApprovals:${quotationApprovals.length}`);
  console.log(`  Invoices:          ${invoices.length}`);
  console.log(`  RFQs:              ${rfqs.length}`);
  console.log(`  RFQ Histories:     ${rfqHistories.length}`);
  console.log(`  Payments:          ${payments.length}`);
  console.log(`  PaymentAllocations:${payAllocations.length}`);
  console.log(`  PricingVersions:   ${pricingVersions.length}`);
  console.log(`  PricingDiscounts:  ${pricingDiscountPolicies.length}`);
  console.log(`  PricingFormulas:   ${pricingFormulas.length}`);
}

main();
