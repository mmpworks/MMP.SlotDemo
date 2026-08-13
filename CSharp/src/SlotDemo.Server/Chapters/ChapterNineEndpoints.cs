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

    /// <summary>
    /// Spins between cancellation polls. The check sits between slices so the timed inner
    /// loop keeps the shape the benchmark is measuring.
    /// </summary>
    private const int CancellationSliceSpins = 100_000;

    public static void MapChapterNine(this WebApplication app, StructuredLogger log) =>
        app.MapPost("/api/ch9/draw-window",
            (DrawWindowBenchmarkRequest request, CancellationToken ct) => Benchmark(request, log, ct));

    public sealed record DrawWindowBenchmarkRequest(string SourceId, ulong Seed, int Spins, int Trials = 5);

    private readonly record struct RaceResult(
        double[] Baseline, double[] Optimized, ulong Checksum, bool OutputsMatch);

    private static async Task<IResult> Benchmark(
        DrawWindowBenchmarkRequest request, StructuredLogger log, CancellationToken ct)
    {
        if (!ReelSources.TryResolve(request.SourceId, out var source, out var error))
            return Results.BadRequest(new { error });
        if (request.Spins is < 100_000 or > 10_000_000)
            return Results.BadRequest(new { error = "Spins must be 100,000..10,000,000." });
        if (request.Trials is < 3 or > 9 || request.Trials % 2 == 0)
            return Results.BadRequest(new { error = "Trials must be an odd number from 3 through 9." });

        var reels = source!.Reels;
        var race = await Task
            .Run(() => Race(reels, request.Seed, request.Spins, request.Trials, ct), ct)
            .ConfigureAwait(false);

        if (!race.OutputsMatch)
            return Results.Problem("The two DrawWindow implementations produced different symbol streams.");

        var baseline = race.Baseline;
        var optimized = race.Optimized;
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
            checksum = $"0x{race.Checksum:X16}",
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

    /// <summary>
    /// Warms both implementations, then times the trials, alternating which one runs first so
    /// the second method does not always receive the warmer CPU. A checksum disagreement ends
    /// the race: an implementation drawing a different stream has no speedup worth reporting.
    /// </summary>
    private static RaceResult Race(
        StripReelSet reels, ulong seed, int spins, int trials, CancellationToken ct)
    {
        var baselineWindow = new Symbol[reels.WindowSize];
        var optimizedWindow = new byte[reels.WindowSize];

        // Warm both methods before timing. The warmup uses separate, equal streams and is
        // long enough for tiered compilation without becoming a visible part of the result.
        RunBaseline(reels, baselineWindow, seed, 20_000, ct);
        RunOptimized(reels, optimizedWindow, seed, 20_000, ct);

        var baseline = new double[trials];
        var optimized = new double[trials];
        ulong checksum = 0;

        for (var trial = 0; trial < trials; trial++)
        {
            var baselineFirst = trial % 2 == 0;
            (double Rate, ulong Checksum) first = baselineFirst
                ? MeasureBaseline(reels, baselineWindow, seed, spins, ct)
                : MeasureOptimized(reels, optimizedWindow, seed, spins, ct);
            (double Rate, ulong Checksum) second = baselineFirst
                ? MeasureOptimized(reels, optimizedWindow, seed, spins, ct)
                : MeasureBaseline(reels, baselineWindow, seed, spins, ct);

            baseline[trial] = baselineFirst ? first.Rate : second.Rate;
            optimized[trial] = baselineFirst ? second.Rate : first.Rate;
            if (first.Checksum != second.Checksum)
                return new RaceResult(baseline, optimized, Checksum: 0, OutputsMatch: false);
            checksum = first.Checksum;
        }

        Array.Sort(baseline);
        Array.Sort(optimized);
        return new RaceResult(baseline, optimized, checksum, OutputsMatch: true);
    }

    private static (double Rate, ulong Checksum) MeasureBaseline(
        StripReelSet reels, Symbol[] window, ulong seed, int spins, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var checksum = RunBaseline(reels, window, seed, spins, ct);
        clock.Stop();
        return (spins / clock.Elapsed.TotalSeconds, checksum);
    }

    private static (double Rate, ulong Checksum) MeasureOptimized(
        StripReelSet reels, byte[] window, ulong seed, int spins, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var checksum = RunOptimized(reels, window, seed, spins, ct);
        clock.Stop();
        return (spins / clock.Elapsed.TotalSeconds, checksum);
    }

    private static ulong RunBaseline(
        StripReelSet reels, Symbol[] window, ulong seed, int spins, CancellationToken ct)
    {
        var rng = SpinRng.ForWorker(seed, 0);
        ulong checksum = 0;
        for (var sliceStart = 0; sliceStart < spins; sliceStart += CancellationSliceSpins)
        {
            ct.ThrowIfCancellationRequested();
            var sliceEnd = Math.Min(sliceStart + CancellationSliceSpins, spins);
            for (var spin = sliceStart; spin < sliceEnd; spin++)
            {
                reels.DrawWindowBaseline(ref rng, window);
                for (var cell = 0; cell < window.Length; cell++)
                    checksum = unchecked(checksum * 31 + window[cell].Id);
            }
        }
        return checksum;
    }

    private static ulong RunOptimized(
        StripReelSet reels, byte[] window, ulong seed, int spins, CancellationToken ct)
    {
        var rng = SpinRng.ForWorker(seed, 0);
        ulong checksum = 0;
        for (var sliceStart = 0; sliceStart < spins; sliceStart += CancellationSliceSpins)
        {
            ct.ThrowIfCancellationRequested();
            var sliceEnd = Math.Min(sliceStart + CancellationSliceSpins, spins);
            for (var spin = sliceStart; spin < sliceEnd; spin++)
            {
                reels.DrawWindowIds(ref rng, window);
                for (var cell = 0; cell < window.Length; cell++)
                    checksum = unchecked(checksum * 31 + window[cell]);
            }
        }
        return checksum;
    }
}
