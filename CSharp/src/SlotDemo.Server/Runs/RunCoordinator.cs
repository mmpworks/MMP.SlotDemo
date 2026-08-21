using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Owns the one active simulation run behind the finale page.
///
/// Two kinds of subject run here: a solved preset (the series' configurable game) and a
/// shipped game document (Orca Dive, Classic Three Reel), both on the same engine, the
/// same recorder, and the same stream. <see cref="RunPreparer"/> turns either kind into a
/// <see cref="PreparedRun"/>; from that point this class neither knows nor cares which
/// kind it is running.
///
/// The coordinator preserves three properties of the simulation engine:
///
/// 1. The exact path and the lossy path never touch. Totals are integer counters inside
///    the engine; everything this class publishes is a copy for display.
/// 2. Telemetry is bounded and drop-oldest, so a stalled browser costs chart points while
///    the workers keep running.
/// 3. Snapshots are absolute, never deltas, so a dropped sample leaves no hole to repair.
///
/// One run at a time. A second start while a run is live is refused rather than queued,
/// because the finale page draws one chart and two runs would share it.
/// </summary>
public sealed class RunCoordinator(RunStreamService stream, StructuredLogger log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly RunPreparer _preparer = new(log);
    private readonly Lock _gate = new();
    private ActiveRun? _current;

    public bool IsRunning
    {
        get { lock (_gate) return _current is { Completion.IsCompleted: false }; }
    }

    /// <summary>
    /// Validate the subject, derive its analytic reference, then start. Returns the HTTP
    /// status and the body the endpoint should send back.
    /// </summary>
    public (int Status, object Body) Start(RunRequest request)
    {
        var result = string.IsNullOrWhiteSpace(request.GameFile)
            ? _preparer.PreparePreset(request)
            : _preparer.PrepareGame(request);
        if (result.Error is { } error) return error;
        return Start(result.Prepared!, request.Stride);
    }

    /// <summary>
    /// The lifecycle half of <see cref="Start(RunRequest)"/>: install the run, spawn its
    /// task, announce it. Internal so tests can drive the whole flow with a hand-built
    /// <see cref="PreparedRun"/> and a fake <see cref="SubjectRunner"/> — no engine, no
    /// game files — which keeps the orchestration testable independently of Core.
    /// </summary>
    internal (int Status, object Body) Start(PreparedRun prepared, long stride)
    {
        lock (_gate)
        {
            if (_current is { Completion.IsCompleted: false })
                return (409, new { title = "A run is already active", status = 409 });

            var recorder = new ConvergenceRecorder(
                prepared.Analytic.TotalRtp,
                prepared.Analytic.Sigma,
                stride > 0 ? stride : ConvergenceRecorder.DefaultStride);

            var cancellation = new CancellationTokenSource();
            var active = new ActiveRun(prepared.RunId, prepared.Facts, prepared.Analytic, recorder, cancellation);
            _current = active;
            // Assigned inside the lock so IsRunning never observes a run without its task.
            active.Completion = ExecuteAsync(prepared.Runner, active, cancellation.Token);
        }

        log.Information(RunLogging.Category,
            "Run {RunId} started: subject {Subject}, analytic {AnalyticRtp}, sigma {Sigma}, {Spins} spins across {Workers} workers, seed {Seed}",
            new LogProperty("RunId", prepared.RunId),
            new LogProperty("Subject", prepared.Facts.Subject),
            new LogProperty("AnalyticRtp", prepared.Analytic.TotalRtp),
            new LogProperty("Sigma", prepared.Analytic.Sigma),
            new LogProperty("Spins", prepared.Facts.TargetSpins),
            new LogProperty("Workers", prepared.Facts.Workers),
            new LogProperty("Seed", prepared.Facts.Seed));

        var described = Describe()!;   // non-null: the run was just installed
        Publish("started", described);
        return (201, described);
    }

    public bool Cancel()
    {
        lock (_gate)
        {
            if (_current is not { Completion.IsCompleted: false } run) return false;
            run.Cancellation.Cancel();
            return true;
        }
    }

    /// <summary>
    /// The current run described for polling and for a page that joins the event stream
    /// late; null when no run has started. See <see cref="ActiveRun.Describe"/>.
    /// </summary>
    public object? Describe()
    {
        ActiveRun? run;
        lock (_gate) run = _current;
        return run?.Describe();
    }

    private async Task ExecuteAsync(SubjectRunner runner, ActiveRun run, CancellationToken ct)
    {
        // Bounded and drop-oldest: the workers publish into this and never look back.
        var channel = Channel.CreateBounded<TelemetrySample>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

        var pump = PumpAsync(run, channel.Reader);

        RunSnapshot final;
        string terminal;
        try
        {
            run.EngineClock.Start();
            var produced = await runner(channel.Writer, ct).ConfigureAwait(false);
            run.EngineClock.Stop();
            final = produced.Totals;
            run.Timings = produced.Timings;
            // Workers notice cancellation at a batch boundary and return normally, so a
            // cancelled run usually completes without throwing. Check the token to determine
            // whether cancellation ended the run.
            terminal = ct.IsCancellationRequested ? "cancelled" : "completed";
        }
        catch (OperationCanceledException)
        {
            // Cancelled before the workers drew a spin; the recorder's latest reading is
            // whatever the run managed, which may be nothing.
            run.EngineClock.Stop();
            final = run.Recorder.Latest;
            terminal = "cancelled";
            channel.Writer.TryComplete();
            log.Warning(RunLogging.Category, "Run {RunId} cancelled at {Spins} spins",
                new LogProperty("RunId", run.RunId),
                new LogProperty("Spins", final.Spins));
        }

        await pump.ConfigureAwait(false);

        var last = run.Recorder.Complete(final);
        // Freeze the observed clock with the totals, before the terminal status goes out.
        run.ObservedClock.Stop();
        // Terminal status lands only after the final snapshot is in the recorder, so a
        // poller that sees "completed" always sees the finished totals with it.
        run.Status = terminal;

        log.Information(RunLogging.Category,
            "Run {RunId} {Status}: {Spins} spins at {SpinsPerSecond} engine spins/s, measured {Measured}, analytic {Analytic}, band {Band}, verdict {Verdict}, industry {Industry}",
            new LogProperty("RunId", run.RunId),
            new LogProperty("Status", run.Status),
            new LogProperty("Spins", final.Spins),
            new LogProperty("SpinsPerSecond", run.Timings?.SpinsPerSecond(final.Spins)
                ?? ActiveRun.Rate(final.Spins, run.EngineClock.Elapsed)),
            new LogProperty("Measured", final.MeasuredRtp),
            new LogProperty("Analytic", run.Analytic.TotalRtp),
            new LogProperty("Band", last.BandHalfWidth),
            new LogProperty("Verdict", last.WithinBand ? "within band" : "outside band"),
            new LogProperty("Industry", run.Recorder.IndustryCheck() switch
            {
                null => "not qualified (below 10M spins)",
                { Passed: true } c => $"pass (deviation {c.Deviation:0.000000})",
                { } c => $"FAIL (deviation {c.Deviation:0.000000})",
            }));

        Publish(terminal, Describe());
    }

    /// <summary>
    /// How often the live readout is pushed to the page. Only the readout is throttled:
    /// samples are drained continuously, because the recorder decides what lands on the
    /// curve and it decides by spins.
    /// </summary>
    private const int ProgressIntervalMs = 100;

    /// <summary>
    /// Drains telemetry into the recorder as fast as it arrives.
    ///
    /// This loop used to sleep 100ms between drains. The channel is bounded and
    /// drop-oldest, so while it slept the workers overwrote the samples it had not read
    /// yet. That cost the curve its stride boundaries: sampling became wall-clock driven
    /// instead of spin driven, which showed up as a curve that hitched between dense
    /// clusters and long straight jumps. Once the engine got fast enough to finish 10M
    /// spins inside one sleep, the first drain landed most of the way through the run and
    /// the curve lost its whole early history, which is the part the lesson is about.
    ///
    /// Draining continuously keeps the channel near empty, so the recorder sees the
    /// samples it needs and the throttle applies only to the SSE readout.
    /// </summary>
    private async Task PumpAsync(ActiveRun run, ChannelReader<TelemetrySample> reader)
    {
        var sinceProgress = Stopwatch.StartNew();
        await foreach (var sample in reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            var point = run.Recorder.Observe(sample.Totals);
            if (point is not null)
                Publish("point", new { runId = run.RunId, point });

            if (sinceProgress.ElapsedMilliseconds < ProgressIntervalMs) continue;
            sinceProgress.Restart();

            // A live readout that updates faster than the curve, at a rate a person
            // can actually watch.
            var latest = run.Recorder.Latest;
            Publish("progress", new
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

    private void Publish(string type, object? payload) =>
        stream.Publish(JsonSerializer.Serialize(new { type, data = payload }, Json));
}
