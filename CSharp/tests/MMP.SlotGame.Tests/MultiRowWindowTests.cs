using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The engine supports 3, 4 or 5 visible rows, config-driven via `windowRows`
/// (docs/game-definition-schema.md). <see cref="StripReelSet"/>, <see cref="WinEvaluator"/>
/// and <see cref="GameAnalyzer"/> already read the window height from the loaded
/// definition everywhere (there was no literal 3 to remove in any of them); the actual gaps
/// closed here are the loader's validation bound (previously "at least 1", now
/// <see cref="StripReelSet.MinRows"/>..<see cref="StripReelSet.MaxRows"/>) and
/// <see cref="StandardPaylines.For"/>'s position-shape generation, which derives its indices
/// from a fixed 3-row assumption rather than the window height it was actually building
/// shapes for.
///
/// Both fixtures below carry a payline AND a scatter-triggered pick bonus that requires
/// every reel, so a single test proves the window height flows correctly through both the
/// line-pay path and the scatter/trigger path — the two places item 3 of the windowRows
/// audit named specifically.
/// </summary>
[Trait("Category", "Fast")]
public sealed class MultiRowWindowTests
{
    [Fact]
    public void ReelSet_CopiesInputStrips_AndWrapsRowsOnShortStrips()
    {
        var original = new Symbol(0, "Original");
        var replacement = new Symbol(1, "Replacement");
        Symbol[][] source = [[original]];
        var reels = new StripReelSet(source, rows: 3);

        source[0][0] = replacement;

        Assert.Equal(original, reels.At(reel: 0, stop: 0, row: 0));

        var rng = SpinRng.ForWorker(1UL, workerId: 0);
        var window = new Symbol[reels.WindowSize];
        reels.DrawWindow(ref rng, window);
        Assert.All(window, symbol => Assert.Equal(original, symbol));
    }

    /// <summary>
    /// A window-height-N game with a clean, hand-verifiable scatter layout: two Star stops
    /// per reel at cyclic gap >= <paramref name="rows"/>, so their in-window ranges never
    /// overlap and the per-reel trigger probability is exactly (starCount * rows) / stripLength
    /// — the formula item 3 of the windowRows directive asked to verify. The payline reads the
    /// middle row (rows / 2), which is the true center at 5 rows and the lower of the two
    /// central positions at 4 (see <see cref="StandardPaylines.For"/> for that convention).
    /// </summary>
    private static string BuildFixture(int rows, int stripLength, int starGap, string name)
    {
        var blanks = stripLength - 2 - 4; // 2 Stars, 4 As
        var strip = "[\"Star\", \"A\", \"A\", \"A\", \"A\""
            + string.Concat(Enumerable.Repeat(", \"Blank\"", starGap - 5))
            + ", \"Star\""
            + string.Concat(Enumerable.Repeat(", \"Blank\"", blanks - (starGap - 5)))
            + "]";
        var middleRow = rows / 2;

        return $$"""
            {
              "name": "{{name}}",
              "windowRows": {{rows}},
              "symbols": [ { "name": "A" }, { "name": "Blank" }, { "name": "Star", "scatter": true } ],
              "reels": [ {{strip}}, {{strip}}, {{strip}} ],
              "paylines": [ { "name": "Center", "rows": [{{middleRow}}, {{middleRow}}, {{middleRow}}] } ],
              "paytable": [ { "symbol": "A", "pays": { "3": 5 } } ],
              "features": [
                { "kind": "scatterPickBonus", "name": "Bonus", "scatter": "Star",
                  "requiredReels": [0, 1, 2],
                  "prizes": [ { "value": 8, "count": 1 } ],
                  "blanks": { "count": 1, "consolation": 2 } }
              ]
            }
            """;
    }

    // Strip layout for both fixtures: Star, A, A, A, A, Blank..., Star, Blank...
    // (starGap - 5) blanks separate the first Star's block from the second Star, so the two
    // Stars sit at cyclic positions 0 and starGap, both >= rows apart in either direction.
    private static readonly string FourRowGame = BuildFixture(rows: 4, stripLength: 16, starGap: 8, "Four Row Fixture");
    private static readonly string FiveRowGame = BuildFixture(rows: 5, stripLength: 20, starGap: 10, "Five Row Fixture");

    [Theory]
    [InlineData(4, 16, 8, 0.015625, 0.078125, 0.125, 0.75, 0.828125)]
    [InlineData(5, 20, 10, 0.008, 0.04, 0.125, 0.75, 0.79)]
    public void SyntheticGame_AnalyticNumbers_MatchTheHandDerivedValues(
        int rows, int stripLength, int starGap,
        double expectedHitFrequency, double expectedLineRtp,
        double expectedTriggerProbability, double expectedBonusRtp, double expectedTotalRtp)
    {
        var json = BuildFixture(rows, stripLength, starGap, $"{rows}-Row Fixture");
        var loaded = GameDefinitionLoader.TryLoad(json, out var definition, out var errors);
        Assert.True(loaded, "Fixture failed to load:\n  " + string.Join("\n  ", errors));

        var analysis = GameAnalyzer.Analyze(definition!);

        // Hand-derived exact rationals: P(AAA) = (4/stripLength)^3; a reel's scatter-trigger
        // probability is (2 Stars * rows) / stripLength, cubed for 3 required reels; the bonus
        // mean (6) and its RTP contribution follow from PickBonus's own closed form, unrelated
        // to window height. All are exact in binary floating point (every fraction here has a
        // power-of-two denominator), so these are hard equalities, not convergence bands.
        Assert.Equal(expectedHitFrequency, analysis.HitFrequency, 12);
        Assert.Equal(expectedLineRtp, analysis.LineRtp, 12);
        Assert.Equal(expectedTriggerProbability, analysis.TriggerProbability, 12);
        Assert.Equal(expectedBonusRtp, analysis.BonusRtp, 12);
        Assert.Equal(expectedTotalRtp, analysis.TotalRtp, 12);
        Assert.Equal(rows, definition!.Reels.Rows);
    }

    [Theory]
    [InlineData(4, 16, 8)]
    [InlineData(5, 20, 10)]
    public async Task SyntheticGame_SimulatedRtp_ConvergesOnTheAnalyticValue(int rows, int stripLength, int starGap)
    {
        var json = BuildFixture(rows, stripLength, starGap, $"{rows}-Row Convergence Fixture");
        var loaded = GameDefinitionLoader.TryLoad(json, out var definition, out var errors);
        Assert.True(loaded, "Fixture failed to load:\n  " + string.Join("\n  ", errors));

        var analytic = GameAnalyzer.Analyze(definition!);
        var plan = new RunPlan($"multirow-{rows}", 0xA11CE_5EED_0000UL + (ulong)rows, WorkerCount: 4, TargetSpins: 3_000_000);
        var result = await new GameRunner(definition!, plan).RunAsync();

        Assert.True(result.BonusTriggers > 0, "No bonus ever triggered; the scatter path did not run.");

        var band = NormalQuantile.TwoSided999 * analytic.SigmaPerUnitWagered / Math.Sqrt(3_000_000);
        var totalRtp = result.Totals.MeasuredRtp;
        Assert.True(
            Math.Abs(totalRtp - analytic.TotalRtp) <= band,
            $"{rows}-row total RTP {totalRtp:R} is outside the analytic band {analytic.TotalRtp:R} +/-{band:R}.");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void WindowRowsOutsideBounds_IsRejectedNamingTheBounds(int badRows)
    {
        var json = FourRowGame.Replace("\"windowRows\": 4", $"\"windowRows\": {badRows}");

        var loaded = GameDefinitionLoader.TryLoad(json, out _, out var errors);

        Assert.False(loaded);
        Assert.Contains(errors, e =>
            e.Contains("windowRows", StringComparison.Ordinal)
            && e.Contains($"{StripReelSet.MinRows}", StringComparison.Ordinal)
            && e.Contains($"{StripReelSet.MaxRows}", StringComparison.Ordinal));
    }

    /// <summary>Regression pin: the two shipped games are unaffected by the [3,5] bound (both already declare windowRows: 3).</summary>
    [Theory]
    [InlineData(GameFiles.OrcaDive)]
    [InlineData(GameFiles.ClassicThreeReel)]
    public void ShippedGames_StillLoadAtTheirDeclaredThreeRowWindow(string name)
    {
        var definition = GameFiles.Load(name);
        Assert.Equal(3, definition.Reels.Rows);
    }
}
