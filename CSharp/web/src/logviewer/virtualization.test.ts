import { describe, expect, it } from 'vitest'
import { bottomScrollTop, computeSpacerHeights, computeVisibleRange, isNearBottom } from './virtualization'

describe('computeVisibleRange', () => {
  it('windows around the scroll position with overscan padding', () => {
    // rowHeight 20, viewport 100 (5 rows visible), scrolled to row 50
    const range = computeVisibleRange(1000, 100, 20, 1000, 8)
    expect(range).toEqual({ start: 42, end: 63 })
  })

  it('clamps to [0, totalRows] at the edges', () => {
    expect(computeVisibleRange(0, 100, 20, 3, 8)).toEqual({ start: 0, end: 3 })
    expect(computeVisibleRange(999_999, 100, 20, 10, 8)).toEqual({ start: 1, end: 10 })
  })

  it('returns an empty range for degenerate inputs', () => {
    expect(computeVisibleRange(0, 100, 20, 0, 8)).toEqual({ start: 0, end: 0 })
    expect(computeVisibleRange(0, 0, 20, 10, 8)).toEqual({ start: 0, end: 0 })
  })
})

describe('computeSpacerHeights', () => {
  it('sizes top/bottom spacers to keep the scrollbar proportional', () => {
    const spacers = computeSpacerHeights({ start: 10, end: 30 }, 100, 20)
    expect(spacers).toEqual({ topPx: 200, bottomPx: 1400 })
  })

  it('never returns a negative bottom spacer', () => {
    const spacers = computeSpacerHeights({ start: 0, end: 100 }, 100, 20)
    expect(spacers.bottomPx).toBe(0)
  })
})

describe('bottomScrollTop / isNearBottom', () => {
  it('computes the scrollTop that pins the newest row in view', () => {
    expect(bottomScrollTop(100, 20, 200)).toBe(1800)
    expect(bottomScrollTop(5, 20, 200)).toBe(0)
  })

  it('treats a scroll position within the threshold as near-bottom', () => {
    expect(isNearBottom(1760, 100, 20, 200, 44)).toBe(true)
    expect(isNearBottom(1700, 100, 20, 200, 44)).toBe(false)
  })
})
