using System.Threading.Channels;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paylines;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Simulation;

/// <summary>Per-spin diagnostic hook (architecture §4). Null by default and therefore free; NOT for 10M-spin runs.</summary>
public delegate void SpinObserver(in SpinOutcome outcome);

/// <summary>The seeding policy as a one-behaviour seam: tests inject scripted streams with one lambda.</summary>
public delegate SpinRng SpinRngFactory(int workerId);

/// <summary>
/// Plays ONE spin: draw, evaluate, award. RNG arrives by ref (invariant R3), so the
/// stream advances in the caller's worker.
/// </summary>
public delegate SpinOutcome SpinPlay(ref SpinRng rng);

/// <summary>
/// Builds one <see cref="SpinPlay"/> per worker. This is the seam that lets a game with
/// its own evaluation rules — wilds, scatter-triggered bonuses, honest pick simulation —
/// reuse the determinism, quota partitioning, batching and telemetry below instead of
/// re-implementing them (OrcaDive is the first such game). A worker's play owns its
/// own scratch buffers, which is why this is a factory and not one shared instance.
/// </summary>
public delegate SpinPlay SpinPlayFactory();

public readonly record struct SpinOutcome(Millicents Wagered, Millicents BasePayout, Millicents FeaturePayout)
{
    public Millicents Total => BasePayout + FeaturePayout;
    public bool IsHit => Total.Value > 0;
}

/// <summary>
/// What the engine needs in order to schedule a run: identity, seed, worker count, quota.
/// <see cref="SimulationConfig"/> exposes one of these; a game whose geometry does not fit
/// the preset shape (ragged strip lengths, a fixed published paytable) supplies its own.
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

    /// <summary>The stock composition: preset strips + line-pay evaluator + independent feature schedules.</summary>
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

    /// <summary>A game that brings its own spin rules supplies the play; everything else is shared.</summary>
    public SimulationEngine(RunPlan plan, SpinRngFactory streamFactory, SpinPlayFactory playFactory)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _plan = plan;
        _streamFactory = streamFactory;
        _playFactory = playFactory;
    }

    public RunTotals Totals { get; } = new();

    /// <summary>
    /// Telemetry goes to a caller-owned writer (lossy, bounded, drop-oldest at the
    /// caller's choice) — the exact math never depends on it. Returns the quiesced
    /// final snapshot after all workers join.
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

            // Absolute snapshot per batch; TryWrite -> dropped under load, by design.
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
            // Symbol carries a string (managed) — no stackalloc; one array per worker, reused for every spin.
            var window = new Symbol[reels.WindowSize];

            return (ref SpinRng rng) =>
            {
                reels.DrawWindow(ref rng, window);
                var basePay = evaluator.Evaluate(window, reels.ReelCount, reels.Rows);
                var featurePay = Millicents.Zero;
                for (var f = 0; f < features.Count; f++)
                    featurePay += features[f].Play(ref rng);
                return new SpinOutcome(wager, basePay, featurePay);
            };
        };
    }
}
