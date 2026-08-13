using System.Threading.Channels;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paylines;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Simulation;

/// <summary>Per-spin diagnostic hook. Null by default and therefore free. Avoid it on 10M-spin runs.</summary>
public delegate void SpinObserver(in SpinOutcome outcome);

/// <summary>Creates the random-number generator for one worker. Tests replace it with a scripted generator.</summary>
public delegate SpinRng SpinRngFactory(int workerId);

/// <summary>
/// Plays one spin: draw, evaluate, award. RNG arrives by ref, so the stream advances in
/// the caller's worker.
/// </summary>
public delegate SpinOutcome SpinPlay(ref SpinRng rng);

/// <summary>
/// Builds one <see cref="SpinPlay"/> per worker. A game with its own evaluation rules
/// (wilds, scatter-triggered bonuses, or picks simulated round by round) can supply its
/// own <see cref="SpinPlay"/> while reusing this engine's worker scheduling and telemetry.
/// Each worker receives a separate instance because a play may own mutable scratch buffers.
/// </summary>
public delegate SpinPlay SpinPlayFactory();

public readonly record struct SpinOutcome(Millicents Wagered, Millicents BasePayout, Millicents FeaturePayout)
{
    public Millicents Total => BasePayout + FeaturePayout;
    public bool IsHit => Total.Value > 0;
}

/// <summary>
/// What the engine needs in order to schedule a run: identity, seed, worker count, quota.
/// <see cref="SimulationConfig"/> provides these values for preset games. Games with
/// unequal reel lengths or a fixed published paytable can provide the values directly.
/// </summary>
public sealed record RunPlan(string RunId, ulong MasterSeed, int WorkerCount, long TargetSpins);

/// <summary>
/// Runs spins on logical workers with fixed, pre-assigned quotas. For a fixed game
/// definition, code version, target spin count, master seed, and worker count, the result
/// is reproducible. Changing the worker count changes the RNG partition.
/// </summary>
public sealed class SimulationEngine
{
    private const int BatchSize = 4096;

    private readonly RunPlan _plan;
    private readonly SpinRngFactory _streamFactory;
    private readonly SpinPlayFactory _playFactory;

    /// <summary>Runs a preset game's reel strips, line-pay evaluator, and feature schedules.</summary>
    public SimulationEngine(SimulationConfig config, ScaledPaytable paytable, SpinRngFactory streamFactory)
        : this(
            config.Plan,
            config.Preset.BuildReels(),
            config.Preset.Paylines,
            paytable,
            config.Features,
            SimulationConfig.Wager,
            streamFactory)
    {
    }

    /// <summary>Run a compiled stock game without rebuilding its shared game data.</summary>
    public SimulationEngine(
        RunPlan plan,
        StripReelSet reels,
        IReadOnlyList<Payline> lines,
        ScaledPaytable paytable,
        IReadOnlyList<Features.FeatureSchedule> features,
        Millicents wager,
        SpinRngFactory streamFactory)
        : this(plan, streamFactory, StockPlayFactory(reels, lines, paytable, features, wager))
    {
    }

    /// <summary>Runs a game-specific <see cref="SpinPlay"/> with the engine's standard scheduling and aggregation.</summary>
    public SimulationEngine(RunPlan plan, SpinRngFactory streamFactory, SpinPlayFactory playFactory)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _plan = plan;
        _streamFactory = streamFactory;
        _playFactory = playFactory;
    }

    public RunTotals Totals { get; } = new();

    /// <summary>
    /// Writes optional progress snapshots to a caller-owned channel. The caller may use a
    /// bounded, drop-oldest channel because dropped snapshots do not affect run totals.
    /// Returns the final snapshot after every worker has completed.
    /// </summary>
    public async Task<RunSnapshot> RunAsync(
        ChannelWriter<TelemetrySample>? telemetry,
        SpinObserver? observer = null,
        CancellationToken ct = default)
    {
        var workers = new Task[_plan.WorkerCount];
        var spinsPerWorker = _plan.TargetSpins / _plan.WorkerCount;
        var remainder = _plan.TargetSpins % _plan.WorkerCount;

        for (var w = 0; w < _plan.WorkerCount; w++)
        {
            var workerId = w;
            // Deterministic quota: worker 0 absorbs the remainder.
            var quota = spinsPerWorker + (workerId == 0 ? remainder : 0);
            workers[workerId] = Task.Run(
                () => WorkerLoop(workerId, quota, telemetry, observer, ct), ct);
        }

        await Task.WhenAll(workers).ConfigureAwait(false);
        var final = Totals.Snapshot();
        telemetry?.TryWrite(new TelemetrySample(_plan.RunId, final));
        telemetry?.TryComplete();
        return final;
    }

    private void WorkerLoop(
        int workerId,
        long quota,
        ChannelWriter<TelemetrySample>? telemetry,
        SpinObserver? observer,
        CancellationToken ct)
    {
        var rng = _streamFactory(workerId);
        var play = _playFactory();

        long done = 0;
        while (done < quota)
        {
            if (ct.IsCancellationRequested) return; // per batch, not per spin

            var batch = (int)Math.Min(BatchSize, quota - done);
            long batchWagered = 0, batchReturned = 0, batchHits = 0;

            for (var i = 0; i < batch; i++)
            {
                var outcome = play(ref rng);

                batchWagered += outcome.Wagered.Value;
                var total = outcome.Total;
                batchReturned += total.Value;
                if (total.Value > 0) batchHits++;

                if (observer is not null) observer(in outcome);
            }

            Totals.AddBatch(batch, batchWagered, batchReturned, batchHits);
            done += batch;

            // Publish cumulative totals after each batch. TryWrite does not block if the channel is full.
            telemetry?.TryWrite(new TelemetrySample(_plan.RunId, Totals.Snapshot()));
        }
    }

    /// <summary>
    /// The strips are built ONCE and shared by every worker: <see cref="StripReelSet"/> is
    /// read-only after construction, so sharing costs nothing and keeps every worker on
    /// byte-identical geometry. The scratch window is per-worker.
    /// </summary>
    private static SpinPlayFactory StockPlayFactory(
        StripReelSet reels,
        IReadOnlyList<Payline> lines,
        ScaledPaytable paytable,
        IReadOnlyList<Features.FeatureSchedule> features,
        Millicents wager)
    {
        return () =>
        {
            var evaluator = new LinePayEvaluator(lines, paytable);
            // Evaluators need symbol ids, not names or flags. Allocate one compact byte
            // window per worker and overwrite it on every spin.
            var window = new byte[reels.WindowSize];

            return (ref SpinRng rng) =>
            {
                reels.DrawWindowIds(ref rng, window);
                var basePay = evaluator.EvaluateIds(window, reels.ReelCount, reels.Rows);
                var featurePay = Millicents.Zero;
                for (var f = 0; f < features.Count; f++)
                    featurePay += features[f].Play(ref rng);
                return new SpinOutcome(wager, basePay, featurePay);
            };
        };
    }
}
