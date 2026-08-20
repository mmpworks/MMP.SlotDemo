using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;
using Xunit;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The claim this whole series is built on, proven on every commit.
///
/// Simulation converging on the exhaustively enumerated RTP is the series' headline result:
/// two independent methods, one answer. Until now every test of it lived behind
/// SLOTGAME_SLOW_TESTS, so the default suite went green without once checking the thing the
/// articles are about. The gated tests are the thorough ones and they stay; this is the
/// cheap version that always runs.
///
/// Seeded and deterministic, so it either passes for this build or it does not. The spin
/// counts are small enough to keep the suite fast and large enough that the band is a real
/// constraint rather than a formality.
/// </summary>
public sealed class FastConvergenceTests
{
    private const int Workers = 4;

    [Theory]
    [InlineData(GameFiles.ClassicThreeReel, 400_000, 0x9E3779B9_7F4A7C15UL)]
    [InlineData(GameFiles.ClassicThreeReel, 400_000, 0x2545F491_4F6CDD1DUL)]
    [InlineData(GameFiles.OrcaDive, 400_000, 0x1A5CB0DE_7EA5E5EDUL)]
    [InlineData(GameFiles.OrcaDive, 400_000, 0xD1B54A32_D192ED03UL)]
    public async Task A_simulated_run_lands_inside_the_analytic_band(string game, long spins, ulong seed)
    {
        var definition = GameFiles.Load(game);
        var analysis = GameAnalyzer.Analyze(definition);

        var plan = new RunPlan($"fast-{game}-{seed}", seed, Workers, spins);
        var result = await new GameRunner(definition, plan, analysis).RunAsync();

        // Guard first: a run that measured nothing would satisfy any band.
        Assert.Equal(spins, result.Totals.Spins);
        Assert.True(result.Totals.WageredMillicents > 0, "nothing was wagered.");

        // The same band the proving ground draws: z * sigma / sqrt(N), at 99.9% here rather
        // than 99% so a correct engine effectively never trips it on a seeded run.
        var halfWidth = NormalQuantile.TwoSided999 * analysis.SigmaPerUnitWagered / Math.Sqrt(spins);
        var measured = result.Totals.MeasuredRtp;
        var deviation = Math.Abs(measured - analysis.TotalRtp);

        Assert.True(
            deviation <= halfWidth,
            $"{game} seed {seed:X}: measured {measured:F6} against analytic {analysis.TotalRtp:F6}, "
            + $"off by {deviation:F6} with a 99.9% half-width of {halfWidth:F6}.");
    }

    /// <summary>
    /// Splitting the same work across a different number of workers must not move the
    /// answer. Integer millicents make the total order-independent, which is the reason the
    /// engine counts money the way it does; this checks the property rather than the claim.
    /// </summary>
    [Theory]
    [InlineData(1, 8)]
    [InlineData(2, 5)]
    public async Task Worker_count_does_not_change_the_totals(int workersA, int workersB)
    {
        const long spins = 120_000;
        const ulong seed = 0x0DDC0FFEE_0BADF00DUL;
        var definition = GameFiles.Load(GameFiles.OrcaDive);
        var analysis = GameAnalyzer.Analyze(definition);

        var a = await new GameRunner(definition, new RunPlan("a", seed, workersA, spins), analysis).RunAsync();
        var b = await new GameRunner(definition, new RunPlan("b", seed, workersB, spins), analysis).RunAsync();

        Assert.Equal(a.Totals.Spins, b.Totals.Spins);
        Assert.Equal(a.Totals.WageredMillicents, b.Totals.WageredMillicents);
    }

    /// <summary>
    /// The same seed and the same plan produce the same totals, exactly. Replayability is
    /// what makes any of these numbers arguable rather than anecdotal.
    /// </summary>
    [Fact]
    public async Task The_same_seed_reproduces_the_run_exactly()
    {
        const long spins = 120_000;
        const ulong seed = 0xF00DFACE_12345678UL;
        var definition = GameFiles.Load(GameFiles.OrcaDive);
        var analysis = GameAnalyzer.Analyze(definition);

        var first = await new GameRunner(definition, new RunPlan("r", seed, Workers, spins), analysis).RunAsync();
        var second = await new GameRunner(definition, new RunPlan("r", seed, Workers, spins), analysis).RunAsync();

        Assert.Equal(first.Totals.Spins, second.Totals.Spins);
        Assert.Equal(first.Totals.WageredMillicents, second.Totals.WageredMillicents);
        Assert.Equal(first.Totals.ReturnedMillicents, second.Totals.ReturnedMillicents);
        Assert.Equal(first.Totals.Hits, second.Totals.Hits);
        Assert.Equal(first.BonusTriggers, second.BonusTriggers);
    }
}
