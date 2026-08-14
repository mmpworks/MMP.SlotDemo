/**
 * Type stacks.
 *
 * The artistic-direction anchor names Futura, which is a licensed commercial
 * face and is not redistributable inside this repository. Jost is the OFL
 * equivalent the channel token doc keeps in the stack as the fallback, and it
 * is what renders here.
 *
 * Futura is deliberately NOT in the stack. It used to sit ahead of Jost, which
 * meant a machine with Futura installed rendered different frames from a clean
 * CI box — and worse, `components/fitText.ts` solves display sizes against
 * Jost's metrics, so the Futura frames were being laid out to the wrong
 * numbers. One face, one set of metrics, identical output everywhere.
 */
import { loadFont as loadJost } from '@remotion/google-fonts/Jost';
import { loadFont as loadJetBrainsMono } from '@remotion/google-fonts/JetBrainsMono';
import { loadFont as loadSourceSerif } from '@remotion/google-fonts/SourceSerif4';

// Only the weights and the subset these compositions actually use. The default
// loads every weight and every subset, which is ~50 requests per family per
// render and a wall of console warnings for no benefit.
const jost = loadJost('normal', { weights: ['500', '700'], subsets: ['latin'] });
const jetBrains = loadJetBrainsMono('normal', { weights: ['500'], subsets: ['latin'] });
const sourceSerif = loadSourceSerif('normal', { weights: ['400'], subsets: ['latin'] });

export const fonts = {
  /** Wordmark lockup, titles, slide headings. */
  display: `${jost.fontFamily}, Avenir, "Century Gothic", sans-serif`,
  /** Body prose on a slide. The workhorse serif. */
  body: `${sourceSerif.fontFamily}, Georgia, "Times New Roman", serif`,
  /** Numbers, code, RTP percentages, benchmark figures. */
  mono: `${jetBrains.fontFamily}, ui-monospace, "Cascadia Code", Consolas, monospace`,
} as const;

/**
 * Every family this project paints with, ready to await.
 * `WaitForFonts` consumes it; nothing else should need to.
 */
export const fontsReady = Promise.all([
  jost.waitUntilDone(),
  jetBrains.waitUntilDone(),
  sourceSerif.waitUntilDone(),
]);
