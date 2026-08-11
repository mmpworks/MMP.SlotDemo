import type { CurvePoint } from '../api/labs'

/**
 * Pure geometry for the proving-ground chart. Everything here is arithmetic from run
 * data to SVG coordinates, kept out of the component so the mapping is testable: an
 * off-by-one in this file would draw the verdict in the wrong place on camera.
 */

export const TWO_SIDED_99 = 2.5758293035489004 // matches NormalQuantile.TwoSided99 server-side

export interface ChartFrame {
  width: number
  height: number
  pad: { left: number; right: number; top: number; bottom: number }
}

export interface ChartInput {
  analyticRtp: number
  sigma: number
  targetSpins: number
  curve: CurvePoint[]
}

export interface ChartGeometry {
  funnel: string
  measured: string
  analyticY: number
  ticksX: { x: number; label: string }[]
  ticksY: { y: number; label: string }[]
  lastX: number
  lastY: number
  halfRange: number
}

export function bandHalfWidth(sigma: number, spins: number, z = TWO_SIDED_99): number {
  return (z * sigma) / Math.sqrt(spins)
}

/**
 * The Y range tracks the widest thing on screen — the band at the first curve point or
 * the farthest measured excursion — so the funnel always fits and shrinking width reads
 * as growing certainty.
 */
export function verticalHalfRange(input: ChartInput): number {
  const first = input.curve[0]
  const halfAtFirst = bandHalfWidth(input.sigma, first.spins)
  const excursion = Math.max(
    ...input.curve.map((p) => Math.abs(p.measuredRtp - input.analyticRtp)),
    0,
  )
  return Math.max(halfAtFirst, excursion) * 1.15
}

export function xScale(frame: ChartFrame, maxSpins: number): (spins: number) => number {
  return (spins) =>
    frame.pad.left + (spins / maxSpins) * (frame.width - frame.pad.left - frame.pad.right)
}

export function yScale(
  frame: ChartFrame,
  analyticRtp: number,
  halfRange: number,
): (rtp: number) => number {
  return (rtp) =>
    frame.pad.top +
    (1 - (rtp - (analyticRtp - halfRange)) / (2 * halfRange)) *
      (frame.height - frame.pad.top - frame.pad.bottom)
}

export function buildGeometry(frame: ChartFrame, input: ChartInput): ChartGeometry | null {
  if (input.curve.length === 0) return null

  const maxSpins = Math.max(input.targetSpins, input.curve[input.curve.length - 1].spins)
  const halfRange = verticalHalfRange(input)
  const x = xScale(frame, maxSpins)
  const y = yScale(frame, input.analyticRtp, halfRange)

  const firstSpins = input.curve[0].spins
  const samples = 160
  const upper: string[] = []
  const lower: string[] = []
  for (let i = 0; i <= samples; i++) {
    const spins = firstSpins + ((maxSpins - firstSpins) * i) / samples
    const half = bandHalfWidth(input.sigma, spins)
    upper.push(`${x(spins).toFixed(1)},${y(input.analyticRtp + half).toFixed(1)}`)
    lower.unshift(`${x(spins).toFixed(1)},${y(input.analyticRtp - half).toFixed(1)}`)
  }

  const measured = input.curve
    .map((p) => `${x(p.spins).toFixed(1)},${y(p.measuredRtp).toFixed(1)}`)
    .join(' ')

  const last = input.curve[input.curve.length - 1]
  return {
    funnel: `${upper.join(' ')} ${lower.join(' ')}`,
    measured,
    analyticY: y(input.analyticRtp),
    ticksX: [0.25, 0.5, 0.75, 1].map((f) => ({
      x: x(maxSpins * f),
      label: `${(maxSpins * f) / 1_000_000}M`,
    })),
    ticksY: [-halfRange, -halfRange / 2, 0, halfRange / 2, halfRange].map((d) => ({
      y: y(input.analyticRtp + d),
      label: `${((input.analyticRtp + d) * 100).toFixed(2)}%`,
    })),
    lastX: x(last.spins),
    lastY: y(last.measuredRtp),
    halfRange,
  }
}
