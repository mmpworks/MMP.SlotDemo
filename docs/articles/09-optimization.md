# Optimize the Machine You Proved

The first eight episodes build a correct slot simulation. They use exact money, stable
random streams, ordered reel strips, checked game definitions, analytic math, and independent
tests. That order matters. Without the proven version, an optimization benchmark can tell us
which wrong answer arrives first.

This episode keeps the original `DrawWindow` implementation beside the production version
and runs both from the same seed. The live lab refuses to report a speedup unless their
checksums match.

## Start with a Release baseline

Measure the complete operation that matters. The project runs five samples inside one test
process, sorts their rates, and reports the median. The first sample often includes tiered-JIT
and dynamic-PGO warmup, so a single process launch is a poor benchmark.

The initial measured preset path reached about 43.5 million spins per second in one cold
Release sample. After removing remainder operations from window construction, repeated warmed
samples established a 75.5-million-spin-per-second median. These numbers describe one
development machine; they are regression markers, not hardware-independent promises.

## Initial window construction

One random stop fills several visible positions on a reel. The direct cyclic formula is:

```csharp
window[windowOffset + row] = strip[(stop + row) % strip.Length];
```

For a five-reel, three-position game, ten million spins perform 50 million random stop
selections and write 150 million visible cells. The formula therefore performs 150 million
remainder operations.

The initial path also writes complete `Symbol` values. `Symbol` contains a byte ID, a name
reference, and two flags. The simulation evaluators immediately reduce every cell back to its
ID.

## Extend each drawing strip by one window

`StripReelSet` now appends `Rows - 1` wrapped entries when it is constructed:

```text
physical strip:  A B C D E
drawing strip:   A B C D E A B
```

A three-position window beginning at D reads `D E A` as a contiguous slice. The physical
strip remains unchanged for stop counts, inspection, and probability calculations.

The memory cost is small because the engine supports three- to five-position windows. Each
reel gains two to four drawing entries, regardless of whether its physical strip contains 22
or 128 stops.

## Give the worker the representation it uses

The UI needs symbol names and flags. The spin evaluator needs IDs. `StripReelSet` therefore
keeps two execution views:

```csharp
public void DrawWindow(ref SpinRng rng, Span<Symbol> window)
public void DrawWindowIds(ref SpinRng rng, Span<byte> window)
```

Workers allocate one byte window and overwrite it for every spin. Diagnostic and teaching
code can still request the full symbols. An equivalence test starts both methods with the
same RNG state and compares the resulting IDs.

Three repeated preset medians after this change were 104.5M, 107.3M, and 105.0M spins per
second. The middle result is roughly 39 percent above the previous 75.5M baseline. Orca Dive,
including its wild and scatter checks, reached a 92.8M median, with warmed samples above 100M.

## Move constant RNG work to construction

Lemire's bounded selection uses a range and rejection threshold. Both depend only on the
reel's stop count. Computing the threshold during every selection repeats a remainder
calculation millions of times. `StripReelSet` now calculates both values once per reel and
passes them to the RNG's internal hot-path method.

The public `NextInt(int bound)` retains validation for general callers. Construction rejects
empty strips before the optimized path becomes available.

## Compile a dense payout view

The preset paytable remains a dictionary because `(symbol, run length) -> money` is readable
and convenient to inspect. The spin loop uses a small dense array built from that dictionary:

```text
index = symbolId * countStride + runLength
```

This replaces tuple hashing with bounds checks and an array read. The improvement was modest:
three medians were 101.2M, 107.2M, and 111.7M spins per second after the byte-window change.
The representation stayed because it improved the center result and preserved the dictionary
as the public source of truth.

## The failed experiments belong in the lesson

Several plausible changes lost:

- Separate unrolled methods for three-, four-, and five-position windows fell to about 72M
  spins per second.
- One flattened drawing array with reel offsets produced inconsistent 71M-76M medians and
  did not beat the jagged arrays reliably.
- Forced `AggressiveInlining` reduced medians to about 71M-72M.
- Copying common fields into locals also ran slower in the whole simulation.
- Removing only the positive-bound validation was neutral.

The current strips are tiny and cache-friendly. Extra offset tables and larger generated code
cost more than the indirections they attempted to remove. Modern .NET's JIT also made better
inlining decisions than the manual hints.

## Try the paired benchmark

Open `#/ch09`. Choose a preset or PAR-loaded game, a seed, and a window count. The server:

1. Warms both implementations.
2. Alternates which version runs first.
3. Uses the same seed and work count.
4. Compares checksums.
5. Reports five samples and their medians.

The page displays random selections and visible-cell writes so the scale remains concrete.
Run it more than once. Laptop power state, other processes, thermals, and the JIT can move a
short benchmark substantially.

## Branch after the proven system

The clean teaching history is one commit containing episodes 1-8 and their verified initial
implementation, followed by an optimization branch such as `codex/optimization-lab`. Each
optimization should be a small commit with its benchmark result. Losing experiments can be
reverted while their measurements remain in the article and teaching notes.

Do not create that branch in a worktree containing unrelated active edits. Establish the
initial-system commit first, then branch from that exact point.

*Source files: `Reels/StripReelSet.cs`, `Simulation/SpinRng.cs`,
`Paytables/Paytable.cs`, `tests/MMP.SlotGame.Tests/PerformanceBaselineTests.cs`, and
`SlotDemo.Server/Chapters/ChapterNineEndpoints.cs`.*
