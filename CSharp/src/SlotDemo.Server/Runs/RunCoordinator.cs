using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Owns the active simulation run and publishes its state to the finale page.
///
/// <see cref="RunPreparer"/> converts presets and shipped game documents into the same
/// <see cref="PreparedRun"/> contract. The coordinator then applies one lifecycle to both.
///
/// Simulation totals stay in the engine. Telemetry copies those cumulative totals into
/// bounded channels for charts and live readouts. Dropping a telemetry message does not
/// change the engine's result.
///
/// Only one run may be active because the finale page presents a single run state.
/// </summary>
public sealed class RunCoordinator(RunStreamService stream, StructuredLogger log)
{
    private const int TelemetryBufferCapacity = 1024;
    private const int ProgressIntervalMs = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RunPreparer _preparer = new(log);
    private readonly Lock _runLock = new();
    private ActiveRun? _currentRun;

    public bool IsRunning
    {
        get { lock (_runLock) return _currentRun is { ExecutionTask.IsCompleted: false }; }
    }

    /// <summary>
    /// Prepares the requested subject and starts it. Returns the status code and response
    /// body for the HTTP endpoint.
    /// </summary>
    public (int Status, object Body) Start(RunRequest request)
    {
        var result = string.IsNullOrWhiteSpace(request.GameFile)
            ? _preparer.PreparePreset(request)
            : _preparer.PrepareShippedGame(request);
        if (result.Error is { } error) return error;
        return Start(result.Prepared!, request.Stride);
    }

    /// <summary>
    /// Installs and launches an already prepared run. Tests use this overload with a fake
    /// <see cref="SimulationExecutor"/> to exercise orchestration without loading a game.
    /// </summary>
    internal (int Status, object Body) Start(PreparedRun prepared, long stride)
    {
        lock (_runLock)
        {
            if (_currentRun is { ExecutionTask.IsCompleted: false })
                return (409, new { title = "A run is already active", status = 409 });

            var recorder = new ConvergenceRecorder(
                prepared.Reference.TotalRtp,
                prepared.Reference.Sigma,
                EffectiveStride(stride, prepared.Configuration.TargetSpins));

            var cancellation = new CancellationTokenSource();
            var active = new ActiveRun(
                prepared.RunId, prepared.Configuration, prepared.Reference, recorder, cancellation);
            _currentRun = active;
            // Assigned inside the lock so IsRunning never observes a run without its task.
            active.ExecutionTask = ExecuteAsync(prepared.Execute, active, cancellation.Token);
        }

        log.Information(RunLogging.Category,
            "Run {RunId} started: subject {Subject}, analytic {AnalyticRtp}, sigma {Sigma}, {Spins} spins across {Workers} workers, seed {Seed}",
            new LogProperty("RunId", prepared.RunId),
            new LogProperty("Subject", prepared.Configuration.GameName),
            new LogProperty("AnalyticRtp", prepared.Reference.TotalRtp),
            new LogProperty("Sigma", prepared.Reference.Sigma),
            new LogProperty("Spins", prepared.Configuration.TargetSpins),
            new LogProperty("Workers", prepared.Configuration.Workers),
            new LogProperty("Seed", prepared.Configuration.Seed));

        var status = GetCurrentStatus()!; // non-null because the run was just installed
        PublishRunEvent("started", status);
        return (201, status);
    }

    public bool Cancel()
    {
        lock (_runLock)
        {
            if (_currentRun is not { ExecutionTask.IsCompleted: false } run) return false;
            run.CancellationSource.Cancel();
            return true;
        }
    }

    /// <summary>
    /// Returns the current recovery snapshot for polling or a late stream subscriber.
    /// Returns null before the first run starts.
    /// </summary>
    public object? GetCurrentStatus()
    {
        ActiveRun? run;
        lock (_runLock) run = _currentRun;
        return run?.CreateStatusSnapshot();
    }

    private async Task ExecuteAsync(SimulationExecutor execute, ActiveRun run, CancellationToken ct)
    {
        var telemetry = CreateTelemetryChannel();
        var pump = PumpTelemetryAsync(run, telemetry.Reader);
        var outcome = await RunSimulationAsync(execute, run, telemetry.Writer, ct)
            .ConfigureAwait(false);

        await pump.ConfigureAwait(false);
        CompleteRun(run, outcome);
    }

    private async Task<RunOutcome> RunSimulationAsync(
        SimulationExecutor execute,
        ActiveRun run,
        ChannelWriter<TelemetrySample> telemetry,
        CancellationToken ct)
    {
        try
        {
            run.EngineClock.Start();
            var produced = await execute(telemetry, ct).ConfigureAwait(false);
            run.EngineClock.Stop();
            run.Timings = produced.Timings;

            // Workers notice cancellation at a batch boundary and return normally, so a
            // completed task can still represent a cancelled run.
            var status = ct.IsCancellationRequested ? "cancelled" : "completed";
            return new RunOutcome(produced.Totals, status);
        }
        catch (OperationCanceledException)
        {
            run.EngineClock.Stop();
            telemetry.TryComplete();
            var finalTotals = run.Recorder.Latest;
            log.Warning(RunLogging.Category, "Run {RunId} cancelled at {Spins} spins",
                new LogProperty("RunId", run.RunId),
                new LogProperty("Spins", finalTotals.Spins));
            return new RunOutcome(finalTotals, "cancelled");
        }
    }

    private void CompleteRun(ActiveRun run, RunOutcome outcome)
    {
        var finalPoint = run.Recorder.RecordFinalSnapshot(outcome.FinalTotals);
        run.ObservedClock.Stop();
        run.Status = outcome.Status;

        log.Information(RunLogging.Category,
            "Run {RunId} {Status}: {Spins} spins at {SpinsPerSecond} engine spins/s, measured {Measured}, analytic {Analytic}, band {Band}, verdict {Verdict}, industry {Industry}",
            new LogProperty("RunId", run.RunId),
            new LogProperty("Status", run.Status),
            new LogProperty("Spins", outcome.FinalTotals.Spins),
            new LogProperty("SpinsPerSecond", run.Timings?.SpinsPerSecond(outcome.FinalTotals.Spins)
                ?? ActiveRun.Rate(outcome.FinalTotals.Spins, run.EngineClock.Elapsed)),
            new LogProperty("Measured", outcome.FinalTotals.MeasuredRtp),
            new LogProperty("Analytic", run.Reference.TotalRtp),
            new LogProperty("Band", finalPoint.BandHalfWidth),
            new LogProperty("Verdict", finalPoint.WithinBand ? "within band" : "outside band"),
            new LogProperty("Industry", run.Recorder.CheckFixedTolerance() switch
            {
                null => "not qualified (below 10M spins)",
                { Passed: true } c => $"pass (deviation {c.Deviation:0.000000})",
                { } c => $"FAIL (deviation {c.Deviation:0.000000})",
            }));

        PublishRunEvent(outcome.Status, GetCurrentStatus());
    }

    /// <summary>
    /// The stride the recorder actually uses. The requested stride (or the default) is
    /// raised until the run fits <see cref="ConvergenceRecorder.MaxCurvePoints"/>, because
    /// point volume is what the browser pays for: each point is an SSE event and a full
    /// chart redraw. The status snapshot reports this effective stride, so the page
    /// always displays the value in force.
    /// </summary>
    internal static long EffectiveStride(long requested, long targetSpins)
    {
        var stride = requested > 0 ? requested : ConvergenceRecorder.DefaultStride;
        return Math.Max(stride, targetSpins / ConvergenceRecorder.MaxCurvePoints);
    }

    private static Channel<TelemetrySample> CreateTelemetryChannel() =>
        Channel.CreateBounded<TelemetrySample>(new BoundedChannelOptions(TelemetryBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    /// <summary>
    /// Drains batch snapshots continuously so the recorder can select chart points by
    /// spin count. Live readout events are limited to one every 100 milliseconds.
    /// </summary>
    private async Task PumpTelemetryAsync(ActiveRun run, ChannelReader<TelemetrySample> reader)
    {
        var sinceProgress = Stopwatch.StartNew();
        await foreach (var sample in reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            var point = run.Recorder.RecordSnapshot(sample.Totals);
            if (point is not null)
                PublishRunEvent("point", new { runId = run.RunId, point });

            if (sinceProgress.ElapsedMilliseconds < ProgressIntervalMs) continue;
            sinceProgress.Restart();

            // Throttle the numeric readout; curve points keep their spin-based cadence.
            var latest = run.Recorder.Latest;
            PublishRunEvent("progress", new
            {
                runId = run.RunId,
                spins = latest.Spins,
                measuredRtp = latest.MeasuredRtp,
                hitFrequency = latest.HitFrequency,
                engineSpinsPerSecond = ActiveRun.Rate(latest.Spins, run.EngineClock.Elapsed),
                observedSpinsPerSecond = ActiveRun.Rate(latest.Spins, run.ObservedClock.Elapsed),
            });
        }
    }

    private void PublishRunEvent(string type, object? payload) =>
        stream.PublishRunEvent(JsonSerializer.Serialize(new { type, data = payload }, JsonOptions));

    private sealed record RunOutcome(RunSnapshot FinalTotals, string Status);
}
