using System.Diagnostics;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// A class rather than a record: the run task and the status change after the object
/// exists, and a record copy would leave the coordinator holding a snapshot of a run
/// that has since moved on.
/// </summary>
internal sealed class ActiveRun(
    string runId,
    RunFacts facts,
    AnalyticView analytic,
    ConvergenceRecorder recorder,
    CancellationTokenSource cancellation)
{
    public string RunId { get; } = runId;
    public RunFacts Facts { get; } = facts;
    public AnalyticView Analytic { get; } = analytic;
    public ConvergenceRecorder Recorder { get; } = recorder;
    public CancellationTokenSource Cancellation { get; } = cancellation;
    public Task Completion { get; set; } = Task.CompletedTask;
    public string Status { get; set; } = "running";

    /// <summary>
    /// Wall-clock from accepting the request to the terminal status: what an observer
    /// experiences, including the cost of streaming the run to a watching page. Stopped
    /// at the terminal state so a finished run keeps the figure it actually achieved.
    /// </summary>
    public Stopwatch ObservedClock { get; } = Stopwatch.StartNew();

    /// <summary>
    /// The workers alone, started immediately before the simulation and stopped as soon
    /// as every worker is done. This is the engine's own throughput; it excludes the
    /// telemetry pump, SSE serialization, and anything a connected browser costs, so it
    /// is the number to quote for engine speed.
    /// </summary>
    public Stopwatch EngineClock { get; } = new();

    /// <summary>
    /// The workers' own accounting, available once they finish. Null while a run is
    /// still going, which is why the live readout falls back to the engine clock.
    /// </summary>
    public EngineTimings? Timings { get; set; }

    /// <summary>Spins per second over an elapsed span; 0 before the first spin lands.</summary>
    internal static double Rate(long spins, TimeSpan elapsed) =>
        spins <= 0 ? 0 : spins / Math.Max(elapsed.TotalSeconds, 1e-9);

    /// <summary>
    /// The run configuration, analytic prediction, latest totals, and the consolidated
    /// curve. A page that connects mid-run reads this once and then follows the event
    /// stream, so a late arrival sees the same chart as an early one.
    /// </summary>
    public object Describe()
    {
        var latest = Recorder.Latest;
        return new
        {
            runId = RunId,
            status = Status,
            stride = Recorder.Stride,
            config = new
            {
                preset = Facts.Subject,
                isGame = Facts.IsGame,
                reels = Facts.Reels,
                rows = Facts.Rows,
                stopsPerReel = Facts.StopsByReel,
                paylines = Facts.Paylines,
                targetRtp = Facts.TargetRtp,
                publishedRtp = Facts.PublishedRtp,
                payScaleFactor = Facts.PayScaleFactor,
                isRepriced = Math.Abs(Facts.PayScaleFactor - 1.0) > 1e-12,
                workers = Facts.Workers,
                targetSpins = Facts.TargetSpins,
                seed = Facts.Seed,
            },
            analytic = new
            {
                baseRtp = Analytic.BaseRtp,
                features = Analytic.Features.Select(f => new { name = f.Name, rtp = f.Rtp }),
                totalRtp = Analytic.TotalRtp,
                sigma = Analytic.Sigma,
            },
            latest = new
            {
                spins = latest.Spins,
                measuredRtp = latest.MeasuredRtp,
                hitFrequency = latest.HitFrequency,
                wageredMillicents = latest.WageredMillicents,
                returnedMillicents = latest.ReturnedMillicents,
            },
            throughput = new
            {
                // The workers' own accounting: time inside the spin loop, excluding the
                // telemetry hand-off they also perform. This is the engine's speed. While a
                // run is still going the workers have not reported yet, so the engine clock
                // stands in.
                engineSeconds = Timings?.SlowestWorkerSpinTime.TotalSeconds
                    ?? EngineClock.Elapsed.TotalSeconds,
                engineSpinsPerSecond = Timings?.SpinsPerSecond(latest.Spins)
                    ?? Rate(latest.Spins, EngineClock.Elapsed),
                // What the workers spent handing snapshots to the telemetry channel.
                telemetrySeconds = Timings?.SlowestWorkerPublishTime.TotalSeconds ?? 0,
                telemetryShare = Timings?.PublishShare ?? 0,
                // The worker phase end to end, telemetry included.
                workerSeconds = EngineClock.Elapsed.TotalSeconds,
                workerSpinsPerSecond = Rate(latest.Spins, EngineClock.Elapsed),
                // What an observer saw, streaming and bookkeeping included.
                observedSeconds = ObservedClock.Elapsed.TotalSeconds,
                observedSpinsPerSecond = Rate(latest.Spins, ObservedClock.Elapsed),
            },
            industry = Recorder.IndustryCheck() is { } check
                ? new
                {
                    spins = check.Spins,
                    deviation = check.Deviation,
                    passed = check.Passed,
                    tolerance = ConvergenceRecorder.IndustryTolerance,
                    minimumSpins = ConvergenceRecorder.IndustryMinimumSpins,
                }
                : null,
            curve = Recorder.Curve,
        };
    }
}
