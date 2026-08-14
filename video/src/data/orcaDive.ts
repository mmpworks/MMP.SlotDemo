/**
 * Orca Dive reel data, transcribed verbatim from
 * `CSharp/games/orca-dive.json` in this repository.
 *
 * Only what the animations draw is copied here. If the game file changes,
 * this file is wrong and the animation is wrong with it — the check is a
 * plain diff against `reels[0]` and `reelStops` in the JSON.
 */

/** `reelStops` — unequal by design. */
export const REEL_STOPS = [26, 29, 26, 29, 26] as const;

/** `reels[0]` — the 26-stop first reel, in stop order. */
export const REEL_ONE = [
  'Penguin',
  'Mackerel',
  'Herring',
  'Green7',
  'Squid',
  'Mackerel',
  'Seal',
  'Herring',
  'WildOrca',
  'Squid',
  'Mackerel',
  'Blue7',
  'Salmon',
  'Penguin',
  'Mackerel',
  'Herring',
  'Green7',
  'Squid',
  'Mackerel',
  'Seal',
  'Herring',
  'WildOrca',
  'Red7',
  'Salmon',
  'Green7',
  'Blue7',
] as const;

/** Two-letter marks so a 26-cell strip stays legible at slide scale. */
export const SYMBOL_MARK: Record<string, string> = {
  Penguin: 'Pn',
  Mackerel: 'Mk',
  Herring: 'He',
  Green7: 'G7',
  Squid: 'Sq',
  Seal: 'Se',
  WildOrca: 'W',
  Blue7: 'B7',
  Salmon: 'Sa',
  Red7: 'R7',
};

/** The stops on reel 1 that carry Green7 — 3, 16, 24. */
export const GREEN7_STOPS = REEL_ONE.reduce<number[]>((stops, symbol, i) => {
  if (symbol === 'Green7') stops.push(i);
  return stops;
}, []);
