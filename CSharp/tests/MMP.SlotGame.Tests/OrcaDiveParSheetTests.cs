using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The Orca Dive definition against its published PAR sheet (docs/par-orca-dive.md).
///
/// Everything here runs off games/orca-dive.json through the public loader. There is no
/// Orca Dive code in the engine to test: the strips, the symbols, the wild substitution,
/// the mixed-seven group, the pay table and the scatter bonus are all data, and if the file
/// and the engine disagree these numbers move.
///
/// The combination table is asserted as EXACT INTEGERS with no tolerance. That is the whole
/// point of the exercise: a published deconstruction gives 31 independent integer counts out
/// of 14,781,416, and a rule set that reproduces all 31 is a rule set that matches the real
/// machine. Any tolerance would let a wrong wild rule or a wrong tie-break slip through,
/// because several of the wrong rule sets still land within a fraction of a percent on RTP
/// while getting individual categories badly wrong.
/// </summary>
[Trait("Category", "Fast")]
public sealed class OrcaDiveParSheetTests
{
    private static GameDefinition Game => GameFiles.Load(GameFiles.OrcaDive);

    private const long StopCombinations = 14_781_416;

    /// <summary>The published table, keyed by (category name, run length). Transcribed, not computed.</summary>
    private static readonly Dictionary<(string Category, int Count), long> Published = new()
    {
        [("Red7", 5)] = 4, [("Red7", 4)] = 80, [("Red7", 3)] = 1_144,
        [("Green7", 5)] = 24, [("Green7", 4)] = 240, [("Green7", 3)] = 3_432,
        [("Blue7", 5)] = 54, [("Blue7", 4)] = 360, [("Blue7", 3)] = 3_432,
        [("Seal", 5)] = 480, [("Seal", 4)] = 3_680, [("Seal", 3)] = 26_000,
        [("WildOrca", 5)] = 2, [("WildOrca", 4)] = 22, [("WildOrca", 3)] = 572,
        [("WildOrca", 2)] = 15_080, [("WildOrca", 1)] = 980_200,
        [("Salmon", 5)] = 1_598, [("Salmon", 4)] = 8_756, [("Salmon", 3)] = 48_672,
        [("Herring", 5)] = 2_590, [("Herring", 4)] = 14_212, [("Herring", 3)] = 63_388,
        [("Squid", 5)] = 4_373, [("Squid", 4)] = 18_333, [("Squid", 3)] = 107_952,
        [("Mackerel", 5)] = 4_198, [("Mackerel", 4)] = 17_598, [("Mackerel", 3)] = 103_584,
        [("MixedSeven", 5)] = 5_210, [("MixedSeven", 4)] = 16_960, [("MixedSeven", 3)] = 64_064,
    };

    [Fact]
    public void CombinationTable_ReproducesThePublishedCountsExactly()
    {
        var analysis = GameAnalyzer.Analyze(Game);

        Assert.Equal(StopCombinations, analysis.StopCombinations);

        var deltas = new List<string>();
        foreach (var ((category, count), expected) in Published)
        {
            var actual = analysis.CountFor(category, count);
            if (expected != actual)
                deltas.Add($"{category} x{count}: computed {actual:N0}, published {expected:N0} (delta {actual - expected:+#;-#;0})");
        }

        // The other direction: nothing may pay in a category or at a length the sheet omits.
        foreach (var ((categoryIndex, count), actual) in analysis.CombinationCounts)
        {
            var category = Game.Categories[categoryIndex].Name;
            if (!Published.ContainsKey((category, count)))
                deltas.Add($"{category} x{count}: computed {actual:N0}, published nothing at all");
        }

        Assert.True(
            deltas.Count == 0,
            "Computed combination counts differ from the published PAR sheet:\n  " + string.Join("\n  ", deltas));

        Assert.Equal(1_516_294L, analysis.HitCombinations);
        Assert.Equal(1_516_294L, Published.Values.Sum());
    }

    /// <summary>
    /// The published returns, to the precision they were published at. The line term is also
    /// pinned as an exact rational: 8,809,880 pay units over 14,781,416 combinations, which is
    /// what makes it a hard equality rather than a rounded coincidence.
    /// </summary>
    [Fact]
    public void ExpectedReturns_MatchThePublishedPercentages()
    {
        var analysis = GameAnalyzer.Analyze(Game);

        Assert.Equal(8_809_880.0 / StopCombinations, analysis.LineRtp, 12);
        Assert.Equal(0.59601, analysis.LineRtp, 5);
        Assert.Equal(0.26510, analysis.BonusRtp, 5);
        Assert.Equal(0.86111, analysis.TotalRtp, 5);
        Assert.Equal(0.10258, analysis.HitFrequency, 5);
        Assert.True(analysis.SigmaPerUnitWagered > 0, "A zero sigma would make every convergence band vacuous.");
    }

    /// <summary>
    /// The definition file says what the PAR sheet says. These are the declarations the loader
    /// verifies the strips against, so this test is really asserting that the published table
    /// is present in the file and being enforced, not merely that the strips are self-consistent.
    /// </summary>
    [Fact]
    public void Definition_DeclaresTheGeometryThePublishedSheetStates()
    {
        var game = Game;

        Assert.Equal("Orca Dive", game.Name);
        Assert.Equal(5, game.ReelCount);
        Assert.Equal(10, game.Symbols.Count);
        Assert.Equal(3, game.Reels.Rows);
        Assert.Single(game.Paylines);
        Assert.Equal(StopCombinations, game.StopCombinations);

        int[] expectedStops = [26, 29, 26, 29, 26];
        for (var reel = 0; reel < expectedStops.Length; reel++)
            Assert.Equal(expectedStops[reel], game.Reels.StopCount(reel));
    }

    /// <summary>
    /// Penguin scatter geometry. Two Penguin symbols per 26-stop strip on reels 1/3/5 and none on
    /// reels 2/4, placed so that no two sit within 3 consecutive stops. That placement rule is
    /// the whole reason the trigger probability is a clean (6/26)^3: overlapping Penguin windows
    /// would double-count stops and drop it below 6 per reel.
    /// </summary>
    [Fact]
    public void PenguinScatter_HasDisjointWindowsAndThePublishedTriggerRate()
    {
        var game = Game;
        var bonus = game.Bonus;
        Assert.NotNull(bonus);
        Assert.Equal([0, 2, 4], bonus.RequiredReels);

        for (var reel = 0; reel < game.ReelCount; reel++)
        {
            var strip = game.Reels.Strip(reel);
            var positions = new List<int>();
            for (var stop = 0; stop < strip.Length; stop++)
            {
                if (strip[stop].Id == bonus.ScatterSymbolId) positions.Add(stop);
            }

            if (!bonus.RequiredReels.Contains(reel))
            {
                Assert.Empty(positions);
                continue;
            }

            Assert.Equal(2, positions.Count);
            var gap = positions[1] - positions[0];
            var wrappedGap = strip.Length - gap;
            Assert.True(
                gap >= game.Reels.Rows && wrappedGap >= game.Reels.Rows,
                $"Reel {reel + 1} Penguin symbols at {positions[0]} and {positions[1]} are closer than the "
                + $"{game.Reels.Rows}-row window (gaps {gap}/{wrappedGap}), so their trigger windows overlap.");
        }

        var analysis = GameAnalyzer.Analyze(game);
        Assert.Equal(Math.Pow(6.0 / 26.0, 3), analysis.TriggerProbability, 12);
        Assert.Equal(0.0122895, analysis.TriggerProbability, 7);
    }

    /// <summary>
    /// The pick bonus expectation, derived the long way in the PAR notes and the short way in
    /// <see cref="PickBonus"/>: 24 prizes summing to 144, 6 blanks, so 24/7 safe picks at an
    /// average of 6 plus one consolation.
    /// </summary>
    [Fact]
    public void PickBonus_ExpectedAwardMatchesThePublishedModel()
    {
        var bonus = Game.Bonus!.Bonus;

        Assert.Equal(24, bonus.PrizeCount);
        Assert.Equal(6, bonus.BlankCount);
        Assert.Equal(30, bonus.GiftCount);
        Assert.Equal(144L, bonus.PrizeTotal);
        Assert.Equal(151.0 / 7.0, bonus.Mean, 12);
        Assert.Equal(21.571429, bonus.Mean, 6);
        Assert.True(bonus.Variance > 0, "A degenerate bonus would hide every pick-simulation bug.");
    }

    /// <summary>
    /// The honest pick simulation against its own closed form. The bonus is played by drawing
    /// real pool entries without replacement, so this is the cheap check that the draw is actually
    /// uniform and actually without replacement: a with-replacement bug, an off-by-one in the
    /// swap, or a blank counted as a prize all move this mean well outside four sigma.
    /// </summary>
    [Fact]
    public void PickBonus_HonestPicks_ConvergeOnTheClosedForm()
    {
        const int rounds = 200_000;
        var bonus = Game.Bonus!.Bonus;
        var rng = SpinRng.ForWorker(0x9E3779B9_7F4A7C15UL, workerId: 0);
        var scratch = new int[bonus.GiftCount];

        long total = 0;
        var lowest = int.MaxValue;
        var highest = 0;
        for (var i = 0; i < rounds; i++)
        {
            var award = bonus.Play(ref rng, scratch);
            total += award;
            lowest = Math.Min(lowest, award);
            highest = Math.Max(highest, award);
        }

        // Floor: a first-pick blank pays the consolation alone. Ceiling: every prize, then a blank.
        Assert.Equal(bonus.Consolation, lowest);
        Assert.True(highest <= bonus.MaxAward, $"Award {highest} exceeds the {bonus.MaxAward} available.");

        var measured = (double)total / rounds;
        var band = 4.0 * Math.Sqrt(bonus.Variance / rounds);
        Assert.True(
            Math.Abs(measured - bonus.Mean) <= band,
            $"Simulated bonus mean {measured:R} is outside 4 sigma of the closed form {bonus.Mean:R} (band +/-{band:R}).");
    }

    /// <summary>
    /// The run rides the shared engine, so it inherits AC-6: same seed and same worker count
    /// give bit-identical totals. Worth pinning because this game draws from the worker stream
    /// a variable number of times per spin (every bonus pick is a draw), so stream position
    /// depends on outcomes in a way the stock preset game never exercises.
    /// </summary>
    [Fact]
    public async Task Run_IsBitIdenticalForTheSameSeedAndWorkerCount()
    {
        var plan = new RunPlan("orca-dive-determinism", 0x5EED_1234_ABCD_0001UL, WorkerCount: 4, TargetSpins: 200_000);

        var first = await new GameRunner(Game, plan).RunAsync();
        var second = await new GameRunner(Game, plan).RunAsync();

        Assert.Equal(first.Totals, second.Totals);
        Assert.Equal(first.LineMillicents, second.LineMillicents);
        Assert.Equal(first.BonusMillicents, second.BonusMillicents);
        Assert.Equal(first.LineHits, second.LineHits);
        Assert.Equal(first.BonusTriggers, second.BonusTriggers);
        Assert.True(first.BonusTriggers > 0, "No bonus triggered; the variable-length draw path did not run.");
    }
}
