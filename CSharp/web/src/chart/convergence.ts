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
 * The Y range is set by the band at two percent of the run, plus any measured
 * excursion past that mark. The opening moments of a run carry bands tens of points
 * wide; scaling to them flattens the whole convergence into a hairline. Anchoring the
 * range further in lets the early funnel run off the top and bottom of the frame —
 * which reads as the funnel entering from off-screen — while the part of the walk a
 * viewer judges fills the plot.
 */
export function verticalHalfRange(input: ChartInput): number {
  const last = input.curve[input.curve.length - 1]
  const maxSpins = Math.max(input.targetSpins, last.spins)
  const referenceSpins = Math.max(input.curve[0].spins, maxSpins / 50)
  const halfAtReference = bandHalfWidth(input.sigma, referenceSpins)
  const excursion = Math.max(
    ...input.curve
      .filter((p) => p.spins >= referenceSpins)
      .map((p) => Math.abs(p.measuredRtp - input.analyticRtp)),
    0,
  )
  return Math.max(halfAtReference, excursion) * 1.3
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

  // Early bands are wider than the anchored Y range on purpose; clamping the funnel
  // to the plot edges draws it entering from off-screen instead of stretching the frame.
  const yTop = frame.pad.top
  const yBottom = frame.height - frame.pad.bottom
  const clamp = (v: number) => Math.min(yBottom, Math.max(yTop, v))

  const firstSpins = input.curve[0].spins
  const samples = 160
  const upper: string[] = []
  const lower: string[] = []
  for (let i = 0; i <= samples; i++) {
    const spins = firstSpins + ((maxSpins - firstSpins) * i) / samples
    const half = bandHalfWidth(input.sigma, spins)
    upper.push(`${x(spins).toFixed(1)},${clamp(y(input.analyticRtp + half)).toFixed(1)}`)
    lower.unshift(`${x(spins).toFixed(1)},${clamp(y(input.analyticRtp - half)).toFixed(1)}`)
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
