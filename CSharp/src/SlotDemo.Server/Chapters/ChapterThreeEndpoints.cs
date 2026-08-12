using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Chapters;

/// <summary>
/// Episode 3's lab — reels and paylines. A strip is an ordered cycle, a window is a
/// contiguous slice of it, and a payline is a row path across the window. Sources are
/// the stock presets plus the shipped games; Orca Dive is the series' worked example,
/// and its ragged strips (26/29/26/29/26) show a shape the presets cannot.
/// </summary>
public static class ChapterThreeEndpoints
{
    private static readonly LogCategory Category = new("Chapter03");

    public static void MapChapterThree(this WebApplication app, StructuredLogger log)
    {
        app.MapGet("/api/ch3/sources", Sources);
        app.MapPost("/api/ch3/spin", (SpinRequest request) => Spin(request, log));
        app.MapPost("/api/ch3/census", (CensusRequest request) => Census(request, log));
    }

    /// <summary>Strip geometry for every source, so the page can draw the actual cycles.</summary>
    private static IResult Sources()
    {
        var ids = ReelPreset.All.Keys
            .Concat(ReelSources.GameFiles().Select(f => $"{ReelSources.GamePrefix}{f}"));

        var described = new List<object>();
        foreach (var id in ids)
        {
            if (!ReelSources.TryResolve(id, out var resolved, out _)) continue;
            var source = resolved!;
            var reels = source.Reels;
            described.Add(new
            {
                id = source.Id,
                name = source.DisplayName,
                isGame = source.IsGame,
                reelCount = reels.ReelCount,
                rows = reels.Rows,
                stopsPerReel = Enumerable.Range(0, reels.ReelCount).Select(reels.StopCount),
                symbols = source.Symbols.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.IsWild,
                    s.IsScatter,
                    // Per-reel occurrence counts: on Orca, Penguin's 2/0/2/0/2 row IS the
                    // scatter rule made visible.
                    perReel = Enumerable.Range(0, reels.ReelCount)
                        .Select(reel => reels.Strip(reel).ToArray().Count(sym => sym.Id == s.Id)),
                }),
                strips = Enumerable.Range(0, reels.ReelCount)
                    .Select(reel => reels.Strip(reel).ToArray().Select(s => (int)s.Id)),
                paylines = source.Paylines.Select(line => new { name = line.Name, rows = line.Rows }),
            });
        }
        return Results.Ok(described);
    }

    public sealed record SpinRequest(string SourceId, ulong Seed, int SpinIndex);

    /// <summary>
    /// One spin, fully exploded: the window cells and every payline's read of them. The
    /// page can replay indexes forward and backward because the stream is deterministic:
    /// the same seed and spin index reproduce the same window.
    /// </summary>
    private static IResult Spin(SpinRequest request, StructuredLogger log)
    {
        if (!ReelSources.TryResolve(request.SourceId, out var resolved, out var error))
            return Results.BadRequest(new { error });
        if (request.SpinIndex is < 0 or > 10_000)
            return Results.BadRequest(new { error = "SpinIndex 0-10000." });

        var source = resolved!;
        var reels = source.Reels;
        var rng = SpinRng.ForWorker(request.Seed, 0);

        // Deterministic replay: re-run DrawWindow past the earlier spins rather than storing
        // them. Replaying the same calls in the same order reproduces the same window.
        // Seeking by a draw count would land elsewhere: NextInt uses rejection sampling, so
        // a spin consumes ReelCount draws or more.
        var window = new Symbol[reels.WindowSize];
        for (var skip = 0; skip < request.SpinIndex; skip++)
            reels.DrawWindow(ref rng, window);
        reels.DrawWindow(ref rng, window);

        var lines = source.Paylines.Select(line =>
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
            "Spin {Index} on {Source}: seed {Seed}, window drawn, {Lines} lines read",
            new LogProperty("Index", request.SpinIndex),
            new LogProperty("Source", source.DisplayName),
            new LogProperty("Seed", request.Seed),
            new LogProperty("Lines", source.Paylines.Count));

        return Results.Ok(new
        {
            source = source.DisplayName,
            spinIndex = request.SpinIndex,
            window = window.Select((s, i) => new
            {
                reel = i / reels.Rows,
                row = i % reels.Rows,
                symbolId = s.Id,
                symbol = s.Name,
                isWild = s.IsWild,
                isScatter = s.IsScatter,
            }),
            lines,
        });
    }

    public sealed record CensusRequest(string SourceId, ulong Seed, int Spins, byte SymbolId);

    /// <summary>
    /// The strip-versus-weighted-die argument, measured: draw N windows and count how
    /// often the chosen symbol lands in the center row per reel, next to the exact strip
    /// probability. Each reel is its own strip, so the per-reel expectations differ: on
    /// Orca, Wild Orca sits at 2/26 on reel 0 and 1/29 on reel 1.
    /// </summary>
    private static IResult Census(CensusRequest request, StructuredLogger log)
    {
        if (!ReelSources.TryResolve(request.SourceId, out var resolved, out var error))
            return Results.BadRequest(new { error });
        if (request.Spins is < 100 or > 1_000_000)
            return Results.BadRequest(new { error = "Spins 100-1,000,000." });

        var source = resolved!;
        var reels = source.Reels;
        if (source.Symbols.All(s => s.Id != request.SymbolId))
            return Results.BadRequest(new { error = $"Source has no symbol id {request.SymbolId}." });

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
            "Census on {Source}: symbol {Symbol} over {Spins} spins, worst gap {Gap}",
            new LogProperty("Source", source.DisplayName),
            new LogProperty("Symbol", request.SymbolId),
            new LogProperty("Spins", request.Spins),
            new LogProperty("Gap", perReel.Max(r => Math.Abs(r.observed - r.expected))));

        return Results.Ok(new { source = source.DisplayName, spins = request.Spins, symbolId = request.SymbolId, perReel });
    }
}
