# video — chapter decks, mechanism animations, and the series title sequence

Source for every moving picture in the *Programming Gems / Slot Games* series:
the six-second series opener, a four-second title card per chapter, a slide
deck per chapter, and one mechanism animation per chapter that an editor can
cut in on its own.

Rendered files are build output. **The code in `src/` is the source of truth**;
`out/` is gitignored and can be deleted at any time.

## Why Remotion

Remotion (React rendering to MP4) over a Vue/HTML deck, for four reasons that
matter to this particular job:

1. **The deliverable is video, not a presentation.** Steve drops these into a
   video edit. Remotion renders MP4 directly from one command; an HTML deck
   would need a screen-capture step, and a capture step is a step that can be
   done differently on two different days.
2. **Frame-exact timing.** Every beat is a frame number in a timing table
   (`src/title/timing.ts`, `src/deck/timing.ts`). Retiming is editing a number,
   and the render is identical every time. Browser animation timing is not.
3. **The decks are data-driven.** One `Deck` component reads
   `src/data/chapters.ts`; nine chapters are nine rows, not nine files. The
   same array drives the title cards, so a chapter title is written down once.
4. **The house already runs it.** `E:\dev\slotdemo-series-video` is a Remotion
   project producing the episode 1–3 figures, and this project mirrors its
   conventions (silent compositions, physical-mass easing, tokens mirrored from
   the site) so the two bodies of work cut together.

The trade the choice makes: no live HTML deck to click through, and a Node
toolchain (~200 MB of `node_modules`) to render. Both were worth it here.

## Setup

```bash
cd video
npm install
```

Node 20 or newer. No account, no API key, no paid service — every dependency is
an npm package and every font is an OFL family vendored by
`@remotion/google-fonts` at install time.

## Render everything — one command

```bash
npm run render:all
```

From a clean checkout that is `npm install && npm run render:all`. Output:

| Path | What |
|---|---|
| `out/titles/series-opener.mp4` | the 6s master opener, 1920×1080 |
| `out/titles/chapter-card-01.mp4` … `-09.mp4` | the 4s chapter cards, 1920×1080 |
| `out/decks/deck-01.mp4` … `-09.mp4` | the nine chapter decks, 1920×1080 |
| `out/mechanisms/mechanism-01.mp4` … `-09.mp4` | each chapter's animation alone |

Subsets:

```bash
npm run render:titles              # opener + nine chapter cards
npm run render:decks               # the nine decks
node render-all.mjs --only=mechanisms
```

One composition, or a single frame as a PNG:

```bash
npx remotion render deck-03 out/deck-03.mp4 --codec=h264 --crf=16
npx remotion still series-opener frame.png --frame=165
```

Interactive preview, with a timeline scrubber over every composition:

```bash
npm run dev
```

Typecheck:

```bash
npm run typecheck
```

## What is where

| Path | Holds |
|---|---|
| `src/data/chapters.ts` | **The content.** Nine chapters: title, kicker, thesis, slides. Every number on a slide is transcribed from that chapter's article, and the article path is recorded on the row. |
| `src/data/orcaDive.ts` | Reel 1 and the stop counts, transcribed verbatim from `CSharp/games/orca-dive.json`. |
| `src/tokens/` | Colors mirrored from `CSharp/web/src/tokens.css`, plus motion curves and type stacks. No color is introduced outside that file. |
| `src/title/` | The opener, the chapter card, the reel drums, the sunburst, and the beat sheet both title compositions read. |
| `src/deck/` | The deck shell, the four slide kinds, and the deck timing table. |
| `src/anim/` | One mechanism animation per chapter, plus the SVG primitives they share. |
| `src/components/` | Frame chrome, type roles, easing helpers, the display-size solver. |
| `render-all.mjs` | The one-command render. Bundles once, renders every target. |

## Conventions this project holds to

- **Silent.** No composition carries audio. These cut under live narration.
- **Physical-mass easing, no bounce.** `tokens/index.ts` exports the curves; a
  title settles into place and a drum is braked. Nothing springs back.
- **Numbers come from the articles.** A figure on a slide appears in
  `docs/articles/0N-*.md`. If an article changes, `src/data/chapters.ts` is
  wrong and has to be corrected — there is no second place a number lives.
- **Timing lives in a table**, not in the JSX.
- **Data over duplication.** Nine chapters share one card component, one deck
  component, and one shell. A tenth chapter is a tenth row.

## Visual register

MMPWorks house — the optimism of 1940s–50s science fiction, per
`E:\dev\MMP.Media\brand\artistic-direction.md`. Deco framing over a near-black
cinematic ground, brass as the single accent, geometric display type
letterspaced rather than condensed, restrained motion.

Type: the anchor names **Futura**, which is licensed and cannot be
redistributed in this repository. **Jost** is the OFL equivalent the channel
token doc already keeps in the stack, and it is what renders here, so a clean
checkout produces the same frames on any machine. A machine with a licensed
Futura installed gets Futura — it sits ahead of Jost in the stack.
