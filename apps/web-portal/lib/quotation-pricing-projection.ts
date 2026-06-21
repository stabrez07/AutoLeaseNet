import type { PricingWaterfallResult } from './quotation-pricing-engine'
import type { CalendarPeriodSetupRow } from './quotation-pricing-catalog'

export interface PricingProjectionPeriod {
  periodLabel: string
  periodStart: string
  periodEnd: string
  revenueSar: number
  interestExpenseSar: number
  insuranceExpenseSar: number
  maintenanceExpenseSar: number
  adminExpenseSar: number
  registrationExpenseSar: number
  cardFeeExpenseSar: number
  trackingExpenseSar: number
  carWashManpowerExpenseSar: number
  replacementExpenseSar: number
  depreciationExpenseSar: number
  commissionExpenseSar: number
  totalExpensesSar: number
  netProfitSar: number
}

export interface ContractProjectionResult {
  contractRef: string
  termMonths: number
  periods: PricingProjectionPeriod[]
  totalRevenueSar: number
  totalExpensesSar: number
  totalNetProfitSar: number
}

export interface FleetProjectionPeriod {
  periodLabel: string
  periodStart: string
  periodEnd: string
  contractCount: number
  revenueSar: number
  totalExpensesSar: number
  netProfitSar: number
}

function toMoney(value: number): number {
  return Math.round(value * 100) / 100
}

export function buildContractProjection(
  contractRef: string,
  waterfall: PricingWaterfallResult,
  termMonths: number,
  calendarPeriods: CalendarPeriodSetupRow[],
  startPeriodIndex: number = 0,
): ContractProjectionResult {
  const periods: PricingProjectionPeriod[] = []

  for (let i = 0; i < termMonths; i++) {
    const periodIdx = startPeriodIndex + i
    const calPeriod = calendarPeriods[periodIdx]

    const revenueSar = waterfall.finalMonthlyRateSar
    const interestExpenseSar = waterfall.breakdown.interestSar
    const insuranceExpenseSar = waterfall.breakdown.insuranceSar
    const maintenanceExpenseSar = waterfall.breakdown.maintenanceSar
    const adminExpenseSar = waterfall.breakdown.adminSar
    const registrationExpenseSar = waterfall.breakdown.registrationSar
    const cardFeeExpenseSar = waterfall.breakdown.cardFeeSar
    const trackingExpenseSar = waterfall.breakdown.trackingSar
    const carWashManpowerExpenseSar = waterfall.breakdown.carWashManpowerSar
    const replacementExpenseSar = waterfall.breakdown.replacementSar
    const depreciationExpenseSar = waterfall.depreciationMonthlySar
    const commissionExpenseSar = waterfall.commissionSar

    const totalExpensesSar = toMoney(
      interestExpenseSar +
        insuranceExpenseSar +
        maintenanceExpenseSar +
        adminExpenseSar +
        registrationExpenseSar +
        cardFeeExpenseSar +
        trackingExpenseSar +
        carWashManpowerExpenseSar +
        replacementExpenseSar +
        depreciationExpenseSar +
        commissionExpenseSar,
    )

    const netProfitSar = toMoney(revenueSar - totalExpensesSar)

    periods.push({
      periodLabel: calPeriod?.periodLabel ?? `Period ${i + 1}`,
      periodStart: calPeriod?.periodStart ?? '',
      periodEnd: calPeriod?.periodEnd ?? '',
      revenueSar: toMoney(revenueSar),
      interestExpenseSar: toMoney(interestExpenseSar),
      insuranceExpenseSar: toMoney(insuranceExpenseSar),
      maintenanceExpenseSar: toMoney(maintenanceExpenseSar),
      adminExpenseSar: toMoney(adminExpenseSar),
      registrationExpenseSar: toMoney(registrationExpenseSar),
      cardFeeExpenseSar: toMoney(cardFeeExpenseSar),
      trackingExpenseSar: toMoney(trackingExpenseSar),
      carWashManpowerExpenseSar: toMoney(carWashManpowerExpenseSar),
      replacementExpenseSar: toMoney(replacementExpenseSar),
      depreciationExpenseSar: toMoney(depreciationExpenseSar),
      commissionExpenseSar: toMoney(commissionExpenseSar),
      totalExpensesSar,
      netProfitSar,
    })
  }

  const totalRevenueSar = toMoney(periods.reduce((sum, p) => sum + p.revenueSar, 0))
  const totalExpensesSar = toMoney(periods.reduce((sum, p) => sum + p.totalExpensesSar, 0))
  const totalNetProfitSar = toMoney(periods.reduce((sum, p) => sum + p.netProfitSar, 0))

  return {
    contractRef,
    termMonths,
    periods,
    totalRevenueSar,
    totalExpensesSar,
    totalNetProfitSar,
  }
}

export function buildFleetProjection(
  contracts: ContractProjectionResult[],
  calendarPeriods: CalendarPeriodSetupRow[],
): FleetProjectionPeriod[] {
  const periodMap = new Map<string, FleetProjectionPeriod>()

  for (const period of calendarPeriods) {
    periodMap.set(period.periodLabel, {
      periodLabel: period.periodLabel,
      periodStart: period.periodStart,
      periodEnd: period.periodEnd,
      contractCount: 0,
      revenueSar: 0,
      totalExpensesSar: 0,
      netProfitSar: 0,
    })
  }

  for (const contract of contracts) {
    for (const period of contract.periods) {
      let fleet = periodMap.get(period.periodLabel)
      if (!fleet) {
        fleet = {
          periodLabel: period.periodLabel,
          periodStart: period.periodStart,
          periodEnd: period.periodEnd,
          contractCount: 0,
          revenueSar: 0,
          totalExpensesSar: 0,
          netProfitSar: 0,
        }
        periodMap.set(period.periodLabel, fleet)
      }
      fleet.contractCount += 1
      fleet.revenueSar += period.revenueSar
      fleet.totalExpensesSar += period.totalExpensesSar
      fleet.netProfitSar += period.netProfitSar
    }
  }

  const result = [...periodMap.values()]
    .filter((p) => p.contractCount > 0)
    .sort((a, b) => a.periodLabel.localeCompare(b.periodLabel))

  return result.map((p) => ({
    ...p,
    revenueSar: toMoney(p.revenueSar),
    totalExpensesSar: toMoney(p.totalExpensesSar),
    netProfitSar: toMoney(p.netProfitSar),
  }))
}
