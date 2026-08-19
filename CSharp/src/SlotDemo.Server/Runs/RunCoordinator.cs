using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Owns the one active simulation run behind the finale page.
///
/// Two kinds of subject run here: a solved preset (the series' configurable game) and a
/// shipped game document (Orca Dive, Classic Three Reel), both on the same engine, the
/// same recorder, and the same stream. The subject decides where the analytic reference
/// comes from: the solver-plus-closed-form pipeline for presets, exhaustive enumeration
/// for games. Both paths produce the same run summary type.
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
    private static readonly LogCategory Category = new("SimulationRun");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Lock _gate = new();
    private ActiveRun? _current;

    /// <summary>What the page shows about the subject, independent of which kind it is.</summary>
    private sealed record RunFacts(
        string Subject,
        bool IsGame,
        int Reels,
        int Rows,
        string StopsByReel,
        int Paylines,
        double TargetRtp,
        int Workers,
        long TargetSpins,
        double PublishedRtp,
        double PayScaleFactor,
        ulong Seed);

    private sealed record AnalyticView(
        double BaseRtp,
        IReadOnlyList<(string Name, double Rtp)> Features,
        double TotalRtp,
        double Sigma);

    /// <summary>Runs the subject's spins; both kinds return the quiesced final snapshot.</summary>
    private delegate Task<(RunSnapshot Totals, EngineTimings Timings)> SubjectRunner(
        ChannelWriter<TelemetrySample> telemetry, CancellationToken ct);

    /// <summary>
    /// A class rather than a record: the run task and the status change after the object
    /// exists, and a record copy would leave the coordinator holding a snapshot of a run
    /// that has since moved on.
    /// </summary>
    private sealed class ActiveRun(
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

    }

    /// <summary>
    /// Re-prices a shipped game's line paytable so the game returns a requested TOTAL RTP.
    ///
    /// Total RTP is line RTP plus the feature's contribution (its trigger probability times
    /// its mean award). Only the line paytable is scaled here, so the feature's share is a
    /// fixed floor: the line target is what is left after the feature is paid for. Asking
    /// for a total at or below that floor is refused rather than quietly clamped, because a
    /// clamped answer would report an RTP the game does not pay.
    ///
    /// Each scaled pay rounds to a whole hundredth of the wager, so the request is a target
    /// rather than a guarantee. The returned analysis is a fresh enumeration of the
    /// re-priced game, which is what the band and the verdict are then measured against.
    /// </summary>
    private static (GameDefinition? Game, GameAnalysis? Analysis, double Factor, (int, object)? Error)
        Reprice(GameDefinition game, GameAnalysis analysis, RunRequest request)
    {
        var bp = request.TargetTotalRtpBasisPoints;
        if (bp < SimulationConfig.MinAggregateBasisPoints || bp > SimulationConfig.MaxAggregateBasisPoints)
            return (null, null, 1.0, (400, new
            {
                title = $"Target total RTP must be {SimulationConfig.MinAggregateBasisPoints}"
                    + $"-{SimulationConfig.MaxAggregateBasisPoints} basis points",
                status = 400,
            }));

        var targetTotal = bp / 10_000.0;
        var featureRtp = analysis.TotalRtp - analysis.LineRtp;
        var targetLine = targetTotal - featureRtp;

        if (analysis.LineRtp <= 0)
            return (null, null, 1.0, (400, new
            {
                title = "This game pays nothing on the line, so its paytable cannot be re-priced",
                status = 400,
            }));

        if (targetLine <= 0)
            return (null, null, 1.0, (400, new
            {
                title = $"This game's feature alone returns {featureRtp * 100:0.####}%, "
                    + $"so a total of {targetTotal * 100:0.##}% cannot be reached by re-pricing lines",
                status = 400,
            }));

        var factor = targetLine / analysis.LineRtp;
        var repriced = game.WithScaledPays(factor);

        GameAnalysis repricedAnalysis;
        try { repricedAnalysis = GameAnalyzer.Analyze(repriced); }
        catch (NotSupportedException ex) { return (null, null, 1.0, (400, new { title = ex.Message, status = 400 })); }

        return (repriced, repricedAnalysis, factor, null);
    }

    /// <summary>Spins per second over an elapsed span; 0 before the first spin lands.</summary>
    private static double Rate(long spins, TimeSpan elapsed) =>
        spins <= 0 ? 0 : spins / Math.Max(elapsed.TotalSeconds, 1e-9);

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
        RunFacts facts;
        AnalyticView analytic;
        SubjectRunner runner;
        string runId;

        if (!string.IsNullOrWhiteSpace(request.GameFile))
        {
            var loaded = PrepareGame(request);
            if (loaded.Error is not null) return loaded.Error.Value;
            (facts, analytic, runner, runId) = loaded.Prepared!.Value;
        }
        else
        {
            var built = PreparePreset(request);
            if (built.Error is not null) return built.Error.Value;
            (facts, analytic, runner, runId) = built.Prepared!.Value;
        }

        lock (_gate)
        {
            if (_current is { Completion.IsCompleted: false })
                return (409, new { title = "A run is already active", status = 409 });

            var recorder = new ConvergenceRecorder(
                analytic.TotalRtp,
                analytic.Sigma,
                request.Stride > 0 ? request.Stride : ConvergenceRecorder.DefaultStride);

            var cancellation = new CancellationTokenSource();
            var active = new ActiveRun(runId, facts, analytic, recorder, cancellation);
            _current = active;
            // Assigned inside the lock so IsRunning never observes a run without its task.
            active.Completion = ExecuteAsync(runner, active, cancellation.Token);
        }

        log.Information(Category,
            "Run {RunId} started: subject {Subject}, analytic {AnalyticRtp}, sigma {Sigma}, {Spins} spins across {Workers} workers, seed {Seed}",
            new LogProperty("RunId", runId),
            new LogProperty("Subject", facts.Subject),
            new LogProperty("AnalyticRtp", analytic.TotalRtp),
            new LogProperty("Sigma", analytic.Sigma),
            new LogProperty("Spins", facts.TargetSpins),
            new LogProperty("Workers", facts.Workers),
            new LogProperty("Seed", facts.Seed));

        var described = Describe()!;   // non-null: the run was just installed
        Publish("started", described);
        return (201, described);
    }

    private ((RunFacts, AnalyticView, SubjectRunner, string)? Prepared, (int, object)? Error)
        PreparePreset(RunRequest request)
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
            return (null, (400, new { title = "Invalid configuration", status = 400, errors }));
        }

        var valid = config!;
        var game = PresetGame.Build(valid);
        var breakdown = game.Analysis;

        // The requested split passed the solver's RTP limits as integers. The REALIZED game
        // is what the solver actually produced after rounding, so it gets checked against the
        // same two constants — a paytable that rounds its way outside the limits is a bug the
        // page must never render as success.
        if (breakdown.TotalRtp > SimulationConfig.MaxAggregateBasisPoints / 10_000.0)
            return (null, (500, new { title = "Solver produced a realized RTP above the ceiling", status = 500, breakdown.TotalRtp }));
        // One basis point of grace on the floor only: a request at exactly the floor may
        // round a hair under it, and the limits must never reject a target they invited.
        // The ceiling stays strict — rounding over the top is the hazard it exists to catch.
        if (breakdown.TotalRtp < (SimulationConfig.MinAggregateBasisPoints - 1) / 10_000.0)
            return (null, (500, new { title = "Solver produced a realized RTP below the floor", status = 500, breakdown.TotalRtp }));

        var facts = new RunFacts(
            valid.Preset.Name, IsGame: false,
            valid.Preset.ReelCount, MMP.SlotGame.Core.Reels.StripReelSet.DefaultRows,
            string.Join('/', valid.Preset.StopCounts), valid.Preset.Paylines.Count,
            valid.TargetTotalRtp, valid.WorkerCount, valid.TargetSpins,
            valid.TargetTotalRtp, 1.0, valid.MasterSeed);

        var analytic = new AnalyticView(
            breakdown.BaseRtp, breakdown.Features, breakdown.TotalRtp, breakdown.SigmaPerUnitWagered);

        var engine = game.Engine();
        return ((facts, analytic,
            async (telemetry, ct) =>
                (await engine.RunAsync(telemetry, observer: null, ct).ConfigureAwait(false), engine.Timings),
            valid.RunId), null);
    }

    private ((RunFacts, AnalyticView, SubjectRunner, string)? Prepared, (int, object)? Error)
        PrepareGame(RunRequest request)
    {
        if (request.WorkerCount is < 1 or > 64)
            return (null, (400, new { title = "WorkerCount must be 1..64", status = 400 }));
        if (request.TargetSpins < 1)
            return (null, (400, new { title = "TargetSpins must be at least 1", status = 400 }));

        var path = Path.Combine(AppContext.BaseDirectory, "games", Path.GetFileName(request.GameFile));
        if (!File.Exists(path))
            return (null, (400, new { title = $"No shipped game named '{request.GameFile}'", status = 400 }));
        if (!GameDefinitionLoader.TryLoad(File.ReadAllText(path), out var definition, out var errors))
            return (null, (400, new { title = "Game definition failed to load", status = 400, errors }));

        var game = definition!;
        GameAnalysis analysis;
        try
        {
            // Enumeration is the analytic twin for a published game: exact RTP and sigma
            // from the document alone, before a single spin.
            analysis = GameAnalyzer.Analyze(game);
        }
        catch (NotSupportedException ex)
        {
            return (null, (400, new { title = ex.Message, status = 400 }));
        }

        var publishedRtp = analysis.TotalRtp;
        var scaleFactor = 1.0;

        if (request.TargetTotalRtpBasisPoints != 0)
        {
            var (repriced, repricedAnalysis, factor, error) = Reprice(game, analysis, request);
            if (error is not null) return (null, error);
            game = repriced!;
            analysis = repricedAnalysis!;
            scaleFactor = factor;
        }

        // Build the game's outcome tables now, while preparing, rather than letting the
        // first worker trigger the Lazy once the run is already being timed. A loaded game
        // is a fresh object every run, so these tables are cold every time; left to the
        // workers, an exhaustive enumeration lands inside the measured run and is reported
        // as though the engine were spinning slowly.
        _ = game.ProgressiveOutcomes;

        var runId = Guid.CreateVersion7().ToString("n");
        var plan = new RunPlan(runId, request.Seed, request.WorkerCount, request.TargetSpins);
        var runner = new GameRunner(game, plan, analysis);

        var facts = new RunFacts(
            game.Name, IsGame: true,
            game.ReelCount, game.Reels.Rows,
            string.Join("/", Enumerable.Range(0, game.ReelCount).Select(game.Reels.StopCount)),
            game.Paylines.Count,
            analysis.TotalRtp, request.WorkerCount, request.TargetSpins,
            publishedRtp, scaleFactor, request.Seed);

        var features = game.Bonus is null
            ? (IReadOnlyList<(string, double)>)[]
            : [(game.Bonus.Name, analysis.BonusRtp)];
        var analytic = new AnalyticView(analysis.LineRtp, features, analysis.TotalRtp, analysis.SigmaPerUnitWagered);

        return ((facts, analytic,
            async (telemetry, ct) =>
            {
                var result = await runner.RunAsync(telemetry, ct).ConfigureAwait(false);
                return (result.Totals, result.Timings);
            },
            runId), null);
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
    /// Returns the run configuration, analytic prediction, latest totals, and
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
                preset = run.Facts.Subject,
                isGame = run.Facts.IsGame,
                reels = run.Facts.Reels,
                rows = run.Facts.Rows,
                stopsPerReel = run.Facts.StopsByReel,
                paylines = run.Facts.Paylines,
                targetRtp = run.Facts.TargetRtp,
                publishedRtp = run.Facts.PublishedRtp,
                payScaleFactor = run.Facts.PayScaleFactor,
                isRepriced = Math.Abs(run.Facts.PayScaleFactor - 1.0) > 1e-12,
                workers = run.Facts.Workers,
                targetSpins = run.Facts.TargetSpins,
                seed = run.Facts.Seed,
            },
            analytic = new
            {
                baseRtp = run.Analytic.BaseRtp,
                features = run.Analytic.Features.Select(f => new { name = f.Name, rtp = f.Rtp }),
                totalRtp = run.Analytic.TotalRtp,
                sigma = run.Analytic.Sigma,
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
                engineSeconds = run.Timings?.SlowestWorkerSpinTime.TotalSeconds
                    ?? run.EngineClock.Elapsed.TotalSeconds,
                engineSpinsPerSecond = run.Timings?.SpinsPerSecond(latest.Spins)
                    ?? Rate(latest.Spins, run.EngineClock.Elapsed),
                // What the workers spent handing snapshots to the telemetry channel.
                telemetrySeconds = run.Timings?.SlowestWorkerPublishTime.TotalSeconds ?? 0,
                telemetryShare = run.Timings?.PublishShare ?? 0,
                // The worker phase end to end, telemetry included.
                workerSeconds = run.EngineClock.Elapsed.TotalSeconds,
                workerSpinsPerSecond = Rate(latest.Spins, run.EngineClock.Elapsed),
                // What an observer saw, streaming and bookkeeping included.
                observedSeconds = run.ObservedClock.Elapsed.TotalSeconds,
                observedSpinsPerSecond = Rate(latest.Spins, run.ObservedClock.Elapsed),
            },
            industry = run.Recorder.IndustryCheck() is { } check
                ? new
                {
                    spins = check.Spins,
                    deviation = check.Deviation,
                    passed = check.Passed,
                    tolerance = ConvergenceRecorder.IndustryTolerance,
                    minimumSpins = ConvergenceRecorder.IndustryMinimumSpins,
                }
                : null,
            curve = run.Recorder.Curve,
        };
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
            log.Warning(Category, "Run {RunId} cancelled at {Spins} spins",
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

        log.Information(Category,
            "Run {RunId} {Status}: {Spins} spins at {SpinsPerSecond} engine spins/s, measured {Measured}, analytic {Analytic}, band {Band}, verdict {Verdict}, industry {Industry}",
            new LogProperty("RunId", run.RunId),
            new LogProperty("Status", run.Status),
            new LogProperty("Spins", final.Spins),
            new LogProperty("SpinsPerSecond", run.Timings?.SpinsPerSecond(final.Spins)
                ?? Rate(final.Spins, run.EngineClock.Elapsed)),
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
    /// Passes every queued sample to the recorder. A fast run
    /// crosses several stride boundaries inside one 100 ms drain, and skipping to the
    /// newest sample would skip those curve points. Publishing stays consolidated.
    /// </summary>
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
                engineSpinsPerSecond = Rate(latest.Spins, run.EngineClock.Elapsed),
                observedSpinsPerSecond = Rate(latest.Spins, run.ObservedClock.Elapsed),
            });
        }
    }

    private void Publish(string type, object? payload) =>
        stream.Publish(JsonSerializer.Serialize(new { type, data = payload }, Json));
}

/// <summary>
/// What the SPA sends to start a run. Untrusted; every field is validated downstream.
/// A non-empty <c>GameFile</c> runs a shipped game document through
/// GameRunner instead of a solved preset; the RTP fields are ignored then, because a
/// published paytable already decided them.
/// </summary>
public sealed record RunRequest(
    string PresetName,
    int BaseRtpBasisPoints,
    int FreeSpinsRtpBasisPoints,
    int PickBonusRtpBasisPoints,
    ulong Seed,
    int WorkerCount,
    long TargetSpins,
    long Stride,
    string GameFile = "",
    /// <summary>
    /// Optional target TOTAL RTP for a shipped game, in basis points. 0 keeps the game's
    /// published paytable, which is the default and what the article describes. Anything
    /// else re-prices the line paytable to hit this total, the way a cabinet's approved
    /// payback versions are produced from one recipe.
    /// </summary>
    int TargetTotalRtpBasisPoints = 0);
