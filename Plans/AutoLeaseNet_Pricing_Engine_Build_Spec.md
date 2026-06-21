# AutoLeaseNet — Vehicle Pricing Engine: Build Specification

> **Purpose of this document:** This is a complete, ready-to-use build prompt. Paste this entire file into Claude Code, Cursor, or any AI coding assistant to scaffold the database schema, pricing calculation engine, and setup screens described below. Sections marked **[ASSUMPTION]** are interpretive choices made to fill gaps in the original requirements — review and adjust before building.

---

## 0. Build Instruction (read this first, AI agent)

Build a vehicle leasing pricing module for an application called AutoLeaseNet. Implement:
1. The normalized relational schema in Section 1 (adapt syntax to whatever database the project already uses; PostgreSQL DDL shown as reference).
2. The pricing calculation engine described in Section 2, implemented as a service/module that runs the waterfall in the exact order given.
3. The setup/admin screens described in Section 4, one screen per master/lookup table, with the fields listed.
4. The income statement projection logic in Section 3.

Do not skip the rate-table versioning (`effective_from` / `effective_to` columns) — lease pricing is a financial system, and historical rates must remain queryable even after they change, so existing contracts' calculations stay reproducible.

---

## 1. Data Model (Normalized Schema)

The schema is split into **Setup/Master tables** (configured via setup screens, rarely change) and **Transactional tables** (created per lease contract, change constantly). All rate/fee tables carry `effective_from`/`effective_to` so historical pricing is never lost when rates are updated.

### 1.1 Setup / Master Tables

```sql
-- Vehicle Category (drives all rate lookups)
CREATE TABLE vehicle_categories (
    category_id SERIAL PRIMARY KEY,
    category_name VARCHAR(100) NOT NULL,
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE
);

-- Vehicle Master
CREATE TABLE vehicles (
    vehicle_id SERIAL PRIMARY KEY,
    vin VARCHAR(17) UNIQUE NOT NULL,
    make VARCHAR(50) NOT NULL,
    model VARCHAR(50) NOT NULL,
    variant VARCHAR(50),
    model_year INT NOT NULL,
    category_id INT REFERENCES vehicle_categories(category_id),
    list_price NUMERIC(12,2) NOT NULL,
    acquisition_cost NUMERIC(12,2) NOT NULL,
    acquisition_date DATE,
    useful_life_months INT NOT NULL,
    status VARCHAR(20) DEFAULT 'ACTIVE', -- ACTIVE, LEASED, RETIRED, REPLACED
    created_at TIMESTAMP DEFAULT now(),
    updated_at TIMESTAMP DEFAULT now()
);

-- Lease Term / Tenor Master
CREATE TABLE lease_terms (
    term_id SERIAL PRIMARY KEY,
    term_months INT NOT NULL UNIQUE,
    description VARCHAR(50)
);

-- Insurance Rate Table (lookup)
CREATE TABLE insurance_rate_table (
    insurance_rate_id SERIAL PRIMARY KEY,
    category_id INT REFERENCES vehicle_categories(category_id),
    min_value NUMERIC(12,2) NOT NULL,
    max_value NUMERIC(12,2) NOT NULL,
    annual_rate_pct NUMERIC(6,4) NOT NULL,   -- e.g. 0.0350 = 3.5%
    effective_from DATE NOT NULL,
    effective_to DATE,
    is_active BOOLEAN DEFAULT TRUE
);

-- Maintenance Rate Table (lookup, supports Strategy A/B)
CREATE TABLE maintenance_rate_table (
    maintenance_rate_id SERIAL PRIMARY KEY,
    category_id INT REFERENCES vehicle_categories(category_id),
    vehicle_age_band_start_months INT NOT NULL,
    vehicle_age_band_end_months INT NOT NULL,
    strategy VARCHAR(10) NOT NULL CHECK (strategy IN ('A','B')),
    rate_type VARCHAR(20) NOT NULL CHECK (rate_type IN ('FIXED_AMOUNT','PERCENT_OF_TFV')),
    rate_value NUMERIC(12,4) NOT NULL,
    effective_from DATE NOT NULL,
    effective_to DATE,
    is_active BOOLEAN DEFAULT TRUE
);

-- Interest Rate Table (supports Strategy A/B)
CREATE TABLE interest_rate_table (
    interest_rate_id SERIAL PRIMARY KEY,
    term_id INT REFERENCES lease_terms(term_id),
    strategy VARCHAR(10) NOT NULL CHECK (strategy IN ('A','B')),
        -- A = Flat: rate applied to original principal every period (constant)
        -- B = Reducing balance: rate applied to outstanding balance each period
    annual_rate_pct NUMERIC(6,4) NOT NULL,
    effective_from DATE NOT NULL,
    effective_to DATE,
    is_active BOOLEAN DEFAULT TRUE
);

-- Residual Value Table (lookup)
CREATE TABLE residual_value_table (
    rv_id SERIAL PRIMARY KEY,
    category_id INT REFERENCES vehicle_categories(category_id),
    term_id INT REFERENCES lease_terms(term_id),
    rv_percent NUMERIC(6,4) NOT NULL,   -- % of original vehicle value retained at term end
    effective_from DATE NOT NULL,
    effective_to DATE,
    is_active BOOLEAN DEFAULT TRUE
);

-- Replacement Policy Setup (Open / Permanent strategy)
CREATE TABLE replacement_policy (
    policy_id SERIAL PRIMARY KEY,
    policy_name VARCHAR(50) NOT NULL,
    strategy VARCHAR(10) NOT NULL CHECK (strategy IN ('OPEN','PERMANENT')),
        -- OPEN      = vehicle may be swapped during the term; carries a replacement premium
        -- PERMANENT = fixed vehicle for the full term; no premium
    replacement_rate_pct NUMERIC(6,4),    -- premium % of TFV, used only if OPEN
    max_replacements_per_term INT,
    is_active BOOLEAN DEFAULT TRUE
);

-- Fee Master (Admin, Registration & Fees, Card Fee, Tracking, Car Wash/Manpower)
CREATE TABLE fee_master (
    fee_id SERIAL PRIMARY KEY,
    fee_code VARCHAR(30) NOT NULL UNIQUE,   -- ADMIN, REGISTRATION, CARD_FEE, TRACKING, CAR_WASH_MANPOWER
    fee_name VARCHAR(100) NOT NULL,
    calculation_method VARCHAR(25) NOT NULL CHECK (
        calculation_method IN ('FIXED_AMOUNT','PERCENT_OF_TFV','PERCENT_OF_INSTALLMENT')
    ),
    fee_value NUMERIC(12,4) NOT NULL,
    frequency VARCHAR(20) NOT NULL CHECK (frequency IN ('ONE_TIME','MONTHLY','ANNUAL')),
    is_active BOOLEAN DEFAULT TRUE
);

-- Commission Rate Table
CREATE TABLE commission_rate_table (
    commission_id SERIAL PRIMARY KEY,
    channel_name VARCHAR(50) NOT NULL,   -- e.g. Direct, Broker, Dealer
    commission_pct NUMERIC(6,4) NOT NULL,
    effective_from DATE NOT NULL,
    effective_to DATE,
    is_active BOOLEAN DEFAULT TRUE
);

-- Profit Margin Setup
CREATE TABLE profit_margin_setup (
    margin_id SERIAL PRIMARY KEY,
    category_id INT REFERENCES vehicle_categories(category_id),
    margin_pct NUMERIC(6,4) NOT NULL,
    effective_from DATE NOT NULL,
    effective_to DATE,
    is_active BOOLEAN DEFAULT TRUE
);

-- Calendar Periods (drives amortization schedules and projections)
CREATE TABLE calendar_periods (
    period_id SERIAL PRIMARY KEY,
    period_label VARCHAR(20) NOT NULL,   -- e.g. '2026-06'
    period_start DATE NOT NULL,
    period_end DATE NOT NULL
);
```

### 1.2 Transactional Tables

```sql
-- Lease Contract Header
CREATE TABLE lease_contracts (
    contract_id SERIAL PRIMARY KEY,
    contract_number VARCHAR(30) UNIQUE NOT NULL,
    customer_id INT,   -- [ASSUMPTION] FK to an existing customers table
    vehicle_id INT REFERENCES vehicles(vehicle_id),
    term_id INT REFERENCES lease_terms(term_id),
    replacement_policy_id INT REFERENCES replacement_policy(policy_id),
    commission_id INT REFERENCES commission_rate_table(commission_id),
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    down_payment NUMERIC(12,2) DEFAULT 0,
    status VARCHAR(20) DEFAULT 'DRAFT',   -- DRAFT, ACTIVE, CLOSED, TERMINATED
    created_at TIMESTAMP DEFAULT now()
);

-- Vehicle Additions (accessories/equipment — affects TFV and RV)
CREATE TABLE vehicle_additions (
    addition_id SERIAL PRIMARY KEY,
    contract_id INT REFERENCES lease_contracts(contract_id),
    description VARCHAR(100) NOT NULL,
    addition_cost NUMERIC(12,2) NOT NULL,
    rv_percent_override NUMERIC(6,4),   -- if null, inherits vehicle category RV%
    added_date DATE NOT NULL
);

-- Pricing Calculation Snapshot (one row per priced version of a contract)
CREATE TABLE pricing_calculations (
    calc_id SERIAL PRIMARY KEY,
    contract_id INT REFERENCES lease_contracts(contract_id),
    calc_date TIMESTAMP DEFAULT now(),
    total_financed_value NUMERIC(12,2) NOT NULL,
    insurance_rate_pct NUMERIC(6,4),
    maintenance_rate_value NUMERIC(12,4),
    interest_amount NUMERIC(12,2),
    insurance_amount NUMERIC(12,2),
    maintenance_amount NUMERIC(12,2),
    admin_amount NUMERIC(12,2),
    profit_amount NUMERIC(12,2),
    registration_fees_amount NUMERIC(12,2),
    card_fee_amount NUMERIC(12,2),
    tracking_amount NUMERIC(12,2),
    car_wash_manpower_amount NUMERIC(12,2),
    residual_value_amount NUMERIC(12,2),
    rv_on_additions_amount NUMERIC(12,2),
    replacement_amount NUMERIC(12,2),
    rate_pre_commission NUMERIC(12,2) NOT NULL,
    commission_amount NUMERIC(12,2) NOT NULL,
    final_rate NUMERIC(12,2) NOT NULL,
    is_final BOOLEAN DEFAULT FALSE
);

-- Interest Schedule (per period)
CREATE TABLE interest_schedule (
    schedule_id SERIAL PRIMARY KEY,
    contract_id INT REFERENCES lease_contracts(contract_id),
    period_id INT REFERENCES calendar_periods(period_id),
    opening_balance NUMERIC(12,2) NOT NULL,
    interest_amount NUMERIC(12,2) NOT NULL,
    closing_balance NUMERIC(12,2) NOT NULL,
    strategy VARCHAR(10) NOT NULL
);

-- Insurance Schedule (always declining balance, per spec)
CREATE TABLE insurance_schedule (
    schedule_id SERIAL PRIMARY KEY,
    contract_id INT REFERENCES lease_contracts(contract_id),
    period_id INT REFERENCES calendar_periods(period_id),
    opening_balance NUMERIC(12,2) NOT NULL,
    insurance_amount NUMERIC(12,2) NOT NULL,
    closing_balance NUMERIC(12,2) NOT NULL
);

-- Maintenance Schedule
CREATE TABLE maintenance_schedule (
    schedule_id SERIAL PRIMARY KEY,
    contract_id INT REFERENCES lease_contracts(contract_id),
    period_id INT REFERENCES calendar_periods(period_id),
    strategy VARCHAR(10) NOT NULL,
    maintenance_amount NUMERIC(12,2) NOT NULL,
    actual_maintenance_cost NUMERIC(12,2),   -- actual incurred cost, for budget-vs-actual variance
    notes TEXT
);

-- Fuel Log (operational tracking, not a pricing component)
CREATE TABLE fuel_log (
    fuel_log_id SERIAL PRIMARY KEY,
    vehicle_id INT REFERENCES vehicles(vehicle_id),
    contract_id INT REFERENCES lease_contracts(contract_id),
    log_date DATE NOT NULL,
    liters NUMERIC(8,2),
    cost NUMERIC(10,2),
    odometer_reading INT
);

-- Replacement Transactions
CREATE TABLE replacement_transactions (
    replacement_id SERIAL PRIMARY KEY,
    contract_id INT REFERENCES lease_contracts(contract_id),
    old_vehicle_id INT REFERENCES vehicles(vehicle_id),
    new_vehicle_id INT REFERENCES vehicles(vehicle_id),
    replacement_date DATE NOT NULL,
    reason VARCHAR(100),
    cost_impact NUMERIC(12,2)
);

-- Income Statement Projection (period-level rollup; contract-level or fleet-level)
CREATE TABLE income_statement_projection (
    projection_id SERIAL PRIMARY KEY,
    contract_id INT REFERENCES lease_contracts(contract_id),   -- NULL = fleet-level aggregate row
    period_id INT REFERENCES calendar_periods(period_id),
    revenue_amount NUMERIC(12,2) NOT NULL,
    interest_expense NUMERIC(12,2),
    insurance_expense NUMERIC(12,2),
    maintenance_expense NUMERIC(12,2),
    admin_expense NUMERIC(12,2),
    registration_fees_expense NUMERIC(12,2),
    card_fee_expense NUMERIC(12,2),
    tracking_expense NUMERIC(12,2),
    car_wash_manpower_expense NUMERIC(12,2),
    replacement_expense NUMERIC(12,2),
    depreciation_expense NUMERIC(12,2),
    commission_expense NUMERIC(12,2),
    net_profit NUMERIC(12,2) NOT NULL
);
```

---

## 2. Pricing Calculation Engine (Waterfall — run in this exact order)

```
STEP 1 — Total Financed Value (TFV)
  TFV = Vehicle Acquisition Cost
      + SUM(Vehicle Additions Cost)
      + Capitalized one-time fees (if Registration/Admin is rolled into financing)
      − Down Payment

STEP 2 — Net Financed Amount (the base that interest & insurance amortize against)
  Net Financed Amount = TFV − Residual Value − RV on Additions
  [ASSUMPTION] RV is treated as a balance-sheet recovery at term end, not a
  P&L rate line — so it reduces the principal base for Steps 4 & 5, and
  appears separately in the Income Statement Projection as asset recovery,
  not as a cost added into the periodic rate.

STEP 3 — Lookups
  Insurance Rate   = lookup insurance_rate_table   BY category_id, TFV value band
  Maintenance Rate = lookup maintenance_rate_table BY category_id, vehicle age band, strategy

STEP 4 — Interest (Strategy A or B)
  IF strategy = A (Flat):
      Interest[period] = (TFV × annual_rate_pct) / periods_per_year
      → constant every period over the full term
  IF strategy = B (Reducing Balance):
      Interest[period] = Opening_Balance[period] × (annual_rate_pct / periods_per_year)
      Opening_Balance[period+1] = Closing_Balance[period]
      → declines each period as principal is repaid

STEP 5 — Insurance (always declining-balance, per spec)
  Insurance[period] = Opening_Balance[period] × (insurance_annual_rate_pct / periods_per_year)
  Opening_Balance[period+1] = Closing_Balance[period]

STEP 6 — Maintenance (Strategy A or B)
  IF strategy = A: Maintenance[period] = Fixed Amount (rate_type = FIXED_AMOUNT)
  IF strategy = B: Maintenance[period] = TFV × rate_value%  (rate_type = PERCENT_OF_TFV)
      → looked up per vehicle-age band, so it can step up as the vehicle ages

STEP 7 — Admin
  Admin = fee_master('ADMIN').fee_value, applied per calculation_method & frequency

STEP 8 — Profit
  Profit = TFV × margin_pct   (from profit_margin_setup, by category_id)

STEP 9 — Registration & Fees
  Registration & Fees = fee_master('REGISTRATION').fee_value
  If frequency = ONE_TIME and amortized into the periodic rate:
      Registration_per_period = Registration & Fees ÷ term_months

STEP 10 — Card Fee
  Card Fee = fee_master('CARD_FEE').fee_value, typically PERCENT_OF_INSTALLMENT

STEP 11 — Tracking
  Tracking = fee_master('TRACKING').fee_value, typically FIXED_AMOUNT, MONTHLY

STEP 12 — Car Wash / Manpower
  Car_Wash_Manpower = fee_master('CAR_WASH_MANPOWER').fee_value, FIXED_AMOUNT, MONTHLY

STEP 13 — Residual Value (RV)
  RV = Vehicle list_price × rv_percent   (lookup residual_value_table BY category_id, term_id)
  → already netted out in Step 2; recorded here for reporting/balance-sheet purposes

STEP 14 — RV on Additions
  RV_on_Additions = SUM(addition_cost × (rv_percent_override OR category default RV%))
  → already netted out in Step 2; recorded separately since additions may
    depreciate at a different rate than the base vehicle

STEP 15 — Replacement (Strategy: Open or Permanent)
  IF replacement_policy.strategy = 'OPEN':
      Replacement = TFV × replacement_rate_pct
  IF replacement_policy.strategy = 'PERMANENT':
      Replacement = 0

STEP 16 — Rate (pre-commission)
  Rate_pre_commission[period] =
        Interest[period]
      + Insurance[period]
      + Maintenance[period]
      + Admin[period]
      + Profit[period]
      + Registration_per_period
      + Card_Fee[period]
      + Tracking[period]
      + Car_Wash_Manpower[period]
      + Replacement[period]

STEP 17 — Commission
  Commission[period] = Rate_pre_commission[period] × commission_pct
  (from commission_rate_table, by sales channel on the contract)

STEP 18 — Final Rate
  Final_Rate[period] = Rate_pre_commission[period] + Commission[period]
  → this is the amount invoiced to the customer each period
```

---

## 3. Income Statement Projection Logic

For each `calendar_period`, per contract (and rolled up fleet-wide):

```
Revenue[period]              = Final_Rate[period]
Less:
  Interest Expense            = Interest[period]
  Insurance Expense           = Insurance[period]
  Maintenance Expense         = Maintenance[period]   (use actual_maintenance_cost if available, else planned)
  Admin Expense                = Admin[period]
  Registration & Fees Expense = Registration_per_period
  Card Fee Expense             = Card_Fee[period]
  Tracking Expense             = Tracking[period]
  Car Wash/Manpower Expense   = Car_Wash_Manpower[period]
  Replacement Expense          = Replacement[period] (+ any actual replacement_transactions.cost_impact in that period)
  Depreciation Expense         = (TFV − RV − RV_on_Additions) ÷ term_months
  Commission Expense           = Commission[period]
= Net Profit[period]
```

Roll forward across all periods in the term → full contract-level P&L.
Aggregate across all active contracts per period → fleet-level Income Statement Projection.

---

## 4. Setup Screens (build one admin screen per table below)

| Screen | Backing Table | Key Fields | Notes |
|---|---|---|---|
| **Vehicle Categories** | `vehicle_categories` | Category Name, Description, Active | Foundation lookup — build first |
| **Vehicle Master** | `vehicles` | VIN, Make, Model, Variant, Year, Category, List Price, Acquisition Cost, Acquisition Date, Useful Life (months), Status | |
| **Lease Terms** | `lease_terms` | Term (months), Description | e.g. 12, 24, 36, 48 |
| **Insurance Rate Table** | `insurance_rate_table` | Category, Value Band (Min/Max), Annual Rate %, Effective From/To | |
| **Maintenance Rate Table** | `maintenance_rate_table` | Category, Age Band (Start/End months), Strategy (A/B), Rate Type, Rate Value, Effective From/To | |
| **Interest Rate Table** | `interest_rate_table` | Term, Strategy (A/B), Annual Rate %, Effective From/To | |
| **Residual Value Table** | `residual_value_table` | Category, Term, RV %, Effective From/To | |
| **Replacement Policy** | `replacement_policy` | Policy Name, Strategy (Open/Permanent), Replacement Rate %, Max Replacements/Term, Active | |
| **Fee Master** | `fee_master` | Fee Code, Fee Name, Calculation Method, Fee Value, Frequency, Active | One row each for Admin, Registration, Card Fee, Tracking, Car Wash/Manpower |
| **Commission Rates** | `commission_rate_table` | Channel Name, Commission %, Effective From/To | |
| **Profit Margin** | `profit_margin_setup` | Category, Margin %, Effective From/To | |
| **Calendar Periods** | `calendar_periods` | Period Label, Start Date, End Date | Can auto-generate monthly periods for a date range rather than manual entry |

All setup screens should support: add/edit, soft-deactivate (never hard-delete a rate row that's been used in a calculation — historical contracts must remain reproducible), and an audit trail (who changed what, when).

---

## 5. Assumptions Made — Review Before Building

- **[ASSUMPTION]** Period = monthly throughout. If AutoLeaseNet needs quarterly/annual billing options, add a `billing_frequency` field to `lease_contracts` and adjust `periods_per_year` in the engine accordingly.
- **[ASSUMPTION]** Currency not specified — schema uses `NUMERIC(12,2)`, add a `currency_code` column if multi-currency is needed.
- **[ASSUMPTION]** `customer_id` in `lease_contracts` assumes a customers table already exists elsewhere in AutoLeaseNet.
- **[ASSUMPTION]** Commission is calculated as a % of Rate (pre-commission). If your business instead uses flat commission amounts for certain channels, extend `commission_rate_table` with a `commission_type` (PERCENT/FIXED) similar to `fee_master`.
- **[ASSUMPTION]** Residual Value reduces the financing base (Step 2) rather than appearing as a periodic rate line item. If your business instead wants RV shown as a negative line in the periodic rate itself, move Steps 13–14 into Step 16's summation as a subtraction.

---

**End of build specification.**
