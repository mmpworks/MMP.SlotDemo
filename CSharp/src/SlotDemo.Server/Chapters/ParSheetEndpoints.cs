using System.Text.Json;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Rtp;

namespace SlotDemo.Server.Chapters;

/// <summary>
/// The full PAR sheet for a shipped game, in one payload. Layout follows the worked
/// example in Muir's "Elements of Slot Design" ch. 8: geometry and cycle, reel census,
/// paytable with hits/probability/hit-rate/RTP slice per rule, the feature block, and
/// the volatility block (sigma, VI, confidence bands). Every number comes from the
/// exhaustive enumerator, so the sheet IS the game rather than a description of it.
/// </summary>
public static class ParSheetEndpoints
{
    private static readonly LogCategory Category = new("ParSheet");

    public static void MapParSheet(this WebApplication app, StructuredLogger log)
    {
        app.MapPost("/api/par/sheet", (SheetRequest request) => Sheet(request, log));
        app.MapGet("/api/par/summary", () => Summary(log));
    }

    /// <summary>
    /// The Harrigan &amp; Dixon Table-1 view: one summary row per shipped game, in the
    /// column set their published study of 23 real PAR sheets used. Their table is the
    /// only peer-reviewed look at what manufacturers actually summarize; ours adds
    /// nothing and hides nothing, so the two read side by side.
    /// </summary>
    private static IResult Summary(StructuredLogger log)
    {
        var rows = new List<object>();
        foreach (var file in ReelSources.GameFiles())
        {
            var path = Path.Combine(AppContext.BaseDirectory, "games", file!);
            if (!GameDefinitionLoader.TryLoad(File.ReadAllText(path), out var definition, out _)) continue;
            var game = definition!;

            GameAnalysis analysis;
            try { analysis = GameAnalyzer.Analyse(game); }
            catch (NotSupportedException) { continue; }

            // Jackpot = the single highest-paying rule; plays per jackpot = cycle over
            // that rule's hit count, the paper's headline rarity figure.
            var top = analysis.CombinationCounts
                .Select(c => new
                {
                    pay = game.Categories[c.Key.CategoryIndex].PayFor(c.Key.Count) / 100.0,
                    hits = c.Value,
                })
                .OrderByDescending(x => x.pay)
                .First();

            rows.Add(new
            {
                file,
                name = game.Name,
                reels = game.ReelCount,
                lines = game.Paylines.Count,
                hasScatter = game.Bonus is not null,
                wagerCredits = 1,
                symbolsPerReel = string.Join("/", Enumerable.Range(0, game.ReelCount).Select(game.Reels.StopCount)),
                cycle = analysis.StopCombinations,
                paybackPercent = analysis.TotalRtp * 100,
                // Line hits only. Harrigan-style sheets often count any credit-awarding
                // outcome (bonus triggers included), a higher figure; the name says which
                // event this measures so the two are never set side by side unlabeled.
                lineHitFrequencyPercent = analysis.HitFrequency * 100,
                playsPerJackpot = top.hits > 0 ? (double)analysis.StopCombinations / top.hits : 0,
                jackpotCredits = top.pay,
                playsPerBonus = analysis.TriggerProbability > 0 ? 1.0 / analysis.TriggerProbability : (double?)null,
                volatilityIndex90 = 1.6449 * analysis.SigmaPerUnitWagered,
            });
        }

        log.Information(Category, "PAR summary served for {Count} games",
            new LogProperty("Count", rows.Count));
        return Results.Ok(rows);
    }

    public sealed record SheetRequest(string GameFile);

    private static IResult Sheet(SheetRequest request, StructuredLogger log)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "games", Path.GetFileName(request.GameFile ?? ""));
        if (!File.Exists(path))
            return Results.BadRequest(new { error = $"No shipped game named '{request.GameFile}'." });

        var json = File.ReadAllText(path);
        if (!GameDefinitionLoader.TryLoad(json, out var definition, out var errors))
            return Results.BadRequest(new { error = "Definition failed to load.", errors });

        var game = definition!;
        GameAnalysis analysis;
        try
        {
            analysis = GameAnalyzer.Analyse(game);
        }
        catch (NotSupportedException ex)
        {
            return Results.Ok(new { supported = false, reason = ex.Message });
        }

        var reels = game.Reels;
        var reelCount = game.ReelCount;

        // ---- reel census: per-reel occurrence counts straight from the strips ----
        var census = game.Symbols.Select(symbol => new
        {
            symbol.Id,
            symbol.Name,
            symbol.IsWild,
            symbol.IsScatter,
            perReel = Enumerable.Range(0, reelCount)
                .Select(reel => reels.Strip(reel).ToArray().Count(s => s.Id == symbol.Id)),
            total = Enumerable.Range(0, reelCount)
                .Sum(reel => reels.Strip(reel).ToArray().Count(s => s.Id == symbol.Id)),
        });

        // ---- paytable rows: pay, hits, probability, hit rate, RTP slice ----
        var paytable = analysis.CombinationCounts
            .OrderBy(c => c.Key.CategoryIndex).ThenByDescending(c => c.Key.Count)
            .Select(c =>
            {
                var category = game.Categories[c.Key.CategoryIndex];
                var probability = (double)c.Value / analysis.StopCombinations;
                var pay = category.PayFor(c.Key.Count) / 100.0;
                return new
                {
                    category = category.Name,
                    kind = category.Kind.ToString(),
                    count = c.Key.Count,
                    payMultiplier = pay,
                    hits = c.Value,
                    probability,
                    hitRate = probability > 0 ? 1.0 / probability : 0,   // one such win every N spins
                    rtpSlice = pay * probability,
                };
            });

        // ---- feature block: trigger stats + the pick-bonus prize inventory ----
        object? feature = null;
        if (game.Bonus is not null)
        {
            var bonus = game.Bonus;
            // Prize tiers are data the compiled PickBonus no longer carries individually;
            // the document is the source, so read them back from it.
            using var doc = JsonDocument.Parse(json);
            var tiers = new List<object>();
            if (doc.RootElement.TryGetProperty("features", out var features))
                foreach (var f in features.EnumerateArray())
                    if (f.TryGetProperty("prizes", out var prizes))
                        foreach (var prize in prizes.EnumerateArray())
                            tiers.Add(new
                            {
                                value = prize.GetProperty("value").GetInt32(),
                                count = prize.GetProperty("count").GetInt32(),
                            });

            feature = new
            {
                bonus.Name,
                scatter = game.Symbols[bonus.ScatterSymbolId].Name,
                requiredReels = bonus.RequiredReels.Select(r => r + 1),
                triggerProbability = analysis.TriggerProbability,
                triggerRate = analysis.TriggerProbability > 0 ? 1.0 / analysis.TriggerProbability : 0,
                prizeTiers = tiers,
                prizes = bonus.Bonus.PrizeCount,
                blanks = bonus.Bonus.BlankCount,
                consolation = bonus.Bonus.Consolation,
                giftCount = bonus.Bonus.GiftCount,
                prizeTotal = bonus.Bonus.PrizeTotal,
                maxAward = bonus.Bonus.MaxAward,
                meanAward = bonus.Bonus.Mean,
                variance = bonus.Bonus.Variance,
                rtpContribution = analysis.BonusRtp,
            };
        }

        // ---- volatility block: sigma, VI, and the band ladder ----
        (string Level, double Z)[] zs = [("90%", 1.6449), ("95%", 1.96), ("99%", NormalQuantile.TwoSided99)];
        long[] ladder = [10_000, 100_000, 1_000_000, 10_000_000, 100_000_000];
        var volatility = new
        {
            sigma = analysis.SigmaPerUnitWagered,
            lineSigma = analysis.LineSigma,
            bonusSigma = analysis.BonusSigma,
            volatilityIndex = zs.Select(z => new { level = z.Level, vi = z.Z * analysis.SigmaPerUnitWagered }),
            bands = ladder.Select(n => new
            {
                spins = n,
                halfWidth95 = 1.96 * analysis.SigmaPerUnitWagered / Math.Sqrt(n),
                halfWidth99 = NormalQuantile.TwoSided99 * analysis.SigmaPerUnitWagered / Math.Sqrt(n),
            }),
        };

        // ---- verification block: the Muir cross-checks, computed live ----
        var scatterReels = game.Bonus is null
            ? []
            : game.Bonus.RequiredReels.Select(reel =>
            {
                var strip = reels.Strip(reel).ToArray();
                var positions = strip.Select((s, i) => (s, i))
                    .Where(x => x.s.Id == game.Bonus!.ScatterSymbolId)
                    .Select(x => x.i).ToArray();
                var separated = positions.All(a => positions.All(b =>
                    a == b || Math.Min((b - a + strip.Length) % strip.Length, (a - b + strip.Length) % strip.Length) >= reels.Rows));
                return new { reel = reel + 1, positions, separated };
            }).ToArray();

        log.Information(Category,
            "PAR sheet for {Game}: cycle {Cycle}, total RTP {TotalRtp}, sigma {Sigma}",
            new LogProperty("Game", game.Name),
            new LogProperty("Cycle", analysis.StopCombinations),
            new LogProperty("TotalRtp", analysis.TotalRtp),
            new LogProperty("Sigma", analysis.SigmaPerUnitWagered));

        return Results.Ok(new
        {
            supported = true,
            game = new
            {
                name = game.Name,
                reels = reelCount,
                rows = reels.Rows,
                stopsPerReel = Enumerable.Range(0, reelCount).Select(reels.StopCount),
                cycle = analysis.StopCombinations,
                paylines = game.Paylines.Select(l => new { l.Name, rows = l.Rows }),
                betCredits = 1,
            },
            census,
            strips = Enumerable.Range(0, reelCount)
                .Select(reel => reels.Strip(reel).ToArray().Select(s => (int)s.Id)),
            paytable,
            totals = new
            {
                lineRtp = analysis.LineRtp,
                bonusRtp = analysis.BonusRtp,
                totalRtp = analysis.TotalRtp,
                hitCombinations = analysis.HitCombinations,
                lineHitFrequency = analysis.HitFrequency,
                lineHitRate = analysis.HitFrequency > 0 ? 1.0 / analysis.HitFrequency : 0,
            },
            feature,
            volatility,
            verification = new { scatterReels },
        });
    }
}
