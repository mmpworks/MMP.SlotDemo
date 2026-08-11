using System.Diagnostics;
using System.Threading.Channels;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Chapters;

/// <summary>
/// Episode 5's lab — the simulation engine. Two claims made measurable: determinism
/// (same seed and worker count, identical totals, any number of times) and the two-lane
/// telemetry design (exact totals untouched while the lossy channel drops under load).
/// </summary>
public static class ChapterFiveEndpoints
{
    private static readonly LogCategory Category = new("Chapter05");

    public static void MapChapterFive(this WebApplication app, StructuredLogger log)
    {
        app.MapPost("/api/ch5/determinism", (DeterminismRequest request, CancellationToken ct) => Determinism(request, log, ct));
        app.MapPost("/api/ch5/telemetry", (TelemetryRequest request, CancellationToken ct) => Telemetry(request, log, ct));
    }

    /// <summary>
    /// <paramref name="GameFile"/> switches the subject from a solved preset to a shipped
    /// game document (Orca Dive is the one the series builds), run through GameRunner on
    /// the same engine. Empty means preset.
    /// </summary>
    public sealed record DeterminismRequest(
        string PresetName, ulong Seed, int WorkerCount, long Spins, int Repeats, bool VarySeed,
        string GameFile = "");

    /// <summary>
    /// Run the same configuration several times and compare the final snapshots field by
    /// field. Same seed: every run identical, bit for bit, including with the worker
    /// count held while wall time varies. VarySeed instead shows what a real difference
    /// looks like, so "identical" is a measurement and never an assumption.
    /// </summary>
    private static async Task<IResult> Determinism(
        DeterminismRequest request, StructuredLogger log, CancellationToken ct)
    {
        if (request.Repeats is < 2 or > 5)
            return Results.BadRequest(new { error = "Repeats 2-5." });
        if (request.Spins is < 1_000 or > 5_000_000)
            return Results.BadRequest(new { error = "Spins 1,000-5,000,000." });

        MMP.SlotGame.Core.Games.Definition.GameDefinition? gameDefinition = null;
        ConfigDraft? draft = null;
        if (!string.IsNullOrWhiteSpace(request.GameFile))
        {
            var path = Path.Combine(AppContext.BaseDirectory, "games", Path.GetFileName(request.GameFile));
            if (!File.Exists(path))
                return Results.BadRequest(new { error = $"No shipped game named '{request.GameFile}'." });
            if (!MMP.SlotGame.Core.Games.Definition.GameDefinitionLoader.TryLoad(
                    File.ReadAllText(path), out gameDefinition, out var loadErrors))
                return Results.BadRequest(new { error = "Definition failed to load.", errors = loadErrors });
            if (request.WorkerCount is < 1 or > 64)
                return Results.BadRequest(new { error = "WorkerCount 1..64." });
        }
        else
        {
            draft = new ConfigDraft(
                request.PresetName,
                SimulationConfig.DefaultBaseRtpBasisPoints,
                SimulationConfig.DefaultFreeSpinsRtpBasisPoints,
                SimulationConfig.DefaultPickBonusRtpBasisPoints,
                request.Seed,
                request.WorkerCount,
                request.Spins);
            if (!SimulationConfig.TryCreate(draft, out _, out var errors))
                return Results.BadRequest(new { error = string.Join(" ", errors), errors });
        }

        var runs = new List<object>(request.Repeats);
        RunSnapshot? first = null;
        var allIdentical = true;

        for (var attempt = 0; attempt < request.Repeats; attempt++)
        {
            // A fresh engine per attempt: the engine itself is stateless between runs, and
            // fresh counters are the reset rule (swap, never zero).
            var seed = request.VarySeed ? request.Seed + (ulong)attempt : request.Seed;

            var clock = Stopwatch.StartNew();
            RunSnapshot snapshot;
            if (gameDefinition is not null)
            {
                var plan = new RunPlan(
                    Guid.CreateVersion7().ToString("n"), seed, request.WorkerCount, request.Spins);
                var runner = new MMP.SlotGame.Core.Games.GameRunner(gameDefinition, plan);
                snapshot = (await runner.RunAsync(telemetry: null, ct).ConfigureAwait(false)).Totals;
            }
            else
            {
                var attemptDraft = draft! with { MasterSeed = seed };
                SimulationConfig.TryCreate(attemptDraft, out var attemptConfig, out _);
                var game = PresetGame.Build(attemptConfig!);
                snapshot = await game.Engine().RunAsync(telemetry: null, observer: null, ct).ConfigureAwait(false);
            }
            clock.Stop();

            if (first is null) first = snapshot;
            else if (snapshot != first.Value) allIdentical = false;

            runs.Add(new
            {
                attempt,
                seed,
                spins = snapshot.Spins,
                wageredMillicents = snapshot.WageredMillicents,
                returnedMillicents = snapshot.ReturnedMillicents,
                hits = snapshot.Hits,
                measuredRtp = snapshot.MeasuredRtp,
                elapsedMs = clock.Elapsed.TotalMilliseconds,
                spinsPerSecond = snapshot.Spins / Math.Max(clock.Elapsed.TotalSeconds, 1e-9),
            });
        }

        log.Information(Category,
            "Determinism check on {Preset}: {Repeats} runs of {Spins} spins, varySeed {VarySeed}, identical {Identical}",
            new LogProperty("Preset", gameDefinition?.Name ?? request.PresetName),
            new LogProperty("Repeats", request.Repeats),
            new LogProperty("Spins", request.Spins),
            new LogProperty("VarySeed", request.VarySeed),
            new LogProperty("Identical", allIdentical));

        return Results.Ok(new
        {
            preset = gameDefinition?.Name ?? request.PresetName,
            varySeed = request.VarySeed,
            identical = allIdentical,
            runs,
        });
    }

    public sealed record TelemetryRequest(string PresetName, ulong Seed, long Spins, int ChannelCapacity);

    /// <summary>
    /// The two-lane argument with numbers on it. The workers write a snapshot per batch
    /// into a bounded drop-oldest channel while a deliberately slow reader drains it.
    /// The response counts samples produced against samples delivered, then puts the
    /// exact final totals next to the last sample the lossy lane happened to carry.
    /// </summary>
    private static async Task<IResult> Telemetry(
        TelemetryRequest request, StructuredLogger log, CancellationToken ct)
    {
        if (request.Spins is < 10_000 or > 10_000_000)
            return Results.BadRequest(new { error = "Spins 10,000-10,000,000." });
        if (request.ChannelCapacity is < 1 or > 4096)
            return Results.BadRequest(new { error = "ChannelCapacity 1-4096." });

        var draft = new ConfigDraft(
            request.PresetName,
            SimulationConfig.DefaultBaseRtpBasisPoints,
            SimulationConfig.DefaultFreeSpinsRtpBasisPoints,
            SimulationConfig.DefaultPickBonusRtpBasisPoints,
            request.Seed,
            WorkerCount: 8,
            request.Spins);
        if (!SimulationConfig.TryCreate(draft, out var config, out var errors))
            return Results.BadRequest(new { error = string.Join(" ", errors), errors });

        var game = PresetGame.Build(config!);

        var channel = Channel.CreateBounded<TelemetrySample>(new BoundedChannelOptions(request.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

        // A reader that sleeps between reads, standing in for a slow browser. The point
        // is what does NOT happen: the workers never notice.
        long delivered = 0;
        TelemetrySample last = default;
        var reader = Task.Run(async () =>
        {
            await foreach (var sample in channel.Reader.ReadAllAsync(CancellationToken.None))
            {
                delivered++;
                last = sample;
                await Task.Delay(5, CancellationToken.None).ConfigureAwait(false);
            }
        }, CancellationToken.None);

        var clock = Stopwatch.StartNew();
        var final = await game.Engine().RunAsync(channel.Writer, observer: null, ct).ConfigureAwait(false);
        clock.Stop();
        await reader.ConfigureAwait(false);

        // Snapshots produced = one per full batch per worker, plus the final one. This is
        // arithmetic on the engine's contract, so the page can show produced vs delivered
        // without instrumenting the hot path.
        var produced = final.Spins / 4096 + config!.WorkerCount + 1;

        log.Information(Category,
            "Telemetry pressure on {Preset}: {Spins} spins, capacity {Capacity}, ~{Produced} samples produced, {Delivered} delivered",
            new LogProperty("Preset", request.PresetName),
            new LogProperty("Spins", final.Spins),
            new LogProperty("Capacity", request.ChannelCapacity),
            new LogProperty("Produced", produced),
            new LogProperty("Delivered", delivered));

        return Results.Ok(new
        {
            preset = request.PresetName,
            channelCapacity = request.ChannelCapacity,
            elapsedMs = clock.Elapsed.TotalMilliseconds,
            spinsPerSecond = final.Spins / Math.Max(clock.Elapsed.TotalSeconds, 1e-9),
            samplesProducedApprox = produced,
            samplesDelivered = delivered,
            samplesDroppedApprox = Math.Max(0, produced - delivered),
            exactFinal = new
            {
                spins = final.Spins,
                wageredMillicents = final.WageredMillicents,
                returnedMillicents = final.ReturnedMillicents,
                measuredRtp = final.MeasuredRtp,
            },
            lastDeliveredSample = new
            {
                spins = last.Totals.Spins,
                measuredRtp = last.Totals.MeasuredRtp,
            },
        });
    }
}
