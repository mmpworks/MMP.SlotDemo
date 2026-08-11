// Chapter lab calls. Each one runs the episode's real C# type on the server, so the
// numbers on screen and the Herald lines in the log viewer come from the same execution.

export interface AmountView {
  millicents: number
  credits: number
  display: string
  bits: string
}

export interface MoneyResponse {
  wager: AmountView
  pay: AmountView
  exactTotal: AmountView
  floatTotal: number
  driftCredits: number
  floatAgrees: boolean
  associativeLeft: number
  associativeRight: number
  refusal: string | null
}

export interface StreamView {
  workerId: number
  state: string[]
  /** Hex strings: a 64-bit draw does not survive a JSON number. */
  raw: string[]
  reduced: number[]
}

export interface RngResponse {
  streams: StreamView[]
  sharedPrefixBits: number
  reproduced: boolean
}

export interface BiasResponse {
  moduloCounts: number[]
  lemireCounts: number[]
  moduloWorstErrorPercent: number
  lemireWorstErrorPercent: number
  space: number
  expected: number
  rejections: number
}

async function postJson<T>(url: string, body: unknown): Promise<T> {
  let response: Response
  try {
    response = await fetch(url, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(body),
    })
  } catch {
    throw new Error(`Could not reach ${url}. Is the server running?`)
  }
  if (!response.ok) {
    const detail = await response.text().catch(() => '')
    throw new Error(`${url} responded with ${response.status}. ${detail}`.trim())
  }
  return (await response.json()) as T
}

export function runMoneyLab(
  wagerCredits: number,
  scaledMultiplier: number,
  repeats: number,
  /** Raw override: the odd amount that makes the type refuse an inexact conversion. */
  wagerMillicents = 0,
): Promise<MoneyResponse> {
  return postJson<MoneyResponse>('/api/ch2/money', {
    wagerCredits,
    scaledMultiplier,
    repeats,
    wagerMillicents,
  })
}

export function runRngLab(
  seed: number,
  workerCount: number,
  draws: number,
  bound: number,
  mixed: boolean,
): Promise<RngResponse> {
  return postJson<RngResponse>('/api/ch2/rng', { seed, workerCount, draws, bound, mixed })
}

export function runBiasLab(
  seed: number,
  bound: number,
  bits: number,
  samples: number,
): Promise<BiasResponse> {
  return postJson<BiasResponse>('/api/ch2/bias', { seed, bound, bits, samples })
}
