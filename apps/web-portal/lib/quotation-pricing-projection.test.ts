import { describe, expect, it } from 'vitest'
import type { PricingWaterfallResult } from './quotation-pricing-engine'
import type { CalendarPeriodSetupRow } from './quotation-pricing-catalog'
import { buildContractProjection, buildFleetProjection } from './quotation-pricing-projection'

function makeWaterfall(overrides: Partial<PricingWaterfallResult> = {}): PricingWaterfallResult {
  return {
    totalFinancedValueSar: 100000,
    netFinancedAmountSar: 65000,
    ratePreCommissionSar: 2000,
    commissionSar: 60,
    finalMonthlyRateSar: 2060,
    residualValueSar: 35000,
    rvOnAdditionsSar: 0,
    depreciationMonthlySar: 2708.33,
    breakdown: {
      interestSar: 400,
      insuranceSar: 300,
      maintenanceSar: 200,
      adminSar: 75,
      profitSar: 333.33,
      registrationSar: 62.5,
      cardFeeSar: 30,
      trackingSar: 65,
      carWashManpowerSar: 45,
      replacementSar: 489.17,
    },
    ...overrides,
  }
}

function makeCalendarPeriods(count: number, year: number = 2026): CalendarPeriodSetupRow[] {
  return Array.from({ length: count }, (_, i) => {
    const month = i + 1
    const label = `${year}-${String(month).padStart(2, '0')}`
    const start = `${label}-01`
    const endDate = new Date(year, month, 0)
    const end = `${label}-${String(endDate.getDate()).padStart(2, '0')}`
    return { id: `period-${label}`, periodLabel: label, periodStart: start, periodEnd: end }
  })
}

describe('buildContractProjection', () => {
  it('generates correct number of periods matching term', () => {
    const waterfall = makeWaterfall()
    const periods = makeCalendarPeriods(24)

    const result = buildContractProjection('CONTRACT-001', waterfall, 24, periods)

    expect(result.contractRef).toBe('CONTRACT-001')
    expect(result.termMonths).toBe(24)
    expect(result.periods).toHaveLength(24)
  })

  it('revenue per period equals final monthly rate', () => {
    const waterfall = makeWaterfall({ finalMonthlyRateSar: 2060 })
    const periods = makeCalendarPeriods(12)

    const result = buildContractProjection('C-1', waterfall, 12, periods)

    for (const period of result.periods) {
      expect(period.revenueSar).toBe(2060)
    }
  })

  it('total revenue equals sum of period revenues', () => {
    const waterfall = makeWaterfall()
    const periods = makeCalendarPeriods(12)

    const result = buildContractProjection('C-1', waterfall, 12, periods)

    const sumRevenue = result.periods.reduce((s, p) => s + p.revenueSar, 0)
    expect(result.totalRevenueSar).toBeCloseTo(sumRevenue, 2)
  })

  it('net profit = revenue - total expenses per period', () => {
    const waterfall = makeWaterfall()
    const periods = makeCalendarPeriods(12)

    const result = buildContractProjection('C-1', waterfall, 12, periods)

    for (const period of result.periods) {
      const expectedNet = Math.round((period.revenueSar - period.totalExpensesSar) * 100) / 100
      expect(period.netProfitSar).toBeCloseTo(expectedNet, 2)
    }
  })

  it('total expenses includes all component expenses plus depreciation and commission', () => {
    const waterfall = makeWaterfall()
    const periods = makeCalendarPeriods(6)

    const result = buildContractProjection('C-1', waterfall, 6, periods)

    for (const period of result.periods) {
      const componentSum =
        period.interestExpenseSar +
        period.insuranceExpenseSar +
        period.maintenanceExpenseSar +
        period.adminExpenseSar +
        period.registrationExpenseSar +
        period.cardFeeExpenseSar +
        period.trackingExpenseSar +
        period.carWashManpowerExpenseSar +
        period.replacementExpenseSar +
        period.depreciationExpenseSar +
        period.commissionExpenseSar

      expect(period.totalExpensesSar).toBeCloseTo(componentSum, 2)
    }
  })

  it('maps calendar period labels correctly', () => {
    const waterfall = makeWaterfall()
    const periods = makeCalendarPeriods(3, 2026)

    const result = buildContractProjection('C-1', waterfall, 3, periods)

    expect(result.periods[0]!.periodLabel).toBe('2026-01')
    expect(result.periods[1]!.periodLabel).toBe('2026-02')
    expect(result.periods[2]!.periodLabel).toBe('2026-03')
  })

  it('uses startPeriodIndex to offset into calendar periods', () => {
    const waterfall = makeWaterfall()
    const periods = makeCalendarPeriods(12, 2026)

    const result = buildContractProjection('C-1', waterfall, 3, periods, 6)

    expect(result.periods[0]!.periodLabel).toBe('2026-07')
    expect(result.periods[1]!.periodLabel).toBe('2026-08')
    expect(result.periods[2]!.periodLabel).toBe('2026-09')
  })

  it('handles case where fewer calendar periods than term months', () => {
    const waterfall = makeWaterfall()
    const periods = makeCalendarPeriods(2)

    const result = buildContractProjection('C-1', waterfall, 5, periods)

    expect(result.periods).toHaveLength(5)
    expect(result.periods[0]!.periodLabel).toBe('2026-01')
    expect(result.periods[1]!.periodLabel).toBe('2026-02')
    expect(result.periods[2]!.periodLabel).toBe('Period 3')
  })

  it('total net profit consistency: equals totalRevenue minus totalExpenses', () => {
    const waterfall = makeWaterfall()
    const periods = makeCalendarPeriods(24)

    const result = buildContractProjection('C-1', waterfall, 24, periods)

    expect(result.totalNetProfitSar).toBeCloseTo(
      result.totalRevenueSar - result.totalExpensesSar,
      1,
    )
  })
})

describe('buildFleetProjection', () => {
  it('aggregates multiple contracts by period', () => {
    const waterfall1 = makeWaterfall({ finalMonthlyRateSar: 2000 })
    const waterfall2 = makeWaterfall({ finalMonthlyRateSar: 3000 })
    const periods = makeCalendarPeriods(12)

    const c1 = buildContractProjection('C-1', waterfall1, 12, periods)
    const c2 = buildContractProjection('C-2', waterfall2, 12, periods)

    const fleet = buildFleetProjection([c1, c2], periods)

    expect(fleet).toHaveLength(12)
    for (const fp of fleet) {
      expect(fp.contractCount).toBe(2)
    }
  })

  it('fleet revenue equals sum of individual contract revenues per period', () => {
    const waterfall1 = makeWaterfall({ finalMonthlyRateSar: 2000 })
    const waterfall2 = makeWaterfall({ finalMonthlyRateSar: 3000 })
    const periods = makeCalendarPeriods(6)

    const c1 = buildContractProjection('C-1', waterfall1, 6, periods)
    const c2 = buildContractProjection('C-2', waterfall2, 6, periods)

    const fleet = buildFleetProjection([c1, c2], periods)

    for (let i = 0; i < 6; i++) {
      const expectedRevenue = c1.periods[i]!.revenueSar + c2.periods[i]!.revenueSar
      expect(fleet[i]!.revenueSar).toBeCloseTo(expectedRevenue, 2)
    }
  })

  it('fleet net profit equals sum of contract net profits per period', () => {
    const waterfall1 = makeWaterfall({ finalMonthlyRateSar: 2000 })
    const waterfall2 = makeWaterfall({ finalMonthlyRateSar: 4000 })
    const periods = makeCalendarPeriods(6)

    const c1 = buildContractProjection('C-1', waterfall1, 6, periods)
    const c2 = buildContractProjection('C-2', waterfall2, 6, periods)

    const fleet = buildFleetProjection([c1, c2], periods)

    for (let i = 0; i < 6; i++) {
      const expectedNet = c1.periods[i]!.netProfitSar + c2.periods[i]!.netProfitSar
      expect(fleet[i]!.netProfitSar).toBeCloseTo(expectedNet, 2)
    }
  })

  it('returns empty array for empty contracts list', () => {
    const periods = makeCalendarPeriods(12)
    const fleet = buildFleetProjection([], periods)
    expect(fleet).toHaveLength(0)
  })

  it('handles overlapping contracts with different start periods', () => {
    const waterfall = makeWaterfall()
    const periods = makeCalendarPeriods(12)

    const c1 = buildContractProjection('C-1', waterfall, 6, periods, 0)
    const c2 = buildContractProjection('C-2', waterfall, 6, periods, 3)

    const fleet = buildFleetProjection([c1, c2], periods)

    const janPeriod = fleet.find((p) => p.periodLabel === '2026-01')
    expect(janPeriod?.contractCount).toBe(1)

    const aprPeriod = fleet.find((p) => p.periodLabel === '2026-04')
    expect(aprPeriod?.contractCount).toBe(2)

    const julPeriod = fleet.find((p) => p.periodLabel === '2026-07')
    expect(julPeriod?.contractCount).toBe(1)
  })

  it('sorted by period label', () => {
    const waterfall = makeWaterfall()
    const periods = makeCalendarPeriods(12)

    const c1 = buildContractProjection('C-1', waterfall, 6, periods, 6)
    const c2 = buildContractProjection('C-2', waterfall, 3, periods, 0)

    const fleet = buildFleetProjection([c1, c2], periods)

    for (let i = 1; i < fleet.length; i++) {
      expect(fleet[i]!.periodLabel > fleet[i - 1]!.periodLabel).toBe(true)
    }
  })
})
