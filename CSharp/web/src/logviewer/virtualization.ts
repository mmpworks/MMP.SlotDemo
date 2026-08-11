// Fixed-row-height virtualization math for the log list. Pure so the
// windowing arithmetic is unit-testable without mounting the component.

export interface VisibleRange {
  /** Inclusive first rendered index. */
  start: number
  /** Exclusive end index. */
  end: number
}

/**
 * Computes which row indices should be rendered for a scrolled viewport,
 * padded by `overscan` rows on each side so fast scrolling and keyboard
 * navigation don't flash empty space before the next frame renders.
 */
export function computeVisibleRange(
  scrollTop: number,
  viewportHeight: number,
  rowHeight: number,
  totalRows: number,
  overscan = 8,
): VisibleRange {
  if (totalRows <= 0 || rowHeight <= 0 || viewportHeight <= 0) {
    return { start: 0, end: 0 }
  }

  // Clamp against totalRows-1 so a stale/out-of-range scrollTop (e.g. after
  // a filter shrinks the row count) can't push start past end.
  const maxFirstVisible = Math.max(0, totalRows - 1)
  const firstVisible = Math.min(Math.floor(scrollTop / rowHeight), maxFirstVisible)
  const visibleCount = Math.ceil(viewportHeight / rowHeight)

  const start = Math.max(0, firstVisible - overscan)
  const end = Math.min(totalRows, firstVisible + visibleCount + overscan)
  return { start, end }
}

export interface SpacerHeights {
  topPx: number
  bottomPx: number
}

/** Spacer-div heights that keep the scrollbar sized as if every row were rendered. */
export function computeSpacerHeights(range: VisibleRange, totalRows: number, rowHeight: number): SpacerHeights {
  return {
    topPx: range.start * rowHeight,
    bottomPx: Math.max(0, totalRows - range.end) * rowHeight,
  }
}

/** Scroll offset that pins the viewport to the newest row (used by the Follow feature). */
export function bottomScrollTop(totalRows: number, rowHeight: number, viewportHeight: number): number {
  return Math.max(0, totalRows * rowHeight - viewportHeight)
}

/** True when the viewport is close enough to the bottom that Follow should stay engaged. */
export function isNearBottom(scrollTop: number, totalRows: number, rowHeight: number, viewportHeight: number, thresholdPx: number): boolean {
  return bottomScrollTop(totalRows, rowHeight, viewportHeight) - scrollTop <= thresholdPx
}
