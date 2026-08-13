# Episode 9 — Optimize the Machine You Proved

## Recording goal

Show optimization as a measured follow-up to a completed system. Keep the original and
optimized `DrawWindow` methods on screen together, run the live comparison, and include the
experiments that lost.

## 0:00-2:30 — Establish the rule

Open the episode 8 tests, then `PerformanceBaselineTests.cs`.

Say: "A benchmark cannot tell us whether the answer is right. The first eight episodes give
us the answer. Today we make the same answer arrive sooner."

Explain Release mode, repeated in-process samples, warmup, and medians. Do not compare one
Debug run with one Release run.

## 2:30-7:00 — Count the hot operation

Draw five reels with three visible positions.

```text
10,000,000 spins
50,000,000 random stop selections
150,000,000 visible-cell writes
```

Open `DrawWindowBaseline`. Point to the remainder operation and the full `Symbol` assignment.
The code is clear and correct. That made it the right first implementation.

## 7:00-11:00 — Remove wraparound from the loop

Draw `A B C D E A B`. Start the window at D and read `D E A` without a remainder.

State the cost: two appended entries per reel for a three-position window, or at most four
under the current five-position limit. The physical strip remains unchanged.

## 11:00-14:30 — Draw IDs for the evaluator

Open `Symbol`, `DrawWindowIds`, and the worker allocation. The UI needs names and flags; the
evaluator needs bytes. Show the equivalence test before showing throughput.

## 14:30-18:00 — Run the web lab

Open `#/ch09`, select Video5x64, and run two million windows per trial. Five trials alternate
which implementation runs first, so each one gets an equal share of the warmer CPU.

Read the shared checksum first. Both implementations hash every symbol id they draw, and the
panel prints that value only when the two hashes agree. A disagreement returns an error
instead of a result, so there is no speedup number to read on a run that drew different
streams. Then compare the median bars, random selections, visible writes, and memory cost.

Repeat with Orca Dive. Mention that a browser-triggered benchmark is a teaching measurement,
not a laboratory-grade hardware comparison.

## 18:00-21:00 — Constant RNG setup and dense pays

Show the precomputed range and rejection threshold. Then show the dictionary and dense payout
view. Both are construction-time transformations; neither changes the game data.

## 21:00-25:00 — Build the PAR outcome table

Put the five stop numbers on screen as bytes: `0C 1C 04 11 19`. Pack them into
`0x0C1C041119`.

Say: "Those five stop numbers determine the full screen. During construction, we already
know what that screen pays and whether it starts a feature. During a spin, we only need the
same five numbers and one lookup."

Open `WinningOutcomeTable`. Show the value's three fields: total multiplier, winning
paylines, and triggered features. Then use Orca Dive's all-zero key. It pays nothing on the
line but starts `PenguinBonus`. This is why the table stores useful outcomes, not merely
positive payouts.

Explain the loader split. `TryLoad` checks candidate data without doing fourteen million
calculations. `LoadFile` constructs a playable game and builds the table before returning.

Then show the measured correction. The single large dictionary managed 1.97 million
outcomes per second. The original evaluator managed 13.78 million. Packing the key was
cheap; fetching random entries from a large dictionary was not.

Open `ProgressiveOutcomeTable`. Start with the 754 reel-0/reel-1 pairs. Only 336 can still
pay or trigger anything, so 418 prefixes stop evaluation there. The remaining arrays narrow
the state once per reel.

Put the equal-work medians together:

```text
rules          16.07M outcomes/second
packed key      2.14M outcomes/second
progressive    20.53M outcomes/second
```

The progressive path beat the rules by 27.7 percent and the dictionary by 9.58 times. Its
complete multicore simulation median reached 158.64M spins per second after the JIT warmed.
Show all five samples, including the first two cold samples. They explain why the episode
uses repeated trials and a median.

## 25:00-28:00 — Show what lost

List the measured results for unrolling, flattened reel storage, and forced inlining, then the
one that came back neutral: skipping the positive-bound validation. Open
`docs/_editing/csharp-performance-audit-2026-08-12.md` for the exact batches.

Say: "The JIT declined several suggestions by running them slower. We listened."

## 28:00-29:00 — Branch discipline

Show the proposed history:

```text
initial-system commit
  └─ codex/optimization-lab
       ├─ benchmark harness
       ├─ wrapped drawing strips
       ├─ byte-ID windows
       ├─ dense payout lookup
       └─ PAR stop-outcome table
```

Create the branch only after the initial-system work is committed cleanly.
