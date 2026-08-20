using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;
using Xunit;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The preset pipeline pays the entry for the run it reached; the loaded-game evaluator
/// searches for the best-paying prefix. Those two rules agree exactly while every pay table
/// is contiguous and non-decreasing in run length, and the preset pipeline only ever sees
/// generated tables. This pins that generator, so the equivalence is enforced rather than
/// assumed.
/// </summary>
public sealed class CanonicalPaytableShapeTests
{
    public static TheoryData<string> Presets =>
        [.. StandardReelPresets.All.Keys];

    [Theory]
    [MemberData(nameof(Presets))]
    public void The_canonical_table_pays_at_every_length_from_the_minimum_to_the_top(string presetName)
    {
        var preset = StandardReelPresets.All[presetName];
        var canonical = Paytable.CanonicalFor(preset.ReelCount, preset.Symbols.Count);

        for (byte symbol = 0; symbol < preset.Symbols.Count; symbol++)
        {
            for (var run = Paytable.MinimumWinningRun; run <= preset.ReelCount; run++)
            {
                Assert.True(
                    canonical.Pays.ContainsKey((symbol, run)),
                    $"symbol {symbol} has no pay at {run} of a kind; a gap splits the two evaluators.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void The_canonical_table_never_pays_less_for_a_longer_run(string presetName)
    {
        var preset = StandardReelPresets.All[presetName];
        var canonical = Paytable.CanonicalFor(preset.ReelCount, preset.Symbols.Count);

        for (byte symbol = 0; symbol < preset.Symbols.Count; symbol++)
        {
            for (var run = Paytable.MinimumWinningRun; run < preset.ReelCount; run++)
            {
                Assert.True(
                    canonical.Pays[(symbol, run + 1)] >= canonical.Pays[(symbol, run)],
                    $"symbol {symbol} pays less for {run + 1} than for {run}.");
            }
        }
    }

    /// <summary>
    /// Solving scales every pay by one positive factor, so the ordering the two tests above
    /// establish has to survive into the table the evaluator actually reads.
    /// </summary>
    [Theory]
    [MemberData(nameof(Presets))]
    public void Solving_preserves_the_ordering(string presetName)
    {
        var preset = StandardReelPresets.All[presetName];
        var reels = preset.BuildReels();
        var canonical = Paytable.CanonicalFor(preset.ReelCount, preset.Symbols.Count);
        var scaled = PaytableSolver.Solve(
            reels, preset.Paylines, canonical, 0.9, SimulationConfig.Wager);

        for (byte symbol = 0; symbol < preset.Symbols.Count; symbol++)
        {
            for (var run = Paytable.MinimumWinningRun; run < preset.ReelCount; run++)
            {
                Assert.True(
                    scaled.PayFor(symbol, run + 1).Value >= scaled.PayFor(symbol, run).Value,
                    $"after solving, symbol {symbol} pays less for {run + 1} than for {run}.");
            }
        }
    }
}
