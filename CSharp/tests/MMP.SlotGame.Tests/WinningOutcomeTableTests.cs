using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Checks the construction-time shortcut against the ordinary evaluator. The table is an
/// optimization, so matching the slower rule-by-rule answer is a non-negotiable condition.
/// </summary>
[Trait("Category", "Fast")]
public sealed class WinningOutcomeTableTests
{
    [Fact]
    public void PackKey_GivesEachReelOneReadableByte()
    {
        var key = WinningOutcomeTable.PackKey([12, 28, 4, 17, 25]);

        Assert.Equal(0x0C1C041119UL, key);
    }

    [Fact]
    public void Construction_StoresOnlyWinsWithTheirPaylinesAndTotal()
    {
        var game = GameDefinitionLoader.Load(SmallThreeReelGame);
        var table = game.WinningOutcomes;

        Assert.Equal(8, table.CombinationCount);
        Assert.Equal(1, table.WinningCombinationCount);
        Assert.True(table.TryGetValue(WinningOutcomeTable.PackKey([0, 0, 0]), out var win));
        Assert.NotNull(win);
        Assert.Equal(500, win.TotalMultiplier);
        Assert.Equal(["Center"], win.Paylines.Select(line => line.Name));
        Assert.Empty(win.TriggeredFeatures);

        Assert.False(table.TryGetValue(WinningOutcomeTable.PackKey([0, 0, 1]), out _));
    }

    [Fact]
    public void OrcaDive_TableMatchesThePublishedWinningCombinationCount()
    {
        var game = GameFiles.Load(GameFiles.OrcaDive);

        Assert.Equal(game.StopCombinations, game.WinningOutcomes.CombinationCount);
        Assert.Equal(1_516_294, game.WinningOutcomes.WinningCombinationCount);
        Assert.Equal(181_656, game.WinningOutcomes.FeatureTriggerCombinationCount);
        Assert.True(game.WinningOutcomes.StoredOutcomeCount >= game.WinningOutcomes.WinningCombinationCount);

        // Stop zero shows Penguin on each required reel. Penguin has no line pay, so this
        // proves a feature-only result is retained rather than mistaken for an empty spin.
        Assert.True(game.WinningOutcomes.TryGetValue(0, out var triggerOnly));
        Assert.NotNull(triggerOnly);
        Assert.Equal(0, triggerOnly.TotalMultiplier);
        Assert.Empty(triggerOnly.Paylines);
        Assert.Equal(["PenguinBonus"], triggerOnly.TriggeredFeatures.Select(feature => feature.Name));
    }

    [Fact]
    public void DrawnKey_ReturnsTheSamePayAsRuleByRuleEvaluation()
    {
        var game = GameDefinitionLoader.Load(SmallThreeReelGame);
        var evaluator = new WinEvaluator(game);
        var rng = Core.Simulation.SpinRng.ForWorker(0xC0FFEEUL, 0);
        var window = new byte[game.Reels.WindowSize];
        var cells = new byte[game.ReelCount];

        for (var spin = 0; spin < 1_000; spin++)
        {
            var key = game.Reels.DrawWindowIdsAndKey(ref rng, window);
            var evaluated = evaluator.EvaluateWindowIds(window, cells);
            var lookedUp = game.WinningOutcomes.TryGetValue(key, out var outcome)
                ? outcome!.TotalMultiplier
                : 0;

            Assert.Equal(evaluated, lookedUp);
        }
    }

    [Fact]
    public void KeyOnlyDraw_UsesTheSameStopsAsWindowDraw()
    {
        var game = GameDefinitionLoader.Load(SmallThreeReelGame);
        var windowRng = Core.Simulation.SpinRng.ForWorker(0x1234UL, 0);
        var keyOnlyRng = Core.Simulation.SpinRng.ForWorker(0x1234UL, 0);
        var window = new byte[game.Reels.WindowSize];

        for (var spin = 0; spin < 1_000; spin++)
        {
            var windowKey = game.Reels.DrawWindowIdsAndKey(ref windowRng, window);
            var keyOnly = game.Reels.DrawStopKey(ref keyOnlyRng);
            Assert.Equal(windowKey, keyOnly);
        }
    }

    private const string SmallThreeReelGame = """
        {
          "name": "Lookup Fixture",
          "windowRows": 3,
          "symbols": [
            { "name": "Pearl" },
            { "name": "Blank" }
          ],
          "reels": [
            ["Pearl", "Blank"],
            ["Pearl", "Blank"],
            ["Pearl", "Blank"]
          ],
          "paylines": [
            { "name": "Center", "rows": [0, 0, 0] }
          ],
          "paytable": [
            { "symbol": "Pearl", "pays": { "3": 5 } }
          ]
        }
        """;
}
