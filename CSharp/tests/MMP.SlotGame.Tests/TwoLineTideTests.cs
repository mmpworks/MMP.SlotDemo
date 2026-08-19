using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Pins the complete two-line-plus-bonus teaching game. The hand calculation uses all 64
/// physical stop combinations, so it checks the analytic path without random sampling.
/// </summary>
[Trait("Category", "Fast")]
public sealed class TwoLineTideTests
{
    private const string GameName = "two-line-tide";

    [Fact]
    public void ExactAnalysis_IncludesBothLinesAndTheirBonusOverlap()
    {
        var game = GameFiles.Load(GameName);
        var analysis = GameAnalyzer.Analyze(game);

        Assert.Equal(2, game.Paylines.Count);
        Assert.NotNull(game.Bonus);
        Assert.Equal(64, analysis.StopCombinations);

        // Across the 64 windows, combined line awards are 8X once, 5X once, and 3X once.
        Assert.Equal(16.0 / 64.0, analysis.LineRtp, 12);

        // Starfish is visible in three of four windows on each required reel: 3/4 x 3/4.
        // The pick game averages 1X when triggered, so bonus RTP is also 9/16.
        Assert.Equal(9.0 / 16.0, analysis.TriggerProbability, 12);
        Assert.Equal(9.0 / 16.0, analysis.BonusRtp, 12);
        Assert.Equal(13.0 / 16.0, analysis.TotalRtp, 12);

        // E[total^2] is 3.0 after line squares, bonus squares, and the line-bonus cross term.
        var expectedSigma = Math.Sqrt(3.0 - Math.Pow(13.0 / 16.0, 2));
        Assert.Equal(expectedSigma, analysis.SigmaPerUnitWagered, 12);
    }

    [Fact]
    public async Task SimulationRunner_CompletesForTwoLinesAndBonus()
    {
        var game = GameFiles.Load(GameName);
        var plan = new RunPlan("two-line-test", MasterSeed: 42, WorkerCount: 1, TargetSpins: 10_000);
        var result = await new GameRunner(game, plan).RunAsync();

        Assert.Equal(10_000, result.Totals.Spins);
        Assert.True(result.LineHits > 0);
        Assert.True(result.BonusTriggers > 0);
        Assert.Equal(13.0 / 16.0, result.Analytic.TotalRtp, 12);
    }

    [Fact]
    public void StopZeroOnEveryReel_PaysBothLinesAndTriggersBonus()
    {
        var game = GameFiles.Load(GameName);
        int[] stops = [0, 0, 0];

        Assert.True(game.WinningOutcomes.TryGetValue(
            WinningOutcomeTable.PackKey(stops), out var outcome));
        Assert.NotNull(outcome);
        Assert.Equal(800, outcome.TotalMultiplier); // 5X Pearl + 3X Shell.
        Assert.Equal(2, outcome.Paylines.Count);
        Assert.Single(outcome.TriggeredFeatures);
    }
}
