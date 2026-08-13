import { FPS } from '../tokens';
import type { Chapter, Slide } from '../data/chapters';

/**
 * Deck timing.
 *
 * A deck is cut for narration: each slide holds long enough to be read aloud,
 * and the animated slide gets the longest hold because it has to complete its
 * mechanism and then rest. These are the durations an editor trims against —
 * change them here, never inside a slide component.
 */
const f = (seconds: number) => Math.round(seconds * FPS);

export const SLIDE_SECONDS: Record<Slide['kind'], number> = {
  points: 9,
  stat: 8,
  code: 11,
  anim: 16,
};

/** The deck's own opening slide: chapter title and thesis. */
export const OPENER_SECONDS = 7;

/** Cross-fade between slides. Short — the cut carries the beat, not the blend. */
export const TRANSITION_FRAMES = f(0.5);

export function slideFrames(slide: Slide): number {
  return f(SLIDE_SECONDS[slide.kind]);
}

export function deckFrames(chapter: Chapter): number {
  return f(OPENER_SECONDS) + chapter.slides.reduce((total, s) => total + slideFrames(s), 0);
}
