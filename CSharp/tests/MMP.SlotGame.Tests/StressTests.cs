using System.Threading.Channels;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Stress tier — the lossy edges of the pipeline must never touch the exact edge.
///
/// Three properties, all of them about the boundary between the counters (exact,
/// integer, load-bearing) and everything around them (telemetry, cancellation, run
/// lifecycle — lossy by design):
///
///   * cancellation leaves the counters SELF-CONSISTENT, not merely non-crashing;
///   * a flooded drop-oldest telemetry channel changes no counter;
///   * reset is by new instance (RT-18) — a fresh engine starts at zero and cannot
///     disturb a previous run's totals.
/// </summary>
[Trait("Category", "Stress")]
public sealed class StressTests
{
    private const ulong Seed = 0x7E57_C0DE_7E57_C0DEUL;

    [StressFact]
    public async Task CancellationMidRun_LeavesConsistentPartialTotals()
    {
        // One worker: the worker is guaranteed to have started before we cancel, so this
        // exercises the mid-run cancellation path and not the never-scheduled path.
        var game = TestGame.Build(
            TestGame.DefaultPreset, masterSeed: Seed, workerCount: 1, targetSpins: 500_000_000);
        var engine = game.Engine();

        using var cts = new CancellationTokenSource();
        var run = engine.RunAsync(telemetry: null, observer: null, ct: cts.Token);

        var spun = await SpinUntilAsync(engine, atLeast: 4096, timeout: TimeSpan.FromSeconds(30));
        Assert.True(spun, "The engine never recorded a batch; cancellation would have been tested against nothing.");

        await cts.CancelAsync();
        try { await run; }
        catch (OperationCanceledException) { /* cancellation is a valid termination */ }

        var snapshot = engine.Totals.Snapshot();

        // The load-bearing property: partial totals are internally consistent.
        Assert.Equal(snapshot.Spins * SimulationConfig.Wager.Value, snapshot.WageredMillicents);
        Assert.True(snapshot.Spins > 0, "Cancelled before any spin was accumulated.");
        Assert.True(snapshot.Spins < game.Config.TargetSpins, "Cancellation did not actually stop the run early.");
        Assert.True(snapshot.Hits <= snapshot.Spins, $"More hits ({snapshot.Hits}) than spins ({snapshot.Spins}).");
        Assert.True(snapshot.ReturnedMillicents >= 0, "Negative returns — accumulation is corrupt.");
    }

    [StressFact]
    public async Task FloodedDropOldestTelemetry_DoesNotChangeTheFinalSnapshot()
    {
        const long spins = 400_000;

        // No reader at all, capacity 4, drop-oldest: the worst case for the telemetry
        // seam. TryWrite must never block a producer and must never touch a counter.
        var channel = Channel.CreateBounded<TelemetrySample>(new BoundedChannelOptions(4)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var withTelemetry = await TestGame
            .Build(TestGame.DefaultPreset, masterSeed: Seed, workerCount: 8, targetSpins: spins)
            .Engine()
            .RunAsync(channel.Writer);

        var withoutTelemetry = await TestGame
            .Build(TestGame.DefaultPreset, masterSeed: Seed, workerCount: 8, targetSpins: spins)
            .Engine()
            .RunAsync(telemetry: null);

        Assert.Equal(withoutTelemetry, withTelemetry);

        // The channel completed and whatever survived is an ABSOLUTE snapshot (RT-19),
        // so a dropped sample costs a chart point and nothing else.
        var samples = new List<TelemetrySample>();
        await foreach (var sample in channel.Reader.ReadAllAsync()) samples.Add(sample);

        Assert.NotEmpty(samples);
        Assert.All(samples, s =>
        {
            Assert.Equal(s.Totals.Spins * SimulationConfig.Wager.Value, s.Totals.WageredMillicents);
            Assert.True(s.Totals.Spins <= spins);
        });
        Assert.Equal(withTelemetry, samples[^1].Totals);
    }

    [StressFact]
    public async Task SlowConsumer_NeverStallsTheSimulation()
    {
        const long spins = 400_000;

        var channel = Channel.CreateBounded<TelemetrySample>(new BoundedChannelOptions(2)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        var game = TestGame.Build(
            TestGame.DefaultPreset, masterSeed: Seed, workerCount: 8, targetSpins: spins);

        // A consumer that reads once every 50 ms — far slower than the producers.
        var consumer = Task.Run(async () =>
        {
            await foreach (var _ in channel.Reader.ReadAllAsync())
                await Task.Delay(50);
        });

        var run = game.Engine().RunAsync(channel.Writer);
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromMinutes(2)));

        Assert.True(ReferenceEquals(finished, run), "The simulation was throttled by its telemetry consumer.");
        var snapshot = await run;
        Assert.Equal(spins, snapshot.Spins);

        await consumer;
    }

    /// <summary>
    /// RT-18: reset swaps in a fresh accumulator, never zeroes live fields. Two engines
    /// over the same config are fully isolated, and a new engine reads zero before it runs.
    /// </summary>
    [StressFact]
    public async Task ResetByNewInstance_IsolatesRuns()
    {
        var game = TestGame.Build(
            TestGame.DefaultPreset, masterSeed: Seed, workerCount: 4, targetSpins: 100_000);

        var first = game.Engine();
        var firstSnapshot = await first.RunAsync(telemetry: null);

        var second = game.Engine();
        Assert.Equal(new RunSnapshot(0, 0, 0, 0), second.Totals.Snapshot());

        var secondSnapshot = await second.RunAsync(telemetry: null);

        Assert.Equal(firstSnapshot, secondSnapshot);                 // same seed, same result
        Assert.Equal(firstSnapshot, first.Totals.Snapshot());        // first run's totals untouched
        Assert.NotSame(first.Totals, second.Totals);
    }

    private static async Task<bool> SpinUntilAsync(SimulationEngine engine, long atLeast, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (engine.Totals.Snapshot().Spins >= atLeast) return true;
            await Task.Delay(10);
        }
        return false;
    }
}
