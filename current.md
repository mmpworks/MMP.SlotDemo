# Current State
_as of 2026-08-19_

## 2026-08-19 session

- **TEACH ME section added.** The nine articles are now readable on the site, in their own
  nav band separated from the labs by a rule: the labs are things you run, TEACH ME is
  things you read. `docs/articles` stays the single source and is copied to the server's
  output at build time, served by `/api/articles`, rendered with `marked`. Navigation runs
  both ways: every article offers "Run this chapter's lab", every chapter page offers
  "Read the article for this chapter". The pairing lives in `web/src/teach/pairing.ts`
  with tests, because it is not a clean 1:1 (episode 1 has no lab; PAR and the proving
  ground have no article). `#/teach/<article-id>` deep-links to one article, which needed
  the hash router to read only the FIRST segment as the page id.

- **Independent verification lives in `python/verification/`.** A Python
  re-implementation of Orca Dive's math that shares no code with the engine: it reads
  `games/orca-dive.json`, recompiles the pay categories, enumerates all 14,781,416 stop
  combinations, and prices the pick bonus by exact subset-sum rather than the engine's
  closed form. It agrees with the engine on every published figure, and its sigma values
  are now pinned in `OrcaDiveParSheetTests`. `coverage.py` measures the confidence band's
  real coverage (98.86/98.96/98.94% against a nominal 99%); `skew.py` shows why that was
  worth checking, since 26% of line variance sits in a 4-in-14.8M outcome. Requires numpy.

- **Curve sampling fixed.** The telemetry pump slept 100ms between drains against a
  bounded drop-oldest channel, so sampling was wall-clock driven while the recorder's
  stride is spin driven. Charts hitched between dense clusters and straight jumps, and
  once the engine outran one sleep a 10M run lost its whole early history. The pump now
  drains continuously and throttles only the SSE readout. Measured: 400 points for 400
  strides, first point one stride in, widest gap one stride.
- **Throughput split in two.** `EngineClock` brackets the workers alone; `ObservedClock`
  covers request to terminal status including streaming. Published as a `throughput`
  block. A run watched in a browser read 6.0M spins/s while the same work headless
  measured 105M; the engine was never slow, the readout was charging streaming to it.
- **Shipped games can be re-priced to a target RTP.** `GameDefinition.WithScaledPays(k)`
  returns a re-priced copy; `/api/run` takes `targetTotalRtpBasisPoints`. Geometry is
  untouched so hit frequency is identical; the feature keeps its own prize table and is a
  floor under the total. Verified on Orca Dive across 7500-9900 bp with bonus RTP fixed at
  26.510% throughout. Out-of-limit targets are refused, never clamped.
- `/api/par/summary` 500 fixed (First() on the empty CombinationCounts a multi-payline
  game reports by design). PAR endpoints had no server tests before this.
- Lab control rows aligned; seed Roll button added.

## 2026-08-16 session

- ch02 gained Lab 3 (die-first rejection demo: full enumeration + 64-bit
  BigInt Lemire explorer); BiasLab is Lab 4. Articles 1-2 got Cussler's
  ELI5/humanize pass (facts/tables/code frozen); series count fixed to nine;
  article 2's stale cross-refs now point at articles 6 and 8. Article 9
  records the declined mask-vs-Lemire strategy pattern with its four issues.


- Decision: MMP.SlotDemo keeps ALL its video and image assets in this repo.
  MMP.Media is no longer used for SlotDemo media.
- New doc: `docs/run-walkthrough.md` — step-by-step walk of one simulation run,
  from harness startup through the SPA to the verdict, with a file map.
- Article 1 gained a "One run, step by step" section and a restructured
  chapter map (table + per-article deep-dive summaries).
- New tldraw canvases in `docs/design/`: simulation-engine mindmap and
  architecture drawing.
- Article 1 additions: "Nobody is playing this game" (spins are math units),
  the stadium clicker/radio scene for counters vs. telemetry, and
  "Scaling, not clamping" (single frozen scale factor; realized-RTP is the
  measured reference; industry payback-version practice).
- Article 2 gained "Modulo bias, counted": pigeonhole explanation, a real
  10M-draw experiment (8-bit source → 26 bins, 11% skew, rejection flattens
  it), the power-of-two divisibility rule, TRNG-doesn't-help note, and why
  outcome-blind discarding is accepted by test labs.
- "Cap" renamed to **the solver's RTP limits**, and a floor added:
  `SimulationConfig.MinAggregateBasisPoints = 7_500` (Nevada's 75% legal
  minimum) beside the 9,900 bp ceiling. Both bound the request only — the
  simulator pretends to be a casino floor; validation never consults them.
  Realized-RTP re-check now guards both ends (floor gets 1 bp rounding grace;
  ceiling stays strict). `/api/run/limits` exposes `minAggregateBasisPoints`;
  Finale.vue previews both bounds. Solver math tests retargeted into the
  admitted range (7600/9500 ×1.25 linearity pair). NOTE: this edits
  MMP.SlotGame.Core, the verbatim import from MMP.SlotGame — upstream needs
  the same change or the mirror diverges. 213+91 C# tests and 51 SPA tests
  green.
- New industry acceptance check in source: `ConvergenceRecorder.IndustryCheck`
  (±0.5pp deviation from analytic over ≥10M spins, a common lab convention rather
  than a clause quoted from GLI-11), surfaced in
  `Describe()` as `industry`, in the completion log line, and as a second
  verdict banner on `Finale.vue`. Below 10M spins the check reports
  not-qualified rather than a verdict. Three new recorder tests; 91 server +
  51 SPA tests green.

## Performance and American spelling audit

- Release baselines: about 43.5 million spins/second for a standard preset and 17.8
  million spins/second for Orca Dive on the current development machine.
- The per-spin loop remains allocation-free. Buffers are allocated once per worker and
  shared counters update once per 4,096-spin batch.
- `GameRunner` can reuse the exact analysis already prepared by `RunCoordinator`, avoiding
  a second enumeration after a loaded-game run.
- `ReelPreset.StopCounts` is cached instead of allocating on every read.
- Reel construction appends the short wrapped prefix needed by one window. Window drawing
  now reads contiguous entries and performs no modulo operation per visible cell.
- The new five-sample Release baseline is 75.5 million preset spins/second median. The
  first warmup sample was 53.3 million, about 22.5% above the earlier cold sample.
- Simulation now draws compact byte-id windows while UI and diagnostic paths retain full
  symbols. Repeated preset medians are approximately 104M-107M spins/second, with a later
  dense-paytable batch reaching a 107M center result. Orca Dive's new median is 92.8M.
- Reel RNG ranges and rejection thresholds are calculated once per reel. Preset paytable
  payouts use a dense construction-time lookup rather than tuple hashing in the spin loop.
- Project prose and construction-phase identifiers now use American spelling. The shared
  MMP.Humanization runbook records American English as the house default.

## Episode 9 optimization follow-up

- Articles and chapter pages 1-8 now carry a short optimization notebook entry, but defer
  implementation until after the initial system is complete and independently verified.
- Article, script, API, and Vue page 9 compare the original full-Symbol/modulo
  `DrawWindow` with the production byte-ID/extended-strip implementation.
- The live benchmark uses equal seeds and work counts, alternates trial order, compares
  checksums, and reports medians plus random-selection and cell-write counts.
- Recommended history: commit the initial system, then create `codex/optimization-lab`.
  No branch was created in the current dirty worktree.

## PAR-configurable paylines and reel strips

- `Payline` and `ReelPreset` are data-focused types.
- `StandardPaylines.cs` owns the old generated line shapes; `StandardReelPresets.cs`
  owns the old demo reel recipes.
- The game-definition loader builds `Payline` and `StripReelSet` directly from
  PAR-transcribed JSON, including custom line paths and unequal strip lengths.
- Chapter 3 labels whether its geometry came from a game file or the demo catalog.
- `ReelPreset` now stores completed strips. Only `StandardReelPresets` uses
  `EvenlySpacedStripBuilder`; exact PAR stop order bypasses generation.

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
2026-08-12 (parameterized reel sets): `StripReelSet` now accepts read-only nested
collections and copies them into private jagged arrays. `ReelPreset` now owns one
`ReelStripSpec` per reel, removing the old machine-wide `StopsPerReel` assumption.
Chapter 3 Lab 3 compares immutable 26- and 36-stop snapshots and repeats one with the
same seed. Core and server tests cover mixed lengths, copies, replacement, and the API.

2026-08-12 (chapter 3 terminology): prose now uses “visible symbol position” for a
top/middle/bottom location on one reel. “Screen row” is reserved for a horizontal band
across all reels. The code property remains `Rows` because it is the matrix dimension;
its XML comment defines both meanings.

2026-08-12 (Orca theme cleanup): removed the remaining source-game branding from
the PAR document. The bonus now presents 24 prize chests and 6 rogue waves; a wave ends
the dive with a safe-return award. Theme-facing web and article prose use the same terms.
Generic engine/schema terms (`blank`, `prize`, `consolation`) remain because the reusable
bonus model is not tied to Orca Dive.

2026-08-12 (tenth-grade teaching pass): all eight articles now introduce their
core idea with a plain worked example or comprehension question. The companion site now
has eight matching chapter routes. New chapter 5 teaches weighted enumeration with a
24-outcome model; the former chapters 5–7 moved to 6–8. Chapters 2–8 include prediction
checks that explain why an answer is right or wrong. `ComprehensionCheck.test.ts` protects
the choose-before-reveal interaction.

2026-08-12 (new episode 5): Steve requested a tenth-grade explanation of every
`GameAnalyzer` method and the `Rtp` directory. Source comments now lead with plain behavior
and concrete examples. Weighted enumeration has its own article and recording script at
`docs/articles/05-weighted-enumeration.md` and
`docs/scripts/05-weighted-enumeration-script.md`. The former episodes 5–7 shifted to 6–8
in both articles and scripts; companion-site route names were deliberately left unchanged.

2026-08-12 (source-comment pass): reviewed comments across production C#, SPA source,
and browser E2E code. Replaced architectural metaphors such as “seam,” “surface,”
“shape,” and “lane” where they obscured concrete behavior; preserved comments that
document units, formulas, ownership, limits, or non-obvious constraints. Teaching examples
for later Claude/Codex passes are in `docs/_editing/source-comment-teaching-2026-08-12.md`.

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
2026-08-13: the Codex-authored `codex/optimization-lab` branch (series renumbered
to 9 articles + optimization lab, byte-id hot path) went through the two-lane house
review (no criticals), a 12-item fix pass (`a99e040`), and a teaching-sync of every
chapter page to the articles' register (Heather; caught 2 more renumber bugs).
Verified green: engine 205 + 11 gated, server 88, web 51, build 0 warnings. Steve
returns to this branch tomorrow; merge is his call. Queued idea: a test that walks
the scripts' `### Block` listings and diffs each against its named source file, so
paste-block sync stops depending on agent discipline — with a pin manifest: six
blocks in scripts 02/03/04/06 deliberately pin the pre-optimization state
(episodes teach the initial system; ep9 shows the optimized versions side by
side), so those diff against the pinned commit, never HEAD. Video assets for
ep1–3 live in `E:\dev\MMP.Media\generated\slotdemo-series\` (callouts wired into
scripts 01–03; MMP.Media + slotdemo-series-video repos committed locally,
unpushed). The Chapter 1 blueprint page question above remains open.

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
