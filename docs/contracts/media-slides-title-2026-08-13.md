# Contract: chapter slides, animations, script refresh, series title sequence

Engagement date: 2026-08-13 · Status: OPEN · First engagement of the contractor tier
(tier ratified by Steve 2026-08-13; mechanics per
the internal MMPWorks contractors proposal, 2026-08-11).

## Scope (acceptance-gate form)

1. **Per-chapter slide decks + animations** for the 9 video lessons
   (`docs/articles/01`–`09`, scripts at `docs/scripts/01`–`09`).
   - One deck per chapter, source-of-truth as code (Remotion or a Vue/HTML deck —
     contractor's call, recorded in the deliverable README), renderable to assets
     Steve can drop into a video edit.
   - Animations illustrate the chapter's core mechanism (e.g. ch3 reels/paylines,
     ch5 weighted enumeration, ch9 the byte-ID window optimization) — derived from
     the article content, never invented numbers.
   - GATE: every deck builds/renders from a clean checkout with one documented command.
2. **Lesson-script refresh** — `docs/scripts/*.md` brought current with the code and
   articles as they stand today (post episode-9 optimization pass, American spelling,
   current benchmark medians from `current.md`).
   - GATE: no script cites a number, API name, or file that the repo no longer contains.
3. **Series title sequence** — a COOL 6-second opener: "Programming Gems" / "Slot Games"
   (two-line lockup), used in all videos, plus a 4-second addition that identifies the
   chapter title (templated — one render per chapter).
   - GATE: 6s master + nine 4s chapter cards render to mp4 (1080p minimum) with one
     documented command; chapter card takes its title from data, not hand-edited copies.

## Skills granted

`remotion` (+ `remotion-best-practices`), `motion-designer`, `presentation-design`,
`storyboard`, `mmpworks-writing-voice` (scripts), `muir-slot-design` (domain),
`ffmpeg`. Visual register: MMPWorks house (1940s-50s sci-fi optimism per brand anchor)
unless Laura overrides in review.

## Sponsor

**Laura** (media designer / creative director). Independent reviewer: one
fool/apprentice-fool pass on the script refresh (factual drift check vs repo).

## Boundaries

- Work in a dedicated worktree/branch: `contract/media-2026-08-13`. The main tree and
  `codex/optimization-lab` are out of bounds.
- Files in scope: new `video/` (or similarly named) asset tree, `docs/scripts/*.md`
  edits, this contract file. Engine code (`CSharp/`), articles, PRD: READ-ONLY.
- STOP conditions: any change that would touch engine code or article content beyond
  reading it; any deliverable that needs a paid external service; scripts contradicting
  the repo (report the contradiction, don't resolve it unilaterally).

## Review clause

Sponsor (Laura persona) + independent factual pass review the branch diff + rendered
samples. Verdict is data (pass / blocker list). Max 2 change-request rounds, then
escalate to Steve. Merge is the sponsor's action, never the contractor's.

## Outcome (appended at close)

**ACCEPTED** 2026-08-13. Merged `contract/media-2026-08-13` into `codex/optimization-lab`.
Four review rounds total: two sponsor (Laura), two independent factual. Both lanes pass.

### Delivered

- `video/` — a Remotion project rendering 28 compositions, all 1920×1080, all silent:
  the 6s series opener, nine 4s chapter cards, nine chapter decks, nine standalone
  mechanism animations. `npm install && npm run render:all` from a clean checkout.
- `docs/scripts/01`–`09` brought current with the code and articles.

### Gates

All three met. Renders exist for every deliverable from one documented command;
chapter cards are templated from `src/data/chapters.ts` (one component, nine rows) and
`render-all.mjs` enumerates its targets out of the bundle, so a tenth chapter is a tenth
row and nothing else changes. Boundaries held throughout — the branch touches only
`docs/scripts/*.md` and `video/`, never engine code or article content.

### Review history

**Round 1 (sponsor)** — fail, 3 blockers, 10 non-blocking. Code slides overflowed their
panel in deck-03 and deck-07; the opener carried visible artifacts in the sunburst; the
opener's payoff frame did not read its own idea (the drums land GEM-GEM-GEM, the series
title, but the gem was a thin outline diamond with three heavy 7s directly below it, so
the eye took the 7s).

**Round 2 (sponsor)** — fail, 1 blocker. The overflow was fixed at the root: `deck/metrics.ts`
made slide geometry a single solved source and reserves two heading lines whether or not
both are used, so `bodyHeight` is constant across a deck; `fitMonoBlock` constrains on
width and height and takes the smaller. The payoff frame was fixed and lands. The sunburst
did not — **because the sponsor misdiagnosed it in round 1.** Laura called the blotching a
JPEG-intermediate compression artifact; the contractor correctly switched both render paths
to PNG, which fixed real banding, but the blotches were never compression. They were an SVG
bug: `radialGradient` without `gradientUnits` defaults to `objectBoundingBox`, so each of
the 24 wedge paths mapped the gradient into its own bounding box and every ray got a private
bullseye with a transparent center. The fix targeted the wrong cause because the sponsor
named the wrong cause. Recorded here so the next engagement reads it.

**Round 3 (close-out)** — pass. `gradientUnits="userSpaceOnUse" cx=0 cy=0 r=100` verified on
the rendered opener at frames 100 and 120, raw and shadow-lifted: the wedges now converge on
one origin and fade outward, with none of the round-2 clots. deck-08's heading orphan and the
mono exponents both fixed — and the contractor caught the same exponent defect in ch05, which
was not in the change request.

### Verified claims

Every hex in `video/src/tokens/index.ts` traces to `CSharp/web/src/tokens.css`, except two
now marked VIDEO-ONLY in the file's header with the reason. `orcaDive.ts` REEL_ONE and
REEL_STOPS are byte-identical to `CSharp/games/orca-dive.json`. Chapter 9's figures trace to
`docs/articles/09-optimization.md`. Typecheck clean.

### Carried forward — two out-of-scope source defects

Both found during the factual pass, both outside this contract's write scope, neither fixed:

- `docs/articles/07-games-as-data.md:440` — "31" should be 32.
- `CSharp/tests/MMP.SlotGame.Tests/MultiRowWindowTests.cs:104-107` — comment claims an
  exactness the test does not establish.

The STOP condition worked as designed: contradictions were reported rather than resolved
unilaterally. These need an owner with engine/article write access.

### Non-blocking, accepted as-is

In-deck mechanism text runs small at 1080p (the ch3 strip's stop indices are the worst
case); `chapters.ts` is ~700 lines and should split per-chapter before the file doubles;
cutaway framing is inconsistent between animations (ch01/ch03 leave a dead lower third,
ch05/ch09 fill); the deck title slide is top-weighted. The all-mono register in the
animations and the on-screen repo-path provenance are both deliberate and now recorded in
`video/README.md`.
