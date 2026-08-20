using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Spins a throwaway workload at startup until the engine reaches its settled speed, so the
/// first run a visitor watches is a real measurement rather than a compilation.
///
/// .NET compiles a method on first use and re-optimizes it once it has been called enough
/// times. The spin loop is the hottest code here, so the first runs after the server starts
/// report a fraction of the engine's real throughput: on developer hardware a first run
/// read 12.4M spins/s and the fourth 143.2M, with identical spins, identical measured RTP
/// and an identical verdict. The math was never in doubt; only the clock was.
///
/// This series teaches what the numbers mean, so a visitor reading a warm-up clock as
/// engine speed is a teaching failure. Warming here moves that cost before anyone is
/// watching, and <see cref="Snapshot"/> lets the page hold its run button until the engine
/// is worth timing.
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
    /// A ceiling on passes. Slower hardware may never reach the threshold, and a page whose
    /// button never enables would be worse than one that reports an honest slower speed, so
    /// warm-up always finishes.
    /// </summary>
    private const int MaxPasses = 12;

    private readonly StructuredLogger _log;

    // A record struct cannot be volatile, and the background writer and the endpoint reader
    // are different threads, so the state is published through a reference the runtime can
    // swap atomically.
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

        // Ran out of passes. Report ready anyway with settled=false: the page should open
        // rather than wait forever, and the number it shows is the truth about this machine.
        _state = new Box(new WarmupState(true, best, MaxPasses, false));
        _log.Warning(Category,
            "Engine warm-up finished without reaching {Target} spins/s; best {Best} over {Passes} passes",
            new LogProperty("Target", SettledSpinsPerSecond),
            new LogProperty("Best", best),
            new LogProperty("Passes", MaxPasses));
    }
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
