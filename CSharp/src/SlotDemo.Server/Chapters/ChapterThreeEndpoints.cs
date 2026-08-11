using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Chapters;

/// <summary>
/// Episode 3's lab — reels and paylines. A strip is an ordered cycle, a window is a
/// contiguous slice of it, and a payline is a row path across the window. Everything
/// here runs the engine's own StripReelSet and Payline.
/// </summary>
public static class ChapterThreeEndpoints
{
    private static readonly LogCategory Category = new("Chapter03");

    public static void MapChapterThree(this WebApplication app, StructuredLogger log)
    {
        app.MapGet("/api/ch3/presets", Presets);
        app.MapPost("/api/ch3/spin", (SpinRequest request) => Spin(request, log));
        app.MapPost("/api/ch3/census", (CensusRequest request) => Census(request, log));
    }

    /// <summary>Strip geometry for every preset, so the page can draw the actual cycles.</summary>
    private static IResult Presets()
    {
        var presets = ReelPreset.All.Values.Select(preset =>
        {
            var reels = preset.BuildReels();
            return new
            {
                name = preset.Name,
                reelCount = preset.ReelCount,
                rows = reels.Rows,
                stopsPerReel = preset.StopsPerReel,
                symbols = preset.SymbolWeights.Select(sw => new
                {
                    id = sw.Symbol.Id,
                    name = sw.Symbol.Name,
                    weight = sw.Weight,
                    probability = (double)sw.Weight / preset.StopsPerReel,
                }),
                // The strip itself: the ordered cycle the window slides over. Reel 0 is
                // representative — stock presets build identical strips per reel.
                strip = reels.Strip(0).ToArray().Select(s => s.Id),
                paylines = preset.Paylines.Select(line => new { name = line.Name, rows = line.Rows }),
            };
        });
        return Results.Ok(presets);
    }

    public sealed record SpinRequest(string PresetName, ulong Seed, int SpinIndex);

    /// <summary>
    /// One spin, fully exploded: the stop each reel landed on, the window cells, and
    /// every payline's read of that window. The page can replay indexes forward and
    /// backward because the stream is deterministic - same seed, same spin, same window.
    /// </summary>
    private static IResult Spin(SpinRequest request, StructuredLogger log)
    {
        if (!ReelPreset.All.TryGetValue(request.PresetName ?? "", out var preset))
            return Results.BadRequest(new { error = $"Unknown preset '{request.PresetName}'." });
        if (request.SpinIndex is < 0 or > 10_000)
            return Results.BadRequest(new { error = "SpinIndex 0-10000." });

        var reels = preset.BuildReels();
        var rng = SpinRng.ForWorker(request.Seed, 0);

        // Deterministic replay: advance the stream past the earlier spins. Each spin
        // consumes exactly ReelCount draws, so the offset is a multiplication, not a log.
        var window = new Symbol[reels.WindowSize];
        for (var skip = 0; skip < request.SpinIndex; skip++)
            reels.DrawWindow(ref rng, window);
        reels.DrawWindow(ref rng, window);

        var lines = preset.Paylines.Select(line =>
        {
            var cells = new List<object>(reels.ReelCount);
            for (var reel = 0; reel < reels.ReelCount; reel++)
            {
                var symbol = window[reel * reels.Rows + line.Rows[reel]];
                cells.Add(new { reel, row = line.Rows[reel], symbolId = symbol.Id, symbol = symbol.Name });
            }
            return new { name = line.Name, rows = line.Rows, cells };
        });

        log.Information(Category,
            "Spin {Index} on {Preset}: seed {Seed}, window drawn, {Lines} lines read",
            new LogProperty("Index", request.SpinIndex),
            new LogProperty("Preset", preset.Name),
            new LogProperty("Seed", request.Seed),
            new LogProperty("Lines", preset.Paylines.Count));

        return Results.Ok(new
        {
            preset = preset.Name,
            spinIndex = request.SpinIndex,
            window = window.Select((s, i) => new
            {
                reel = i / reels.Rows,
                row = i % reels.Rows,
                symbolId = s.Id,
                symbol = s.Name,
            }),
            lines,
        });
    }

    public sealed record CensusRequest(string PresetName, ulong Seed, int Spins, byte SymbolId);

    /// <summary>
    /// The strip-versus-weighted-die argument, measured: draw N windows and count how
    /// often the chosen symbol lands in the centre row per reel, next to the exact
    /// strip probability. The counts converge on the strip's ratio because the strip IS
    /// the distribution.
    /// </summary>
    private static IResult Census(CensusRequest request, StructuredLogger log)
    {
        if (!ReelPreset.All.TryGetValue(request.PresetName ?? "", out var preset))
            return Results.BadRequest(new { error = $"Unknown preset '{request.PresetName}'." });
        if (request.Spins is < 100 or > 1_000_000)
            return Results.BadRequest(new { error = "Spins 100-1,000,000." });

        var reels = preset.BuildReels();
        if (!preset.SymbolWeights.Any(sw => sw.Symbol.Id == request.SymbolId))
            return Results.BadRequest(new { error = $"Preset has no symbol id {request.SymbolId}." });

        var rng = SpinRng.ForWorker(request.Seed, 0);
        var window = new Symbol[reels.WindowSize];
        var centre = reels.Rows / 2;
        var counts = new int[reels.ReelCount];

        for (var spin = 0; spin < request.Spins; spin++)
        {
            reels.DrawWindow(ref rng, window);
            for (var reel = 0; reel < reels.ReelCount; reel++)
                if (window[reel * reels.Rows + centre].Id == request.SymbolId)
                    counts[reel]++;
        }

        var perReel = Enumerable.Range(0, reels.ReelCount).Select(reel => new
        {
            reel,
            observed = (double)counts[reel] / request.Spins,
            expected = reels.ProbabilityOf(reel, request.SymbolId),
            count = counts[reel],
        }).ToArray();

        log.Information(Category,
            "Census on {Preset}: symbol {Symbol} over {Spins} spins, worst gap {Gap}",
            new LogProperty("Preset", preset.Name),
            new LogProperty("Symbol", request.SymbolId),
            new LogProperty("Spins", request.Spins),
            new LogProperty("Gap", perReel.Max(r => Math.Abs(r.observed - r.expected))));

        return Results.Ok(new { preset = preset.Name, spins = request.Spins, symbolId = request.SymbolId, perReel });
    }
}
