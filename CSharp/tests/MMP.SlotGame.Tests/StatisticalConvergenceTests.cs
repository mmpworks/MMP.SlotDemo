using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// AC-1 / AC-7 / RT-4 — statistical honesty.
///
/// A single seeded run landing inside a confidence band proves almost nothing: with one
/// draw you cannot tell a correct game from a game biased by a fraction of a sigma. So
/// this tier does two different things:
///
///   (a) COVERAGE — 32 independent seeds × 3M spins, and the assertion is on how many
///       land in the 99% band (≥ 29/32). That is a test of the band itself, and it fails
///       both when the game is biased and when the analytic sigma is wrong.
///   (b) DEMONSTRATION — one 30M-spin run at the 99.9% band, which is the AC-1 headline.
///
/// Every seed is a compile-time constant, so all verdicts here are constants too. No
/// retries, no reruns, no "flaky" skips.
///
/// The band always comes from ANALYTIC sigma (RT-7). Using the empirical sample sigma
/// would let a broken game widen its own acceptance band.
/// </summary>
[Trait("Category", "Slow")]
public sealed class StatisticalConvergenceTests
{
    private const int Workers = 8;

    [SlowFact]
    public async Task Coverage_32Seeds_3MSpins_LandInThe99PercentBand()
    {
        const int seedCount = 32;
        const long spinsPerSeed = 3_000_000;

        var reference = TestGame.Build(TestGame.DefaultPreset);
        var analytic = reference.Analyse();
        var band = NormalQuantile.TwoSided99 * analytic.SigmaPerUnitWagered / Math.Sqrt(spinsPerSeed);

        var inBand = 0;
        var outliers = new List<string>();
        long pooledWagered = 0, pooledReturned = 0;

        for (var i = 0; i < seedCount; i++)
        {
            // Fixed, well-separated seeds: constants, so the verdict is a constant.
            var seed = 0x5DEECE66DUL * (ulong)(i + 1) + 0xB504F333UL;
            var game = TestGame.Build(
                TestGame.DefaultPreset,
                masterSeed: seed,
                workerCount: Workers,
                targetSpins: spinsPerSeed);

            var snapshot = await game.Engine().RunAsync(telemetry: null);

            Assert.Equal(spinsPerSeed, snapshot.Spins);
            Assert.Equal(spinsPerSeed * SimulationConfig.Wager.Value, snapshot.WageredMillicents);

            pooledWagered += snapshot.WageredMillicents;
            pooledReturned += snapshot.ReturnedMillicents;

            var delta = snapshot.MeasuredRtp - analytic.TotalRtp;
            if (Math.Abs(delta) <= band) inBand++;
            else outliers.Add($"seed[{i}]={seed:X}: measured {snapshot.MeasuredRtp:F6}, z={delta / (band / NormalQuantile.TwoSided99):F2}");
        }

        Assert.True(
            inBand >= 29,
            $"""
             AC-7 coverage failure: only {inBand}/{seedCount} seeds landed in the 99% band.
               analytic RTP = {analytic.TotalRtp:R}
               analytic σ   = {analytic.SigmaPerUnitWagered:R}
               band (±)     = {band:R} at N = {spinsPerSeed}
             Outliers:
               {string.Join("\n  ", outliers)}
             """);

        // The pooled estimate over 96M spins is the sharper instrument: a systematic
        // bias too small to knock seeds out of the band still shows up here.
        var pooledRtp = (double)pooledReturned / pooledWagered;
        var pooledN = seedCount * (double)spinsPerSeed;
        var pooledBand = 4.0 * analytic.SigmaPerUnitWagered / Math.Sqrt(pooledN);
        Assert.True(
            Math.Abs(pooledRtp - analytic.TotalRtp) <= pooledBand,
            $"""
             Pooled mean over {pooledN:N0} spins is outside 4 analytic sigma — that is a bias, not variance.
               pooled   = {pooledRtp:R}
               analytic = {analytic.TotalRtp:R}
               band (±) = {pooledBand:R}
             """);
    }

    /// <summary>
    /// AC-1, demonstrated: a user runs ≥ 30M spins from the SPA and the measured RTP is
    /// inside the 99.9% band around the configured total. This is the run that number
    /// comes from.
    /// </summary>
    [SlowFact]
    public async Task Ac1_30MillionSpins_LandInThe999PercentBand()
    {
        const long spins = 30_000_000;
        const ulong seed = 0x0BADC0DE_1DEA5EEDUL;

        var game = TestGame.Build(
            TestGame.DefaultPreset, masterSeed: seed, workerCount: Workers, targetSpins: spins);
        var analytic = game.Analyse();

        var snapshot = await game.Engine().RunAsync(telemetry: null);

        // Harness guards — these catch "the test passed because nothing ran".
        Assert.Equal(spins, snapshot.Spins);
        Assert.Equal(spins * SimulationConfig.Wager.Value, snapshot.WageredMillicents);
        Assert.True(snapshot.Hits > 0 && snapshot.Hits < spins, $"Implausible hit count {snapshot.Hits}.");
        Assert.True(analytic.SigmaPerUnitWagered > 0, "Analytic sigma is zero; the band would be vacuous.");

        var band = NormalQuantile.TwoSided999 * analytic.SigmaPerUnitWagered / Math.Sqrt(spins);
        var delta = snapshot.MeasuredRtp - analytic.TotalRtp;

        Assert.True(
            Math.Abs(delta) <= band,
            $"""
             AC-1: 30M-spin measured RTP is outside the 99.9% analytic band.
               measured = {snapshot.MeasuredRtp:R}
               analytic = {analytic.TotalRtp:R}  (configured {game.Config.TargetTotalRtp:R})
               σ        = {analytic.SigmaPerUnitWagered:R}
               band (±) = {band:R}
               z        = {delta / (band / NormalQuantile.TwoSided999):F3}
             """);
    }

    /// <summary>
    /// The same convergence claim, once per preset, at a smaller N. Catches a preset
    /// whose geometry or paytable is broken in a way the default preset hides.
    /// </summary>
    [SlowFact]
    public async Task EveryPreset_ConvergesAt5MillionSpins()
    {
        const long spins = 5_000_000;
        const ulong seed = 0x2545F491_4F6CDD1DUL;

        var failures = new List<string>();

        foreach (var preset in ReelPresetNames())
        {
            var game = TestGame.Build(
                preset, masterSeed: seed, workerCount: Workers, targetSpins: spins);
            var analytic = game.Analyse();
            var snapshot = await game.Engine().RunAsync(telemetry: null);

            var band = NormalQuantile.TwoSided999 * analytic.SigmaPerUnitWagered / Math.Sqrt(spins);
            var delta = snapshot.MeasuredRtp - analytic.TotalRtp;
            if (Math.Abs(delta) > band)
                failures.Add(
                    $"{preset}: measured {snapshot.MeasuredRtp:F6}, analytic {analytic.TotalRtp:F6}, " +
                    $"σ {analytic.SigmaPerUnitWagered:F4}, band ±{band:F6}, z {delta / (band / NormalQuantile.TwoSided999):F3}");
        }

        Assert.True(failures.Count == 0, "Presets outside the 99.9% band:\n  " + string.Join("\n  ", failures));
    }

    private static IEnumerable<string> ReelPresetNames() =>
        TestGame.AllPresetNames().Select(row => (string)row[0]);
}
