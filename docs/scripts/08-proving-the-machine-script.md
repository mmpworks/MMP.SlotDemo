# Episode 8 — Proving the Machine: Three Implementations, One Answer

**Target:** 26–28 min. **Format:** create the file, paste the finished source, then
walk it. Today the file that goes in is a test, because the tests are the product.
**Subject:** the engine. This is the one episode where the companion site gets a real
segment: the closing ten-million-spin run, and it runs on camera.
**Companion article:** `docs/articles/08-proving-the-machine.md`
**Companion site:** MMP.SlotDemo, branch `main`, pages `#/ch08` and the finale run
**Files created on camera:**
`CSharp/tests/MMP.SlotGame.Tests/ExhaustiveGroundTruthTests.cs`.
**Shown, not created:** `CSharp/src/MMP.SlotGame.Core/Games/GameAnalyzer.cs`.

> **Discipline note for this recording.** Every earlier episode capped the browser at
> three minutes. This one budgets about four, and all of it lands at the end. The
> walkthrough is still the episode; the finale is what it builds to.

---

## Prep checklist

**Repo — the subject**
- [ ] Rider on `CSharp/MMP.SlotDemo.slnx`, tree expanded to `MMP.SlotGame.Tests`
- [ ] `ExhaustiveGroundTruthTests.cs` moved aside so it gets created on camera
- [ ] `CSharp/src/MMP.SlotGame.Core/Games/GameAnalyzer.cs` open in a background tab
- [ ] `ConcurrencyTests.cs`, `NoAmbientRngTests.cs`, `DomainDataImmutabilityTests.cs`,
      `GameConvergenceTests.cs` open in background tabs
- [ ] Test runner loaded with the whole suite, run once so nothing pays a cold start
- [ ] A terminal ready for the full suite run, with `SLOTGAME_SLOW_TESTS=1` exported in
      it — the two convergence tests this episode leads with are `[SlowFact]` and are
      skipped without it, so an ungated run shows them as skips on camera
- [ ] Clipboard manager staged with Block A

**Companion site — the finale**
- [ ] This repository checked out, branch `main`
- [ ] `cd CSharp/web && npm run build`, then `dotnet run --project CSharp/src/SlotDemo.Server`
- [ ] `http://localhost:5090/#/ch08`, Lab 1 — "The census" — run once
- [ ] The finale run page loaded and a full ten-million-spin run completed once before
      recording, so the timing is known and the machine is warm
- [ ] `logs/` cleared so the viewer starts empty

**OBS**
- [ ] Scenes: `RIDER`, `BROWSER`, `TERMINAL`
- [ ] Zoom-to-mouse hotkey bound and tested at code-reading zoom
- [ ] Browser window sized so the convergence chart and the band both read at 1080p

---

## 0:00–1:30 — Cold open

**Scene:** RIDER, the test project tree.

- "Seven episodes built a slot machine. This one asks how you know it is right."
- "The answer is three separate implementations of the same game, written to disagree if
  any of them is wrong."
- Name them: the analytic closed form from episode 4, the simulator from episode 6, and
  a third one we write today that visits every outcome the game can produce: the
  *preset* three-reel game, so the enumeration is a clean 22 cubed.
- "One file goes in on camera, and it is a test file. In this project the tests are the
  product."

## 1:30–4:00 — Why one implementation cannot check itself

**Scene:** RIDER or whiteboard.

Draw three boxes and the sentence that connects them.

1. **The analytic path** counts probabilities and never plays a spin.
2. **The simulator** plays spins and never computes a probability.
3. **The enumerator** walks every stop combination and does neither; it adds everything
   up and divides.

The failure mode this structure exists for:

- "Every other RTP assertion in this project compares an analytic number against an
  empirical number. If both of them came from the same evaluation routine, a bug in that
  routine cancels itself out, and every convergence test passes green on a wrong game."
- "Two numbers agreeing is only evidence when the two numbers were produced
  independently."
- So the third implementation shares data with the engine and shares no behavior. Same
  strips, same paylines, same integer paytable. Its own window construction, its own run
  matching, its own averaging.

**Close the section with:** "The three paths share the game definition, but they do not
share payout code. Their agreement is the check."

## 4:00–4:45 — Create the file

**Scene:** RIDER.

- New file. **Path on screen and said out loud:**
  `CSharp/tests/MMP.SlotGame.Tests/ExhaustiveGroundTruthTests.cs`
- Paste **Block A**. "Two hundred and fifty-three lines. Three assertions, and about half
  the file is the independent implementation those assertions check against."

### Block A — `CSharp/tests/MMP.SlotGame.Tests/ExhaustiveGroundTruthTests.cs`

```csharp
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// RT-8 — THE independent ground truth, and the load-bearing test of this harness.
///
/// Every other RTP assertion in the project compares an analytic number against an
/// empirical number. If both came from the same evaluation routine, a bug in that
/// routine would cancel itself out and every convergence test would pass green on a
/// wrong game. So this file reimplements slot evaluation from scratch — straight-line
/// loops, no Core evaluation code — and enumerates ALL 22^3 = 10,648 stop combinations
/// of Classic3 exactly.
///
/// What it shares with Core: the DATA (strip symbol ids, payline row vectors, the
/// integer paytable dictionary). What it does NOT share: window construction, run
/// matching, probability weighting, EV summation, variance summation. Those are all
/// re-derived here.
///
/// Three assertions:
///   1. exhaustive base RTP  == AnalyticMath.RealizedBaseRtp        (1e-12 relative)
///   2. exhaustive payout sum == LinePayEvaluator payout sum         (exact, integer)
///   3. exhaustive base sigma == AnalyticMath.SigmaPerUnitWagered    (1e-9 relative)
///
/// (3) runs on a config with 0-bp features so the analytic sigma reduces to its
/// base-game part; features are independent additive variance terms (RT-5) and are
/// pinned separately in FeatureScheduleTests.
/// </summary>
[Trait("Category", "Fast")]
public sealed class ExhaustiveGroundTruthTests
{
    private const string Preset = "Classic3";

    [Theory]
    [InlineData(5000)]
    [InlineData(7500)]
    [InlineData(9900)]
    public void Classic3_ExhaustiveBaseRtp_MatchesAnalytic(int baseBp)
    {
        var game = TestGame.Build(Preset, baseBp, freeSpinsBp: 0, pickBonusBp: 0);
        var truth = Enumerate(game);

        var analytic = AnalyticMath.RealizedBaseRtp(
            game.Reels, game.Lines, game.Paytable, SimulationConfig.Wager);

        Assert.Equal(10_648, truth.Combinations);
        Assert.True(truth.MeanPerUnitWagered > 0, "Ground truth paid nothing — the enumeration is broken.");

        var relative = Math.Abs(analytic - truth.MeanPerUnitWagered) / truth.MeanPerUnitWagered;
        Assert.True(
            relative <= 1e-12,
            $"""
             Analytic base RTP disagrees with the exhaustive ground truth (baseBp={baseBp}).
               analytic   = {analytic:R}
               exhaustive = {truth.MeanPerUnitWagered:R}
               relative   = {relative:R} (budget 1e-12)
             """);
    }

    /// <summary>
    /// The exhaustive enumerator and Core's LinePayEvaluator must agree on EVERY single
    /// window, not merely on the average — an averaging test can hide two errors that
    /// cancel. Integer millicents make this an exact equality.
    /// </summary>
    [Fact]
    public void Classic3_LinePayEvaluator_MatchesIndependentEnumeration_PerWindow()
    {
        var game = TestGame.Build(Preset, freeSpinsBp: 0, pickBonusBp: 0);
        var truth = Enumerate(game);

        var evaluator = game.Evaluator();
        var window = new Symbol[game.Reels.WindowSize];
        var strips = new Symbol[3][];
        for (var reel = 0; reel < 3; reel++) strips[reel] = game.Reels.Strip(reel).ToArray();

        var stops = strips[0].Length;
        Int128 coreSum = 0;
        var mismatches = 0;
        var firstMismatch = "";

        for (var s0 = 0; s0 < stops; s0++)
        for (var s1 = 0; s1 < stops; s1++)
        for (var s2 = 0; s2 < stops; s2++)
        {
            FillWindow(window, strips, s0, s1, s2);
            var corePay = evaluator.Evaluate(window, 3, StripReelSet.DefaultRows).Value;
            coreSum += corePay;

            var truthPay = truth.PayoutAt(s0, s1, s2, stops);
            if (corePay == truthPay) continue;

            mismatches++;
            if (firstMismatch.Length == 0)
                firstMismatch = $"stops ({s0},{s1},{s2}): core={corePay} truth={truthPay}";
        }

        Assert.True(
            mismatches == 0,
            $"LinePayEvaluator disagrees with the independent enumerator on {mismatches} of " +
            $"{stops * stops * stops} windows. First: {firstMismatch}");
        Assert.Equal(truth.PayoutSum, coreSum);
    }

    [Fact]
    public void Classic3_ExhaustiveBaseSigma_MatchesAnalyticSigma()
    {
        // 0-bp features isolate the base-game variance term (RT-5: feature variances add).
        var game = TestGame.Build(Preset, freeSpinsBp: 0, pickBonusBp: 0);
        Assert.Empty(game.Config.Features);

        var truth = Enumerate(game);
        var truthSigma = Math.Sqrt(truth.VariancePerUnitWagered);

        var analyticSigma = AnalyticMath.SigmaPerUnitWagered(
            game.Reels, game.Lines, game.Paytable, game.Config.Features, SimulationConfig.Wager);

        Assert.True(truthSigma > 0, "Ground-truth variance collapsed to zero — the enumeration is broken.");

        var relative = Math.Abs(analyticSigma - truthSigma) / truthSigma;
        Assert.True(
            relative <= 1e-9,
            $"""
             Analytic sigma disagrees with the exhaustive ground truth.
               analytic   = {analyticSigma:R}
               exhaustive = {truthSigma:R}
               relative   = {relative:R} (budget 1e-9)
               analytic var   = {analyticSigma * analyticSigma:R}
               exhaustive var = {truth.VariancePerUnitWagered:R}
             """);
    }

    // ---------------------------------------------------------------------------
    // Independent ground truth. Nothing below this line calls Core evaluation code.
    // ---------------------------------------------------------------------------

    private sealed class GroundTruth
    {
        public required long[] Payouts { get; init; }      // indexed [s0*S*S + s1*S + s2]
        public required Int128 PayoutSum { get; init; }
        public required Int128 PayoutSumSquared { get; init; }
        public required int Combinations { get; init; }
        public required double Wager { get; init; }

        public double MeanPerUnitWagered => (double)PayoutSum / Combinations / Wager;

        public double VariancePerUnitWagered
        {
            get
            {
                var mean = MeanPerUnitWagered;
                var meanSquare = (double)PayoutSumSquared / Combinations / (Wager * Wager);
                return meanSquare - mean * mean;
            }
        }

        public long PayoutAt(int s0, int s1, int s2, int stops) => Payouts[(s0 * stops + s1) * stops + s2];
    }

    /// <summary>
    /// Exhaustive enumeration of all S^3 stop combinations for a 3-reel game.
    ///
    /// Every stop combination is equally likely (one uniform stop per reel, reels
    /// independent — PRD "slot geometry"), so the unweighted average over all
    /// combinations IS the expected payout, exactly. No probabilities are multiplied
    /// anywhere in here: that is deliberate, because multiplying marginals is precisely
    /// what the analytic path does and what this test exists to check.
    ///
    /// Sums are Int128 because Σ payout² over 10,648 combinations overflows Int64.
    /// </summary>
    private static GroundTruth Enumerate(PresetGame game)
    {
        Assert.Equal(3, game.Reels.ReelCount);

        // --- copy out the raw data (ids, rows, pays). No behavior comes with it. ---
        var strips = new byte[3][];
        for (var reel = 0; reel < 3; reel++)
        {
            var strip = game.Reels.Strip(reel);
            var ids = new byte[strip.Length];
            for (var i = 0; i < strip.Length; i++) ids[i] = strip[i].Id;
            strips[reel] = ids;
        }

        var lineRows = new int[game.Lines.Count][];
        for (var i = 0; i < game.Lines.Count; i++) lineRows[i] = [.. game.Lines[i].Rows];

        var pays = new Dictionary<(byte, int), long>();
        foreach (var (key, value) in game.Paytable.Pays) pays[key] = value.Value;

        var stops = strips[0].Length;
        var payouts = new long[stops * stops * stops];
        Int128 sum = 0;
        Int128 sumSquared = 0;
        var index = 0;

        for (var s0 = 0; s0 < stops; s0++)
        for (var s1 = 0; s1 < stops; s1++)
        for (var s2 = 0; s2 < stops; s2++)
        {
            long payout = 0;

            foreach (var rows in lineRows)
            {
                // A window cell is the strip symbol `row` positions along from the stop,
                // wrapping cyclically (RT-1: a reel is an ordered cyclic strip).
                var a = strips[0][(s0 + rows[0]) % stops];
                var b = strips[1][(s1 + rows[1]) % stops];
                var c = strips[2][(s2 + rows[2]) % stops];

                // Left-to-right run of the leading symbol.
                var run = 1;
                if (b == a)
                {
                    run = 2;
                    if (c == a) run = 3;
                }

                if (run >= 3 && pays.TryGetValue((a, run), out var pay))
                    payout += pay;
            }

            payouts[index++] = payout;
            sum += payout;
            sumSquared += (Int128)payout * payout;
        }

        return new GroundTruth
        {
            Payouts = payouts,
            PayoutSum = sum,
            PayoutSumSquared = sumSquared,
            Combinations = stops * stops * stops,
            Wager = SimulationConfig.Wager.Value,
        };
    }

    private static void FillWindow(Symbol[] window, Symbol[][] strips, int s0, int s1, int s2)
    {
        Span<int> stops = [s0, s1, s2];
        for (var reel = 0; reel < 3; reel++)
        {
            var strip = strips[reel];
            for (var row = 0; row < StripReelSet.DefaultRows; row++)
                window[reel * StripReelSet.DefaultRows + row] = strip[(stops[reel] + row) % strip.Length];
        }
    }
}
```

## 4:45–14:00 — Walk the ground truth

**Scene:** RIDER throughout. Zoom on each region as it comes up.

### Beat 1 — the class comment states the contract of independence

Read the two lists aloud.

- **Shared:** the data. Strip symbol ids, payline row vectors, and the integer paytable
  dictionary.
- **Not shared:** window construction, run matching, probability weighting, expected-value
  summation, variance summation. All re-derived here.
- "Sharing the data is required, because otherwise the two implementations are analyzing
  different games. Sharing any behavior would defeat the point."
- The horizontal rule further down draws the same line at the boundary: nothing below it
  calls Core evaluation code.

### Beat 2 — deliberately unclever code

Point at the triple-nested loop and the run matching.

- Three `for` loops, a modulo, and an if-chain that counts to three. No abstraction, no
  helper, no generality over reel count.
- "In production code this would be a smell. Here it is the requirement. Every
  abstraction is a place a bug can hide."
- The run matching is written the long way on purpose: check whether the second reel
  matches the first, and only then the third. It is the definition of a left-to-right
  run, transcribed rather than factored.

### Beat 3 — no probabilities anywhere

Read the `Enumerate` doc comment.

- Every stop combination is equally likely, so the unweighted average over all
  combinations is the expected payout, exactly.
- **Then:** no probabilities are multiplied anywhere in this
  method, and that is deliberate, because multiplying marginals is precisely what the
  analytic path does and what this test exists to check.
- "If the enumerator multiplied marginals too, it would reproduce the analytic path's
  bug and agree with it. The independence has to be in the *method*, not only in the
  code."

### Beat 4 — `Int128`, and a comment that saves an afternoon

Sums are `Int128` because the sum of squared payouts over 10,648 combinations overflows
`Int64`.

- The overflow would be silent. The test would pass or fail on a number that wrapped.
- "The comment records why this accumulator is wider than the values added to it."

### Beat 5 — the per-window test, and why the average is not enough

**Scene:** RIDER, zoomed on `Classic3_LinePayEvaluator_MatchesIndependentEnumeration_PerWindow`.

Read its doc comment, then say why it exists beside the RTP test.

- The enumerator and the engine's evaluator must agree on every single window, rather
  than on the mean.
- "An averaging test can hide two errors that cancel. Overpay one window, underpay
  another, and the average is perfect. Ten thousand equalities catch both."
- Integer millicents make it an exact equality, which is invariant M1 from episode 2
  collecting for the last time. "If this ever needed a tolerance, floating point got
  into the pay path."
- Point at the failure message: it counts mismatches and reports the first stop triple by
  coordinate. "A failing test that tells you which of ten thousand windows broke is worth
  ten that tell you a number is wrong."

### Beat 6 — three tolerances, three reasons

Put the three budgets side by side: 1e-12 for RTP, exact for the payout sum, 1e-9 for
sigma.

- The payout sum is integer arithmetic on both sides, so exactness is available and
  anything less would be hiding something.
- RTP is one division at the end of an integer sum, so the only error is that division.
  1e-12 is near the limit of double precision, which is what the comparison should
  allow.
- Sigma involves a subtraction of two large nearly equal numbers, which loses precision
  by construction. 1e-9 is the budget the arithmetic supports.
- "Three different numbers, each derived from what the arithmetic can deliver. None of
  them chosen by running the test and picking a value that passed."

### Beat 7 — the sanity assertions inside the test

Two assertions look redundant until you ask what they catch.

- `Assert.Equal(10_648, truth.Combinations)` pins that the enumeration visited the whole
  space. An enumerator that silently walked a subset would still produce a mean, and the
  mean might even be close.
- `truth.MeanPerUnitWagered > 0` with the message "the enumeration is broken" catches an
  enumerator that pays nothing at all. Zero equals zero, so without this the comparison
  could pass against a completely dead implementation.
- "Both of these guard the checker rather than the code under test. An oracle you never
  verify is an oracle you should not trust."

### Beat 8 — the zero-basis-point features, and isolating a term

The sigma test builds the game with both feature contributions at zero and then asserts
the feature list is empty.

- That reduces the analytic sigma to its base-game part, which is the part the
  enumeration covers.
- Feature variances are independent additive terms in this model, and they are pinned
  separately in `FeatureScheduleTests`.
- "Test one term at a time and a failure names its own cause. Test the sum and a failure
  tells you only that something in the sum is wrong."

## 14:00–17:30 — The enumeration referee for real games

**Scene:** RIDER, `CSharp/src/MMP.SlotGame.Core/Games/GameAnalyzer.cs`, walked rather than
pasted.

The exhaustive test covers a 10,648-outcome classic. A five-reel game with ragged strips
has 14,781,416 stop combinations, and `GameAnalyzer` is how the same idea scales.

### Beat 9 — enumerate symbol tuples instead of stop tuples

Read the class comment's first paragraph.

- Every combination of one symbol per reel, weighted by how many stops produce it,
  rather than every individual stop.
- For Orca Dive that is tens of thousands of tuples instead of fourteen million, for the
  same exact answer.
- **Why it works:** a payline reads exactly one cell per reel. "The optimization follows
  from that rule, so a change to the domain will also change the count."

### Beat 10 — the scatter rides through as a second weight

The paragraph that makes this analysis correct.

- The scatter does not read a single cell, so it cannot be a plain symbol in the tuple.
- Beside each reel's plain symbol counts sits a second count: stops that show that symbol
  on the payline *and* the scatter somewhere in the window. Multiply the second weight on
  the required reels and the first everywhere else.
- **Then the consequence:** line pay and the feature are correlated, because a scatter in
  the window costs that reel a payline symbol. A sigma built by adding their variances
  would be wrong.
- "Episode 7 said the scatter bonus breaks the independence assumption. This is the code
  that stopped leaning on it."

### Beat 11 — the recursion, and reel count as a loop bound

`Descend` walks one reel deeper, multiplying in the symbol weight, and stops when it runs
out of reels.

- Symbols absent from a reel are skipped, so a game whose reels carry different symbol
  sets costs nothing for the ones it does not use.
- The `Enumeration` class exists because the descent carries state a recursive signature
  would have to thread through every call: five running totals — `_hits`, `_payUnits`,
  `_paySquareUnits`, `_payTriggerUnits`, `_triggerWeight` — plus a `_counts` dictionary
  keyed by category and run length. Point at the fields rather than quoting a number;
  the comment explains the choice without counting them. "A class chosen for
  readability."

### Beat 12 — two analysis routes, each bounded

- `Analyze` sends a single-payline game through weighted symbol enumeration.
- A multi-payline game uses the compiled physical-window table, which keeps all line and
  bonus overlaps attached to the window that produced them.
- `GuardEnumerationSize` limits weighted enumeration to 200 million symbol combinations.
  Physical-window construction has its own 100 million-combination limit. Both bounds turn
  an oversized definition into a clear error instead of an apparent hang.

> **Illustration (40 seconds, BROWSER).** Chapter 8 page, Lab 1 — "The census." Run the
> analyzer over the classic three-reel game and over Orca Dive. Read the two figures the
> response actually carries: `stopCombinations` — 14,781,416 for Orca Dive — and the
> per-category combination counts beside it, which are the winning outcomes the walk
> found. Then read the RTP figures against the published sheet. "Same answer as the
> sheet, off a walk that never visited fourteen million stops, and the reason is one
> sentence about paylines." Cut back.
>
> The lab reports `stopCombinations` and per-category counts; there is no single
> "enumerated tuple" total in the response, so do not go looking for one on camera.

## 17:30–21:00 — The rest of the proof

**Scene:** RIDER, background tabs, no full walkthroughs.

Each of these gets sixty to ninety seconds. Name the test, say what it holds, and say why
it is shaped the way it is.

- **`ConcurrencyTests.ParallelRun_EqualsSequentialReplication_BitForBit`** — open it and
  read the assertions. Four field equalities and then the whole snapshot. **Why replicate
  by hand rather than compare two parallel runs:** the replication reproduces the engine's
  contract from the outside, including the RNG consumption order, so the test knows what
  the answer should be rather than only that two runs agree. The class comment says the
  order is part of the contract, and swapping the window draw with the feature play
  desynchronizes every stream while nothing looks wrong.
- **Why the operator matters.** `Assert.Equal` on integers, at 300,000 spins, at 1, 2,
  and 8 workers. "The day this needs a tolerance is the day invariant M1 broke, and the
  test will say so by failing rather than by being edited."
- **`NoAmbientRngTests.CoreSourceContainsNoAmbientRandomnessOrClock`** reads the assembly
  and looks for an absence. **Why a reflection test:** rule R3 is a rule about what nobody
  should write, and the only reliable enforcement of that is a machine checking, rather
  than everyone remembering.
- **`SpinRng_IsAMutableStruct_SoRefPassingIsLoadBearing`** is the strangest test in the
  repo. It asserts a property that looks like a defect,
  because the whole determinism design depends on it. "Somebody will try to make this
  readonly to be tidy. This is what stops them, with a name that explains why."
- **`DomainDataImmutabilityTests`** — nine tests across the shared types, each asserting
  the constructor copied the caller's data. Beat 6 of episode 3 and beat 4 of episode 4,
  collected. **Why they live together:** the rule is one rule, so the failures should read
  as one class.
- **`StatisticalConvergenceTests.Coverage_32Seeds_3MSpins_LandInThe99PercentBand`** — the
  chi-square honesty beat, said properly. A single seeded run landing inside a band proves
  almost nothing: with one draw you cannot tell a correct game from one biased by a
  fraction of a sigma. Thirty-two seeds and an assertion on how many land inside is a test
  of the band itself, and it fails both when the game is biased and when the analytic
  sigma is wrong.
- **"A healthy generator lands in the tail about one run in a hundred."** A one-seed test
  either flakes or gets tuned until it stops flaking.
- **`GameConvergenceTests.OrcaDive_TenMillionSpins_ReproduceThePublishedReturns`** checks
  the run against numbers printed outside this project. The finale segment reads them.

## 21:00–22:00 — Run everything

**Scene:** TERMINAL.

- Run the full suite with the slow tier enabled:
  `SLOTGAME_SLOW_TESTS=1 dotnet test`. Let it run. Green.
- Say why the variable is there: the convergence and stress classes are gated by cost,
  never by confidence. "The gate is opt-in so the fast loop stays fast. It is not a
  quarantine — nothing in there is allowed to be flaky."
- "Every claim made across eight episodes has a test in that output. The invariants from
  episode 2, the geometry from episode 3, the closed forms from episode 4, the
  weighted enumeration from episode 5, determinism from episode 6, the validation boundary
  from episode 7, and today's three
  independent implementations agreeing."

## 22:00–26:00 — The finale run

**Scene:** BROWSER, the finale page. This is the finale and it gets four minutes.

- Start a ten-million-spin run on camera and let it go. Talk over it rather than
  narrating each frame.
- **While the early spins land:** point at the band being wide and the line swinging
  inside it. "Early volatility is the game being a game. The band already knows how wide
  that swing should be, because episode 4 computed sigma before spin one."
- **As N grows:** the band narrows as one over the square root of N, and the line settles
  toward the analytic target. Point at both together. "The line is the simulator. The band
  is the closed form. Neither one is watching the other."
- **Point at the telemetry counters:** dropped samples in the thousands, and the spin
  counter dead on pace. Episode 6's lossy telemetry, working as designed, at full speed.
- **At completion:** the final verdict, inside the band. Read the measured RTP against the
  analytic target and against the enumerated ground truth. Three numbers, three
  implementations. Name which hit-frequency the on-screen counter is showing before
  reading it: it counts any award, base or feature, and the published figure is line
  pays only.
- **Put the Orca Dive card up while the numbers are on screen:** total return 86.111%,
  line pay 59.601%, bonus 26.510%, line hit frequency 10.258%. "Those four were printed
  by somebody who has never heard of this code, and the run just reproduced them."
- Then rerun the same seed and worker count and let the final total come out identical.
  "Ten million spins, sixteen cores, and the same total to the last millicent. That is
  invariant M2, where a viewer can watch it happen."

## 26:00–27:30 — Wrap the series

**Scene:** RIDER, the Core project tree.

- Walk the folders one last time and name what each episode left: `Money` and
  `Simulation/SpinRng` from episode 2, `Reels` from 3, `Paytables` and `Rtp` from 4,
  `Games/GameAnalyzer` from 5, the engine from 6, `Games/Definition` from 7.
- The chain, said once: exact money made parallel totals provable, provable totals made
  determinism testable, an exact closed form made the band real, a real band made
  convergence checkable, and games as data made a published par sheet an outside
  authority to check against.
- "None of the individual pieces were clever. The design was making each piece so strict
  that the next one got to be ordinary."
- Close on the three implementations. "One counts probabilities, one plays spins, one
  visits every outcome. They agree."
- Then hand off the follow-up: "One more episode after this one, and it is about speed.
  Three implementations have the answer pinned down, so now we can rewrite the hot path
  and still have something to check the faster version against."

---

## Recording notes

- Budget: roughly twenty-two minutes in Rider and the terminal, four in the browser. The
  browser time is the finale and it is protected. If a take runs long, cut from the
  segment at 17:30, not from the finale.
- Strongest visuals in order: the finale band narrowing around the settling line, the two
  identical ten-million-spin totals from the same seed, and the full suite going green in
  the terminal. Hold on each.
- Zoom hotkey belongs on: the shared-versus-not-shared lists in the class comment, the
  horizontal rule that says nothing below calls Core evaluation code, the three tolerance
  budgets, and the final RTP comparison on the finale page.
- The paste block is the finished file verbatim. If a paste lands wrong, cut and re-paste
  rather than hand-fixing: the file has to match the repo.
- Warm the finale page before recording. A cold first run on camera is the one demo
  failure this series cannot absorb, because it is the last thing viewers see.
- Running long? Compress beat 11 and beat 12 into one minute and drop the census lab
  illustration. Keep beat 1 (independence), beat 3 (no probabilities), beat 5 (per-window),
  the chi-square honesty beat, and the entire finale.
