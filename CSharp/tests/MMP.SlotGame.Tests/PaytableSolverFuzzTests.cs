using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Randomized target-RTP and paytable-shape fuzzing over <see cref="PaytableSolver.Solve"/>.
/// <see cref="SolverTests"/> pins the shipped presets at their exact configured targets; this
/// suite instead throws small, random canonical paytables at random targets in [0.80, 0.99) to
/// check the solver's two universal promises: it never compiles a negative pay, and the
/// recomputed <see cref="AnalyticMath.RealizedBaseRtp"/> lands near whatever target it was asked
/// to hit, for tables it was never tuned against.
/// </summary>
[Trait("Category", "Fast")]
public sealed class PaytableSolverFuzzTests
{
    private const int Iterations = 2_000;

    [Fact]
    public void Solve_AcrossRandomTargetsAndSmallPaytables_HitsTargetWithinToleranceAndNeverPaysNegative()
    {
        var rng = new Random(4001);
        var preset = StandardReelPresets.All["Classic3"];
        var reels = preset.BuildReels();

        for (var i = 0; i < Iterations; i++)
        {
            var targetRtp = 0.80 + rng.NextDouble() * 0.19; // [0.80, 0.99)
            var canonical = RandomCanonicalPaytable(rng, reels.ReelCount, symbolCount: rng.Next(2, 9));

            var scaled = PaytableSolver.Solve(reels, preset.Paylines, canonical, targetRtp, SimulationConfig.Wager);

            Assert.All(scaled.Pays.Values, pay => Assert.True(
                pay.Value >= 0,
                $"Iteration {i}: solved pay {pay.Value} is negative for target {targetRtp:R}."));

            var realized = AnalyticMath.RealizedBaseRtp(reels, preset.Paylines, scaled, SimulationConfig.Wager);

            // Half-millicent rounding residual per compiled pay, summed over a handful of
            // cells: bounded generously (not SolverTests' tight per-preset budget) because this
            // table is a random shape, not one already tuned to land cleanly at its target.
            var tolerance = 0.02 + canonical.Pays.Count * 0.001;
            Assert.True(
                Math.Abs(realized - targetRtp) <= tolerance,
                $"Iteration {i}: realized RTP {realized:R} vs target {targetRtp:R} "
                + $"(tolerance {tolerance:R}, {canonical.Pays.Count} pay cells).");
        }
    }

    /// <summary>A sparse random canonical table over (symbol, run-length) cells, always with at least one positive pay so the solver's zero-EV guard never fires on generated noise.</summary>
    private static Paytable RandomCanonicalPaytable(Random rng, int reelCount, int symbolCount)
    {
        var pays = new Dictionary<(byte, int), double>();
        for (byte symbol = 0; symbol < symbolCount; symbol++)
        {
            for (var count = Paytable.MinimumWinningRun; count <= reelCount; count++)
            {
                if (rng.Next(3) == 0) continue;
                pays[(symbol, count)] = rng.Next(1, 5000);
            }
        }
        if (pays.Count == 0) pays[(0, Paytable.MinimumWinningRun)] = 1;
        return new Paytable(pays);
    }
}
