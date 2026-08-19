// Typed clients for the chapter labs and the finale run. One post helper, one get
// helper. Each response interface mirrors its server record field for field.

async function requestJson<T>(url: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(url, init)
  } catch {
    throw new Error(`Could not reach ${url}. Is the server running?`)
  }
  if (!response.ok) {
    const detail = await response.text().catch(() => '')
    throw new Error(`${url} responded with ${response.status}. ${detail}`.trim())
  }
  return (await response.json()) as T
}

export function getJson<T>(url: string): Promise<T> {
  return requestJson<T>(url)
}

export function postJson<T>(url: string, body: unknown): Promise<T> {
  return requestJson<T>(url, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  })
}

// ---- chapter 3 ----

export interface SourceSymbol {
  id: number
  name: string
  isWild: boolean
  isScatter: boolean
  perReel: number[]
}

export interface SourceView {
  id: string
  name: string
  isGame: boolean
  configurationSource: string
  reelCount: number
  rows: number
  stopsPerReel: number[]
  symbols: SourceSymbol[]
  strips: number[][]
  paylines: { name: string; rows: number[] }[]
  paytable: {
    name: string
    kind: string
    pays: { count: number; payHundredths: number }[]
  }[]
}

export interface SpinView {
  source: string
  spinIndex: number
  window: {
    reel: number
    row: number
    symbolId: number
    symbol: string
    isWild: boolean
    isScatter: boolean
  }[]
  lines: {
    name: string
    rows: number[]
    cells: { reel: number; row: number; symbolId: number; symbol: string }[]
  }[]
}

export interface CensusView {
  source: string
  spins: number
  symbolId: number
  perReel: { reel: number; observed: number; expected: number; count: number }[]
}

export interface ReelSnapshotView {
  seed: number
  shortSnapshot: { stops: number; window: string[] }
  repeatedShortSnapshot: { stops: number; window: string[] }
  longSnapshot: { stops: number; window: string[] }
  shortSnapshotRepeatedExactly: boolean
  shortSnapshotKeptOriginalFirstSymbol: boolean
}

// ---- chapter 4 ----

export interface SolveView {
  preset: string
  targetBaseRtp: number
  unscaledEvMultiplier: number
  scaleFactor: number
  realizedBaseRtp: number
  driftBasisPoints: number
  paytable: {
    symbolId: number
    symbol: string
    count: number
    canonical: number
    probability: number
    scaledMillicents: number
    scaledCredits: number
  }[]
}

export interface PublishedRow {
  category: string
  count: number
  payMultiplier: number
  combinations: number
  probability: number
  rtpContribution: number
}

export interface PublishedView {
  supported: boolean
  reason?: string
  game?: string
  stopCombinations?: number
  lineRtp?: number
  bonusRtp?: number
  totalRtp?: number
  sigma?: number
  rows?: PublishedRow[]
}

export interface BandView {
  preset: string
  analytic: {
    baseRtp: number
    features: { name: string; rtp: number }[]
    totalRtp: number
    sigma: number
  }
  bands: { spins: number; halfWidth99: number; halfWidth999: number }[]
}

// ---- chapter 5 ----

export interface DeterminismView {
  preset: string
  varySeed: boolean
  identical: boolean
  runs: {
    attempt: number
    seed: number
    spins: number
    wageredMillicents: number
    returnedMillicents: number
    hits: number
    measuredRtp: number
    elapsedMs: number
    spinsPerSecond: number
  }[]
}

export interface TelemetryView {
  preset: string
  channelCapacity: number
  elapsedMs: number
  spinsPerSecond: number
  samplesProducedApprox: number
  samplesDelivered: number
  samplesDroppedApprox: number
  exactFinal: { spins: number; wageredMillicents: number; returnedMillicents: number; measuredRtp: number }
  lastDeliveredSample: { spins: number; measuredRtp: number }
}

// ---- chapter 6 ----

export interface GameSummary {
  file: string
  valid: boolean
  errors?: string[]
  name?: string
  source?: string | null
  reels?: number
  rows?: number
  stopsPerReel?: number[]
  stopCombinations?: number
  symbols?: { id: number; name: string; isWild: boolean; isScatter: boolean }[]
  paylines?: { name: string; rows: number[] }[]
  categories?: { name: string; kind: string; pays: { count: number; payHundredths: number }[] }[]
  bonus?: {
    name: string
    scatterSymbolId: number
    requiredReels: number[]
    prizes: number
    blanks: number
    maxAward: number
  } | null
}

export interface ValidateView {
  valid: boolean
  errors?: string[]
  name?: string
  reels?: number
  rows?: number
  stopCombinations?: number
  symbols?: number
  categories?: number
  hasBonus?: boolean
}

// ---- chapter 7 ----

export interface EnumerateView {
  supported: boolean
  reason?: string
  game?: string
  stopCombinations?: number
  hitCombinations?: number
  triggerCombinations?: number
  hitFrequency?: number
  triggerProbability?: number
  lineRtp?: number
  bonusRtp?: number
  totalRtp?: number
  sigma?: number
  combinations?: { category: string; count: number; combinations: number; probability: number }[]
}

export interface RefereeView {
  supported: boolean
  reason?: string
  game?: string
  spins?: number
  measured?: {
    totalRtp: number
    lineRtp: number
    bonusRtp: number
    hitFrequency: number
    triggerFrequency: number
  }
  exact?: {
    totalRtp: number
    lineRtp: number
    bonusRtp: number
    hitFrequency: number
    triggerProbability: number
  }
  bandHalfWidth?: number
  withinBand?: boolean
}

// ---- the finale run ----

export interface RunLimits {
  maxAggregateBasisPoints: number
  minAggregateBasisPoints: number
  games: string[]
  defaults: {
    presetName: string
    baseRtpBasisPoints: number
    freeSpinsRtpBasisPoints: number
    pickBonusRtpBasisPoints: number
    stride: number
  }
  workerCeiling: number
  presets: { name: string; reels: number; rows: number; stopsPerReel: number[]; paylines: number }[]
}

export interface CurvePoint {
  spins: number
  measuredRtp: number
  hitFrequency: number
  bandHalfWidth: number
  withinBand: boolean
}

export interface RunDescription {
  runId: string
  status: string
  stride: number
  config: {
    preset: string
    isGame: boolean
    reels: number
    rows: number
    stopsPerReel: string
    paylines: number
    targetRtp: number
    workers: number
    targetSpins: number
    seed: number
  }
  analytic: {
    baseRtp: number
    features: { name: string; rtp: number }[]
    totalRtp: number
    sigma: number
  }
  latest: {
    spins: number
    measuredRtp: number
    hitFrequency: number
    wageredMillicents: number
    returnedMillicents: number
  }
  /** GLI-style acceptance: null until the run has at least `minimumSpins` spins. */
  industry: {
    spins: number
    deviation: number
    passed: boolean
    tolerance: number
    minimumSpins: number
  } | null
  curve: CurvePoint[]
}

export interface RunStreamEvent {
  type: string
  data: unknown
}

// ---- chapter 9 ----

export interface OptimizationBenchmarkView {
  source: string
  spins: number
  trials: number
  reels: number
  rows: number
  randomSelections: number
  visibleCellWrites: number
  checksum: string
  baseline: { label: string; samples: number[]; medianSpinsPerSecond: number }
  optimized: { label: string; samples: number[]; medianSpinsPerSecond: number }
  speedup: number
  percentFaster: number
  memoryTradeoff: string
}
