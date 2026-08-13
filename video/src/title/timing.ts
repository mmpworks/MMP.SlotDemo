import { FPS } from '../tokens';

/**
 * The title sequence's beat sheet, in frames at 30 fps.
 *
 * Both title compositions are cut to a fixed length by contract — a 6-second
 * master and a 4-second chapter card — so the beats are written as one table
 * rather than scattered through the components. Read this to retime, not the
 * JSX.
 */
const f = (seconds: number) => Math.round(seconds * FPS);

export const OPENER_FRAMES = f(6);

export const OPENER = {
  /** The horizon line opens from the center — the machine takes power. */
  horizonIn: f(0.0),
  horizonDuration: f(0.7),
  /** Drums drop into frame, then turn. Columns are staggered. */
  drumsIn: f(0.45),
  drumsInDuration: f(0.5),
  spinStart: f(0.7),
  landFrames: [f(2.25), f(2.55), f(2.85)] as const,
  /** Rays bloom out from behind the landed row. */
  sunburstIn: f(2.6),
  sunburstDuration: f(1.1),
  /** The drum row recedes so the lockup owns the frame. */
  drumsOut: f(2.9),
  drumsOutDuration: f(0.7),
  /** Line 1 settles: tracking closes, opacity rises. */
  line1In: f(3.15),
  line1Duration: f(0.85),
  /** The rule between the two lines draws left to right. */
  ruleIn: f(3.75),
  ruleDuration: f(0.6),
  /** Line 2 rises into place. */
  line2In: f(4.1),
  line2Duration: f(0.8),
  /** Deco corners strike last, closing the frame. */
  frameIn: f(4.5),
  frameDuration: f(0.6),
} as const;

export const CHAPTER_CARD_FRAMES = f(4);

export const CHAPTER_CARD = {
  frameIn: f(0.0),
  frameDuration: f(0.6),
  /** The chapter numeral rises behind the type. */
  numeralIn: f(0.15),
  numeralDuration: f(0.9),
  kickerIn: f(0.45),
  kickerDuration: f(0.5),
  ruleIn: f(0.7),
  ruleDuration: f(0.55),
  titleIn: f(0.95),
  titleDuration: f(0.8),
  /** Everything is at rest by here; the rest of the card is hold. */
  restBy: f(1.75),
} as const;
