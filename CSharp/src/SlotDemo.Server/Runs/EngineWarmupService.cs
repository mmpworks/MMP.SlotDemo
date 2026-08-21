using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Runs a throwaway simulation at startup until tiered compilation no longer dominates
/// measured throughput.
///
/// .NET compiles a method on first use and re-optimizes hot methods later. On developer
/// hardware, the same run measured 12.4M spins/s on its first pass and 143.2M on its fourth
/// while producing the same RTP and verdict.
///
/// <see cref="CurrentState"/> keeps the run button disabled until the engine reaches the
/// throughput threshold or exhausts the configured warm-up passes.
/// </summary>
public sealed class EngineWarmupService : BackgroundService
{
    private static readonly LogCategory Category = new("Warmup");

    /// <summary>
    /// The rate at which the engine is treated as settled. Taken from the observed gap on
    /// developer hardware, where warm runs land well above 100M and cold ones an order of
    /// magnitude lower, so one threshold separates them without tuning.
    /// </summary>
    public const double SettledSpinsPerSecond = 100_000_000;

    /// <summary>Spins per warm-up pass. Large enough to trigger re-optimization, small enough to be quick.</summary>
    private const long SpinsPerPass = 2_000_000;

    /// <summary>
    /// Maximum warm-up passes. Reaching this limit enables the page with
    /// <c>Settled == false</c> so slower hardware does not wait indefinitely.
    /// </summary>
    private const int MaxPasses = 12;

    private readonly StructuredLogger _log;

    // A record struct cannot be volatile, and the background writer and the endpoint reader
    // are different threads, so the state is published through a reference the runtime can
    // swap atomically.
    private volatile WarmupStateReference _state = new(new WarmupState(false, 0, 0, false));

    public EngineWarmupService(StructuredLogger log) => _log = log;

    private sealed record WarmupStateReference(WarmupState Value);

    /// <summary>Latest warm-up result, published atomically for the readiness endpoint.</summary>
    public WarmupState CurrentState => _state.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Information(Category, "Engine warm-up started, target {Target} spins/s",
            new LogProperty("Target", SettledSpinsPerSecond));

        var preset = StandardReelPresets.All[SimulationConfig.DefaultPresetName];
        var reels = preset.BuildReels();
        var canonical = Paytable.CanonicalFor(preset.ReelCount, preset.Symbols.Count);
        var paytable = PaytableSolver.Solve(
            reels, preset.Paylines, canonical, 0.75, SimulationConfig.Wager);

        var best = 0.0;

        for (var pass = 1; pass <= MaxPasses && !stoppingToken.IsCancellationRequested; pass++)
        {
            var plan = new RunPlan($"warmup-{pass}", (ulong)pass, Environment.ProcessorCount, SpinsPerPass);
            var engine = new SimulationEngine(
                plan, reels, preset.Paylines, paytable, [], SimulationConfig.Wager,
                workerId => SpinRng.ForWorker(plan.MasterSeed, workerId));

            var totals = await engine.RunAsync(telemetry: null, observer: null, stoppingToken)
                .ConfigureAwait(false);

            var rate = engine.Timings.SpinsPerSecond(totals.Spins);
            if (rate > best) best = rate;
            PublishState(ready: false, best, pass, settled: false);

            if (rate >= SettledSpinsPerSecond)
            {
                PublishState(ready: true, best, pass, settled: true);
                _log.Information(Category, "Engine warm after {Passes} passes at {Rate} spins/s",
                    new LogProperty("Passes", pass),
                    new LogProperty("Rate", rate));
                return;
            }
        }

        if (stoppingToken.IsCancellationRequested) return;

        PublishState(ready: true, best, MaxPasses, settled: false);
        _log.Warning(Category,
            "Engine warm-up finished without reaching {Target} spins/s; best {Best} over {Passes} passes",
            new LogProperty("Target", SettledSpinsPerSecond),
            new LogProperty("Best", best),
            new LogProperty("Passes", MaxPasses));
    }

    private void PublishState(bool ready, double bestRate, int passes, bool settled) =>
        _state = new WarmupStateReference(new WarmupState(ready, bestRate, passes, settled));
}

/// <summary>
/// <paramref name="Ready"/> means the page may start a run. <paramref name="Settled"/> says
/// whether the engine actually reached its target speed, which is a different question: a
/// slow machine finishes warm-up ready but unsettled.
/// </summary>
public readonly record struct WarmupState(
    bool Ready,
    double BestSpinsPerSecond,
    int PassesRun,
    bool Settled);
