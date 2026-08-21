using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Runs the spin loop at startup so .NET can compile and optimize the hot methods before a
/// visitor measures them. <see cref="Snapshot"/> reports progress to the readiness endpoint,
/// which holds the run button during warm-up.
///
/// On the developer machine used to set the threshold, throughput rose from 12.4M spins/s
/// on the first pass to 143.2M on the fourth. The spins and RTP were unchanged; only the
/// timing changed.
/// </summary>
public sealed class EngineWarmupService : BackgroundService
{
    private static readonly LogCategory Category = new("Warmup");

    /// <summary>
    /// Throughput at which the developer machine is considered warm.
    /// </summary>
    public const double SettledSpinsPerSecond = 100_000_000;

    /// <summary>Spins per warm-up pass. Large enough to trigger re-optimization, small enough to be quick.</summary>
    private const long SpinsPerPass = 2_000_000;

    /// <summary>
    /// Maximum passes before the page is released on hardware that does not reach the
    /// developer-machine threshold.
    /// </summary>
    private const int MaxPasses = 12;

    private readonly StructuredLogger _log;

    // The background service writes while the readiness endpoint reads. Box gives the
    // record struct a reference that volatile can publish atomically.
    private volatile Box _state = new(new WarmupState(false, 0, 0, false));

    public EngineWarmupService(StructuredLogger log) => _log = log;

    private sealed record Box(WarmupState Value);

    /// <summary>What the readiness endpoint reports. Safe to read from any thread.</summary>
    public WarmupState Snapshot => _state.Value;

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
            _state = new Box(new WarmupState(false, best, pass, false));

            if (rate >= SettledSpinsPerSecond)
            {
                _state = new Box(new WarmupState(true, best, pass, true));
                _log.Information(Category, "Engine warm after {Passes} passes at {Rate} spins/s",
                    new LogProperty("Passes", pass),
                    new LogProperty("Rate", rate));
                return;
            }
        }

        if (stoppingToken.IsCancellationRequested) return;

        // Release the page after the pass limit even when this machine stays below the
        // developer threshold. Settled remains false so the response preserves that fact.
        _state = new Box(new WarmupState(true, best, MaxPasses, false));
        _log.Warning(Category,
            "Engine warm-up finished without reaching {Target} spins/s; best {Best} over {Passes} passes",
            new LogProperty("Target", SettledSpinsPerSecond),
            new LogProperty("Best", best),
            new LogProperty("Passes", MaxPasses));
    }
}

/// <summary>
/// Readiness state reported to the SPA. <paramref name="Ready"/> releases the run button;
/// <paramref name="Settled"/> records whether the measured rate reached the threshold.
/// </summary>
public readonly record struct WarmupState(
    bool Ready,
    double BestSpinsPerSecond,
    int PassesRun,
    bool Settled);
