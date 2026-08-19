using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Chapters;

/// <summary>
/// Episode 7 endpoints for comparing simulation with exhaustive enumeration. The analyzer
/// evaluates every stop combination, while the simulator samples outcomes with a seeded
/// random-number generator. Agreement provides an independent check of both calculations.
/// </summary>
public static class ChapterSevenEndpoints
{
    private static readonly LogCategory Category = new("Chapter07");

    public static void MapChapterSeven(this WebApplication app, StructuredLogger log)
    {
        app.MapPost("/api/ch7/enumerate", (EnumerateRequest request) => Enumerate(request, log));
        app.MapPost("/api/ch7/referee", (RefereeRequest request, CancellationToken ct) => Referee(request, log, ct));
    }

    private static string GamePath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "games", Path.GetFileName(file));

    public sealed record EnumerateRequest(string GameFile);

    /// <summary>
    /// The exhaustive census prices every stop combination. A single-payline report also
    /// breaks wins out by category. A multi-payline window may pay several categories, so
    /// that report keeps the combined window result instead.
    /// </summary>
    private static IResult Enumerate(EnumerateRequest request, StructuredLogger log)
    {
        var path = GamePath(request.GameFile ?? "");
        if (!File.Exists(path))
            return Results.BadRequest(new { error = $"No shipped game named '{request.GameFile}'." });

        if (!GameDefinitionLoader.TryLoad(File.ReadAllText(path), out var definition, out var errors))
            return Results.BadRequest(new { error = "Definition failed to load.", errors });

        GameAnalysis analysis;
        try
        {
            analysis = GameAnalyzer.Analyze(definition!);
        }
        catch (NotSupportedException ex)
        {
            // Return the analyzer's validation error when the game cannot be enumerated exactly.
            return Results.Ok(new { supported = false, reason = ex.Message });
        }

        var game = definition!;
        var combinations = analysis.CombinationCounts
            .OrderBy(c => c.Key.CategoryIndex).ThenBy(c => c.Key.Count)
            .Select(c => new
            {
                category = game.Categories[c.Key.CategoryIndex].Name,
                count = c.Key.Count,
                combinations = c.Value,
                probability = (double)c.Value / analysis.StopCombinations,
            });

        log.Information(Category,
            "Enumerated {Game}: {Space} combinations, line RTP {LineRtp}, bonus RTP {BonusRtp}",
            new LogProperty("Game", game.Name),
            new LogProperty("Space", analysis.StopCombinations),
            new LogProperty("LineRtp", analysis.LineRtp),
            new LogProperty("BonusRtp", analysis.BonusRtp));

        return Results.Ok(new
        {
            supported = true,
            game = game.Name,
            stopCombinations = analysis.StopCombinations,
            hitCombinations = analysis.HitCombinations,
            triggerCombinations = analysis.TriggerCombinations,
            hitFrequency = analysis.HitFrequency,
            triggerProbability = analysis.TriggerProbability,
            lineRtp = analysis.LineRtp,
            bonusRtp = analysis.BonusRtp,
            totalRtp = analysis.TotalRtp,
            sigma = analysis.SigmaPerUnitWagered,
            combinations,
        });
    }

    public sealed record RefereeRequest(string GameFile, ulong Seed, int WorkerCount, long Spins);

    /// <summary>
    /// Enumeration versus simulation on the same game data: run the spins, then put the
    /// measured figures next to the exact ones with the band at that N. The verdict is
    /// computed the same way the finale computes it, so this is the finale in miniature
    /// with the referee's numbers visible.
    /// </summary>
    private static async Task<IResult> Referee(RefereeRequest request, StructuredLogger log, CancellationToken ct)
    {
        var path = GamePath(request.GameFile ?? "");
        if (!File.Exists(path))
            return Results.BadRequest(new { error = $"No shipped game named '{request.GameFile}'." });
        if (request.Spins is < 10_000 or > 20_000_000)
            return Results.BadRequest(new { error = "Spins 10,000-20,000,000." });
        if (request.WorkerCount is < 1 or > 32)
            return Results.BadRequest(new { error = "WorkerCount 1-32." });

        if (!GameDefinitionLoader.TryLoad(File.ReadAllText(path), out var definition, out var errors))
            return Results.BadRequest(new { error = "Definition failed to load.", errors });

        var plan = new RunPlan(
            Guid.CreateVersion7().ToString("n"), request.Seed, request.WorkerCount, request.Spins);
        var runner = new GameRunner(definition!, plan);

        GameRunResult result;
        try
        {
            result = await runner.RunAsync(telemetry: null, ct).ConfigureAwait(false);
        }
        catch (NotSupportedException ex)
        {
            return Results.Ok(new { supported = false, reason = ex.Message });
        }

        var analytic = result.Analytic;
        var band = NormalQuantile.TwoSided99 * analytic.SigmaPerUnitWagered / Math.Sqrt(result.Totals.Spins);
        var withinBand = Math.Abs(result.Totals.MeasuredRtp - analytic.TotalRtp) <= band;

        log.Information(Category,
            "Referee on {Game}: measured {Measured} vs exact {Exact}, band {Band}, verdict {Verdict}",
            new LogProperty("Game", definition!.Name),
            new LogProperty("Measured", result.Totals.MeasuredRtp),
            new LogProperty("Exact", analytic.TotalRtp),
            new LogProperty("Band", band),
            new LogProperty("Verdict", withinBand ? "within band" : "outside band"));

        return Results.Ok(new
        {
            supported = true,
            game = definition!.Name,
            spins = result.Totals.Spins,
            measured = new
            {
                totalRtp = result.Totals.MeasuredRtp,
                lineRtp = result.LineRtp,
                bonusRtp = result.BonusRtp,
                hitFrequency = result.LineHitFrequency,
                triggerFrequency = result.BonusTriggerFrequency,
            },
            exact = new
            {
                totalRtp = analytic.TotalRtp,
                lineRtp = analytic.LineRtp,
                bonusRtp = analytic.BonusRtp,
                hitFrequency = analytic.HitFrequency,
                triggerProbability = analytic.TriggerProbability,
            },
            bandHalfWidth = band,
            withinBand,
        });
    }
}
