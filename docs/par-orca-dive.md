# PAR reference — Orca Dive

Orca Dive is an original fictional game. Its reel geometry, paytable, and bonus
structure are reconstructed from a public third-party statistical analysis of a
real machine's published combination counts and returns — not from any
manufacturer's internal materials. This document is the validation target for
`games/orca-dive.json`: the analytic calculator and the 10M-spin simulation
must both reproduce the numbers below. The game is DATA, not code, so
everything below is a description of that file.

## Provenance

- **Public source:** Wizard of Odds statistical slot analysis used during the original
  reconstruction. The source game's branding is intentionally omitted from Orca Dive.
- **Fetched:** 2026-08-07
- **Method:** the cited page's author reconstructed reel strips and return
  figures from 212 recorded spins at the Wynn, January 2012, and published the
  resulting combination-count table and return percentages.
- **No company materials were consulted.** No manufacturer PAR sheet, internal
  documentation, ROM data, or other proprietary or confidential source was
  used at any point. Every number in this document traces to the one public
  URL above.
- This citation lives only in this file — the shipped game artifact
  (`games/orca-dive.json`) carries no reference to the source, by design.

## Reel geometry

Reels 1, 3, 5: 26 stops. Reels 2, 4: 29 stops.
Total single-line stop combinations: 26 × 29 × 26 × 29 × 26 = **14,781,416**.

Per-reel symbol counts, reel-major (our own layout — the source presents this
symbol-major; we present it reel-major with a running stop total per row):

| Reel | Stops | Red 7 | Green 7 | Blue 7 | Seal | Wild Orca | Salmon | Herring | Squid | Mackerel | Penguin |
|------|-------|-------|---------|--------|------|-----------|--------|---------|-------|----------|---------|
| R1   | 26    | 1     | 3       | 2      | 2    | 2         | 2      | 4       | 3     | 5        | 2       |
| R2   | 29    | 2     | 2       | 3      | 4    | 1         | 3      | 5       | 6     | 3        | 0       |
| R3   | 26    | 1     | 1       | 1      | 5    | 1         | 4      | 2       | 4     | 5        | 2       |
| R4   | 29    | 2     | 2       | 3      | 4    | 1         | 4      | 5       | 4     | 4        | 0       |
| R5   | 26    | 1     | 2       | 3      | 3    | 1         | 3      | 3       | 4     | 4        | 2       |

Note: the source's line-pay combination counts are reproducible from symbol
counts alone (per-reel multiset probabilities); exact strip *ordering* only
matters for the scatter-window probability, where the source uses the
visible-window approximation covered under "Strip orderings" below.

## Pay table

Per unit TOTAL bet, single line, left-aligned. Grouped by symbol family
(our own arrangement — the source lists all ten categories as one flat table
in paytable order; we group them by kind and use ascending run length):

**Sevens**

| Symbol | 3oak | 4oak | 5oak |
|--------|------|------|------|
| Mixed 7 (any two colors) | 5 | 40 | 100 |
| Blue 7 | 20 | 50 | 200 |
| Green 7 | 25 | 50 | 250 |
| Red 7 | 40 | 100 | 5000 |

**Fish**

| Symbol | 3oak | 4oak | 5oak |
|--------|------|------|------|
| Squid | 5 | 10 | 50 |
| Mackerel | 5 | 10 | 50 |
| Herring | 10 | 25 | 150 |
| Salmon | 10 | 25 | 150 |

**Seal**

| Symbol | 3oak | 4oak | 5oak |
|--------|------|------|------|
| Seal | 20 | 50 | 200 |

**Wild Orca** (pays on its own account at every length)

| Symbol | 1oak | 2oak | 3oak | 4oak | 5oak |
|--------|------|------|------|------|------|
| Wild Orca | 2 | 5 | 10 | 50 | 2000 |

Rules implied by the source's combination counts (refined during
implementation; see "Reconciliation notes" below for how each was pinned
down):
- Wild Orca substitutes for the four FISH only (salmon, herring, squid,
  mackerel), and pays on its own account at 1-5 oak. It does NOT stand in for
  a seal or a 7.
- A fish win needs at least one real fish in the run; an all-wild run is the
  wild paying for itself.
- "Mixed 7" = any left-aligned run of 7s (Red/Green/Blue in any mix) taken
  as its own pay category. Wilds do not extend it.
- Only the single highest-paying interpretation of a line pays (standard
  best-win-per-line rule), and equal pays go to the LONGER run. The source's
  counts are mutually exclusive per category.

## Line-pay combination counts (validation target, out of 14,781,416)

Grouped by symbol family with subtotals, ascending run length (our own
arrangement — the source presents one flat table, symbol rows by descending
run length):

**Sevens**

| Symbol | 3 | 4 | 5 | Total |
|--------|---|---|---|-------|
| Red 7 | 1,144 | 80 | 4 | 1,228 |
| Green 7 | 3,432 | 240 | 24 | 3,696 |
| Blue 7 | 3,432 | 360 | 54 | 3,846 |
| Mixed 7 | 64,064 | 16,960 | 5,210 | 86,234 |
| **Subtotal** | | | | **95,004** |

**Fish**

| Symbol | 3 | 4 | 5 | Total |
|--------|---|---|---|-------|
| Salmon | 48,672 | 8,756 | 1,598 | 59,026 |
| Herring | 63,388 | 14,212 | 2,590 | 80,190 |
| Squid | 107,952 | 18,333 | 4,373 | 130,658 |
| Mackerel | 103,584 | 17,598 | 4,198 | 125,380 |
| **Subtotal** | | | | **395,254** |

**Seal**

| Symbol | 3 | 4 | 5 | Total |
|--------|---|---|---|-------|
| Seal | 26,000 | 3,680 | 480 | 30,160 |

**Wild Orca**

| Symbol | 1 | 2 | 3 | 4 | 5 | Total |
|--------|---|---|---|---|---|-------|
| Wild Orca | 980,200 | 15,080 | 572 | 22 | 2 | 995,876 |

**Grand total: 95,004 + 395,254 + 30,160 + 995,876 = 1,516,294**

Hit frequency: 1,516,294 / 14,781,416 = **10.258%** (~1 in 9.7 spins).
Line-pay expected return: **59.601%** of a one-unit total wager.

## Penguin scatter → pick bonus

- Penguin symbols exist only on reels 1, 3, 5 (2 per 26-stop strip). With a
  3-row visible window and no two Penguin symbols within 3 consecutive stops,
  each reel shows a Penguin symbol in the window with probability 6/26.
- Trigger: a Penguin visible in the window on ALL of reels 1, 3, 5:
  P = (6/26)^3 = **0.0122895** (~1 in 81.4 spins).
- Bonus: open from 30 treasure chests — 24 prizes + 6 rogue waves. A rogue wave
  ends the dive and pays a +1 safe-return award. Prize pool: 1×2, 2×5, 3×1,
  4×1, 5×9, 10×3, 15×2, 20×1 → 24 prizes summing to 144 (avg 6.0/pick).
  Including the six safe-return awards, the pool total is 150.
- Expected total picks through the rogue wave: (n+1)/(p+1) = 31/7 = 4.428571.
  Expected safe picks: 24/7 = 3.428571. Expected safe win: 24/7 × 6 = 144/7 =
  20.571429. Plus the 1× safe-return award = **21.571429** expected bonus win.
- Bonus return: 0.0122895 × 21.571429 = **26.510%** of wager.

Note the source's model treats picks as drawn with the *average* prize value
(expectation is exact under uniform-without-replacement by symmetry — the
expected value of each successive safe pick equals the pool average).

## Total

| Component | Return |
|-----------|--------|
| Line pay  | 59.601% |
| Bonus     | 26.510% |
| **Total RTP** | **86.111%** |
| House edge | 13.889% |

## Strip orderings

The source published symbol COUNTS, not orderings. Line pays depend on counts
alone (one payline cell per reel), so the orderings in `games/orca-dive.json`
are ours. The scatter is the exception: it reads the whole 3-row window, so
the two Penguin symbols on each of reels 1/3/5 are placed at stops 0 and 13, a
cyclic gap of 13 that keeps their 3-stop trigger windows disjoint and makes
each reel show a Penguin with probability exactly 6/26.

The file carries the published table twice over, as `reelStops` and
`symbolCounts`, and the loader verifies the strips against both. That is the
check a hand transcription needs: the strips are 136 entries long, the table is
short, and the table is what the published maths was derived from. A dropped or
mistyped stop fails to load rather than quietly shifting the odds.

## Simulation acceptance targets (10M spins, single line, unit bet)

- Analytic line-pay EV must match 59.601% and the combination-count table
  above **exactly** (integer counts, no tolerance).
- Simulated total RTP must converge into the ±z·σ/√N band around 86.111%
  (engine's existing convergence criterion), with component splits
  (line vs. bonus) individually inside their own bands.
- Simulated hit frequency ≈ 10.258% within statistical tolerance.

## Reconciliation notes

Outcome: **all 32 integer combination counts reproduce exactly**, along with the
1,516,294 total, the 10.258% hit frequency and the 59.601% line-pay return.
There are no unexplained deltas. The pay rules stated above are the ones the
counts imply; they are a refinement of the looser summary this document
originally carried, and each refinement was forced by the counts rather than
chosen to make them fit.

Three rules had to be pinned down. Each was decided by arithmetic on the
published counts rather than by preference.

### 1. Wild Orca substitutes for fish only

The first reading, in which the wild substitutes for every line symbol, overshoots badly.
It gives Seal three-of-a-kind 73,632 against a published 26,000, and Red 7
five-of-a-kind 108 against a published 4. Restricting substitution to the four
fish makes every seal and seven category fall out as a plain product:

- Seal 3oak = 2 x 4 x 5 x (29 - 4) x 26 = 26,000
- Seal 5oak = 2 x 4 x 5 x 4 x 3 = 480
- Red 7 5oak = 1 x 2 x 1 x 2 x 1 = 4

and every fish category as a product over (fish + wild) counts, less the
all-wild line:

- Mackerel 5oak = (5+2)(3+1)(5+1)(4+1)(4+1) - 2 = 4,200 - 2 = 4,198
- Squid 5oak = (3+2)(6+1)(4+1)(4+1)(4+1) - 2 = 4,375 - 2 = 4,373
- Herring 5oak = (4+2)(5+1)(2+1)(5+1)(3+1) - 2 = 2,592 - 2 = 2,590
- Salmon 5oak = (2+2)(3+1)(4+1)(4+1)(3+1) - 2 = 1,600 - 2 = 1,598

The symbol is named Wild **Orca**, so this is the reading the name suggests as
well as the one the numbers force.

### 2. Mixed 7 excludes wilds, and is a category rather than a fallback

Counting sevens per reel (6, 7, 3, 7, 6) and subtracting the pure-color runs
reproduces all three mixed counts directly:

- Mixed 3oak = 6 x 7 x 3 x (29 - 7) x 26 - (2 + 6 + 6) x 22 x 26 = 72,072 - 8,008 = 64,064
- Mixed 4oak = 6 x 7 x 3 x 7 x (26 - 6) - (4 + 12 + 18) x 20 = 17,640 - 680 = 16,960
- Mixed 5oak = 6 x 7 x 3 x 7 x 6 - (4 + 24 + 54) = 5,292 - 82 = 5,210

Wilds play no part in any of these.

#> **Pay basis.** Every pay in this engine is a multiple of the TOTAL spin bet, not of one
> line's share of it. Orca Dive has a single payline, so the two readings coincide here and
> the numbers below are unaffected. They diverge the moment a game has more than one line:
> a two-line game declaring a pay of 5 awards five times the whole bet on each line that
> hits. Real PAR sheets usually quote per-line pays, so a sheet transcribed into this
> format needs its pays converted first.

## 3. Ties go to the longer run

With rules 1 and 2 in place, 28 of the 32 counts matched immediately and the
remaining four were a pure relabelling between Red 7 and Mixed 7, totalling
220 combinations, with the same pay on either reading:

| Line | Red 7 reading | Mixed 7 reading | Combinations |
|---|---|---|---|
| J7 J7 J7 J7 + another 7 | 4oak, pays 100 | 5oak, pays 100 | 20 |
| J7 J7 J7 + another 7 + non-7 | 3oak, pays 40 | 4oak, pays 40 | 200 |

The source books all 220 to Mixed 7, so the tie-break is "longer run wins".
Because the pays are equal either way, this affects only the category split;
the line EV of 59.601% holds under both readings. The same rule resolves the
fish/wild ties consistently (Squid 5oak and Wild 4oak both pay 50, and the
published Squid count includes the four-wilds-then-squid lines), which is
what makes it a single rule rather than two special cases.

### Bonus expectation

The pick bonus needs no reconciliation, but the implementation derives its
moments rather than assuming the average-prize shortcut. Over a uniformly
random order of all 30 treasure chests, prize *i* is collected exactly when it precedes
all 6 rogue waves (probability 1/7), and prizes *i* and *j* are both collected when
they lead the 8-item subset consisting of themselves and the 6 rogue waves
(probability 2/(8x7) = 1/28). That gives

- E[award] = 144/7 + 1 = 151/7 = 21.571429, matching the published figure
- Var[award] = 469.744898

exactly, without enumerating a permutation. The simulation does not use these:
it opens real treasure chests without replacement, and the closed form exists to check it.

### Verified numbers

| Quantity | Published | Computed |
|---|---|---|
| Stop combinations | 14,781,416 | 14,781,416 |
| Winning combinations | 1,516,294 | 1,516,294 |
| Hit frequency | 10.258% | 10.2581% |
| Line-pay return | 59.601% | 59.6011% (8,809,880 / 14,781,416) |
| Bonus trigger rate | 0.0122895 | (6/26)^3 exactly |
| Bonus return | 26.510% | 26.5102% |
| Total RTP | 86.111% | 86.1112% |

## Volatility index: which z, and why

The VI column is quoted at the 90% confidence level, matching Harrigan and Dixon so the
figures can be read side by side. Their Table 2 states the calculation and works an
example:

> Volatility index = (z-score for confidence interval) * (standard deviation of the game)
>
> z-score for a 90% confidence interval is: 1.65
>
> Volatility index: 10.476 i.e., 6.349285 x 1.65

Two details worth keeping straight. Their z is the rounded 90% value; the exact one is
1.6449, which would turn that 10.476 into 10.444 and change the printed digit. And the
interval it feeds is payback plus or minus VI divided by the square root of plays, which
is the same z*sigma/sqrt(N) the proving ground draws, written the way a PAR sheet writes
it.

Verified against the paper 2009-06, Journal of Gambling Issues issue 23, Table 2, on
2026-08-19.
