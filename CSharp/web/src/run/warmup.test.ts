import { describe, expect, it } from 'vitest'
import { WARMED_SPINS_PER_SECOND, isWarmupRun } from './warmup'

describe('isWarmupRun', () => {
  it('flags a completed run below the warmed threshold', () => {
    // The reported first-run rate on developer hardware.
    expect(isWarmupRun('completed', 12_400_000)).toBe(true)
  })

  it('clears a completed run at or above the threshold', () => {
    expect(isWarmupRun('completed', WARMED_SPINS_PER_SECOND)).toBe(false)
    // The reported third and fourth run rates.
    expect(isWarmupRun('completed', 136_400_000)).toBe(false)
    expect(isWarmupRun('completed', 143_200_000)).toBe(false)
  })

  it('judges nothing until the run has finished', () => {
    // A run in flight has not had the chance to reach its rate yet, so calling it a
    // warm-up mid-run would flash a warning at every fast run too.
    expect(isWarmupRun('running', 1_000)).toBe(false)
    expect(isWarmupRun('cancelled', 1_000)).toBe(false)
    expect(isWarmupRun(undefined, 1_000)).toBe(false)
  })

  it('treats an absent measurement as nothing to report', () => {
    expect(isWarmupRun('completed', 0)).toBe(false)
    expect(isWarmupRun('completed', -1)).toBe(false)
    expect(isWarmupRun('completed', Number.NaN)).toBe(false)
  })
})
