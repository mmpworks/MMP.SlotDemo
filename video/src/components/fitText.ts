/**
 * Solve a display font size so a single line fits a given zone width.
 *
 * Remotion renders in a headless browser, so a measured fit would mean a
 * layout pass and a second render. A solved fit is deterministic instead:
 * the display face averages a known advance per uppercase character at these
 * weights, tracking adds a known amount per character, and the clamp absorbs
 * whatever error is left. Any title that would still overflow hits the floor
 * and is a copy problem, not a layout problem — keep chapter titles short.
 */

/** Average advance per uppercase character, in em, for the display stack. */
const AVG_ADVANCE_EM = 0.63;

export function fitDisplaySize(
  text: string,
  zoneWidth: number,
  trackingEm: number,
  bounds: { min: number; max: number },
): number {
  const perChar = AVG_ADVANCE_EM + trackingEm;
  const raw = zoneWidth / Math.max(1, text.length * perChar);
  return Math.round(Math.max(bounds.min, Math.min(bounds.max, raw)));
}
