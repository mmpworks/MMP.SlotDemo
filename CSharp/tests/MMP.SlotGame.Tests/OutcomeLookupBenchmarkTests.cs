using System.Diagnostics;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;
using Xunit.Abstractions;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Same-work comparison of the original rule evaluator, one packed-key dictionary lookup,
/// and progressive reel-prefix narrowing. Enable the slow tier to run it in Release mode.
/// </summary>
public sealed class OutcomeLookupBenchmarkTests(ITestOutputHelper output)
{
    private const int Spins = 10_000_000;
    private const int Trials = 5;

    [SlowFact]
    public void OrcaDive_ComparesThreeEquivalentOutcomePaths()
    {
        var game = GameFiles.Load(GameFiles.OrcaDive);

        // Warm each path before recording samples so tiered compilation is not charged to
        // whichever implementation happens to run first.
        _ = RunRules(game, 50_000, 1);
        _ = RunPacked(game, 50_000, 1);
        _ = RunProgressive(game, 50_000, 1);

        var rules = new double[Trials];
        var packed = new double[Trials];
        var progressive = new double[Trials];

        for (var trial = 0; trial < Trials; trial++)
        {
            var seed = 0xA11CE000UL + (uint)trial;
            (double Rate, ulong Checksum) expected;
            (double Rate, ulong Checksum) packedResult;
            (double Rate, ulong Checksum) progressiveResult;

            // Rotate the order so no implementation always receives the coolest CPU or
            // always runs after the other tables have warmed the caches.
            if (trial % 3 == 0)
            {
                expected = Measure(() => RunRules(game, Spins, seed));
                packedResult = Measure(() => RunPacked(game, Spins, seed));
                progressiveResult = Measure(() => RunProgressive(game, Spins, seed));
            }
            else if (trial % 3 == 1)
            {
                packedResult = Measure(() => RunPacked(game, Spins, seed));
                progressiveResult = Measure(() => RunProgressive(game, Spins, seed));
                expected = Measure(() => RunRules(game, Spins, seed));
            }
            else
            {
                progressiveResult = Measure(() => RunProgressive(game, Spins, seed));
                expected = Measure(() => RunRules(game, Spins, seed));
                packedResult = Measure(() => RunPacked(game, Spins, seed));
            }

            Assert.Equal(expected.Checksum, packedResult.Checksum);
            Assert.Equal(expected.Checksum, progressiveResult.Checksum);
            rules[trial] = expected.Rate;
            packed[trial] = packedResult.Rate;
            progressive[trial] = progressiveResult.Rate;
        }

        Array.Sort(rules);
        Array.Sort(packed);
        Array.Sort(progressive);
        output.WriteLine($"rules median:       {rules[Trials / 2]:N0} outcomes/second");
        output.WriteLine($"packed median:      {packed[Trials / 2]:N0} outcomes/second");
        output.WriteLine($"progressive median: {progressive[Trials / 2]:N0} outcomes/second");
        output.WriteLine($"packed/rules:       {packed[Trials / 2] / rules[Trials / 2]:F3}x");
        output.WriteLine($"progressive/rules:  {progressive[Trials / 2] / rules[Trials / 2]:F3}x");
        output.WriteLine($"progressive/packed: {progressive[Trials / 2] / packed[Trials / 2]:F3}x");
    }

    private static (double Rate, ulong Checksum) Measure(Func<ulong> run)
    {
        var clock = Stopwatch.StartNew();
        var checksum = run();
        clock.Stop();
        return (Spins / clock.Elapsed.TotalSeconds, checksum);
    }

    private static ulong RunRules(GameDefinition game, int spins, ulong seed)
    {
        var rng = SpinRng.ForWorker(seed, 0);
        var evaluator = new WinEvaluator(game);
        var window = new byte[game.Reels.WindowSize];
        var cells = new byte[game.ReelCount];
        ulong checksum = 0;
        for (var spin = 0; spin < spins; spin++)
        {
            game.Reels.DrawWindowIds(ref rng, window);
            var multiplier = evaluator.EvaluateWindowIds(window, cells);
            var feature = game.Bonus is not null && WinEvaluator.IsTriggeredIds(window, game.Reels.Rows, game.Bonus);
            checksum = Mix(checksum, multiplier, feature);
        }
        return checksum;
    }

    private static ulong RunPacked(GameDefinition game, int spins, ulong seed)
    {
        var rng = SpinRng.ForWorker(seed, 0);
        ulong checksum = 0;
        for (var spin = 0; spin < spins; spin++)
        {
            var key = game.Reels.DrawStopKey(ref rng);
            game.WinningOutcomes.TryGetValue(key, out var outcome);
            checksum = Mix(checksum, outcome?.TotalMultiplier ?? 0, outcome?.TriggeredFeatures.Count > 0);
        }
        return checksum;
    }

    private static ulong RunProgressive(GameDefinition game, int spins, ulong seed)
    {
        var rng = SpinRng.ForWorker(seed, 0);
        Span<byte> stops = stackalloc byte[WinningOutcomeTable.MaximumReels];
        var gameStops = stops[..game.ReelCount];
        ulong checksum = 0;
        for (var spin = 0; spin < spins; spin++)
        {
            game.Reels.DrawStops(ref rng, gameStops);
            game.ProgressiveOutcomes.TryGetValue(gameStops, out var outcome);
            checksum = Mix(checksum, outcome?.TotalMultiplier ?? 0, outcome?.TriggeredFeatures.Count > 0);
        }
        return checksum;
    }

    private static ulong Mix(ulong checksum, int multiplier, bool feature) =>
        unchecked(checksum * 31 + (uint)multiplier * 2UL + (feature ? 1UL : 0UL));
}
