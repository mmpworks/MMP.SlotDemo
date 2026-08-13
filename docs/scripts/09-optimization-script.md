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

Open `#/ch09`, select Video5x64, and run two million windows per trial. Read the correctness
gate first. Then compare the median bars, random selections, visible writes, and memory cost.

Repeat with Orca Dive. Mention that a browser-triggered benchmark is a teaching measurement,
not a laboratory-grade hardware comparison.

## 18:00-21:00 — Constant RNG setup and dense pays

Show the precomputed range and rejection threshold. Then show the dictionary and dense payout
view. Both are construction-time transformations; neither changes the game data.

## 21:00-24:00 — Show what lost

List the measured results for unrolling, flattened reel storage, forced inlining, and local
field caching. Open the dated performance note for exact batches.

Say: "The JIT declined several suggestions by running them slower. We listened."

## 24:00-25:00 — Branch discipline

Show the proposed history:

```text
initial-system commit
  └─ codex/optimization-lab
       ├─ benchmark harness
       ├─ wrapped drawing strips
       ├─ byte-ID windows
       └─ dense payout lookup
```

Create the branch only after the initial-system work is committed cleanly.
