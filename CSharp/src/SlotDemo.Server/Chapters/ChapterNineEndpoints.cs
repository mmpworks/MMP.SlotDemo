using System.Diagnostics;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Chapters;

/// <summary>Episode 9 endpoints for measuring optimizations against the original code.</summary>
public static class ChapterNineEndpoints
{
    private static readonly LogCategory Category = new("Chapter09");

    public static void MapChapterNine(this WebApplication app, StructuredLogger log) =>
        app.MapPost("/api/ch9/draw-window", (DrawWindowBenchmarkRequest request) => Benchmark(request, log));

    public sealed record DrawWindowBenchmarkRequest(string SourceId, ulong Seed, int Spins, int Trials = 5);

    private static IResult Benchmark(DrawWindowBenchmarkRequest request, StructuredLogger log)
    {
        if (!ReelSources.TryResolve(request.SourceId, out var source, out var error))
            return Results.BadRequest(new { error });
        if (request.Spins is < 100_000 or > 10_000_000)
            return Results.BadRequest(new { error = "Spins must be 100,000..10,000,000." });
        if (request.Trials is < 3 or > 9 || request.Trials % 2 == 0)
            return Results.BadRequest(new { error = "Trials must be an odd number from 3 through 9." });

        var reels = source!.Reels;
        var baselineWindow = new Symbol[reels.WindowSize];
        var optimizedWindow = new byte[reels.WindowSize];

        // Warm both methods before timing. The warmup uses separate, equal streams and is
        // long enough for tiered compilation without becoming a visible part of the result.
        RunBaseline(reels, baselineWindow, request.Seed, 20_000);
        RunOptimized(reels, optimizedWindow, request.Seed, 20_000);

        var baseline = new double[request.Trials];
        var optimized = new double[request.Trials];
        ulong? expectedChecksum = null;

        for (var trial = 0; trial < request.Trials; trial++)
        {
            // Alternate order so the second method does not always receive the warmer CPU.
            var baselineFirst = trial % 2 == 0;
            (double Rate, ulong Checksum) first = baselineFirst
                ? MeasureBaseline(reels, baselineWindow, request.Seed, request.Spins)
                : MeasureOptimized(reels, optimizedWindow, request.Seed, request.Spins);
            (double Rate, ulong Checksum) second = baselineFirst
                ? MeasureOptimized(reels, optimizedWindow, request.Seed, request.Spins)
                : MeasureBaseline(reels, baselineWindow, request.Seed, request.Spins);

            baseline[trial] = baselineFirst ? first.Rate : second.Rate;
            optimized[trial] = baselineFirst ? second.Rate : first.Rate;
            var baselineChecksum = baselineFirst ? first.Checksum : second.Checksum;
            var optimizedChecksum = baselineFirst ? second.Checksum : first.Checksum;
            if (baselineChecksum != optimizedChecksum)
                return Results.Problem("The two DrawWindow implementations produced different symbol streams.");
            expectedChecksum ??= baselineChecksum;
        }

        Array.Sort(baseline);
        Array.Sort(optimized);
        var baselineMedian = baseline[baseline.Length / 2];
        var optimizedMedian = optimized[optimized.Length / 2];
        var speedup = optimizedMedian / baselineMedian;

        log.Information(Category,
            "DrawWindow benchmark {Source}: baseline {Baseline}, optimized {Optimized}, speedup {Speedup}",
            new LogProperty("Source", source.DisplayName),
            new LogProperty("Baseline", baselineMedian),
            new LogProperty("Optimized", optimizedMedian),
            new LogProperty("Speedup", speedup));

        return Results.Ok(new
        {
            source = source.DisplayName,
            request.Spins,
            request.Trials,
            reels = reels.ReelCount,
            rows = reels.Rows,
            randomSelections = (long)request.Spins * reels.ReelCount,
            visibleCellWrites = (long)request.Spins * reels.WindowSize,
            checksum = $"0x{expectedChecksum.GetValueOrDefault():X16}",
            outputsMatch = true,
            baseline = new
            {
                label = "Initial: full Symbol values and modulo wrapping",
                samples = baseline,
                medianSpinsPerSecond = baselineMedian,
            },
            optimized = new
            {
                label = "Optimized: byte ids and extended lookup strips",
                samples = optimized,
                medianSpinsPerSecond = optimizedMedian,
            },
            speedup,
            percentFaster = (speedup - 1.0) * 100.0,
            memoryTradeoff = $"{reels.ReelCount * (reels.Rows - 1)} extra wrapped entries across {reels.ReelCount} reels",
        });
    }

    private static (double Rate, ulong Checksum) MeasureBaseline(
        StripReelSet reels, Symbol[] window, ulong seed, int spins)
    {
        var clock = Stopwatch.StartNew();
        var checksum = RunBaseline(reels, window, seed, spins);
        clock.Stop();
        return (spins / clock.Elapsed.TotalSeconds, checksum);
    }

    private static (double Rate, ulong Checksum) MeasureOptimized(
        StripReelSet reels, byte[] window, ulong seed, int spins)
    {
        var clock = Stopwatch.StartNew();
        var checksum = RunOptimized(reels, window, seed, spins);
        clock.Stop();
        return (spins / clock.Elapsed.TotalSeconds, checksum);
    }

    private static ulong RunBaseline(StripReelSet reels, Symbol[] window, ulong seed, int spins)
    {
        var rng = SpinRng.ForWorker(seed, 0);
        ulong checksum = 0;
        for (var spin = 0; spin < spins; spin++)
        {
            reels.DrawWindowBaseline(ref rng, window);
            for (var cell = 0; cell < window.Length; cell++)
                checksum = unchecked(checksum * 31 + window[cell].Id);
        }
        return checksum;
    }

    private static ulong RunOptimized(StripReelSet reels, byte[] window, ulong seed, int spins)
    {
        var rng = SpinRng.ForWorker(seed, 0);
        ulong checksum = 0;
        for (var spin = 0; spin < spins; spin++)
        {
            reels.DrawWindowIds(ref rng, window);
            for (var cell = 0; cell < window.Length; cell++)
                checksum = unchecked(checksum * 31 + window[cell]);
        }
        return checksum;
    }
}
