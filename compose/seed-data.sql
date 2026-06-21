USE AutoLeaseNet;
GO

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

-- ═══════════════════════════════════════════════════════════════════════════════
-- 1. BRANCHES (KSA cities — Auto Lead leasing company locations)
-- ═══════════════════════════════════════════════════════════════════════════════

INSERT INTO Branches (Id, Code, NameEn, NameAr, CityEn, CityAr, RegionEn, RegionAr, LicenseNumber, Address, Latitude, Longitude, PhoneNumber, TajeerBranchId, TajeerOperatorId, IsActive, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES
('11111111-0001-0000-0000-000000000001','RYD-HQ',  'Riyadh Head Office',       N'المكتب الرئيسي - الرياض', 'Riyadh',  N'الرياض',  'Central', N'الوسطى',  '4031001234','King Fahd Road, Al Olaya District',      24.7136, 46.6753, '+966112345678', 1, 100001, 1, @TenantId, @Now, @Now),
('11111111-0001-0000-0000-000000000002','RYD-NTH', 'Riyadh North',             N'الرياض - الشمال',          'Riyadh',  N'الرياض',  'Central', N'الوسطى',  '4031001235','Exit 5, Al Nakheel District',             24.7800, 46.6900, '+966112345679', 2, 100001, 1, @TenantId, @Now, @Now),
('11111111-0001-0000-0000-000000000003','JED-01',  'Jeddah Corniche',          N'جدة - الكورنيش',          'Jeddah',  N'جدة',     'Western', N'الغربية', '4032002345','Madinah Road, Al Salamah District',       21.5433, 39.1728, '+966126789012', 3, 100001, 1, @TenantId, @Now, @Now),
('11111111-0001-0000-0000-000000000004','DMM-01',  'Dammam Industrial',        N'الدمام - الصناعية',        'Dammam',  N'الدمام',  'Eastern', N'الشرقية', '4033003456','King Saud Street, Industrial Area',      26.3927, 49.9777, '+966138901234', 4, 100001, 1, @TenantId, @Now, @Now),
('11111111-0001-0000-0000-000000000005','MED-01',  'Madinah Airport',          N'المدينة - المطار',         'Madinah', N'المدينة المنورة','Western', N'الغربية', '4034004567','Prince Mohammad Airport Rd',              24.4672, 39.7051, '+966148012345', 5, 100001, 1, @TenantId, @Now, @Now),
('11111111-0001-0000-0000-000000000006','KHB-01',  'Khobar Corniche',          N'الخبر - الكورنيش',         'Khobar',  N'الخبر',   'Eastern', N'الشرقية', '4035005678','Prince Turki Street',                    26.2799, 50.2083, '+966139123456', 6, 100001, 1, @TenantId, @Now, @Now);

PRINT 'Branches: 6 rows';
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2. RENT POLICIES (standard fleet leasing tiers)
-- ═══════════════════════════════════════════════════════════════════════════════

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO RentPolicies (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, BaseDailyRate, BaseHourlyRate, AllowedKmPerDay, AllowedKmPerHour, UnlimitedKm, LateHourFee, ExtraKmFee, MinRentalDays, MaxRentalDays, SecurityDeposit, TajeerRentPolicyId, IsActive, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES
('22222222-0001-0000-0000-000000000001','ECO-STD',  'Economy Standard',          N'اقتصادي عادي',           'Economy class, standard mileage',       N'الفئة الاقتصادية - مسافة عادية',     150.00, 25.00,  200, 30, 0, 50.00,  0.75, 1, 365, 1500.00, 101, 1, @TenantId, @Now, @Now),
('22222222-0001-0000-0000-000000000002','ECO-UNL',  'Economy Unlimited',         N'اقتصادي بلا حدود',       'Economy class, unlimited km',           N'الفئة الاقتصادية - كيلومترات غير محدودة', 185.00, 30.00, 0, 0, 1, 50.00, 0.00, 1, 365, 1500.00, 102, 1, @TenantId, @Now, @Now),
('22222222-0001-0000-0000-000000000003','MID-STD',  'Midsize Standard',          N'متوسط عادي',             'Midsize sedan, standard mileage',       N'سيدان متوسطة - مسافة عادية',         220.00, 35.00,  250, 35, 0, 75.00,  1.00, 1, 365, 2000.00, 103, 1, @TenantId, @Now, @Now),
('22222222-0001-0000-0000-000000000004','SUV-STD',  'SUV Standard',              N'دفع رباعي عادي',         'SUV class, standard mileage',           N'الفئة الرباعية - مسافة عادية',       320.00, 50.00,  200, 30, 0, 100.00, 1.25, 1, 365, 3000.00, 104, 1, @TenantId, @Now, @Now),
('22222222-0001-0000-0000-000000000005','SUV-PREM', 'SUV Premium',               N'دفع رباعي متميز',       'Premium SUV, high mileage',             N'الفئة الرباعية المتميزة - مسافة عالية', 450.00, 70.00, 300, 40, 0, 125.00, 1.50, 1, 365, 5000.00, 105, 1, @TenantId, @Now, @Now),
('22222222-0001-0000-0000-000000000006','PICK-STD', 'Pickup Standard',           N'بيك أب عادي',            'Pickup truck, utility mileage',         N'بيك أب - مسافة خدمية',               280.00, 45.00,  250, 35, 0, 85.00,  1.10, 1, 365, 2500.00, 106, 1, @TenantId, @Now, @Now),
('22222222-0001-0000-0000-000000000007','LUX-01',   'Luxury Sedan',              N'سيدان فاخرة',            'Luxury sedan, chauffeur quality',       N'سيدان فاخرة - جودة السائق الخاص',    600.00, 95.00,  200, 25, 0, 150.00, 2.00, 1, 180, 7500.00, 107, 1, @TenantId, @Now, @Now),
('22222222-0001-0000-0000-000000000008','CORP-UNL', 'Corporate Unlimited',       N'شركات بلا حدود',         'Corporate fleet, unlimited km, long term', N'أسطول الشركات - طويل الأجل',       200.00, NULL,   0, 0, 1, 60.00,  0.00, 30, 1095, 0.00, 108, 1, @TenantId, @Now, @Now);

PRINT 'RentPolicies: 8 rows';
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- 3. EXTENDED COVERAGES (insurance/protection add-ons)
-- ═══════════════════════════════════════════════════════════════════════════════

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO ExtendedCoverages (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, CoverageType, DailyRate, DeductibleAmount, TajeerExtendedCoverageId, IsActive, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES
('33333333-0001-0000-0000-000000000001','CDW-STD', 'Collision Damage Waiver',      N'إعفاء ضرر التصادم',       'Standard CDW covers body damage up to 50,000 SAR',  N'يغطي أضرار الهيكل حتى 50,000 ريال',     1, 35.00, 2500.00, 201, 1, @TenantId, @Now, @Now),
('33333333-0001-0000-0000-000000000002','CDW-FUL', 'Full CDW Zero Excess',         N'إعفاء ضرر كامل بدون تحمل', 'Zero deductible CDW',                                N'تغطية كاملة بدون مبلغ تحمل',             1, 65.00, 0.00,    202, 1, @TenantId, @Now, @Now),
('33333333-0001-0000-0000-000000000003','PAI-01',  'Personal Accident Insurance',  N'تأمين الحوادث الشخصية',   'Covers driver and passengers up to 100K SAR each',  N'يغطي السائق والركاب حتى 100 ألف ريال لكل', 2, 20.00, 0.00, 203, 1, @TenantId, @Now, @Now),
('33333333-0001-0000-0000-000000000004','TIRE-01', 'Tire & Windshield Protection', N'حماية الإطارات والزجاج',  'Covers tire puncture and windshield replacement',    N'يغطي ثقوب الإطارات واستبدال الزجاج الأمامي', 3, 15.00, 500.00, 204, 1, @TenantId, @Now, @Now),
('33333333-0001-0000-0000-000000000005','RSA-01',  'Roadside Assistance 24/7',     N'المساعدة على الطريق 24/7', '24/7 towing, flat tire, battery jump, fuel delivery', N'سحب، إطار، بطارية، توصيل وقود على مدار الساعة', 4, 10.00, 0.00, 205, 1, @TenantId, @Now, @Now);

PRINT 'ExtendedCoverages: 5 rows';
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- 4. CUSTOMERS (mix of B2B corporate + B2C individual — realistic KSA names)
-- ═══════════════════════════════════════════════════════════════════════════════

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO Customers (Id, Type, Status, DisplayName, DisplayNameAr, Email, Mobile, NationalAddress, PreferredLanguage, LegalName, LegalNameAr, CommercialRegistration, VatNumber, BillingAddress, CreditLimit, CreditCurrency, PersonNameEn, PersonNameAr, IdTypeCode, PersonIdNumber, DateOfBirth, NationalityCode, KycVerified, PiiOptedOut, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- B2B Corporate
('44444444-0001-0000-0000-000000000001', 1, 1, 'Saudi Aramco Fleet Services',   N'أرامكو السعودية - خدمات الأسطول',    'fleet@aramco.com',       '+966501234567', 'RRRD2929, Dhahran 31311', 1, 'Saudi Arabian Oil Company', N'شركة الزيت العربية السعودية', '2052001234', '300000000000003', 'P.O. Box 5000, Dhahran 31311', 5000000.00, 'SAR', NULL, NULL, NULL, NULL, NULL, 'SA', 1, 0, @TenantId, @Now, @Now),
('44444444-0001-0000-0000-000000000002', 1, 1, 'SABIC Transportation',          N'سابك - النقل',                       'transport@sabic.com',    '+966502345678', 'RRRD1234, Riyadh 12345',  1, 'Saudi Basic Industries Corp', N'الشركة السعودية للصناعات الأساسية', '1010012345', '300000000100003', 'P.O. Box 5101, Riyadh 11422', 3000000.00, 'SAR', NULL, NULL, NULL, NULL, NULL, 'SA', 1, 0, @TenantId, @Now, @Now),
('44444444-0001-0000-0000-000000000003', 1, 1, 'Al Rajhi Banking Group',        N'مجموعة الراجحي المصرفية',             'fleet@alrajhibank.com',  '+966503456789', 'RRRD5678, Riyadh 11411',  0, 'Al Rajhi Bank', N'مصرف الراجحي', '1010054321', '300000000200003', 'P.O. Box 28, Riyadh 11411', 2000000.00, 'SAR', NULL, NULL, NULL, NULL, NULL, 'SA', 1, 0, @TenantId, @Now, @Now),
('44444444-0001-0000-0000-000000000004', 1, 1, 'Mobily Telecom',                N'موبايلي للاتصالات',                   'fleet@mobily.com.sa',    '+966504567890', 'RRRD3344, Riyadh 12214',  0, 'Etihad Etisalat Company', N'شركة اتحاد اتصالات', '1010200500', '300000000300003', 'P.O. Box 8888, Riyadh 12214', 1500000.00, 'SAR', NULL, NULL, NULL, NULL, NULL, 'SA', 1, 0, @TenantId, @Now, @Now),
('44444444-0001-0000-0000-000000000005', 1, 1, 'Red Sea Global',                N'البحر الأحمر العالمية',                'logistics@redseaglobal.com', '+966505678901', 'RRRD7788, Jeddah 21589', 1, 'Red Sea Global Co', N'شركة البحر الأحمر العالمية', '4030123456', '300000000400003', 'P.O. Box 1234, Jeddah 21589', 4000000.00, 'SAR', NULL, NULL, NULL, NULL, NULL, 'SA', 1, 0, @TenantId, @Now, @Now),
-- B2C Individual
('44444444-0001-0000-0000-000000000006', 0, 1, 'Mohammed Al-Harbi',             N'محمد الحربي',                         'mharbi@gmail.com',       '+966551234567', 'RRRD9911, Riyadh 12345',  0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'Mohammed Abdullah Al-Harbi', N'محمد عبدالله الحربي', 1, '1088765432', '1990-03-15', 'SA', 1, 0, @TenantId, @Now, @Now),
('44444444-0001-0000-0000-000000000007', 0, 1, 'Fahad Al-Otaibi',               N'فهد العتيبي',                         'fahad.otaibi@outlook.sa', '+966552345678', 'RRRD8822, Jeddah 21465', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'Fahad Saad Al-Otaibi', N'فهد سعد العتيبي', 1, '1092345678', '1985-07-22', 'SA', 1, 0, @TenantId, @Now, @Now),
('44444444-0001-0000-0000-000000000008', 0, 1, 'Noura Al-Shamrani',             N'نورة الشمراني',                       'noura.s@hotmail.com',    '+966553456789', 'RRRD7733, Dammam 32411',  0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'Noura Ibrahim Al-Shamrani', N'نورة ابراهيم الشمراني', 1, '1076543210', '1992-11-08', 'SA', 1, 0, @TenantId, @Now, @Now),
('44444444-0001-0000-0000-000000000009', 0, 1, 'Abdullah Al-Dosari',            N'عبدالله الدوسري',                     'adosari@yahoo.com',      '+966554567890', 'RRRD6644, Khobar 31952',  0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'Abdullah Khalid Al-Dosari', N'عبدالله خالد الدوسري', 1, '1064321098', '1988-01-30', 'SA', 1, 0, @TenantId, @Now, @Now),
('44444444-0001-0000-0000-000000000010', 0, 1, 'Sara Al-Ghamdi',                N'سارة الغامدي',                        'sara.gh@gmail.com',      '+966555678901', 'RRRD5555, Madinah 42312', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'Sara Ahmed Al-Ghamdi', N'سارة أحمد الغامدي', 1, '1055432109', '1995-05-20', 'SA', 1, 0, @TenantId, @Now, @Now);

PRINT 'Customers: 10 rows (5 B2B, 5 B2C)';
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- 5. VEHICLES (realistic KSA fleet — Toyota, Hyundai, Nissan, etc.)
-- ═══════════════════════════════════════════════════════════════════════════════

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();
DECLARE @RydHQ UNIQUEIDENTIFIER = '11111111-0001-0000-0000-000000000001';
DECLARE @RydNth UNIQUEIDENTIFIER = '11111111-0001-0000-0000-000000000002';
DECLARE @Jed UNIQUEIDENTIFIER = '11111111-0001-0000-0000-000000000003';
DECLARE @Dmm UNIQUEIDENTIFIER = '11111111-0001-0000-0000-000000000004';

INSERT INTO Vehicles (Id, Status, PlateNumber, PlateLetters, PlateTypeCode, Vin, Make, Model, ModelYear, Color, FuelType, TransmissionType, BodyType, Seats, LicenseExpiryDate, InsuranceExpiryDate, InsuranceCompany, InsurancePolicyNumber, OwnerBranchId, CurrentBranchId, CurrentKm, PurchasePrice, PurchaseDate, DepreciationPerMonth, CurrentBookValue, TelematicsProvider, DeviceImei, Notes, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- Toyota Camry fleet
('55555555-0001-0000-0000-000000000001', 0, '1234', N'أ ب ت', 1, 'JTDBR3FH5M1234001', 'Toyota','Camry',     2025, 'White',  1, 1, 1, 5, '2027-03-15', '2026-12-31', 'Tawuniya',  'TWN-2025-001', @RydHQ, @RydHQ, 12500, 118000.00, '2024-09-01', 1180.00, 104140.00, 'Wialon', '860012345678901', NULL, @TenantId, @Now, @Now),
('55555555-0001-0000-0000-000000000002', 0, '1235', N'أ ب ت', 1, 'JTDBR3FH5M1234002', 'Toyota','Camry',     2025, 'Silver', 1, 1, 1, 5, '2027-03-15', '2026-12-31', 'Tawuniya',  'TWN-2025-002', @RydHQ, @RydHQ, 8200,  118000.00, '2024-11-01', 1180.00, 108460.00, 'Wialon', '860012345678902', NULL, @TenantId, @Now, @Now),
('55555555-0001-0000-0000-000000000003', 0, '2345', N'ب ج د', 1, 'JTDBR3FH5M1234003', 'Toyota','Camry',     2024, 'Black',  1, 1, 1, 5, '2026-08-20', '2026-08-20', 'Bupa Arabia','BUP-2024-101', @Jed,   @Jed,   31000, 112000.00, '2023-12-15', 1120.00, 91840.00,  'Wialon', '860012345678903', NULL, @TenantId, @Now, @Now),
-- Toyota Corolla fleet
('55555555-0001-0000-0000-000000000004', 0, '3456', N'ج د ه', 1, 'JTDKN3DU5A5678001', 'Toyota','Corolla',   2025, 'White',  1, 0, 1, 5, '2027-05-01', '2027-01-15', 'Tawuniya',  'TWN-2025-003', @RydNth, @RydNth, 5600, 86000.00, '2025-01-10', 860.00,  81580.00, 'Wialon', '860012345678904', NULL, @TenantId, @Now, @Now),
('55555555-0001-0000-0000-000000000005', 0, '3457', N'ج د ه', 1, 'JTDKN3DU5A5678002', 'Toyota','Corolla',   2025, 'Grey',   1, 0, 1, 5, '2027-05-01', '2027-01-15', 'Tawuniya',  'TWN-2025-004', @Dmm,   @Dmm,   3200, 86000.00, '2025-02-01', 860.00,  82560.00, 'Wialon', '860012345678905', NULL, @TenantId, @Now, @Now),
-- Hyundai Sonata
('55555555-0001-0000-0000-000000000006', 0, '4567', N'د ه و', 1, 'KMHEC4DC0PA123001', 'Hyundai','Sonata',    2025, 'Blue',   1, 1, 1, 5, '2027-04-10', '2026-12-31', 'ACIG',      'ACG-2025-001', @RydHQ, @RydHQ, 9800, 102000.00, '2024-10-15', 1020.00, 93840.00, 'Wialon', '860012345678906', NULL, @TenantId, @Now, @Now),
('55555555-0001-0000-0000-000000000007', 0, '4568', N'د ه و', 1, 'KMHEC4DC0PA123002', 'Hyundai','Sonata',    2024, 'White',  1, 1, 1, 5, '2026-09-30', '2026-09-30', 'ACIG',      'ACG-2024-005', @Jed,   @Jed,   27500, 98000.00, '2023-11-01', 980.00,  79380.00, 'Wialon', '860012345678907', NULL, @TenantId, @Now, @Now),
-- Hyundai Tucson
('55555555-0001-0000-0000-000000000008', 0, '5678', N'ه و ز', 1, 'KM8J33ALXPU456001', 'Hyundai','Tucson',    2025, 'Grey',   1, 1, 2, 5, '2027-06-01', '2027-01-31', 'Malath',    'MLT-2025-001', @RydNth, @RydNth, 7100, 112000.00, '2025-01-20', 1120.00, 106400.00, 'Wialon', '860012345678908', NULL, @TenantId, @Now, @Now),
-- Nissan Patrol
('55555555-0001-0000-0000-000000000009', 0, '6789', N'و ز ح', 1, 'JN1TBNT30Z0789001', 'Nissan','Patrol',    2025, 'Pearl',  1, 1, 2, 7, '2027-02-28', '2026-12-31', 'Tawuniya',  'TWN-2025-005', @RydHQ, @RydHQ, 15200, 248000.00, '2024-08-01', 2480.00, 222320.00, 'Wialon', '860012345678909', NULL, @TenantId, @Now, @Now),
('55555555-0001-0000-0000-000000000010', 0, '6790', N'و ز ح', 1, 'JN1TBNT30Z0789002', 'Nissan','Patrol',    2024, 'Black',  1, 1, 2, 7, '2026-07-15', '2026-07-15', 'Bupa Arabia','BUP-2024-201', @Dmm,  @Dmm,   42000, 240000.00, '2023-06-01', 2400.00, 167400.00, 'Wialon', '860012345678910', NULL, @TenantId, @Now, @Now),
-- Toyota Hilux
('55555555-0001-0000-0000-000000000011', 0, '7890', N'ز ح ط', 1, 'MROFR22G8M1012001', 'Toyota','Hilux',     2025, 'White',  2, 1, 3, 5, '2027-04-30', '2027-02-28', 'Tawuniya',  'TWN-2025-006', @Dmm,  @Dmm,   4500, 138000.00, '2025-03-01', 1380.00, 133860.00, 'Wialon', '860012345678911', NULL, @TenantId, @Now, @Now),
('55555555-0001-0000-0000-000000000012', 0, '7891', N'ز ح ط', 1, 'MROFR22G8M1012002', 'Toyota','Hilux',     2024, 'Silver', 2, 1, 3, 5, '2026-10-31', '2026-10-31', 'Malath',    'MLT-2024-003', @Jed,  @Jed,   28000, 132000.00, '2024-01-15', 1320.00, 108240.00, 'Wialon', '860012345678912', NULL, @TenantId, @Now, @Now),
-- Kia K5
('55555555-0001-0000-0000-000000000013', 0, '8901', N'ح ط ي', 1, 'KNAGM4A79P5234001', 'Kia',   'K5',       2025, 'Red',    1, 1, 1, 5, '2027-07-15', '2027-03-31', 'ACIG',      'ACG-2025-010', @RydHQ, @RydHQ, 2100, 95000.00, '2025-04-01', 950.00, 92150.00, 'Wialon', '860012345678913', NULL, @TenantId, @Now, @Now),
-- Chevrolet Tahoe
('55555555-0001-0000-0000-000000000014', 0, '9012', N'ط ي ك', 1, '1GNSKRKD0PR345001', 'Chevrolet','Tahoe',  2025, 'Black',  1, 1, 2, 8, '2027-08-01', '2027-04-30', 'Tawuniya',  'TWN-2025-007', @RydHQ, @RydHQ, 6800, 285000.00, '2024-12-01', 2850.00, 267900.00, 'Wialon', '860012345678914', NULL, @TenantId, @Now, @Now),
-- GMC Yukon
('55555555-0001-0000-0000-000000000015', 0, '9013', N'ط ي ك', 1, '1GKS2HKJ8PR456001', 'GMC',   'Yukon',    2024, 'White',  1, 1, 2, 8, '2026-11-30', '2026-11-30', 'Bupa Arabia','BUP-2024-301', @RydNth, @RydNth, 35000, 310000.00, '2023-09-15', 3100.00, 241800.00, 'Wialon', '860012345678915', NULL, @TenantId, @Now, @Now);

PRINT 'Vehicles: 15 rows';
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- 6. DRIVERS (linked to customers — realistic KSA license data)
-- ═══════════════════════════════════════════════════════════════════════════════

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO Drivers (Id, Status, CustomerId, PersonNameEn, PersonNameAr, IdTypeCode, PersonIdNumber, DateOfBirth, NationalityCode, DriverLicenseNumber, LicenseClass, LicenseExpiryDate, Mobile, Email, TammAuthorizationStatus, DefensiveDrivingCertHeld, AccidentCountLast3Yrs, PiiOptedOut, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- Aramco drivers
('66666666-0001-0000-0000-000000000001', 0, '44444444-0001-0000-0000-000000000001', 'Saleh Mohammed Al-Qahtani', N'صالح محمد القحطاني', 1, '1077654321', '1987-04-12', 'SA', '2511234567', 3, '2028-06-30', '+966561234567', NULL, 0, 1, 0, 0, @TenantId, @Now, @Now),
('66666666-0001-0000-0000-000000000002', 0, '44444444-0001-0000-0000-000000000001', 'Yousef Abdullah Al-Mutairi', N'يوسف عبدالله المطيري', 1, '1089876543', '1991-09-25', 'SA', '2511234568', 3, '2027-12-15', '+966562345678', NULL, 0, 0, 0, 0, @TenantId, @Now, @Now),
-- SABIC drivers
('66666666-0001-0000-0000-000000000003', 0, '44444444-0001-0000-0000-000000000002', 'Khalid Ibrahim Al-Shehri',  N'خالد ابراهيم الشهري', 1, '1066789012', '1983-02-18', 'SA', '2511234569', 3, '2028-03-20', '+966563456789', NULL, 0, 1, 1, 0, @TenantId, @Now, @Now),
-- Individual customers as their own drivers
('66666666-0001-0000-0000-000000000004', 0, '44444444-0001-0000-0000-000000000006', 'Mohammed Abdullah Al-Harbi', N'محمد عبدالله الحربي', 1, '1088765432', '1990-03-15', 'SA', '2511234570', 3, '2027-09-10', '+966551234567', 'mharbi@gmail.com', 0, 0, 0, 0, @TenantId, @Now, @Now),
('66666666-0001-0000-0000-000000000005', 0, '44444444-0001-0000-0000-000000000007', 'Fahad Saad Al-Otaibi',      N'فهد سعد العتيبي',    1, '1092345678', '1985-07-22', 'SA', '2511234571', 3, '2028-01-05', '+966552345678', 'fahad.otaibi@outlook.sa', 0, 1, 0, 0, @TenantId, @Now, @Now),
('66666666-0001-0000-0000-000000000006', 0, '44444444-0001-0000-0000-000000000008', 'Noura Ibrahim Al-Shamrani', N'نورة ابراهيم الشمراني', 1, '1076543210', '1992-11-08', 'SA', '2511234572', 1, '2027-05-20', '+966553456789', 'noura.s@hotmail.com', 0, 0, 0, 0, @TenantId, @Now, @Now),
('66666666-0001-0000-0000-000000000007', 0, '44444444-0001-0000-0000-000000000009', 'Abdullah Khalid Al-Dosari', N'عبدالله خالد الدوسري', 1, '1064321098', '1988-01-30', 'SA', '2511234573', 3, '2027-11-18', '+966554567890', 'adosari@yahoo.com', 0, 0, 1, 0, @TenantId, @Now, @Now),
('66666666-0001-0000-0000-000000000008', 0, '44444444-0001-0000-0000-000000000010', 'Sara Ahmed Al-Ghamdi',      N'سارة أحمد الغامدي',   1, '1055432109', '1995-05-20', 'SA', '2511234574', 1, '2028-02-28', '+966555678901', 'sara.gh@gmail.com', 0, 0, 0, 0, @TenantId, @Now, @Now),
-- Extra corporate drivers
('66666666-0001-0000-0000-000000000009', 0, '44444444-0001-0000-0000-000000000003', 'Ahmed Hassan Al-Zahrani',   N'أحمد حسن الزهراني',  1, '1045678901', '1980-06-10', 'SA', '2511234575', 3, '2027-08-22', '+966567890123', NULL, 0, 1, 0, 0, @TenantId, @Now, @Now),
('66666666-0001-0000-0000-000000000010', 0, '44444444-0001-0000-0000-000000000004', 'Omar Nasser Al-Subaie',     N'عمر ناصر السبيعي',   1, '1034567890', '1986-12-03', 'SA', '2511234576', 3, '2028-04-15', '+966568901234', NULL, 0, 0, 0, 0, @TenantId, @Now, @Now);

PRINT 'Drivers: 10 rows';
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- 7. APPROVAL TIERS (3-tier approval for quotations)
-- ═══════════════════════════════════════════════════════════════════════════════

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO ApprovalTiers (Id, TierLevel, RequiredRoleCode, MinAmountSar, IsActive, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES
('77777777-0001-0000-0000-000000000001', 1, 'SalesManager',    50000.00,  1, @TenantId, @Now, @Now),
('77777777-0001-0000-0000-000000000002', 2, 'RegionalDirector', 200000.00, 1, @TenantId, @Now, @Now),
('77777777-0001-0000-0000-000000000003', 3, 'CFO',             500000.00, 1, @TenantId, @Now, @Now);

PRINT 'ApprovalTiers: 3 rows';
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- 8. PRICING VERSIONS (current active version + historical)
-- ═══════════════════════════════════════════════════════════════════════════════

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO PricingVersions (Id, Name, Status, EffectiveFromUtc, EffectiveToUtc, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES
('88888888-0001-0000-0000-000000000001', 'H1 2025 Launch Pricing', 'Expired', '2025-01-01T00:00:00+03:00', '2025-06-30T23:59:59+03:00', @TenantId, @Now, @Now),
('88888888-0001-0000-0000-000000000002', 'H2 2025 Revised',        'Expired', '2025-07-01T00:00:00+03:00', '2025-12-31T23:59:59+03:00', @TenantId, @Now, @Now),
('88888888-0001-0000-0000-000000000003', '2026 Standard Pricing',  'Active',  '2026-01-01T00:00:00+03:00', NULL, @TenantId, @Now, @Now);

PRINT 'PricingVersions: 3 rows';
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- 9. PRICING DISCOUNT POLICIES
-- ═══════════════════════════════════════════════════════════════════════════════

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO PricingDiscountPolicies (Id, MaxDiscountPercent, AllowedPresetsCsv, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES
('99999999-0001-0000-0000-000000000001', 25.00, '5,10,15,20,25', @TenantId, @Now, @Now);

PRINT 'PricingDiscountPolicies: 1 row';
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- 10. PRICING FORMULA DEFINITIONS (waterfall components)
-- ═══════════════════════════════════════════════════════════════════════════════

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO PricingFormulaDefinitions (Id, Code, Expression, OutputField, Precision, RoundingMode, IsActive, TenantId, CreatedAtUtc, UpdatedAtUtc)
VALUES
('AAAAAAAA-0001-0000-0000-000000000001', 'TFV',            'AcquisitionCost + AdditionsCost + CapitalizedFees - DownPayment', 'TotalFinancedValue', 2, 'MidpointAwayFromZero', 1, @TenantId, @Now, @Now),
('AAAAAAAA-0001-0000-0000-000000000002', 'NET_FINANCED',   'TFV - ResidualValue - RvOnAdditions',                             'NetFinancedAmount',  2, 'MidpointAwayFromZero', 1, @TenantId, @Now, @Now),
('AAAAAAAA-0001-0000-0000-000000000003', 'INTEREST',       'BalanceBasis * AnnualRate / 12',                                  'InterestAmount',     4, 'MidpointAwayFromZero', 1, @TenantId, @Now, @Now),
('AAAAAAAA-0001-0000-0000-000000000004', 'INSURANCE',      'OpeningBalance * InsuranceAnnualRate / 12',                       'InsuranceAmount',    4, 'MidpointAwayFromZero', 1, @TenantId, @Now, @Now),
('AAAAAAAA-0001-0000-0000-000000000005', 'MAINTENANCE',    'CASE Strategy WHEN A THEN FixedAmount WHEN B THEN TFV * RatePercent END', 'MaintenanceAmount', 2, 'MidpointAwayFromZero', 1, @TenantId, @Now, @Now),
('AAAAAAAA-0001-0000-0000-000000000006', 'ADMIN_FEE',      'FeeValue BY Method AND Frequency',                                'AdminFeeAmount',     2, 'MidpointAwayFromZero', 1, @TenantId, @Now, @Now),
('AAAAAAAA-0001-0000-0000-000000000007', 'PROFIT_MARGIN',  'TFV * MarginPercent / TermMonths',                                'ProfitAmount',       2, 'MidpointAwayFromZero', 1, @TenantId, @Now, @Now),
('AAAAAAAA-0001-0000-0000-000000000008', 'REPLACEMENT',    'CASE Policy WHEN OPEN THEN TFV * ReplacementRate ELSE 0 END',     'ReplacementAmount',  2, 'MidpointAwayFromZero', 1, @TenantId, @Now, @Now),
('AAAAAAAA-0001-0000-0000-000000000009', 'COMMISSION',     'RatePreCommission * CommissionPercent',                            'CommissionAmount',   2, 'MidpointAwayFromZero', 1, @TenantId, @Now, @Now),
('AAAAAAAA-0001-0000-0000-000000000010', 'FINAL_RATE',     'RatePreCommission + CommissionAmount',                             'FinalMonthlyRate',   2, 'MidpointAwayFromZero', 1, @TenantId, @Now, @Now);

PRINT 'PricingFormulaDefinitions: 10 rows';
GO

-- ═══════════════════════════════════════════════════════════════════════════════
-- 11. LEASES (active contracts — mix of statuses)
-- ═══════════════════════════════════════════════════════════════════════════════

DECLARE @TenantId UNIQUEIDENTIFIER = 'a1a1a1a1-0001-0000-0000-000000000001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

INSERT INTO Leases (Id, CustomerId, VehicleId, PrimaryDriverId, RentPolicyId, ExtendedCoverageId, WorkingBranchId, ReceiveBranchId, ReturnBranchId, TajeerContractNumber, Status, ContractStartUtc, ContractEndUtc, ContractTypeCode, AllowedKmPerDay, AllowedKmPerHour, AllowedLateHours, UnlimitedKm, RentAmount, PaidAmount, RemainingAmount, TotalAmount, VatAmount, PaymentMethodCode, ExtensionCount, PiiOptedOut, TenantId, CreatedAtUtc, UpdatedAtUtc, SavedAtUtc)
VALUES
-- Aramco — 2 active leases
('BBBBBBBB-0001-0000-0000-000000000001', '44444444-0001-0000-0000-000000000001', '55555555-0001-0000-0000-000000000009', '66666666-0001-0000-0000-000000000001', '22222222-0001-0000-0000-000000000005', '33333333-0001-0000-0000-000000000002', '11111111-0001-0000-0000-000000000001', '11111111-0001-0000-0000-000000000001', '11111111-0001-0000-0000-000000000001', 5001001, 2, '2025-11-01T08:00:00+03:00', '2026-10-31T17:00:00+03:00', 1, 300, 40, 4, 0, 450.00, 81000.00, 81000.00, 162000.00, 24300.00, 1, 0, 0, @TenantId, @Now, @Now, @Now),
('BBBBBBBB-0001-0000-0000-000000000002', '44444444-0001-0000-0000-000000000001', '55555555-0001-0000-0000-000000000014', '66666666-0001-0000-0000-000000000002', '22222222-0001-0000-0000-000000000005', '33333333-0001-0000-0000-000000000002', '11111111-0001-0000-0000-000000000001', '11111111-0001-0000-0000-000000000001', '11111111-0001-0000-0000-000000000001', 5001002, 2, '2026-01-15T08:00:00+03:00', '2027-01-14T17:00:00+03:00', 1, 200, 25, 4, 0, 600.00, 54000.00, 162000.00, 216000.00, 32400.00, 1, 0, 0, @TenantId, @Now, @Now, @Now),
-- SABIC — 1 active
('BBBBBBBB-0001-0000-0000-000000000003', '44444444-0001-0000-0000-000000000002', '55555555-0001-0000-0000-000000000011', '66666666-0001-0000-0000-000000000003', '22222222-0001-0000-0000-000000000006', '33333333-0001-0000-0000-000000000001', '11111111-0001-0000-0000-000000000004', '11111111-0001-0000-0000-000000000004', '11111111-0001-0000-0000-000000000004', 5001003, 2, '2026-03-01T08:00:00+03:00', '2027-02-28T17:00:00+03:00', 1, 250, 35, 4, 0, 280.00, 25200.00, 75600.00, 100800.00, 15120.00, 1, 0, 0, @TenantId, @Now, @Now, @Now),
-- Individual B2C leases
('BBBBBBBB-0001-0000-0000-000000000004', '44444444-0001-0000-0000-000000000006', '55555555-0001-0000-0000-000000000001', '66666666-0001-0000-0000-000000000004', '22222222-0001-0000-0000-000000000003', '33333333-0001-0000-0000-000000000001', '11111111-0001-0000-0000-000000000001', '11111111-0001-0000-0000-000000000001', '11111111-0001-0000-0000-000000000001', 5001004, 2, '2026-04-01T08:00:00+03:00', '2026-09-30T17:00:00+03:00', 1, 250, 35, 2, 0, 220.00, 19800.00, 19800.00, 39600.00, 5940.00, 2, 0, 0, @TenantId, @Now, @Now, @Now),
('BBBBBBBB-0001-0000-0000-000000000005', '44444444-0001-0000-0000-000000000007', '55555555-0001-0000-0000-000000000006', '66666666-0001-0000-0000-000000000005', '22222222-0001-0000-0000-000000000003', '33333333-0001-0000-0000-000000000005', '11111111-0001-0000-0000-000000000003', '11111111-0001-0000-0000-000000000003', '11111111-0001-0000-0000-000000000003', 5001005, 2, '2026-05-15T08:00:00+03:00', '2027-05-14T17:00:00+03:00', 1, 250, 35, 2, 0, 220.00, 13200.00, 66000.00, 79200.00, 11880.00, 2, 0, 0, @TenantId, @Now, @Now, @Now);

PRINT 'Leases: 5 rows';
GO

PRINT '=== SEED COMPLETE ===';
GO
