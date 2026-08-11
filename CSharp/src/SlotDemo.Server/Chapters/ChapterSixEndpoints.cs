using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;

namespace SlotDemo.Server.Chapters;

/// <summary>
/// Episode 6's lab — games as data. A slot game is a JSON document; the loader either
/// returns a valid game or the complete list of reasons it refused. The shipped games
/// live in games/ and travel with the server, so the lab loads the real files.
/// </summary>
public static class ChapterSixEndpoints
{
    private static readonly LogCategory Category = new("Chapter06");

    public static void MapChapterSix(this WebApplication app, StructuredLogger log)
    {
        app.MapGet("/api/ch6/games", () => Games(log));
        app.MapPost("/api/ch6/validate", (ValidateRequest request) => Validate(request, log));
    }

    private static string GamesDirectory =>
        Path.Combine(AppContext.BaseDirectory, "games");

    /// <summary>The shipped definitions, loaded and summarized through the real loader.</summary>
    private static IResult Games(StructuredLogger log)
    {
        var summaries = new List<object>();
        foreach (var path in Directory.GetFiles(GamesDirectory, "*.json").OrderBy(p => p))
        {
            var json = File.ReadAllText(path);
            if (!GameDefinitionLoader.TryLoad(json, out var definition, out var errors))
            {
                summaries.Add(new { file = Path.GetFileName(path), valid = false, errors });
                continue;
            }

            var game = definition!;
            summaries.Add(new
            {
                file = Path.GetFileName(path),
                valid = true,
                name = game.Name,
                source = game.Source,
                reels = game.ReelCount,
                rows = game.Reels.Rows,
                stopsPerReel = Enumerable.Range(0, game.ReelCount).Select(game.Reels.StopCount),
                stopCombinations = game.StopCombinations,
                symbols = game.Symbols.Select(s => new { s.Id, s.Name, s.IsWild, s.IsScatter }),
                paylines = game.Paylines.Select(l => new { l.Name, l.Rows }),
                categories = game.Categories.Select(c => new
                {
                    c.Name,
                    kind = c.Kind.ToString(),
                    pays = Enumerable.Range(1, game.ReelCount)
                        .Where(k => c.PayFor(k) > 0)
                        .Select(k => new { count = k, payHundredths = c.PayFor(k) }),
                }),
                bonus = game.Bonus is null ? null : new
                {
                    game.Bonus.Name,
                    scatterSymbolId = game.Bonus.ScatterSymbolId,
                    requiredReels = game.Bonus.RequiredReels,
                    prizes = game.Bonus.Bonus.PrizeCount,
                    blanks = game.Bonus.Bonus.BlankCount,
                    maxAward = game.Bonus.Bonus.MaxAward,
                },
            });
        }

        log.Information(Category, "Listed {Count} shipped game definitions",
            new LogProperty("Count", summaries.Count));
        return Results.Ok(summaries);
    }

    public sealed record ValidateRequest(string Json);

    /// <summary>
    /// The loader as a teaching surface: paste any JSON, get back either the compiled
    /// game or every problem at once. Collecting the full list instead of stopping at
    /// the first is the design decision on display - a author fixes a file in one pass.
    /// </summary>
    private static IResult Validate(ValidateRequest request, StructuredLogger log)
    {
        if (string.IsNullOrWhiteSpace(request.Json))
            return Results.BadRequest(new { error = "Json is required." });
        if (request.Json.Length > 256 * 1024)
            return Results.BadRequest(new { error = "Json over 256 KB." });

        if (!GameDefinitionLoader.TryLoad(request.Json, out var definition, out var errors))
        {
            log.Warning(Category, "Definition rejected with {Count} problems",
                new LogProperty("Count", errors.Count));
            return Results.Ok(new { valid = false, errors });
        }

        var game = definition!;
        log.Information(Category, "Definition '{Name}' compiled: {Reels} reels, {Combinations} combinations",
            new LogProperty("Name", game.Name),
            new LogProperty("Reels", game.ReelCount),
            new LogProperty("Combinations", game.StopCombinations));

        return Results.Ok(new
        {
            valid = true,
            name = game.Name,
            reels = game.ReelCount,
            rows = game.Reels.Rows,
            stopCombinations = game.StopCombinations,
            symbols = game.Symbols.Count,
            categories = game.Categories.Count,
            hasBonus = game.Bonus is not null,
        });
    }
}
