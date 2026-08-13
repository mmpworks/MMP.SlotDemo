using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// AC-5 / invariant M2 — N-worker accumulation equals the single-threaded replication of
/// the same seeded partition, bit for bit.
///
/// The replication below reproduces the engine's contract exactly: quota =
/// TargetSpins / WorkerCount with worker 0 absorbing the remainder; stream =
/// SpinRng.ForWorker(masterSeed, workerId); per spin, draw the window FIRST and then
/// play each feature in schedule order (the RNG consumption order is part of the
/// contract — swap those two and the streams desynchronize even though nothing looks
/// wrong).
///
/// Money is integer millicents, so this is an EXACT equality. If it ever passes only
/// within a tolerance, floating point has leaked into the accumulation path and
/// invariant M1 is broken.
/// </summary>
[Trait("Category", "Fast")]
public sealed class ConcurrencyTests
{
    private const long Spins = 300_000;
    private const ulong Seed = 0x1357_9BDF_2468_ACE0UL;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    public async Task ParallelRun_EqualsSequentialReplication_BitForBit(int workers)
    {
        var game = TestGame.Build(
            TestGame.DefaultPreset, masterSeed: Seed, workerCount: workers, targetSpins: Spins);

        var parallel = await game.Engine().RunAsync(telemetry: null);
        var sequential = ReplicateSequentially(game);

        Assert.Equal(sequential.Spins, parallel.Spins);
        Assert.Equal(sequential.WageredMillicents, parallel.WageredMillicents);
        Assert.Equal(sequential.ReturnedMillicents, parallel.ReturnedMillicents);
        Assert.Equal(sequential.Hits, parallel.Hits);
        Assert.Equal(sequential, parallel);
    }

    /// <summary>
    /// Repeat the 8-worker equivalence several times. A torn accumulator is intermittent
    /// by nature — a single green run is not evidence that Interlocked.Add is doing its
    /// job under real contention.
    /// </summary>
    [Fact]
    public async Task ParallelRun_IsRaceFreeUnderRepetition()
    {
        var game = TestGame.Build(
            TestGame.DefaultPreset, masterSeed: Seed, workerCount: 8, targetSpins: 60_000);
        var expected = ReplicateSequentially(game);

        for (var attempt = 0; attempt < 12; attempt++)
        {
            // A fresh engine per attempt: reset is by new instance, never by zeroing (RT-18).
            var actual = await game.Engine().RunAsync(telemetry: null);
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// Maximum contention: one worker per logical core, all hammering the same four
    /// counters. Same exactness requirement.
    /// </summary>
    [Fact]
    public async Task ParallelRun_EqualsSequentialReplication_AtProcessorCount()
    {
        var workers = Math.Clamp(Environment.ProcessorCount, 1, 64);
        var game = TestGame.Build(
            TestGame.DefaultPreset, masterSeed: Seed, workerCount: workers, targetSpins: Spins);

        var parallel = await game.Engine().RunAsync(telemetry: null);
        Assert.Equal(ReplicateSequentially(game), parallel);
    }

    /// <summary>
    /// The observer hook must see exactly one callback per spin, and its per-spin totals
    /// must reconcile with the batched counters. This is the seam a diagnostic run uses;
    /// if it double-fires or drops, the diagnostic lies about the game.
    /// </summary>
    [Fact]
    public async Task SpinObserver_FiresOncePerSpin_AndReconcilesWithTheCounters()
    {
        const long spins = 20_000;
        var game = TestGame.Build(
            TestGame.DefaultPreset, masterSeed: Seed, workerCount: 1, targetSpins: spins);

        long observed = 0, observedWagered = 0, observedReturned = 0, observedHits = 0;
        void Observe(in SpinOutcome outcome)
        {
            observed++;
            observedWagered += outcome.Wagered.Value;
            observedReturned += outcome.Total.Value;
            if (outcome.IsHit) observedHits++;
        }

        var snapshot = await game.Engine().RunAsync(telemetry: null, observer: Observe);

        Assert.Equal(spins, observed);
        Assert.Equal(snapshot.Spins, observed);
        Assert.Equal(snapshot.WageredMillicents, observedWagered);
        Assert.Equal(snapshot.ReturnedMillicents, observedReturned);
        Assert.Equal(snapshot.Hits, observedHits);
    }

    // ---------------------------------------------------------------------------
    // Single-threaded replication of the engine's partition + stream contract.
    // ---------------------------------------------------------------------------

    private static RunSnapshot ReplicateSequentially(PresetGame game)
    {
        var config = game.Config;
        var reels = config.Preset.BuildReels();
        var evaluator = game.Evaluator();
        var wager = SimulationConfig.Wager;
        var window = new Symbol[reels.WindowSize];

        var perWorker = config.TargetSpins / config.WorkerCount;
        var remainder = config.TargetSpins % config.WorkerCount;

        long spins = 0, wagered = 0, returned = 0, hits = 0;

        for (var workerId = 0; workerId < config.WorkerCount; workerId++)
        {
            var quota = perWorker + (workerId == 0 ? remainder : 0);
            var rng = SpinRng.ForWorker(config.MasterSeed, workerId);

            for (long i = 0; i < quota; i++)
            {
                reels.DrawWindow(ref rng, window);
                var pay = evaluator.Evaluate(window, reels.ReelCount, reels.Rows);
                for (var f = 0; f < config.Features.Count; f++)
                    pay += config.Features[f].Play(ref rng);

                spins++;
                wagered += wager.Value;
                returned += pay.Value;
                if (pay.Value > 0) hits++;
            }
        }

        return new RunSnapshot(spins, wagered, returned, hits);
    }
}
