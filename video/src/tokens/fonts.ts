/**
 * Type stacks.
 *
 * The artistic-direction anchor names Futura, which is a licensed commercial
 * face and is not redistributable inside this repo. Jost is the OFL equivalent
 * the channel token doc keeps in the stack as the fallback, and it is what
 * renders here so a clean checkout produces the same frames on any machine.
 * A machine with a licensed Futura installed gets Futura, because it sits
 * ahead of Jost in the stack.
 */
import { loadFont as loadJost } from '@remotion/google-fonts/Jost';
import { loadFont as loadJetBrainsMono } from '@remotion/google-fonts/JetBrainsMono';
import { loadFont as loadSourceSerif } from '@remotion/google-fonts/SourceSerif4';

const jost = loadJost();
const jetBrains = loadJetBrainsMono();
const sourceSerif = loadSourceSerif();

export const fonts = {
  /** Wordmark lockup, titles, slide headings. */
  display: `"Futura Std ExtraBold", Futura, ${jost.fontFamily}, Avenir, "Century Gothic", sans-serif`,
  /** Body prose on a slide. The workhorse serif. */
  body: `${sourceSerif.fontFamily}, Georgia, "Times New Roman", serif`,
  /** Numbers, code, RTP percentages, benchmark figures. */
  mono: `${jetBrains.fontFamily}, ui-monospace, "Cascadia Code", Consolas, monospace`,
} as const;

/** Await this before a composition measures or paints text. */
export const fontsReady = Promise.all([
  jost.waitUntilDone(),
  jetBrains.waitUntilDone(),
  sourceSerif.waitUntilDone(),
]);
