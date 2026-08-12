# Current State
_as of 2026-08-12 (post editor-pass)_

## What we're building right now
MMP.SlotDemo is the companion site for the *Building a Slot Machine RTP Simulator*
series: a .NET 10 server + Vue 3/Vite SPA where every episode has a page whose labs
run the real engine (`CSharp/src/MMP.SlotGame.Core/`, imported verbatim from
MMP.SlotGame) and narrate each step through Herald into an on-page log stream. The
original PRD (`docs/PRD.md`, v1 accepted 2026-08-10) framed the repo as a reusable
project harness — that harness is still the skeleton underneath, but the live work
is the episode site, the Orca Dive game, and the proving ground.

## Active decisions
- 2026-08-11 — Orca Dive is the anchor game for the labs; the ch4 solver lab
  re-prices Orca Dive's own published paytable by default (presets remain
  selectable), mirroring how a cabinet's approved payback versions are produced.
- 2026-08-11 — Hit frequency names its event: the PAR summary field and column are
  `lineHitFrequencyPercent` (line hits only, 10.26% for Orca), so it cannot be
  confused with the Harrigan-style any-award convention (11.45%).
- 2026-08-11 — The repo is a public-release surface. Internal review docs are
  removed before release (`docs/reviews/clancy-copy-pass-2026-08-11.md` was
  deleted in `abc2df1`); site prose has been through a Clancy→Cussler humanize pass.
- Long-running convergence/stress tests are SKIP-gated, never weakened: set
  `SLOTGAME_SLOW_TESTS=1` to run them (`CSharp/tests/MMP.SlotGame.Tests/Support/TestTiers.cs`).
- Herald.OSS runs in native mode with a custom 10-level set; `SLOTDEMO_LOG_INGEST_URL=`
  (empty) drops the relay sink, which is what the test host does.

## Open questions
- `docs/chapter-01-blueprint-concept.md` describes an interactive Chapter 1
  "Blueprint" page (clickable SVG system map that routes into each chapter). No
  `Chapter01.vue` exists — only 02–07 plus `Finale`, `Library`, and `ParSheet`. Whether
  ch1 gets built as a page, and when, is undecided.
## Next action
2026-08-12 (later): the full editor cycle landed as `6d1811e` (local, unpushed).
The article series, video scripts, and PAR reference migrated in from MMP.SlotGame
(`01acaef`; pointer left there); `docs/architecture.md` ported and adapted to the
shipped system. Four Clancy→Cussler waves ran (findings + dispositions in
`docs/_editing/`, gitignored): facts/references/register, script navigation
layer, a 288-site subtraction pass per the MMP.Humanization runbook
(`E:\dev\MMP.Humanization\RUNBOOK.md` — load it before ANY prose pass), and the
article-01 rewrite as the series front door with a chapter-by-chapter roadmap.
README's hand-maintained test counts were removed rather than fixed. Verified
green after the pass: engine 197+9 gated, server 80, SPA 45, build 0 warnings.
First action for whoever picks this up: push `main` when Steve says publish, and
decide the Chapter 1 blueprint page question above.

## Stop condition
Halt and return to Steve when: (a) work would add a new chapter page or change the
site's episode structure; (b) a change touches published numbers — RTP, hit
frequency, PAR sheet figures, band math — since those are cited on camera; (c) a
test would need its assertion weakened or a skip gate widened to pass; (d) the
Chapter 1 blueprint question needs an answer rather than a proposal.

## Needs approval
- **Steve's sign-off:** anything pushed to `origin` (public repo, `mmpworks/MMP.SlotDemo`),
  any new episode page or navigation change, any change to published RTP/PAR/band
  numbers, and adding internal review or WIP docs to a public-release surface.
- **Agent can just do it:** bug fixes with a regression test, stale-doc corrections,
  test additions, refactors that keep the suites green, local commits on a branch.
