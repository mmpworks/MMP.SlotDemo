using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Validates a <see cref="RunRequest"/> and builds the subject, display facts, analytic
/// reference, and execution delegate required by <see cref="RunCoordinator"/>.
///
/// Presets use solver and closed-form results. Shipped games use exhaustive enumeration.
/// Both paths return the same <see cref="PreparedRun"/> contract.
/// </summary>
internal sealed class RunPreparer(StructuredLogger log)
{
    public RunPreparationResult PreparePreset(RunRequest request)
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
            return RunPreparationResult.Failure(
                400, new { title = "Invalid configuration", status = 400, errors });
        }

        var valid = config!;
        var game = PresetGame.Build(valid);
        var analysis = game.Analysis;

        // Validate the realized paytable after rounding, not just the requested integer
        // basis points. Rounding can move the solved RTP outside the accepted range.
        if (analysis.TotalRtp > SimulationConfig.MaxAggregateBasisPoints / 10_000.0)
            return RunPreparationResult.Failure(500, new
            {
                title = "Solver produced a realized RTP above the ceiling",
                status = 500,
                analysis.TotalRtp,
            });
        // Allow one basis point below the floor because a request at the floor can round
        // down. Keep the ceiling strict so rounding cannot produce an over-limit game.
        if (analysis.TotalRtp < (SimulationConfig.MinAggregateBasisPoints - 1) / 10_000.0)
            return RunPreparationResult.Failure(500, new
            {
                title = "Solver produced a realized RTP below the floor",
                status = 500,
                analysis.TotalRtp,
            });

        var configuration = new RunConfiguration(
            valid.Preset.Name, IsShippedGame: false,
            valid.Preset.ReelCount, MMP.SlotGame.Core.Reels.StripReelSet.DefaultRows,
            string.Join('/', valid.Preset.StopCounts), valid.Preset.Paylines.Count,
            valid.TargetTotalRtp, valid.WorkerCount, valid.TargetSpins,
            valid.TargetTotalRtp, 1.0, valid.MasterSeed);

        var reference = new AnalyticReference(
            analysis.BaseRtp, analysis.Features, analysis.TotalRtp, analysis.SigmaPerUnitWagered);

        var engine = game.Engine();
        return RunPreparationResult.Success(new PreparedRun(
            configuration, reference,
            async (telemetry, ct) =>
                (await engine.RunAsync(telemetry, observer: null, ct).ConfigureAwait(false), engine.Timings),
            valid.RunId));
    }

    public RunPreparationResult PrepareShippedGame(RunRequest request)
    {
        if (request.WorkerCount is < 1 or > 64)
            return RunPreparationResult.Failure(
                400, new { title = "WorkerCount must be 1..64", status = 400 });
        if (request.TargetSpins < 1)
            return RunPreparationResult.Failure(
                400, new { title = "TargetSpins must be at least 1", status = 400 });

        var gamePath = Path.Combine(
            AppContext.BaseDirectory, "games", Path.GetFileName(request.GameFile));
        if (!File.Exists(gamePath))
            return RunPreparationResult.Failure(
                400, new { title = $"No shipped game named '{request.GameFile}'", status = 400 });
        if (!GameDefinitionLoader.TryLoad(
                File.ReadAllText(gamePath), out var definition, out var errors))
            return RunPreparationResult.Failure(
                400, new { title = "Game definition failed to load", status = 400, errors });

        var game = definition!;
        GameAnalysis analysis;
        try
        {
            // Enumerate the document before simulation to obtain its exact RTP and sigma.
            analysis = GameAnalyzer.Analyze(game);
        }
        catch (NotSupportedException ex)
        {
            return RunPreparationResult.Failure(
                400, new { title = ex.Message, status = 400 });
        }

        var publishedRtp = analysis.TotalRtp;
        var scaleFactor = 1.0;

        if (request.TargetTotalRtpBasisPoints != 0)
        {
            var reprice = RepriceLinePays(game, analysis, request);
            if (reprice.Error is { } error) return RunPreparationResult.Failure(error);
            game = reprice.Game!;
            analysis = reprice.Analysis!;
            scaleFactor = reprice.Factor;
        }

        // Materialize the per-run Lazy tables before starting the engine clock. Otherwise
        // the first worker's timing includes exhaustive enumeration as simulation work.
        _ = game.ProgressiveOutcomes;

        var runId = Guid.CreateVersion7().ToString("n");
        var plan = new RunPlan(runId, request.Seed, request.WorkerCount, request.TargetSpins);
        var runner = new GameRunner(game, plan, analysis);

        var configuration = new RunConfiguration(
            game.Name, IsShippedGame: true,
            game.ReelCount, game.Reels.Rows,
            string.Join("/", Enumerable.Range(0, game.ReelCount).Select(game.Reels.StopCount)),
            game.Paylines.Count,
            analysis.TotalRtp, request.WorkerCount, request.TargetSpins,
            publishedRtp, scaleFactor, request.Seed);

        var features = game.Bonus is null
            ? (IReadOnlyList<(string, double)>)[]
            : [(game.Bonus.Name, analysis.BonusRtp)];
        var reference = new AnalyticReference(
            analysis.LineRtp, features, analysis.TotalRtp, analysis.SigmaPerUnitWagered);

        return RunPreparationResult.Success(new PreparedRun(
            configuration, reference,
            async (telemetry, ct) =>
            {
                var result = await runner.RunAsync(telemetry, ct).ConfigureAwait(false);
                return (result.Totals, result.Timings);
            },
            runId));
    }

    /// <summary>
    /// Rescales a shipped game's line paytable toward a requested total RTP.
    ///
    /// Total RTP includes line and feature RTP. This method changes only line pays, so the
    /// feature RTP is the minimum reachable total. Targets at or below that value fail.
    ///
    /// Pays round to hundredths of a wager. The returned analysis re-enumerates the scaled
    /// game and supplies the actual RTP used by confidence-band checks.
    /// </summary>
    private static PaytableRepriceResult RepriceLinePays(
        GameDefinition game,
        GameAnalysis analysis,
        RunRequest request)
    {
        var targetBasisPoints = request.TargetTotalRtpBasisPoints;
        if (targetBasisPoints < SimulationConfig.MinAggregateBasisPoints
            || targetBasisPoints > SimulationConfig.MaxAggregateBasisPoints)
            return PaytableRepriceResult.Failure(400, new
            {
                title = $"Target total RTP must be {SimulationConfig.MinAggregateBasisPoints}"
                    + $"-{SimulationConfig.MaxAggregateBasisPoints} basis points",
                status = 400,
            });

        var targetTotalRtp = targetBasisPoints / 10_000.0;
        var featureRtp = analysis.TotalRtp - analysis.LineRtp;
        var targetLineRtp = targetTotalRtp - featureRtp;

        if (analysis.LineRtp <= 0)
            return PaytableRepriceResult.Failure(400, new
            {
                title = "This game pays nothing on the line, so its paytable cannot be re-priced",
                status = 400,
            });

        if (targetLineRtp <= 0)
            return PaytableRepriceResult.Failure(400, new
            {
                title = $"This game's feature alone returns {featureRtp * 100:0.####}%, "
                    + $"so a total of {targetTotalRtp * 100:0.##}% cannot be reached by re-pricing lines",
                status = 400,
            });

        var payScaleFactor = targetLineRtp / analysis.LineRtp;
        var repricedGame = game.WithScaledPays(payScaleFactor);

        GameAnalysis repricedAnalysis;
        try { repricedAnalysis = GameAnalyzer.Analyze(repricedGame); }
        catch (NotSupportedException ex)
        {
            return PaytableRepriceResult.Failure(
                400, new { title = ex.Message, status = 400 });
        }

        return PaytableRepriceResult.Success(repricedGame, repricedAnalysis, payScaleFactor);
    }

    /// <summary>
    /// Returns either the scaled game and its new analysis or an error. On error,
    /// <see cref="Factor"/> remains 1.0.
    /// </summary>
    private sealed record PaytableRepriceResult(
        GameDefinition? Game,
        GameAnalysis? Analysis,
        double Factor,
        (int Status, object Body)? Error)
    {
        public static PaytableRepriceResult Success(
            GameDefinition game,
            GameAnalysis analysis,
            double factor) =>
            new(game, analysis, factor, null);

        public static PaytableRepriceResult Failure(int status, object body) =>
            new(null, null, 1.0, (status, body));
    }
}
