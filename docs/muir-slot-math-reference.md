# Slot Math Reference — Muir, *Elements of Slot Design* (3rd Edition)

A page-indexed summary of the mathematics in Robert Muir's *Elements of Slot
Design*, 3rd edition (Game Design Automation Pty Ltd, 2013-2023; chapter 3 by
Mark Sinosich of Imagine Numbers). This file states each method in our own
words and points at the page and equation numbers in the book. Buy the book
at GameDesignAutomation.com; this summary is a map, and it replaces nothing.

The engine in this repository implements these methods. The last column of
each table names the code that carries the idea.

## 1. The core quantities (Chapter 2, pages 4-7)

| Quantity | Formula | Book ref | In this repo |
|---|---|---|---|
| Probability of a line combination | product of per-reel symbol probabilities | Eq. 1, p. 4 | `GameAnalyzer` weighted enumeration |
| Average win of a rule | prize x probability | Eq. 2, p. 4 | `GameAnalyzer` |
| RTP | average win / average bet x 100% | Eq. 3, p. 4 | `GameAnalysis.TotalRtp` |
| Hits over cycle | p = hits / cycle, cycle = product of strip lengths | Eq. 5, p. 5 | `GameDefinition.StopCombinations` |
| Hit rate | 1 / probability = cycle / hits | Eq. 6-7, p. 6 | `GameAnalysis.HitFrequency` (inverse) |
| Hit frequency | 100% / hit rate | Eq. 8, p. 6 | `GameAnalysis.HitFrequency` |

Two working rules sit under everything (pp. 5-7):

- **Count hits, not probabilities.** Both give the same answer; integer hit
  counts are easier to check and carry no rounding.
- **RTP is additive per pay rule.** The game's RTP is the sum of each rule's
  prize x hits / (cycle x bet). Tune one rule at a time.

Left-to-right rules count "not the symbol" on the reel that ends the run:
hits(left 2 A) = 1 x 1 x (10 - 1) in the book's 3x10 example (Eq. 11, p. 7).
"Any"-position rules sum their mutually exclusive combinations (Eq. 13-16,
p. 7).

## 2. Scatters (Chapter 2, pages 8-9)

- A scatter hits anywhere in the visible window, so each instance on a strip
  contributes window-height hits per reel: hits({S,S,S}) = 3 x 3 x 3 = 27 on
  three 10-stop reels with one scatter each (p. 8). A 4-high window gives 4
  hits per instance (p. 8).
- The "not scatter" count is stops where NO scatter shows in the column
  (p. 8).
- **Stacked or adjacent scatters change the math** (pp. 8-9): total hits stay
  the same but the not-scatter count changes, so the reel layout itself can
  move RTP. Standard practice: keep scatters separated on the strip so at
  most one shows per reel, and verify the assumption when strips change.

In this repo: `StripReelSet.WindowSymbolCount` / `WindowCountDistribution`
count the window occurrences per stop; the PAR tests pin the resulting
integers.

## 3. Wilds (Chapter 2, pages 9-12)

- A wild extends a symbol's hit count on its reel: {A,A,{A,W}} counts A-or-W
  on that reel (pp. 9-10).
- Pay-doubling wilds are handled by counting the wild-assisted combinations
  as their own rules with their own prize (Eq. 17, p. 10).
- Expanding (scattered) wilds can double-count a window; the book's `merge`
  concept collapses those to one hit (p. 11).
- **Prioritisation (highest win pays) requires discounting** (pp. 11-12): a
  window like {W,W,A} matches both left-3-A and left-2-B, so the lower rule's
  hits must subtract the overlap. The book's example drops from 860% to 780%
  RTP when discounted (Eq. 18-22) — skipping this step overstates RTP badly.

In this repo: `WinEvaluator` implements best-win-per-line with the
continues/requires split; `MuirCrossCheckTests` reproduce the book's hand
method against Orca Dive.

## 4. Paylines, ways, windows (Chapter 2 pp. 12-14; Chapter 3 pp. 17-20)

- **RTP is payline-count invariant** for line pays (p. 12): hits and bet both
  scale linearly with lines, so Excel models one line. Scatter prizes are
  multiplied by the bet to keep their RTP constant (p. 12).
- Maximum paylines = product of per-reel window heights: 3^5 = 243 for 5x3
  (pp. 12-13).
- **243-ways games** (Ch. 3, pp. 17-18): count a symbol anywhere in the
  column, left to right. A ways win absorbs its subsets — a 5-of-a-kind is
  one win, not coinciding 3/4/5 wins (the subset rule, p. 17, Figure 2
  p. 18). A ways game computes like a one-line game with every symbol
  scattered — valid only while no wild sits on the leftmost reels (p. 18).
- **Wild on reel 1 breaks the shortcut** (pp. 19-20): wild-made ways overlap
  across symbols, and the lower-paying symbol's hits must be discounted by
  the all-wild ways (hits(5B) minus hits({W,W,W,W,W}), p. 20). Design
  guidance: keep wilds off the leftmost reels.
- Ways games bet 25-30 credits and avoid 2-of-a-kind rules (pp. 18-19).
- Irregular windows change only scatter counts, never line pays (p. 14).
- Virtual reels (Telnaes, US 4448419) weight physical stops; per-payline RTP
  then differs unless the strip is balanced around pivot points (pp. 13-14).

## 5. Free games and features (Chapter 2 pp. 14-16; Chapter 5 p. 22)

| Quantity | Formula | Book ref |
|---|---|---|
| Expected free spins, no retrigger | E = n1 p1 | Eq. 24, p. 15 |
| Combined win | F = B0 + n1 p1 B1 | Eq. 25-26, p. 15 |
| Expected free spins with retrigger | E = n1 p1 / (1 - n2 p2) | Eq. 27, p. 15 |
| Final RTP with retrigger | (B0 + B1 n1 p1 / (1 - n2 p2)) / bet | Eq. 28, p. 15 |
| Pick-feature average win | number of choices x average prize value | Eq. 23, p. 15 |

The retrigger series is geometric: each free spin adds n2 p2 expected spins,
so the multiplier 1 / (1 - n2 p2) closes the infinite sum. Feature sigma
terms scale by the retriggered expectation, not the first-trigger value
(p. 35).

In this repo: `GameAnalyzer` prices the pick bonus analytically
(`PickBonus.Mean` / `MeanSquared`); the free-spin engine path closes the same
series.

## 6. Volatility and verification (Chapter 2 pp. 15-16; Chapter 7 pp. 25-29)

- Standard deviation over all prizes: sigma = sqrt(sum p_i (x_i - mean)^2)
  (Eq. 29, p. 15). Feature-prize probabilities multiply by the expected
  free-spin count (pp. 15-16, worked at Eq. 43-47, pp. 35-36).
- Zero-win outcomes belong in sigma but cannot be computed analytically when
  wins coincide (p. 16) — a known limit of spreadsheet models.
- Confidence range after N games: rtp +/- volatility_index / sqrt(N), with
  volatility index 1.64 sigma (90%), 1.96 sigma (95%), 2.58 sigma (99%)
  (Eq. 30-31, p. 16).
- **Coinciding wins** (pp. 25-26): theory prices each rule independently; the
  player experiences the sum. Only full-cycle or Monte Carlo simulation sees
  the combined distribution; full-cycle cannot span a feature series, Monte
  Carlo carries sampling error (p. 26).
- **Build theory and simulation independently and reconcile** (p. 25) — two
  differently built models rarely share the same bug. Verify the shipped
  product by logging stops and wins and diffing against the model
  ("Compare Server Play", p. 29), never by staring at aggregate RTP.

In this repo: `GameAnalyzer` is the theory side; `SimulationEngine` +
`GameRunner` are the simulation side; `GameAnalysis.SigmaPerUnitWagered`
carries the coinciding-aware sigma for single-line games; the PAR tests are
the reconciliation.

## 7. Progressives (Chapter 6, pages 23-24)

| Quantity | Formula | Book ref |
|---|---|---|
| Mystery average trigger point | (startup + max) / 2 | Eq. 32, p. 24 |
| Mystery average turnover to win | (max - startup) / (2 x increment) | Eq. 33, p. 24 |
| Mystery average win | (startup + max) / 2 | Eq. 34, p. 24 |
| Mystery RTP | (max + startup) x increment / (max - startup) | Eq. 35, p. 24 |
| Game-triggered RTP | trigger probability x startup + increment | Eq. 36, p. 24 |

Key insight (pp. 23-24): the increment is money not yet won, so trigger
frequency is RTP-neutral for the increment stream; only the startup value
and its frequency move RTP.

## 8. State-heavy games (Chapter 4 p. 21; Chapter 3 p. 20; Chapter 9 pp. 40-45)

- Metamorphic games carry state between spins. Size the state space first: a
  5x3 sticky-wild window has 2^15 = 32,768 wild patterns; 5x4 has over a
  million (p. 21). If enumeration is impractical, simplifications carry the
  error risk — verify them by simulation.
- Random window sizes multiply the model: six reels of 2-7 rows = 6^6 =
  46,656 window shapes (p. 20).
- The book's worked example (Ch. 8, pp. 31-36) walks a full 5x3
  wild+scatter+retrigger game through Excel: left-4 hits with wilds (Eq. 37,
  p. 32), any-4 scatter hits (Eq. 38, p. 33), final RTP 93.867699%
  (Eq. 42, p. 34), retriggered free-spin expectation (Eq. 45, p. 35), and
  sigma 14.1848 (Eq. 47, p. 36). It is the best template for checking an
  engine by hand.

## Page index

| Pages | Content |
|---|---|
| 4-7 | Probability, RTP, hits/cycle, hit rate, left-to-right and any rules |
| 8-9 | Scatters, window-area counting, stacking hazard |
| 9-12 | Wilds, expanding wilds, prioritisation discounting |
| 12-14 | Multiline invariance, virtual reels, irregular windows |
| 14-16 | Pick features, free games, retrigger series, sigma, confidence |
| 17-20 | 243 ways (Sinosich): subset rule, wild-on-reel-1, random windows |
| 21 | Metamorphic games, state-space sizing |
| 22 | Pick features worked in a spreadsheet |
| 23-24 | Progressives: increment, startup, mystery vs game-triggered |
| 25-30 | Simulation vs theory, coinciding wins, product verification |
| 31-39 | Full worked game analysis, Excel and Slot Designer |
| 40-45 | Slot Designer SD3/SD4 language examples |
