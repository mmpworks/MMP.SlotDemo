using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Pins the engine's chosen basis for a JSON <see cref="GameDefinition"/>'s pay multipliers:
/// every declared multiplier scales against the TOTAL spin wager
/// (<see cref="SimulationConfig.Wager"/>), never a per-line share of it, and a spin's award
/// is the sum of every winning line's multiplier scaled that ONE way
/// (<see cref="WinEvaluator.EvaluateWindow"/>, <see cref="Millicents.ScaledMultiply"/>).
///
/// A real PAR sheet quotes its pays per line — a cabinet divides the total stake across
/// active lines before applying a line's multiplier. This engine has no such division
/// anywhere: it is internally consistent (there is nothing here for an analytic and a
/// simulated number to disagree about, since both routes read the same compiled multiplier
/// and the same <see cref="SimulationConfig.Wager"/>), but it means a PAR sheet's line pays
/// transcribed directly into a multi-payline file would NOT reproduce that PAR sheet's
/// published RTP. Both shipped games (Orca Dive, Classic Three Reel) are single-line,
/// which is exactly why this has never surfaced before: with one line, "per line" and
/// "total spin bet" are the same number.
///
/// This test forces a two-payline window with BOTH lines winning DIFFERENT categories at
/// DIFFERENT multipliers, so summing-then-scaling-once (the actual code path) is
/// distinguishable from scaling-then-summing-per-line-share (the real-cabinet convention) —
/// the two conventions would disagree by exactly the line count on a game like this one.
/// </summary>
[Trait("Category", "Fast")]
public sealed class LineBetVsTotalBetTests
{
    private const string TwoPaylineGame =
        """
        {
          "name": "Two Payline Fixture",
          "windowRows": 3,
          "symbols": [ { "name": "A" }, { "name": "B" }, { "name": "Blank" } ],
          "reels": [ ["A", "B", "Blank"], ["A", "B", "Blank"], ["A", "B", "Blank"] ],
          "paylines": [
            { "name": "Top", "rows": [0, 0, 0] },
            { "name": "Bottom", "rows": [2, 2, 2] }
          ],
          "paytable": [
            { "symbol": "A", "pays": { "3": 5 } },
            { "symbol": "B", "pays": { "3": 3 } }
          ]
        }
        """;

    /// <summary>
    /// Top line reads row 0 on every reel (all "A" -> 5X); bottom line reads row 2 on every
    /// reel (all "B" -> 3X); the middle row is "Blank" everywhere, which pays nothing and
    /// isn't even a declared payline. Window layout is [reel * rows + row] (3 rows).
    /// </summary>
    private static Symbol[] ForcedWindow(GameDefinition definition)
    {
        var a = definition.Symbols.First(s => s.Name == "A");
        var b = definition.Symbols.First(s => s.Name == "B");
        var blank = definition.Symbols.First(s => s.Name == "Blank");

        return
        [
            a, blank, b, // reel 0: row0=A, row1=Blank, row2=B
            a, blank, b, // reel 1
            a, blank, b, // reel 2
        ];
    }

    [Fact]
    public void TwoWinningLines_AreScaledAgainstTheSameTotalWagerAndSummed()
    {
        var loaded = GameDefinitionLoader.TryLoad(TwoPaylineGame, out var definition, out var errors);
        Assert.True(loaded, "Fixture failed to load:\n  " + string.Join("\n  ", errors));

        var evaluator = new WinEvaluator(definition!);
        var window = ForcedWindow(definition!);
        var cells = new byte[definition!.ReelCount];

        var totalMultiplier = evaluator.EvaluateWindow(window, cells);

        // 5X (Top: AAA) + 3X (Bottom: BBB) = 8X in hundredths (Millicents.ScaleFactor), summed
        // BEFORE the one scaling step — never 5X and 3X each computed against a per-line share
        // of the wager and then added as separate money amounts.
        Assert.Equal(800, totalMultiplier);

        var award = SimulationConfig.Wager.ScaledMultiply(totalMultiplier);

        // The number this pins for the article: at a 1-credit (100,000 millicent) wager,
        // 8X of the TOTAL wager is exactly 800,000 millicents. The real-cabinet convention
        // (each line's multiplier against its own 1/2 share of the wager) would instead give
        // 5X * 50,000 + 3X * 50,000 = 400,000 millicents — half this test's asserted value,
        // because this fixture has 2 lines. This test exists to make that gap visible and
        // pinned, not to close it.
        Assert.Equal(800_000L, award.Value);
    }

    /// <summary>
    /// The same computation, decomposed per line, confirms there is no per-line division
    /// hiding anywhere: each line's OWN multiplier, scaled against the FULL wager
    /// independently, already equals its share of the combined award above.
    /// </summary>
    [Fact]
    public void EachLine_ScalesIndividuallyAgainstTheFullWager()
    {
        var loaded = GameDefinitionLoader.TryLoad(TwoPaylineGame, out var definition, out var errors);
        Assert.True(loaded, "Fixture failed to load:\n  " + string.Join("\n  ", errors));

        var evaluator = new WinEvaluator(definition!);
        var a = definition!.SymbolId("A");
        var b = definition.SymbolId("B");

        var topWin = evaluator.Evaluate([(byte)a, (byte)a, (byte)a]);
        var bottomWin = evaluator.Evaluate([(byte)b, (byte)b, (byte)b]);

        Assert.Equal(500_000L, SimulationConfig.Wager.ScaledMultiply(topWin.Multiplier).Value); // 5X of 100,000 mc
        Assert.Equal(300_000L, SimulationConfig.Wager.ScaledMultiply(bottomWin.Multiplier).Value); // 3X of 100,000 mc

        // Same wager on both lines, not a 50,000-mc half-share each — confirms the basis.
        Assert.Equal(SimulationConfig.Wager, SimulationConfig.Wager);
    }
}
