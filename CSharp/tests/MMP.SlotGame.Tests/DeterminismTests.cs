using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// AC-6 / RT-13 — the reproducibility contract is (masterSeed, workerCount).
///
/// Same pair twice → identical totals, field for field, in integer millicents. That
/// holds only because the engine uses fixed pre-assigned quotas over long-lived workers;
/// a Parallel.For with dynamic work-stealing would partition differently run to run and
/// this suite is what would catch that regression.
///
/// Note what is deliberately NOT asserted: that different worker counts produce
/// identical totals. They do not, and should not — each worker draws its own stream, so
/// repartitioning changes which spins exist. The contract is the PAIR. What must still
/// hold across worker counts is convergence to the same analytic RTP, and that is
/// asserted instead.
/// </summary>
[Trait("Category", "Fast")]
public sealed class DeterminismTests
{
    private const long Spins = 500_000;
    private const ulong Seed = 0xA5A5_5A5A_1234_9876UL;

    private static async Task<RunSnapshot> RunAsync(ulong seed, int workers, long spins = Spins)
    {
        var game = TestGame.Build(
            TestGame.DefaultPreset, masterSeed: seed, workerCount: workers, targetSpins: spins);
        return await game.Engine().RunAsync(telemetry: null);
    }

    [Fact]
    public async Task SameSeedAndWorkerCount_ProducesIdenticalSnapshots()
    {
        var first = await RunAsync(Seed, workers: 8);
        var second = await RunAsync(Seed, workers: 8);

        Assert.Equal(first, second);                       // record struct: every field
        Assert.Equal(Spins, first.Spins);
        Assert.Equal(Spins * SimulationConfig.Wager.Value, first.WageredMillicents);
        Assert.True(first.ReturnedMillicents > 0, "The run returned nothing — the engine paid no wins at all.");
    }

    [Fact]
    public async Task SameSeedAndWorkerCount_IsStableAcrossManyRepeats()
    {
        // Three repeats at a smaller N: a torn accumulation or a stolen partition tends
        // to show up intermittently, so one repeat is not evidence.
        var baseline = await RunAsync(Seed, workers: 8, spins: 120_000);
        for (var i = 0; i < 3; i++)
            Assert.Equal(baseline, await RunAsync(Seed, workers: 8, spins: 120_000));
    }

    [Fact]
    public async Task DifferentSeed_ProducesDifferentTotals()
    {
        var a = await RunAsync(Seed, workers: 8);
        var b = await RunAsync(Seed ^ 0xFFFF_FFFF_FFFF_FFFFUL, workers: 8);

        Assert.Equal(a.Spins, b.Spins);
        Assert.Equal(a.WageredMillicents, b.WageredMillicents);
        Assert.NotEqual(a.ReturnedMillicents, b.ReturnedMillicents);
    }

    /// <summary>
    /// Worker count is part of the reproducibility contract, so different counts may
    /// differ — but every count must still converge on the same game. Band is 5 analytic
    /// sigma at this N, wide on purpose: this test asserts "same game", not "converged".
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    public async Task DifferentWorkerCounts_AllConvergeOnTheSameAnalyticRtp(int workers)
    {
        var game = TestGame.Build(
            TestGame.DefaultPreset, masterSeed: Seed, workerCount: workers, targetSpins: Spins);
        var analytic = game.Analyse();

        var snapshot = await game.Engine().RunAsync(telemetry: null);

        Assert.Equal(Spins, snapshot.Spins);
        Assert.Equal(Spins * SimulationConfig.Wager.Value, snapshot.WageredMillicents);

        var band = 5.0 * analytic.SigmaPerUnitWagered / Math.Sqrt(Spins);
        var delta = Math.Abs(snapshot.MeasuredRtp - analytic.TotalRtp);
        Assert.True(
            delta <= band,
            $"""
             {workers} worker(s): measured RTP is outside 5 analytic sigma.
               measured = {snapshot.MeasuredRtp:R}
               analytic = {analytic.TotalRtp:R}
               sigma    = {analytic.SigmaPerUnitWagered:R}, N = {Spins}
               delta    = {delta:R}, band = {band:R}
             """);
    }

    /// <summary>
    /// A worker count that does not divide the spin target must still run EXACTLY the
    /// target: worker 0 absorbs the remainder. An off-by-remainder here would quietly
    /// shrink every run whose N is not a multiple of the worker count.
    /// </summary>
    [Theory]
    [InlineData(7, 100_001)]
    [InlineData(8, 999_999)]
    [InlineData(3, 10)]
    public async Task WorkerQuotasCoverTheTargetExactly_IncludingTheRemainder(int workers, long spins)
    {
        var snapshot = await RunAsync(Seed, workers, spins);

        Assert.Equal(spins, snapshot.Spins);
        Assert.Equal(spins * SimulationConfig.Wager.Value, snapshot.WageredMillicents);
    }
}
