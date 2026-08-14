/**
 * Design tokens for every composition in this project.
 *
 * Colors mirror `CSharp/web/src/tokens.css` — the companion site's token file
 * — with two exceptions, marked VIDEO-ONLY below. Those two are shades this
 * project needs and the site has no use for: an outer vignette stop darker
 * than the page ground, and a facet shade for a dimmed glyph. They were added
 * here rather than to the site file, because the site is not ours to widen.
 *
 * Everything else must exist in that file. Adding a third video-only color is
 * a decision to make deliberately, not a habit — the whole point is that a
 * viewer moving between the site and the video sees one house.
 *
 * Kept as a flat const object rather than CSS custom properties because
 * Remotion's interpolate() math reads numeric values directly.
 */
export const colors = {
  ground: '#0a0c10', // void-navy — base ground
  surface: '#12151c', // deep-slate — panel background
  surfaceRaised: '#181c25', // raised-slate — emphasized panel
  surfaceDormant: '#0d0f13', // dormant-slate — receded panel
  groundDeep: '#06080b', // VIDEO-ONLY. deep-void — vignette's outer stop, below the site's darkest ground

  brass: '#d1a355', // primary accent
  brassBright: '#f0c988', // bright brass — highlight/emphasis
  brassDim: '#7a6238', // dim brass — inactive rules, kickers
  brassShadow: '#4d3f24', // VIDEO-ONLY. shadowed brass — facet shading on a dimmed glyph

  textPrimary: '#ece7dd', // parchment
  textSecondary: '#9a978f', // parchment-dim
  textMuted: '#64625c', // parchment-mute — provenance lines

  signalGreen: '#6fcf97', // measured / passing
  signalBlue: '#7fb3d5', // analytic / idle
  signalAsh: '#4a4f5c', // inert
  signalEmber: '#e2685f', // rejected / dropped
  signalAmber: '#e3a53f', // warning
  signalTeal: '#7fd1c9', // interpolated value
} as const;

/**
 * Motion — physical mass, no bounce, no spring overshoot.
 * Mirrors the site's --ease-out / --ease-in-out cubic-beziers.
 * A title settles into place; a spacecraft has weight; nothing springs.
 */
export const easing = {
  out: [0.16, 1, 0.3, 1] as const,
  inOut: [0.65, 0, 0.35, 1] as const,
  in: [0.7, 0, 0.84, 0] as const,
};

export const FPS = 30;
export const HD = { width: 1920, height: 1080 } as const;

/** Every animated beat holds its final state at least this long before cutting. */
export const HOLD_SECONDS_MIN = 2;

/**
 * Letterspacing steps. Display type in this register is letterspaced, never
 * condensed — the era made one word monumental with air, not weight.
 */
export const tracking = {
  display: '0.08em',
  kicker: '0.22em',
  smallCaps: '0.14em',
} as const;
