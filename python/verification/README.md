# Independent verification

An independent re-implementation of Orca Dive's math, written to check the C# engine from
the outside.

The engine already checks itself: chapter 7 enumerates every outcome exhaustively and
compares that against a simulation. That is a valuable check and it is also a closed loop,
because both halves are the same codebase. If a shared assumption is wrong, the two halves
agree with each other and are both wrong together.

These scripts share no code with the engine. They read `CSharp/games/orca-dive.json` and
recompute everything from the JSON: they recompile the pay categories the way
`GameDefinitionBuilder` does, enumerate all 14,781,416 stop combinations, and price the
pick bonus with an exact subset-sum treatment rather than the engine's closed form. When
these agree with the engine, the agreement means something.

They were written during the 2026-08-19 correctness review and are kept because a check you
cannot re-run is a claim, not a check.

## Running them

```bash
pip install numpy
cd python/verification
python orca_check.py
python coverage.py
python skew.py
```

`numpy` is the only dependency. Each script prints its results and exits; none of them
write files or touch the engine.

## What each one answers

### `orca_check.py` — does the engine's PAR sheet reproduce from the game file alone?

Enumerates the full cycle, counts every winning combination by category and run length,
computes the exact bonus moments, and derives line RTP, total RTP and sigma.

Agrees with the engine on every published figure:

| | value |
|---|---|
| Cycle | 14,781,416 |
| Winning combinations | 1,516,294 |
| Line RTP | 0.5960105581 |
| Bonus RTP | 0.2651017621 |
| Total RTP | 0.8611123203 |
| Sigma per unit wagered | 6.129329334 |
| Line sigma | 5.124748451 |
| Bonus sigma | 3.379536650 |
| Bonus mean / variance | 21.571429 / 469.744898 |

Those sigma values are pinned in `OrcaDiveParSheetTests`, so a change to the covariance
algebra now fails a test rather than passing quietly.

### `coverage.py` — is the confidence band on the proving ground actually right?

Checking the formula is not the same as checking the band. This samples 20,000 replicate
runs from the exact outcome law and counts how often the measured RTP lands inside the
nominal 99% band:

| Spins | Nominal | Empirical |
|---|---|---|
| 1e6 | 99.00% | 98.86% |
| 1e7 | 99.00% | 98.96% |
| 1e8 | 99.00% | 98.94% |

It also confirms the two hit-frequency conventions the series is careful to separate:
line-only 10.2581% and any-award 11.4517%.

### `skew.py` — is the normal approximation safe for this game?

The reason to doubt the band is that the per-spin return is wildly skewed: about 26% of the
line variance sits in a single outcome that occurs 4 times in 14.8 million spins, expected
just 2.7 times in a 10M-spin run. This script quantifies that concentration and the
Berry-Esseen bound.

The conclusion is that the approximation holds anyway, which is why `coverage.py` finds the
band sound. Worth knowing that it was checked rather than assumed.

## A caveat about what this does and does not cover

These scripts verify the ANALYTIC math for one game. They do not exercise the RNG, the
window drawing, the parallel workers, or the server. Those are covered by the C# suites,
including an exhaustive comparison of the optimized window draw against a modulo baseline.
