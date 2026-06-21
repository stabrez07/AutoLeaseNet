import type {
  FeeCode,
  FeeMasterSetupRow,
  InterestRateSetupRow,
  QuotationPricingSetupData,
  QuotationPricingVehicleProfile,
  ReplacementPolicySetupRow,
} from './quotation-pricing-catalog'

export interface PricingWaterfallInput {
  setup: QuotationPricingSetupData
  vehicle: QuotationPricingVehicleProfile
  termMonths: number
  downPaymentSar?: number
  additionsCostSar?: number
  replacementPolicyName?: string
  salesChannelName?: string
  vehicleAgeMonths?: number
}

export interface PricingWaterfallResult {
  totalFinancedValueSar: number
  netFinancedAmountSar: number
  ratePreCommissionSar: number
  commissionSar: number
  finalMonthlyRateSar: number
  residualValueSar: number
  rvOnAdditionsSar: number
  depreciationMonthlySar: number
  breakdown: {
    interestSar: number
    insuranceSar: number
    maintenanceSar: number
    adminSar: number
    profitSar: number
    registrationSar: number
    cardFeeSar: number
    trackingSar: number
    carWashManpowerSar: number
    replacementSar: number
  }
}

function toMoney(value: number): number {
  return Math.round(value * 100) / 100
}

function findFee(rows: FeeMasterSetupRow[], code: FeeCode): FeeMasterSetupRow | undefined {
  return rows.find((x) => x.isActive && x.feeCode === code)
}

function feeAmountPerMonth(
  fee: FeeMasterSetupRow | undefined,
  tfv: number,
  termMonths: number,
): number {
  if (!fee) return 0

  const base =
    fee.calculationMethod === 'FIXED_AMOUNT'
      ? fee.feeValue
      : fee.calculationMethod === 'PERCENT_OF_TFV'
        ? (tfv * fee.feeValue) / 100
        : 0

  if (fee.frequency === 'MONTHLY') return base
  if (fee.frequency === 'ANNUAL') return base / 12
  return base / Math.max(1, termMonths)
}

function chooseInterestRate(
  setup: QuotationPricingSetupData,
  termMonths: number,
): InterestRateSetupRow | undefined {
  const exact = setup.interestRateTable.find((x) => x.isActive && x.termMonths === termMonths)
  if (exact) return exact

  const nearest = [...setup.interestRateTable]
    .filter((x) => x.isActive)
    .sort((a, b) => Math.abs(a.termMonths - termMonths) - Math.abs(b.termMonths - termMonths))[0]

  return nearest
}

function chooseReplacementPolicy(
  setup: QuotationPricingSetupData,
  preferredPolicyName?: string,
): ReplacementPolicySetupRow | undefined {
  if (preferredPolicyName) {
    const preferred = setup.replacementPolicy.find(
      (x) => x.isActive && x.policyName.toLowerCase() === preferredPolicyName.toLowerCase(),
    )
    if (preferred) return preferred
  }

  return (
    setup.replacementPolicy.find((x) => x.isActive && x.strategy === 'PERMANENT') ||
    setup.replacementPolicy.find((x) => x.isActive)
  )
}

export function calculatePricingWaterfallMonthly(
  input: PricingWaterfallInput,
): PricingWaterfallResult {
  const { setup, vehicle } = input
  const termMonths = Math.max(1, input.termMonths)
  const additions = Math.max(0, input.additionsCostSar ?? 0)
  const downPayment = Math.max(0, input.downPaymentSar ?? 0)

  const adminFee = findFee(setup.feeMaster, 'ADMIN')
  const registrationFee = findFee(setup.feeMaster, 'REGISTRATION')
  const cardFee = findFee(setup.feeMaster, 'CARD_FEE')
  const trackingFee = findFee(setup.feeMaster, 'TRACKING')
  const cwmFee = findFee(setup.feeMaster, 'CAR_WASH_MANPOWER')

  const oneTimeAdminCapitalized =
    adminFee && adminFee.frequency === 'ONE_TIME'
      ? feeAmountPerMonth(adminFee, vehicle.basePriceSar, termMonths) * termMonths
      : 0
  const oneTimeRegistrationCapitalized =
    registrationFee && registrationFee.frequency === 'ONE_TIME'
      ? feeAmountPerMonth(registrationFee, vehicle.basePriceSar, termMonths) * termMonths
      : 0

  const tfv =
    vehicle.basePriceSar +
    additions +
    oneTimeAdminCapitalized +
    oneTimeRegistrationCapitalized -
    downPayment

  const rvRow =
    setup.residualValueTable.find(
      (x) => x.isActive && x.vehicleType === vehicle.vehicleType && x.termMonths === termMonths,
    ) || setup.residualValueTable.find((x) => x.isActive && x.vehicleType === vehicle.vehicleType)

  const rvPercent = rvRow?.rvPercent ?? 35
  const residualValueSar = (vehicle.basePriceSar * rvPercent) / 100
  const rvOnAdditionsSar = (additions * rvPercent) / 100

  const netFinanced = Math.max(0, tfv - residualValueSar - rvOnAdditionsSar)

  const interestRateRow = chooseInterestRate(setup, termMonths)
  const interestRate = (interestRateRow?.annualRatePercent ?? vehicle.interestRatePercent) / 100

  const insuranceRow =
    setup.insurance.find(
      (x) =>
        x.vehicleType === vehicle.vehicleType &&
        tfv >= x.minVehicleValueSar &&
        tfv <= x.maxVehicleValueSar,
    ) || setup.insurance.find((x) => x.vehicleType === vehicle.vehicleType)
  const insuranceRate = (insuranceRow?.ratePercent ?? 1.5) / 100

  const maintenanceRow =
    setup.maintenance.find((x) => x.vehicleType === vehicle.vehicleType) ||
    setup.maintenance.find((x) => x.manufacturer === vehicle.make)
  const maintenanceStrategy = maintenanceRow?.strategy ?? 'A'
  const maintenanceRateType = maintenanceRow?.rateType ?? 'FIXED_AMOUNT'
  const maintenanceRateValue = maintenanceRow?.rateValue ?? maintenanceRow?.mtcRateSar ?? 0

  const ageMonths = Math.max(0, input.vehicleAgeMonths ?? 0)

  let totalInterest = 0
  let totalInsurance = 0
  let totalMaintenance = 0

  const monthlyPrincipal = netFinanced / termMonths
  let openingBalance = netFinanced

  for (let p = 1; p <= termMonths; p++) {
    const interestPeriod =
      interestRateRow?.strategy === 'A'
        ? (tfv * interestRate) / 12
        : (openingBalance * interestRate) / 12

    const insurancePeriod = (openingBalance * insuranceRate) / 12

    const maintenancePeriod =
      maintenanceStrategy === 'B' && maintenanceRateType === 'PERCENT_OF_TFV'
        ? (tfv * maintenanceRateValue) / 100
        : maintenanceRateValue

    totalInterest += interestPeriod
    totalInsurance += insurancePeriod
    totalMaintenance += maintenancePeriod

    openingBalance = Math.max(0, openingBalance - monthlyPrincipal)

    if (ageMonths + p > 9999) {
      break
    }
  }

  const interestSar = totalInterest / termMonths
  const insuranceSar = totalInsurance / termMonths
  const maintenanceSar = totalMaintenance / termMonths

  const adminSar = feeAmountPerMonth(adminFee, tfv, termMonths)
  const registrationSar = feeAmountPerMonth(registrationFee, tfv, termMonths)
  const trackingSar = feeAmountPerMonth(trackingFee, tfv, termMonths)
  const carWashManpowerSar = feeAmountPerMonth(cwmFee, tfv, termMonths)

  const marginRow =
    setup.profitMarginSetup.find((x) => x.isActive && x.vehicleType === vehicle.vehicleType) ||
    setup.profitMarginSetup.find((x) => x.isActive)
  const marginPercent = marginRow?.marginPercent ?? 8
  const profitSar = (tfv * (marginPercent / 100)) / termMonths

  const replacementPolicy = chooseReplacementPolicy(setup, input.replacementPolicyName)
  const replacementSar =
    replacementPolicy?.strategy === 'OPEN'
      ? (tfv * ((replacementPolicy.replacementRatePercent ?? 0) / 100)) / termMonths
      : 0

  const preInstallmentBase =
    interestSar +
    insuranceSar +
    maintenanceSar +
    adminSar +
    profitSar +
    registrationSar +
    trackingSar +
    carWashManpowerSar +
    replacementSar

  const cardFeeSar =
    cardFee?.calculationMethod === 'PERCENT_OF_INSTALLMENT'
      ? (preInstallmentBase * cardFee.feeValue) / 100
      : feeAmountPerMonth(cardFee, tfv, termMonths)

  const ratePreCommissionSar = preInstallmentBase + cardFeeSar

  const commissionRow =
    setup.commissionRateTable.find(
      (x) =>
        x.isActive &&
        x.channelName.toLowerCase() === (input.salesChannelName ?? 'Direct').toLowerCase(),
    ) || setup.commissionRateTable.find((x) => x.isActive)
  const commissionSar = ratePreCommissionSar * ((commissionRow?.commissionPercent ?? 0) / 100)

  const depreciationMonthlySar = (tfv - residualValueSar - rvOnAdditionsSar) / termMonths
  const finalMonthlyRateSar = ratePreCommissionSar + commissionSar

  return {
    totalFinancedValueSar: toMoney(tfv),
    netFinancedAmountSar: toMoney(netFinanced),
    ratePreCommissionSar: toMoney(ratePreCommissionSar),
    commissionSar: toMoney(commissionSar),
    finalMonthlyRateSar: toMoney(finalMonthlyRateSar),
    residualValueSar: toMoney(residualValueSar),
    rvOnAdditionsSar: toMoney(rvOnAdditionsSar),
    depreciationMonthlySar: toMoney(depreciationMonthlySar),
    breakdown: {
      interestSar: toMoney(interestSar),
      insuranceSar: toMoney(insuranceSar),
      maintenanceSar: toMoney(maintenanceSar),
      adminSar: toMoney(adminSar),
      profitSar: toMoney(profitSar),
      registrationSar: toMoney(registrationSar),
      cardFeeSar: toMoney(cardFeeSar),
      trackingSar: toMoney(trackingSar),
      carWashManpowerSar: toMoney(carWashManpowerSar),
      replacementSar: toMoney(replacementSar),
    },
  }
}
