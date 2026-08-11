import { describe, expect, it } from 'vitest'
import {
  bandHalfWidth,
  buildGeometry,
  verticalHalfRange,
  xScale,
  yScale,
  type ChartFrame,
  type ChartInput,
} from './convergence'
import type { CurvePoint } from '../api/labs'

const frame: ChartFrame = {
  width: 960,
  height: 420,
  pad: { left: 64, right: 20, top: 16, bottom: 34 },
}

function point(spins: number, measuredRtp: number): CurvePoint {
  return { spins, measuredRtp, hitFrequency: 0.3, bandHalfWidth: bandHalfWidth(8.6, spins), withinBand: true }
}

function input(curve: CurvePoint[], overrides?: Partial<ChartInput>): ChartInput {
  return { analyticRtp: 0.98, sigma: 8.6, targetSpins: 10_000_000, curve, ...overrides }
}

describe('bandHalfWidth', () => {
  it('narrows by 10x for every 100x in spins', () => {
    expect(bandHalfWidth(8.6, 1_000_000)).toBeCloseTo(bandHalfWidth(8.6, 10_000) / 10, 12)
  })
})

describe('scales', () => {
  it('maps the spin domain onto the padded x extent', () => {
    const x = xScale(frame, 10_000_000)
    expect(x(0)).toBe(frame.pad.left)
    expect(x(10_000_000)).toBe(frame.width - frame.pad.right)
  })

  it('maps rtp so the analytic centre sits mid-plot and higher rtp is higher on screen', () => {
    const y = yScale(frame, 0.98, 0.01)
    const mid = frame.pad.top + (frame.height - frame.pad.top - frame.pad.bottom) / 2
    expect(y(0.98)).toBeCloseTo(mid, 6)
    expect(y(0.99)).toBeLessThan(y(0.98)) // SVG y grows downward
    expect(y(0.97)).toBeGreaterThan(y(0.98))
  })
})

describe('verticalHalfRange', () => {
  it('is set by the band when the walk stays inside it', () => {
    const c = [point(50_000, 0.9801), point(100_000, 0.9799)]
    const expected = bandHalfWidth(8.6, 50_000) * 1.15
    expect(verticalHalfRange(input(c))).toBeCloseTo(expected, 12)
  })

  it('grows to hold a measured excursion outside the band', () => {
    const c = [point(1_000_000, 1.2)] // wildly out
    expect(verticalHalfRange(input(c))).toBeCloseTo((1.2 - 0.98) * 1.15, 12)
  })
})

describe('buildGeometry', () => {
  it('returns null with no points rather than an empty chart', () => {
    expect(buildGeometry(frame, input([]))).toBeNull()
  })

  it('places the last point marker at the end of the measured line', () => {
    const c = [point(50_000, 0.99), point(10_000_000, 0.9801)]
    const g = buildGeometry(frame, input(c))!
    const lastPair = g.measured.split(' ').at(-1)!
    expect(`${g.lastX.toFixed(1)},${g.lastY.toFixed(1)}`).toBe(lastPair)
  })

  it('keeps every funnel vertex inside the frame', () => {
    const c = [point(50_000, 0.98), point(10_000_000, 0.98)]
    const g = buildGeometry(frame, input(c))!
    for (const pair of g.funnel.split(' ')) {
      const [x, y] = pair.split(',').map(Number)
      expect(x).toBeGreaterThanOrEqual(frame.pad.left - 0.1)
      expect(x).toBeLessThanOrEqual(frame.width - frame.pad.right + 0.1)
      expect(y).toBeGreaterThanOrEqual(frame.pad.top - 0.1)
      expect(y).toBeLessThanOrEqual(frame.height - frame.pad.bottom + 0.1)
    }
  })

  it('extends the x domain when a run overshoots its target', () => {
    const c = [point(50_000, 0.98), point(12_000_000, 0.98)]
    const g = buildGeometry(frame, input(c))!
    expect(g.lastX).toBeLessThanOrEqual(frame.width - frame.pad.right + 0.1)
  })

  it('labels the analytic centre tick with the analytic rtp', () => {
    const g = buildGeometry(frame, input([point(50_000, 0.98)]))!
    expect(g.ticksY[2].label).toBe('98.00%')
  })
})
