using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;
using Xunit.Abstractions;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The interview exercise, run for real: ingest a PAR sheet as data, spin the machine it
/// describes millions of times, and check the measured RTP against the published percentages.
///
/// Both shipped definitions run through the same code here, at different reel counts, symbol
/// counts and strip lengths, with and without a feature. That is the agnosticism claim being
/// tested end to end rather than asserted in a comment.
///
/// The band is z*sigma/sqrt(N) with sigma from <see cref="GameAnalyzer"/>, never from the
/// sample. An empirical sigma would let a broken game widen its own acceptance band, and
/// Orca Dive has a long right tail (one combination in 3.7 million pays 5000) that makes
/// the sample sigma an unreliable estimate at any N a test can afford.
///
/// Line and feature are checked SEPARATELY as well as together. They are anti-correlated in a
/// small way (a scatter in the window costs that reel a payline symbol), so a bug that moved
/// money from one to the other could leave the total sitting comfortably in its band.
/// </summary>
[Trait("Category", "Slow")]
public sealed class GameConvergenceTests(ITestOutputHelper output)
{
    private const int Workers = 8;

    [SlowFact]
    public async Task OrcaDive_TenMillionSpins_ReproduceThePublishedReturns()
    {
        var result = await RunAsync(GameFiles.OrcaDive, spins: 10_000_000, seed: 0x1A5CB0DE_7EA5E5EDUL);

        Assert.True(result.BonusTriggers > 0, "No bonus ever triggered; the scatter path did not run.");
        AssertConverged(result, spins: 10_000_000);
    }

    /// <summary>
    /// The same machinery on a three-reel, six-symbol, feature-free game with uneven strips.
    /// Nothing in the engine changed between this test and the one above; only the file did.
    /// </summary>
    [SlowFact]
    public async Task ClassicThreeReel_FiveMillionSpins_ConvergeOnItsAnalyticReturn()
    {
        var result = await RunAsync(GameFiles.ClassicThreeReel, spins: 5_000_000, seed: 0x2545F491_4F6CDD1DUL);

        Assert.Equal(0, result.BonusTriggers);
        Assert.Equal(0, result.BonusMillicents);
        AssertConverged(result, spins: 5_000_000);
    }

    private async Task<GameRunResult> RunAsync(string game, long spins, ulong seed)
    {
        var definition = GameFiles.Load(game);
        var plan = new RunPlan($"{game}-{spins}", seed, Workers, spins);
        var result = await new GameRunner(definition, plan).RunAsync();

        // Harness guards: these catch a green test that measured nothing.
        Assert.Equal(spins, result.Totals.Spins);
        Assert.Equal(spins * SimulationConfig.Wager.Value, result.Totals.WageredMillicents);
        Assert.Equal(result.Totals.ReturnedMillicents, result.LineMillicents + result.BonusMillicents);

        Report(definition.Name, result, spins, seed);
        return result;
    }

    private void AssertConverged(GameRunResult result, long spins)
    {
        var analytic = result.Analytic;

        AssertInBand("total RTP", result.Totals.MeasuredRtp, analytic.TotalRtp, analytic.SigmaPerUnitWagered, spins);
        AssertInBand("line RTP", result.LineRtp, analytic.LineRtp, analytic.LineSigma, spins);
        AssertInBand(
            "line hit frequency", result.LineHitFrequency, analytic.HitFrequency,
            BernoulliSigma(analytic.HitFrequency), spins);

        if (analytic.TriggerCombinations == 0) return;

        AssertInBand("bonus RTP", result.BonusRtp, analytic.BonusRtp, analytic.BonusSigma, spins);
        AssertInBand(
            "bonus trigger frequency", result.BonusTriggerFrequency, analytic.TriggerProbability,
            BernoulliSigma(analytic.TriggerProbability), spins);
    }

    private static double BernoulliSigma(double p) => Math.Sqrt(p * (1.0 - p));

    private static void AssertInBand(string what, double measured, double analytic, double sigma, long spins)
    {
        Assert.True(sigma > 0, $"Analytic sigma for {what} is zero; the band would be vacuous.");

        var band = NormalQuantile.TwoSided999 * sigma / Math.Sqrt(spins);
        var delta = measured - analytic;

        Assert.True(
            Math.Abs(delta) <= band,
            $"""
             {what} is outside the 99.9% analytic band after {spins:N0} spins.
               measured = {measured:R}
               analytic = {analytic:R}
               sigma    = {sigma:R}
               band (+/-) = {band:R}
               z        = {delta / (band / NormalQuantile.TwoSided999):F3}
             """);
    }

    /// <summary>The run sheet a reader wants: measured beside analytic, with the z-score of each.</summary>
    private void Report(string name, GameRunResult result, long spins, ulong seed)
    {
        var a = result.Analytic;
        output.WriteLine($"{name}: {spins:N0} spins, seed 0x{seed:X}, {Workers} workers");
        output.WriteLine($"  {"",-24}{"measured",12}{"analytic",12}{"z",9}");
        Row("total RTP", result.Totals.MeasuredRtp, a.TotalRtp, a.SigmaPerUnitWagered);
        Row("  line pay", result.LineRtp, a.LineRtp, a.LineSigma);
        if (a.TriggerCombinations > 0) Row("  pick bonus", result.BonusRtp, a.BonusRtp, a.BonusSigma);
        Row("line hit frequency", result.LineHitFrequency, a.HitFrequency, BernoulliSigma(a.HitFrequency));
        if (a.TriggerCombinations > 0)
            Row("bonus trigger rate", result.BonusTriggerFrequency, a.TriggerProbability, BernoulliSigma(a.TriggerProbability));
        output.WriteLine($"  bonuses played: {result.BonusTriggers:N0}; line hits: {result.LineHits:N0}");

        void Row(string label, double measured, double analytic, double sigma)
        {
            var z = (measured - analytic) / (sigma / Math.Sqrt(spins));
            output.WriteLine($"  {label,-24}{measured,12:P4}{analytic,12:P4}{z,9:F3}");
        }
    }
}
