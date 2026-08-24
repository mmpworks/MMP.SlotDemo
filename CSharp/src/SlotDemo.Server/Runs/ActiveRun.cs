using System.Diagnostics;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Mutable state for an accepted run. The completion task, status, timings, and recorder
/// change while the coordinator executes the run.
/// </summary>
internal sealed class ActiveRun(
    string runId,
    RunConfiguration configuration,
    AnalyticReference reference,
    ConvergenceRecorder recorder,
    CancellationTokenSource cancellation)
{
    public string RunId { get; } = runId;
    public RunConfiguration Configuration { get; } = configuration;
    public AnalyticReference Reference { get; } = reference;
    public ConvergenceRecorder Recorder { get; } = recorder;
    public CancellationTokenSource CancellationSource { get; } = cancellation;
    public Task ExecutionTask { get; set; } = Task.CompletedTask;
    public string Status { get; set; } = "running";

    /// <summary>
    /// Wall-clock time from accepting the request to publishing the terminal state. This
    /// includes orchestration and telemetry overhead.
    /// </summary>
    public Stopwatch ObservedClock { get; } = Stopwatch.StartNew();

    /// <summary>
    /// Elapsed worker phase, from immediately before simulation until all workers finish.
    /// It excludes event serialization and browser delivery.
    /// </summary>
    public Stopwatch EngineClock { get; } = new();

    /// <summary>
    /// Worker-reported spin and telemetry timings. Null until every worker finishes.
    /// </summary>
    public EngineTimings? Timings { get; set; }

    /// <summary>Spins per second over an elapsed span; 0 before the first spin lands.</summary>
    internal static double Rate(long spins, TimeSpan elapsed) =>
        spins <= 0 ? 0 : spins / Math.Max(elapsed.TotalSeconds, 1e-9);

    /// <summary>
    /// Builds the current recovery snapshot: configuration, analytic target, totals,
    /// throughput, acceptance check, and all recorded curve points.
    /// </summary>
    public object CreateStatusSnapshot()
    {
        var latest = Recorder.Latest;
        return new
        {
            runId = RunId,
            status = Status,
            stride = Recorder.Stride,
            config = CreateConfigSnapshot(),
            analytic = CreateAnalyticSnapshot(),
            latest = CreateTotalsSnapshot(latest),
            throughput = CreateThroughputSnapshot(latest.Spins),
            industry = CreateToleranceSnapshot(),
            curve = Recorder.Curve,
        };
    }

    private object CreateConfigSnapshot() => new
    {
        preset = Configuration.GameName,
        isGame = Configuration.IsShippedGame,
        reels = Configuration.Reels,
        rows = Configuration.Rows,
        stopsPerReel = Configuration.StopCounts,
        paylines = Configuration.Paylines,
        targetRtp = Configuration.TargetRtp,
        publishedRtp = Configuration.PublishedRtp,
        payScaleFactor = Configuration.PayScaleFactor,
        isRepriced = Math.Abs(Configuration.PayScaleFactor - 1.0) > 1e-12,
        workers = Configuration.Workers,
        targetSpins = Configuration.TargetSpins,
        seed = Configuration.Seed,
    };

    private object CreateAnalyticSnapshot() => new
    {
        baseRtp = Reference.BaseRtp,
        features = Reference.FeatureContributions.Select(
            feature => new { name = feature.Name, rtp = feature.Rtp }),
        totalRtp = Reference.TotalRtp,
        sigma = Reference.Sigma,
    };

    private static object CreateTotalsSnapshot(RunSnapshot totals) => new
    {
        spins = totals.Spins,
        measuredRtp = totals.MeasuredRtp,
        hitFrequency = totals.HitFrequency,
        wageredMillicents = totals.WageredMillicents,
        returnedMillicents = totals.ReturnedMillicents,
    };

    private object CreateThroughputSnapshot(long spins) => new
    {
        engineSeconds = Timings?.SlowestWorkerSpinTime.TotalSeconds
            ?? EngineClock.Elapsed.TotalSeconds,
        engineSpinsPerSecond = Timings?.SpinsPerSecond(spins)
            ?? Rate(spins, EngineClock.Elapsed),
        telemetrySeconds = Timings?.SlowestWorkerPublishTime.TotalSeconds ?? 0,
        telemetryShare = Timings?.PublishShare ?? 0,
        workerSeconds = EngineClock.Elapsed.TotalSeconds,
        workerSpinsPerSecond = Rate(spins, EngineClock.Elapsed),
        observedSeconds = ObservedClock.Elapsed.TotalSeconds,
        observedSpinsPerSecond = Rate(spins, ObservedClock.Elapsed),
    };

    private object? CreateToleranceSnapshot() => Recorder.CheckFixedTolerance() is { } check
        ? new
        {
            spins = check.Spins,
            deviation = check.Deviation,
            passed = check.Passed,
            tolerance = ConvergenceRecorder.FixedTolerance,
            minimumSpins = ConvergenceRecorder.MinimumSpinsForFixedTolerance,
        }
        : null;
}
