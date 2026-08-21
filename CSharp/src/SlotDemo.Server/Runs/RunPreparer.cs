using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Turns a validated <see cref="RunRequest"/> into a <see cref="PreparedRun"/>: the subject
/// to spin plus the facts and analytic reference the page shows about it.
///
/// Two kinds of subject prepare here. A solved preset gets its analytic result from the
/// solver and closed-form feature math; a shipped game document is enumerated. Both return
/// the same <see cref="PreparedRun"/>, which is what lets the coordinator run either kind
/// through identical plumbing.
/// </summary>
internal sealed class RunPreparer(StructuredLogger log)
{
    public PrepareResult PreparePreset(RunRequest request)
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
            log.Warning(RunLogging.Category, "Run rejected: {Errors}",
                new LogProperty("Errors", string.Join(" | ", errors)));
            return PrepareResult.Fail(400, new { title = "Invalid configuration", status = 400, errors });
        }

        var valid = config!;
        var game = PresetGame.Build(valid);
        var breakdown = game.Analysis;

        // The requested split passed the solver's RTP limits as integers. The REALIZED game
        // is what the solver actually produced after rounding, so it gets checked against the
        // same two constants — a paytable that rounds its way outside the limits is a bug the
        // page must never render as success.
        if (breakdown.TotalRtp > SimulationConfig.MaxAggregateBasisPoints / 10_000.0)
            return PrepareResult.Fail(500, new { title = "Solver produced a realized RTP above the ceiling", status = 500, breakdown.TotalRtp });
        // One basis point of grace on the floor only: a request at exactly the floor may
        // round a hair under it, and the limits must never reject a target they invited.
        // The ceiling stays strict — rounding over the top is the hazard it exists to catch.
        if (breakdown.TotalRtp < (SimulationConfig.MinAggregateBasisPoints - 1) / 10_000.0)
            return PrepareResult.Fail(500, new { title = "Solver produced a realized RTP below the floor", status = 500, breakdown.TotalRtp });

        var facts = new RunFacts(
            valid.Preset.Name, IsGame: false,
            valid.Preset.ReelCount, MMP.SlotGame.Core.Reels.StripReelSet.DefaultRows,
            string.Join('/', valid.Preset.StopCounts), valid.Preset.Paylines.Count,
            valid.TargetTotalRtp, valid.WorkerCount, valid.TargetSpins,
            valid.TargetTotalRtp, 1.0, valid.MasterSeed);

        var analytic = new AnalyticView(
            breakdown.BaseRtp, breakdown.Features, breakdown.TotalRtp, breakdown.SigmaPerUnitWagered);

        var engine = game.Engine();
        return PrepareResult.Ok(new PreparedRun(
            facts, analytic,
            async (telemetry, ct) =>
                (await engine.RunAsync(telemetry, observer: null, ct).ConfigureAwait(false), engine.Timings),
            valid.RunId));
    }

    public PrepareResult PrepareGame(RunRequest request)
    {
        if (request.WorkerCount is < 1 or > 64)
            return PrepareResult.Fail(400, new { title = "WorkerCount must be 1..64", status = 400 });
        if (request.TargetSpins < 1)
            return PrepareResult.Fail(400, new { title = "TargetSpins must be at least 1", status = 400 });

        var path = Path.Combine(AppContext.BaseDirectory, "games", Path.GetFileName(request.GameFile));
        if (!File.Exists(path))
            return PrepareResult.Fail(400, new { title = $"No shipped game named '{request.GameFile}'", status = 400 });
        if (!GameDefinitionLoader.TryLoad(File.ReadAllText(path), out var definition, out var errors))
            return PrepareResult.Fail(400, new { title = "Game definition failed to load", status = 400, errors });

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
            return PrepareResult.Fail(400, new { title = ex.Message, status = 400 });
        }

        var publishedRtp = analysis.TotalRtp;
        var scaleFactor = 1.0;

        if (request.TargetTotalRtpBasisPoints != 0)
        {
            var (repriced, repricedAnalysis, factor, error) = Reprice(game, analysis, request);
            if (error is not null) return new PrepareResult(null, error);
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

        return PrepareResult.Ok(new PreparedRun(
            facts, analytic,
            async (telemetry, ct) =>
            {
                var result = await runner.RunAsync(telemetry, ct).ConfigureAwait(false);
                return (result.Totals, result.Timings);
            },
            runId));
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
}
