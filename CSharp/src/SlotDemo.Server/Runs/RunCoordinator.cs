using System.Text.Json;
using System.Threading.Channels;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Owns the one active simulation run behind the finale page.
///
/// Three separations carry this class, all of them inherited from the engine's own
/// design rather than invented here:
///
/// 1. The exact path and the lossy path never touch. Totals are integer counters inside
///    the engine; everything this class publishes is a copy for display.
/// 2. Telemetry is bounded and drop-oldest. A browser that stalls costs chart points.
///    The workers never wait on it.
/// 3. Snapshots are absolute, never deltas, so a dropped sample leaves no hole to repair.
///
/// One run at a time. A second start while a run is live is refused rather than queued:
/// the page is a demonstration surface, and two runs sharing one chart would teach the
/// wrong thing.
/// </summary>
public sealed class RunCoordinator(RunStreamService stream, StructuredLogger log)
{
    private static readonly LogCategory Category = new("SimulationRun");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Lock _gate = new();
    private ActiveRun? _current;

    /// <summary>
    /// A class rather than a record: the run task and the status change after the object
    /// exists, and a record copy would leave the coordinator holding a snapshot of a run
    /// that has since moved on.
    /// </summary>
    private sealed class ActiveRun(
        string runId,
        SimulationConfig config,
        RtpBreakdown analytic,
        ConvergenceRecorder recorder,
        CancellationTokenSource cancellation)
    {
        public string RunId { get; } = runId;
        public SimulationConfig Config { get; } = config;
        public RtpBreakdown Analytic { get; } = analytic;
        public ConvergenceRecorder Recorder { get; } = recorder;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public Task Completion { get; set; } = Task.CompletedTask;
        public string Status { get; set; } = "running";
    }

    public bool IsRunning
    {
        get { lock (_gate) return _current is { Completion.IsCompleted: false }; }
    }

    /// <summary>
    /// Validate, solve, check the realized game against the cap, then start. Returns the
    /// HTTP status and the body the endpoint should send back.
    /// </summary>
    public (int Status, object Body) Start(RunRequest request)
    {
        var draft = new ConfigDraft(
            request.PresetName,
            request.BaseRtpBasisPoints,
            request.FreeSpinsRtpBasisPoints,
            request.PickBonusRtpBasisPoints,
            request.Seed,
            request.WorkerCount,
            request.TargetSpins);

        if (!SimulationConfig.TryCreate(draft, out var config, out var errors))
        {
            log.Warning(Category, "Run rejected: {Errors}",
                new LogProperty("Errors", string.Join(" | ", errors)));
            return (400, new { title = "Invalid configuration", status = 400, errors });
        }

        var valid = config!;
        var game = PresetGame.Build(valid);
        var analytic = game.Analysis;

        // The requested split passed the cap as integers. The REALIZED game is what the
        // solver actually produced after rounding, so it gets checked too — a paytable
        // that rounds its way over 99% is a bug the page must never render as success.
        if (analytic.TotalRtp > SimulationConfig.MaxAggregateBasisPoints / 10_000.0)
            return (500, new { title = "Solver produced a realized RTP above the cap", status = 500, analytic.TotalRtp });

        lock (_gate)
        {
            if (_current is { Completion.IsCompleted: false })
                return (409, new { title = "A run is already active", status = 409 });

            var recorder = new ConvergenceRecorder(
                analytic.TotalRtp,
                analytic.SigmaPerUnitWagered,
                request.Stride > 0 ? request.Stride : ConvergenceRecorder.DefaultStride);

            var cancellation = new CancellationTokenSource();
            var active = new ActiveRun(valid.RunId, valid, analytic, recorder, cancellation);
            _current = active;
            // Assigned inside the lock so IsRunning never observes a run without its task.
            active.Completion = ExecuteAsync(game, active, cancellation.Token);
        }

        log.Information(Category,
            "Run {RunId} started: preset {Preset}, target {TargetRtp}, realized {RealizedRtp}, sigma {Sigma}, {Spins} spins across {Workers} workers, seed {Seed}",
            new LogProperty("RunId", valid.RunId),
            new LogProperty("Preset", valid.Preset.Name),
            new LogProperty("TargetRtp", valid.TargetTotalRtp),
            new LogProperty("RealizedRtp", analytic.TotalRtp),
            new LogProperty("Sigma", analytic.SigmaPerUnitWagered),
            new LogProperty("Spins", valid.TargetSpins),
            new LogProperty("Workers", valid.WorkerCount),
            new LogProperty("Seed", valid.MasterSeed));

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
    /// The whole run as one object: config echo, analytic prediction, newest totals, and
    /// the consolidated curve. A page that connects mid-run reads this once and then
    /// follows the event stream, so a late arrival sees the same chart as an early one.
    /// </summary>
    public object? Describe()
    {
        ActiveRun? run;
        lock (_gate) run = _current;
        if (run is null) return null;

        var latest = run.Recorder.Latest;
        return new
        {
            runId = run.RunId,
            status = run.Status,
            stride = run.Recorder.Stride,
            config = new
            {
                preset = run.Config.Preset.Name,
                reels = run.Config.Preset.ReelCount,
                rows = MMP.SlotGame.Core.Reels.StripReelSet.DefaultRows,
                stopsPerReel = run.Config.Preset.StopsPerReel,
                paylines = run.Config.Preset.Paylines.Count,
                targetRtp = run.Config.TargetTotalRtp,
                workers = run.Config.WorkerCount,
                targetSpins = run.Config.TargetSpins,
                seed = run.Config.MasterSeed,
            },
            analytic = new
            {
                baseRtp = run.Analytic.BaseRtp,
                features = run.Analytic.Features.Select(f => new { name = f.Name, rtp = f.Rtp }),
                totalRtp = run.Analytic.TotalRtp,
                sigma = run.Analytic.SigmaPerUnitWagered,
            },
            latest = new
            {
                spins = latest.Spins,
                measuredRtp = latest.MeasuredRtp,
                hitFrequency = latest.HitFrequency,
                wageredMillicents = latest.WageredMillicents,
                returnedMillicents = latest.ReturnedMillicents,
            },
            curve = run.Recorder.Curve,
        };
    }

    private async Task ExecuteAsync(PresetGame game, ActiveRun run, CancellationToken ct)
    {
        // Bounded and drop-oldest: the workers publish into this and never look back.
        var channel = Channel.CreateBounded<TelemetrySample>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

        var engine = game.Engine();
        var pump = PumpAsync(run, channel.Reader);

        RunSnapshot final;
        string terminal;
        try
        {
            final = await engine.RunAsync(channel.Writer, observer: null, ct).ConfigureAwait(false);
            // Workers notice cancellation at a batch boundary and return normally, so a
            // cancelled run usually completes RunAsync without throwing. The token, not
            // the exception, is the truth about why the run stopped.
            terminal = ct.IsCancellationRequested ? "cancelled" : "completed";
        }
        catch (OperationCanceledException)
        {
            final = engine.Totals.Snapshot();
            terminal = "cancelled";
            channel.Writer.TryComplete();
            log.Warning(Category, "Run {RunId} cancelled at {Spins} spins",
                new LogProperty("RunId", run.RunId),
                new LogProperty("Spins", final.Spins));
        }

        await pump.ConfigureAwait(false);

        var last = run.Recorder.Complete(final);
        // Terminal status lands only after the final snapshot is in the recorder, so a
        // poller that sees "completed" always sees the finished totals with it.
        run.Status = terminal;
        log.Information(Category,
            "Run {RunId} {Status}: {Spins} spins, measured {Measured}, analytic {Analytic}, band {Band}, verdict {Verdict}",
            new LogProperty("RunId", run.RunId),
            new LogProperty("Status", run.Status),
            new LogProperty("Spins", final.Spins),
            new LogProperty("Measured", final.MeasuredRtp),
            new LogProperty("Analytic", run.Analytic.TotalRtp),
            new LogProperty("Band", last.BandHalfWidth),
            new LogProperty("Verdict", last.WithinBand ? "within band" : "outside band"));

        Publish(terminal, Describe());
    }

    /// <summary>
    /// Drains everything waiting, keeps only the newest snapshot, and hands it to the
    /// recorder. Two rates meet here: workers produce snapshots per 4,096-spin batch,
    /// and the browser gets a point only when the run crosses a stride boundary. Between
    /// those, a 100 ms pace keeps the live counters moving without flooding the socket.
    /// </summary>
    private async Task PumpAsync(ActiveRun run, ChannelReader<TelemetrySample> reader)
    {
        var ticks = 0;
        while (await reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
        {
            // Every drained sample reaches the recorder — a fast run crosses several
            // stride boundaries inside one 100 ms drain, and feeding only the newest
            // sample would skip those curve points. Publishing stays consolidated.
            var got = false;
            while (reader.TryRead(out var sample))
            {
                got = true;
                var point = run.Recorder.Observe(sample.Totals);
                if (point is not null)
                    Publish("point", new { runId = run.RunId, point });
            }

            if (got)
            {

                // A live readout that updates faster than the curve, at a rate a person can
                // actually watch.
                if (++ticks % 2 == 0)
                {
                    var latest = run.Recorder.Latest;
                    Publish("progress", new
                    {
                        runId = run.RunId,
                        spins = latest.Spins,
                        measuredRtp = latest.MeasuredRtp,
                        hitFrequency = latest.HitFrequency,
                    });
                }
            }

            await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private void Publish(string type, object? payload) =>
        stream.Publish(JsonSerializer.Serialize(new { type, data = payload }, Json));
}

/// <summary>What the SPA sends to start a run. Untrusted; every field is validated downstream.</summary>
public sealed record RunRequest(
    string PresetName,
    int BaseRtpBasisPoints,
    int FreeSpinsRtpBasisPoints,
    int PickBonusRtpBasisPoints,
    ulong Seed,
    int WorkerCount,
    long TargetSpins,
    long Stride);
