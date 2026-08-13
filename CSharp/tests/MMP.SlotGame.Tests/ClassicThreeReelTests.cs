using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The second config-driven game, and the reason it exists: it is a DIFFERENT SHAPE. Three
/// reels instead of five, six symbols instead of ten, strips of 22/24/22 instead of
/// 26/29/26/29/26, a wild covering two symbols instead of four, and no feature at all. It
/// loads through the same loader, evaluates through the same evaluator and is analyzed by the
/// same analyzer as Orca Dive, with no code anywhere that knows which is which.
///
/// The analyzer is then checked against an INDEPENDENT exhaustive enumeration in the spirit of
/// <see cref="ExhaustiveGroundTruthTests"/>: all 22 x 24 x 22 = 11,616 stop combinations, with
/// the matching rules re-derived here from scratch. It shares the DATA with the engine (symbol
/// names, strip contents, pay values) and none of the BEHAVIOR. If both the analyzer and this
/// enumeration had the same bug in run matching, the Orca Dive numbers would still be
/// right and this test would still be green, which is why the two games together are worth
/// more than either alone.
/// </summary>
[Trait("Category", "Fast")]
public sealed class ClassicThreeReelTests
{
    private static GameDefinition Game => GameFiles.Load(GameFiles.ClassicThreeReel);

    [Fact]
    public void Definition_LoadsADifferentGeometryThroughTheSameLoader()
    {
        var game = Game;

        Assert.Equal(3, game.ReelCount);
        Assert.Equal(6, game.Symbols.Count);
        Assert.Equal(22, game.Reels.StopCount(0));
        Assert.Equal(24, game.Reels.StopCount(1));
        Assert.Equal(22, game.Reels.StopCount(2));
        Assert.Equal(22L * 24 * 22, game.StopCombinations);
        Assert.Null(game.Bonus);
    }

    /// <summary>A game without a feature contributes no bonus term and no bonus variance.</summary>
    [Fact]
    public void WithoutAFeature_TheBonusTermsVanish()
    {
        var analysis = GameAnalyzer.Analyze(Game);

        Assert.Equal(0, analysis.TriggerCombinations);
        Assert.Equal(0.0, analysis.BonusRtp);
        Assert.Equal(0.0, analysis.BonusSigma);
        Assert.Equal(analysis.LineRtp, analysis.TotalRtp);
        Assert.Equal(analysis.LineSigma, analysis.SigmaPerUnitWagered, 12);
    }

    /// <summary>
    /// The example game should look like a real machine, not a demo that pays double. This is
    /// a guard on the FILE, not on the engine: the pay values are data, and data drifts.
    /// </summary>
    [Fact]
    public void ExampleGame_PaysAPlausibleReturn()
    {
        var analysis = GameAnalyzer.Analyze(Game);

        Assert.InRange(analysis.TotalRtp, 0.85, 0.99);
        Assert.InRange(analysis.HitFrequency, 0.05, 0.50);
    }

    [Fact]
    public void Analyzer_MatchesAnIndependentExhaustiveEnumeration()
    {
        var game = Game;
        var analysis = GameAnalyzer.Analyze(game);
        var truth = Enumerate(game);

        Assert.Equal(11_616L, truth.Combinations);
        Assert.Equal(truth.Combinations, analysis.StopCombinations);
        Assert.True(truth.PayUnits > 0, "Ground truth paid nothing; the enumeration is broken.");

        var deltas = new List<string>();
        foreach (var key in truth.Counts.Keys.Union(analysis.CombinationCounts.Keys))
        {
            var expected = truth.Counts.GetValueOrDefault(key);
            var actual = analysis.CombinationCounts.GetValueOrDefault(key);
            if (expected != actual)
                deltas.Add($"{game.Categories[key.CategoryIndex].Name} x{key.Count}: analyzer {actual:N0}, exhaustive {expected:N0}");
        }

        Assert.True(
            deltas.Count == 0,
            "Analyzer disagrees with the independent enumeration:\n  " + string.Join("\n  ", deltas));

        Assert.Equal(truth.Hits, analysis.HitCombinations);

        // truth.PayUnits sums PayFor(run), which is scaled by Millicents.ScaleFactor
        // (PayCategory.PayFor); analysis.LineRtp is already divided back to real units, so
        // this must be too.
        var exhaustiveRtp = (double)truth.PayUnits / Millicents.ScaleFactor / truth.Combinations;
        var relative = Math.Abs(analysis.LineRtp - exhaustiveRtp) / exhaustiveRtp;
        Assert.True(
            relative <= 1e-12,
            $"Analyzer line RTP {analysis.LineRtp:R} disagrees with exhaustive {exhaustiveRtp:R} (relative {relative:R}).");
    }

    // ---------------------------------------------------------------------------
    // Independent ground truth. Nothing below this line calls engine evaluation code.
    // ---------------------------------------------------------------------------

    private sealed record Truth(
        Dictionary<(int CategoryIndex, int Count), long> Counts, long Hits, long PayUnits, long Combinations);

    /// <summary>
    /// Exhaustive enumeration of all stop combinations, with the matching rules written out
    /// longhand.
    /// Every stop combination is equally likely (one uniform stop per reel, reels independent),
    /// so an unweighted count over all of them IS the exact distribution. No probabilities are
    /// multiplied anywhere in here: multiplying per-reel weights is precisely what the analyzer
    /// does, and what this exists to check.
    /// </summary>
    private static Truth Enumerate(GameDefinition game)
    {
        // Data only: symbol ids, strip contents, the center row, the pay values.
        var seven = (byte)game.SymbolId("Seven");
        var bar = (byte)game.SymbolId("Bar");
        var bell = (byte)game.SymbolId("Bell");
        var wild = (byte)game.SymbolId("Wild");
        var cherry = (byte)game.SymbolId("Cherry");
        var lemon = (byte)game.SymbolId("Lemon");

        var strips = new byte[3][];
        for (var reel = 0; reel < 3; reel++)
        {
            var strip = game.Reels.Strip(reel);
            strips[reel] = new byte[strip.Length];
            for (var stop = 0; stop < strip.Length; stop++) strips[reel][stop] = strip[stop].Id;
        }

        var counts = new Dictionary<(int, int), long>();
        long hits = 0, payUnits = 0, combinations = 0;
        var cells = new byte[3];

        for (var s0 = 0; s0 < strips[0].Length; s0++)
        for (var s1 = 0; s1 < strips[1].Length; s1++)
        for (var s2 = 0; s2 < strips[2].Length; s2++)
        {
            combinations++;

            // Center row: one position along the strip from the stop, wrapping cyclically.
            cells[0] = strips[0][(s0 + 1) % strips[0].Length];
            cells[1] = strips[1][(s1 + 1) % strips[1].Length];
            cells[2] = strips[2][(s2 + 1) % strips[2].Length];

            var bestPay = 0;
            var bestRun = 0;
            var bestName = "";

            void Consider(string category, int run, int pay)
            {
                if (pay == 0 || pay < bestPay) return;
                if (pay == bestPay && run <= bestRun) return;
                bestPay = pay;
                bestRun = run;
                bestName = category;
            }

            // Pay VALUES come from the definition, because they are data and this test is not
            // about transcribing them. Everything about WHICH category matches, and how long
            // the run is, is re-derived below and shares nothing with the engine.
            int Pay(string category, int run) => game.Category(category).PayFor(run);

            // Plain symbols: nothing stands in for them.
            foreach (var (plainName, plain) in new[] { ("Seven", seven), ("Bar", bar), ("Bell", bell) })
            {
                var run = RunOf(cells, c => c == plain);
                Consider(plainName, run, Pay(plainName, run));
            }

            // The wild on its own account.
            var wildRun = RunOf(cells, c => c == wild);
            Consider("Wild", wildRun, Pay("Wild", wildRun));

            // Fruit: the wild stands in, but the run needs at least one real fruit.
            foreach (var (fruitName, fruit) in new[] { ("Cherry", cherry), ("Lemon", lemon) })
            {
                var run = RunOf(cells, c => c == fruit || c == wild);
                var real = false;
                for (var i = 0; i < run; i++) real |= cells[i] == fruit;
                Consider(fruitName, run, real ? Pay(fruitName, run) : 0);
            }

            // The fruit group: any fruit continues it, the wild does not.
            var mixed = RunOf(cells, c => c == cherry || c == lemon);
            Consider("MixedFruit", mixed, Pay("MixedFruit", mixed));

            if (bestPay == 0) continue;

            hits++;
            payUnits += bestPay;
            var key = (game.Category(bestName).Index, bestRun);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return new Truth(counts, hits, payUnits, combinations);
    }

    private static int RunOf(byte[] cells, Func<byte, bool> matches)
    {
        var run = 0;
        while (run < cells.Length && matches(cells[run])) run++;
        return run;
    }
}
