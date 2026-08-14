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

/**
 * Monospace advance, in em. JetBrains Mono is 600/1000 units wide — for a
 * monospace face this is a fact about the metrics, not an average, so a block
 * of code solved against it fits on the first try.
 */
const MONO_ADVANCE_EM = 0.6;

/**
 * Solve a mono font size so a block of code fits BOTH the width available to
 * its longest line and the height available to all of its lines.
 *
 * Sizing from width alone is what let long code slides print past their panel:
 * a twelve-line excerpt of short lines passes every width check and still
 * overruns the bottom of the frame. Both constraints are checked here and the
 * smaller wins.
 */
export function fitMonoBlock(
  lines: readonly string[],
  zone: { width: number; height: number },
  lineHeight: number,
  bounds: { min: number; max: number },
): number {
  const longest = lines.reduce((n, line) => Math.max(n, line.length), 1);
  const fromWidth = zone.width / (longest * MONO_ADVANCE_EM);
  const fromHeight = zone.height / (Math.max(1, lines.length) * lineHeight);
  const raw = Math.min(fromWidth, fromHeight);
  return Math.floor(Math.max(bounds.min, Math.min(bounds.max, raw)));
}
