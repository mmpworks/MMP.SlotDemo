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
