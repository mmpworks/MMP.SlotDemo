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
/// Owns the active simulation run behind the finale page.
///
/// A request can use a solved preset or a shipped game document such as Orca Dive. Presets
/// get their analytic result from the solver and closed-form feature math; game documents
/// are enumerated. After preparation, both paths use the same engine, recorder, and stream.
///
/// Engine totals remain integer counters. Telemetry contains absolute snapshots copied for
/// display and may drop old samples under pressure without changing those totals. A second
/// start is rejected while a run is active because the page has one run state and one chart.
/// </summary>
public sealed class RunCoordinator(RunStreamService stream, StructuredLogger log)
{
    private static readonly LogCategory Category = new("SimulationRun");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Lock _gate = new();
    private ActiveRun? _current;

    /// <summary>Run configuration shared by preset and loaded-game responses.</summary>
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

    /// <summary>Common execution contract produced by both preparation paths.</summary>
    private delegate Task<(RunSnapshot Totals, EngineTimings Timings)> SubjectRunner(
        ChannelWriter<TelemetrySample> telemetry, CancellationToken ct);

    /// <summary>
    /// Mutable state for the current run. The task, status, timings, and clocks change over
    /// the run's lifetime, so this object is shared rather than copied as a record.
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
        /// Wall-clock time from request acceptance through the terminal state, including
        /// telemetry processing and streaming.
        /// </summary>
        public Stopwatch ObservedClock { get; } = Stopwatch.StartNew();

        /// <summary>
        /// Elapsed time for the worker phase. Used for live engine throughput until the
        /// workers return their own timings.
        /// </summary>
        public Stopwatch EngineClock { get; } = new();

        /// <summary>
        /// Worker timings returned at completion; <see langword="null"/> while running.
        /// </summary>
        public EngineTimings? Timings { get; set; }

    }

    /// <summary>
    /// Scales a shipped game's line pays toward a requested total RTP.
///
    /// Feature RTP is unchanged, so it forms the floor and is subtracted before calculating
    /// the line-pay scale factor. Targets at or below that floor cannot be reached by changing
    /// line pays and are rejected.
///
    /// Scaled pays are rounded to hundredths of a wager. The method enumerates the resulting
    /// game again and returns its realized RTP for the confidence band.
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
    /// Prepares the requested subject and starts it unless another run is active. Returns
    /// the status code and response body consumed by the endpoint.
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
            // Publish the task while holding the same lock used by IsRunning.
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

        // The request passed the integer limits, but paytable rounding can move the realized
        // RTP. Validate the solved value before reporting success.
        if (breakdown.TotalRtp > SimulationConfig.MaxAggregateBasisPoints / 10_000.0)
            return (null, (500, new { title = "Solver produced a realized RTP above the ceiling", status = 500, breakdown.TotalRtp }));
        // Allow one basis point below the floor because a request at the floor can round
        // slightly under it. Keep the ceiling strict so rounding cannot exceed the cap.
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
            // Enumerate the document before sampling to obtain its RTP and sigma.
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

        // A loaded game is a new object with cold lazy tables. Build them before starting the
        // engine clock so table enumeration is not counted as spin time.
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
    /// Builds the current response for polling and for browsers that join the event stream
    /// after a run has started. Includes the retained curve so a late browser can catch up.
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
                // Worker spin time excludes telemetry publication. Before final timings are
                // available, use the coordinator's worker-phase clock.
                engineSeconds = run.Timings?.SlowestWorkerSpinTime.TotalSeconds
                    ?? run.EngineClock.Elapsed.TotalSeconds,
                engineSpinsPerSecond = run.Timings?.SpinsPerSecond(latest.Spins)
                    ?? Rate(latest.Spins, run.EngineClock.Elapsed),
                // Time the slowest worker spent publishing telemetry snapshots.
                telemetrySeconds = run.Timings?.SlowestWorkerPublishTime.TotalSeconds ?? 0,
                telemetryShare = run.Timings?.PublishShare ?? 0,
                // Worker phase measured by the coordinator, telemetry included.
                workerSeconds = run.EngineClock.Elapsed.TotalSeconds,
                workerSpinsPerSecond = Rate(latest.Spins, run.EngineClock.Elapsed),
                // Full request lifetime, including recorder and stream work.
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
        // Keep telemetry non-blocking. PumpAsync drains this queue; if it falls behind, an
        // old absolute snapshot can be dropped without changing engine totals.
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
            // Workers normally observe cancellation at a batch boundary and return their
            // partial totals, so the token determines the terminal status.
            terminal = ct.IsCancellationRequested ? "cancelled" : "completed";
        }
        catch (OperationCanceledException)
        {
            // Cancellation can arrive before a worker returns totals. Preserve the latest
            // snapshot already accepted by the recorder.
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
        // Freeze observed throughput before publishing the terminal response.
        run.ObservedClock.Stop();
        // Set the status after Complete so a terminal response includes final totals.
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
    /// Minimum interval between live progress events. Curve points are governed by spin
    /// stride and are published whenever the recorder creates them.
    /// </summary>
    private const int ProgressIntervalMs = 100;

    /// <summary>
    /// Drains telemetry continuously so the bounded channel does not discard snapshots at
    /// stride boundaries. Chart points are published as they are recorded; progress events
    /// are throttled separately to <see cref="ProgressIntervalMs"/>.
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

            // Progress drives the numeric readout; curve points have their own spin stride.
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
/// Request body accepted from the SPA. A non-empty <c>GameFile</c> selects a shipped game
/// document instead of a solved preset. Preset RTP fields are ignored on that path.
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
    /// Optional total RTP target for a shipped game, in basis points. Zero keeps the
    /// published paytable; another value scales the line pays toward the requested total.
    /// </summary>
    int TargetTotalRtpBasisPoints = 0);
