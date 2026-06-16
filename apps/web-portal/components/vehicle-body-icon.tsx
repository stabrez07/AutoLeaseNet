'use client'

// Maps both string names and numeric codes to a numeric key used by ICONS map.
const STR_TO_NUM: Record<string, number> = {
  Sedan: 1, sedan: 1, '1': 1,
  Suv: 2, suv: 2, SUV: 2, '2': 2,
  Hatchback: 3, hatchback: 3, '3': 3,
  Pickup: 4, pickup: 4, '4': 4,
  Van: 5, van: 5, '5': 5,
  Bus: 6, bus: 6, '6': 6,
  Coupe: 7, coupe: 7, '7': 7,
}

// SVG constants — viewBox "0 0 160 72"
// Front wheel centre (FX, WY), rear (RX, WY), radius WR
// Body bottom at BY
const FX = 33, RX = 127, WY = 61, WR = 11, BY = 50

function Wheels() {
  return (
    <>
      <path d={`M${FX - WR},${BY} A${WR},${WR} 0 0 1 ${FX + WR},${BY}`} />
      <path d={`M${RX - WR},${BY} A${WR},${WR} 0 0 1 ${RX + WR},${BY}`} />
      <circle cx={FX} cy={WY} r={WR} />
      <circle cx={RX} cy={WY} r={WR} />
    </>
  )
}

function Underline() {
  return (
    <>
      <line x1={7} y1={BY} x2={FX - WR} y2={BY} />
      <line x1={FX + WR} y1={BY} x2={RX - WR} y2={BY} />
      <line x1={RX + WR} y1={BY} x2={153} y2={BY} />
    </>
  )
}

const G_PROPS = {
  fill: 'none' as const,
  stroke: 'currentColor',
  strokeWidth: 2,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
}

const ICONS: Record<number, () => React.ReactElement> = {
  // 1 — Sedan: classic 3-box (hood + cabin + distinct trunk)
  1: () => (
    <g {...G_PROPS}>
      {/* profile: bumper → hood rise → windshield → roof → C-pillar → trunk → rear */}
      <polyline points={`7,${BY} 7,41 34,38 48,25 60,12 96,12 113,25 143,32 153,34 153,${BY}`} />
      <Underline />
      <Wheels />
    </g>
  ),

  // 2 — SUV: tall, boxy cabin spanning most of body length
  2: () => (
    <g {...G_PROPS}>
      <polyline points={`7,${BY} 7,40 34,36 44,26 50,10 116,10 127,26 153,30 153,${BY}`} />
      <Underline />
      <Wheels />
    </g>
  ),

  // 3 — Hatchback: 2-box, steeply sloping rear hatch direct to body bottom
  3: () => (
    <g {...G_PROPS}>
      {/* body sides + roof */}
      <polyline points={`7,${BY} 7,41 34,38 48,25 60,12 100,12 122,${BY}`} />
      {/* rear bumper shelf */}
      <line x1={122} y1={BY} x2={153} y2={BY} />
      <line x1={153} y1={BY} x2={153} y2={44} />
      <Underline />
      <Wheels />
    </g>
  ),

  // 4 — Pickup: cab at front, open flatbed at rear
  4: () => (
    <g {...G_PROPS}>
      {/* cab front */}
      <polyline points={`7,${BY} 7,40 34,37 46,26 55,14 88,14 90,${BY}`} />
      {/* bed: front wall + top rail + rear wall */}
      <line x1={90} y1={36} x2={90} y2={BY} />
      <line x1={90} y1={36} x2={153} y2={36} />
      <line x1={153} y1={36} x2={153} y2={BY} />
      {/* cab bottom */}
      <line x1={7} y1={BY} x2={FX - WR} y2={BY} />
      <path d={`M${FX - WR},${BY} A${WR},${WR} 0 0 1 ${FX + WR},${BY}`} />
      <line x1={FX + WR} y1={BY} x2={90} y2={BY} />
      {/* bed bottom */}
      <line x1={90} y1={BY} x2={RX - WR} y2={BY} />
      <path d={`M${RX - WR},${BY} A${WR},${WR} 0 0 1 ${RX + WR},${BY}`} />
      <line x1={RX + WR} y1={BY} x2={153} y2={BY} />
      <circle cx={FX} cy={WY} r={WR} />
      <circle cx={RX} cy={WY} r={WR} />
    </g>
  ),

  // 5 — Van: flat-nose, very tall rectangular cabin
  5: () => (
    <g {...G_PROPS}>
      {/* front face near-vertical, tall roof */}
      <polyline points={`7,${BY} 7,8 153,8 153,${BY}`} />
      {/* front windshield: slanted top of front face */}
      <line x1={7} y1={8} x2={18} y2={8} />
      <line x1={7} y1={26} x2={26} y2={8} />
      {/* side window strip */}
      <line x1={26} y1={11} x2={26} y2={27} />
      <line x1={26} y1={11} x2={80} y2={11} />
      <line x1={80} y1={11} x2={80} y2={27} />
      <line x1={26} y1={27} x2={80} y2={27} />
      <Underline />
      <Wheels />
    </g>
  ),

  // 6 — Bus: long, tall rectangle with window strip
  6: () => (
    <g {...G_PROPS}>
      <polyline points={`7,${BY} 7,8 153,8 153,${BY}`} />
      {/* front slant */}
      <line x1={7} y1={26} x2={22} y2={8} />
      {/* window strip across top */}
      {[24, 46, 68, 90, 112].map((x) => (
        <rect key={x} x={x} y={12} width={18} height={12} rx={1} />
      ))}
      {/* rear door hint */}
      <line x1={140} y1={28} x2={140} y2={BY} />
      <Underline />
      <Wheels />
    </g>
  ),

  // 7 — Coupe: low, sporty, long fastback slope at rear
  7: () => (
    <g {...G_PROPS}>
      {/* very low, long profile — roof starts later and slopes gradual to rear */}
      <polyline points={`7,${BY} 7,43 34,40 50,28 62,14 102,14 148,42 153,${BY}`} />
      <Underline />
      <Wheels />
    </g>
  ),
}

export function VehicleBodyIcon({
  bodyType,
  className = 'h-14 w-28 text-slate-600',
}: {
  bodyType: number | string
  className?: string
}) {
  const key = typeof bodyType === 'number' ? bodyType : (STR_TO_NUM[String(bodyType)] ?? 1)
  const Icon = ICONS[key] ?? ICONS[1]!
  return (
    <svg viewBox="0 0 160 72" className={className} aria-hidden="true">
      <Icon />
    </svg>
  )
}
