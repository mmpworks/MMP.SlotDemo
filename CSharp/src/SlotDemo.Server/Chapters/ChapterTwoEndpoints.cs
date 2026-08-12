using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using SlotDemo.Server.Chapters.Money;
using SlotDemo.Server.Chapters.Simulation;

namespace SlotDemo.Server.Chapters;

/// <summary>
/// Episode 2's lab endpoints — exact money and deterministic randomness.
///
/// Every route runs the real <see cref="Millicents"/> and <see cref="SpinRng"/> types and
/// narrates each step through Herald, so the page's controls on the left and the log
/// stream underneath tell the same story from two directions.
/// </summary>
public static class ChapterTwoEndpoints
{
    private static readonly LogCategory Category = new("Chapter02");

    public static void MapChapterTwo(this WebApplication app, StructuredLogger log)
    {
        app.MapPost("/api/ch2/money", (MoneyRequest request) => Money(request, log));
        app.MapPost("/api/ch2/rng", (RngRequest request) => Rng(request, log));
        app.MapPost("/api/ch2/bias", (BiasRequest request) => Bias(request, log));
    }

    // ---- exact money -------------------------------------------------------------

    /// <param name="WagerMillicents">
    /// The odd-amount seam: whole credits always divide evenly by the pay scale, so the way
    /// to watch the type refuse an inexact conversion is to hand it a raw amount that does
    /// not divide evenly.
    /// </param>
    public sealed record MoneyRequest(
        long WagerCredits, int ScaledMultiplier, long Repeats, long WagerMillicents = 0);

    public sealed record AmountView(long Millicents, double Credits, string Display, string Bits);

    public sealed record MoneyResponse(
        AmountView Wager,
        AmountView Pay,
        AmountView ExactTotal,
        double FloatTotal,
        double DriftCredits,
        bool FloatAgrees,
        double AssociativeLeft,
        double AssociativeRight,
        string? Refusal);

    private static IResult Money(MoneyRequest request, StructuredLogger log)
    {
        if (request.Repeats <= 0 || request.ScaledMultiplier < 0)
            return Results.BadRequest(new { error = "Repeats must be positive; ScaledMultiplier cannot be negative." });
        if (request.WagerMillicents <= 0 && request.WagerCredits <= 0)
            return Results.BadRequest(new { error = "Give a positive WagerCredits or WagerMillicents." });

        var wager = request.WagerMillicents > 0
            ? new Millicents(request.WagerMillicents)
            : Millicents.FromCredits(request.WagerCredits);
        log.Information(Category,
            "Wager {Display} stored as {Raw} millicents",
            new LogProperty("Display", wager.ToString()),
            new LogProperty("Raw", wager.Value));

        Millicents pay;
        string? refusal = null;
        try
        {
            pay = wager.ScaledMultiply(request.ScaledMultiplier);
            log.Information(Category,
                "ScaledMultiply({Scaled}) -> {Raw} millicents ({Display})",
                new LogProperty("Scaled", request.ScaledMultiplier),
                new LogProperty("Raw", pay.Value),
                new LogProperty("Display", pay.ToString()));
        }
        catch (InvalidOperationException ex)
        {
            // An inexact conversion is the expected result for this lab input.
            pay = Millicents.Zero;
            refusal = ex.Message;
            log.Warning(Category, "ScaledMultiply refused: {Reason}", new LogProperty("Reason", ex.Message));
        }

        // Integer scaling stands in for repeated addition. Both are exact; this one returns
        // in milliseconds.
        var exactTotal = pay * request.Repeats;

        // The same arithmetic in double, accumulated the way a float-based engine would.
        // Past 2M steps the loop stops and scales the result, to keep the request bounded.
        var floatStep = wager.ToCredits() * (request.ScaledMultiplier / 100.0);
        double floatTotal = 0;
        var accumulateSteps = Math.Min(request.Repeats, 2_000_000);
        for (long i = 0; i < accumulateSteps; i++)
            floatTotal += floatStep;
        if (accumulateSteps < request.Repeats)
            floatTotal = floatTotal / accumulateSteps * request.Repeats;

        var drift = floatTotal - exactTotal.ToCredits();
        var agrees = drift == 0;
        log.Information(Category,
            "Exact {Exact} credits vs float {Float} credits — drift {Drift}",
            new LogProperty("Exact", exactTotal.ToCredits()),
            new LogProperty("Float", floatTotal),
            new LogProperty("Drift", drift));

        // Addition order changes a double sum; it never changes a long sum. Three tenths
        // is the smallest example that shows it: no value here is representable in binary,
        // so where the rounding lands depends on which pair is added first.
        var left = (0.1 + 0.2) + 0.3;
        var right = 0.1 + (0.2 + 0.3);
        if (left != right)
            log.Warning(Category,
                "Grouping changes a double sum: (a+b)+c = {Left}, a+(b+c) = {Right}",
                new LogProperty("Left", left),
                new LogProperty("Right", right));

        return Results.Ok(new MoneyResponse(
            View(wager), View(pay), View(exactTotal),
            floatTotal, drift, agrees, left, right, refusal));
    }

    private static AmountView View(Millicents amount) =>
        new(amount.Value, amount.ToCredits(), amount.ToString(), Bits((ulong)amount.Value));

    // ---- deterministic randomness -------------------------------------------------

    public sealed record RngRequest(ulong Seed, int WorkerCount, int Draws, int Bound, bool Mixed);

    /// <summary>
    /// Raw draws travel as hex strings on purpose: a 64-bit unsigned value does not survive
    /// a JSON number in the browser, where every number is a double with 53 bits of mantissa.
    /// </summary>
    public sealed record StreamView(int WorkerId, string[] State, string[] Raw, int[] Reduced);

    public sealed record RngResponse(StreamView[] Streams, int SharedPrefixBits, bool Reproduced);

    private static IResult Rng(RngRequest request, StructuredLogger log)
    {
        if (request.WorkerCount is < 1 or > 8 || request.Draws is < 1 or > 32 || request.Bound < 2)
            return Results.BadRequest(new { error = "WorkerCount 1-8, Draws 1-32, Bound >= 2." });

        var streams = new StreamView[request.WorkerCount];
        var firstDraws = new ulong[request.WorkerCount];
        for (var worker = 0; worker < request.WorkerCount; worker++)
        {
            var rng = request.Mixed
                ? SpinRng.ForWorker(request.Seed, worker)
                : SpinRng.ForWorkerUnmixed(request.Seed, worker);

            var state = rng.State;
            var raw = new string[request.Draws];
            var rawValues = new ulong[request.Draws];
            var reduced = new int[request.Draws];
            for (var i = 0; i < request.Draws; i++)
            {
                rawValues[i] = rng.NextUInt64();
                raw[i] = Hex(rawValues[i]);
                var replay = request.Mixed
                    ? SpinRng.ForWorker(request.Seed, worker)
                    : SpinRng.ForWorkerUnmixed(request.Seed, worker);
                for (var skip = 0; skip < i; skip++) replay.NextUInt64();
                reduced[i] = replay.NextInt(request.Bound);
            }

            firstDraws[worker] = rawValues[0];
            streams[worker] = new StreamView(
                worker,
                [Hex(state.S0), Hex(state.S1), Hex(state.S2), Hex(state.S3)],
                raw,
                reduced);
        }

        // How many leading bits worker 0 and worker 1 share on their first draw: the
        // cheapest visible measure of "these streams rhyme".
        var shared = request.WorkerCount > 1
            ? SharedLeadingBits(firstDraws[0], firstDraws[1])
            : 0;

        // Same seed, same worker, second pass, so the page can compare the two draws.
        var replayRng = request.Mixed
            ? SpinRng.ForWorker(request.Seed, 0)
            : SpinRng.ForWorkerUnmixed(request.Seed, 0);
        var reproduced = replayRng.NextUInt64() == firstDraws[0];

        log.Information(Category,
            "{Mode} seeding, {Workers} workers: workers 0/1 share {Shared} leading bits; replay match {Reproduced}",
            new LogProperty("Mode", request.Mixed ? "SplitMix64" : "seed+workerId"),
            new LogProperty("Workers", request.WorkerCount),
            new LogProperty("Shared", shared),
            new LogProperty("Reproduced", reproduced));

        if (!request.Mixed && shared >= 8)
            log.Warning(Category,
                "Correlated streams: {Shared} identical leading bits across workers",
                new LogProperty("Shared", shared));

        return Results.Ok(new RngResponse(streams, shared, reproduced));
    }

    // ---- modulo bias, made visible ------------------------------------------------

    public sealed record BiasRequest(ulong Seed, int Bound, int Bits, int Samples);

    public sealed record BiasResponse(
        int[] ModuloCounts,
        int[] LemireCounts,
        double ModuloWorstErrorPercent,
        double LemireWorstErrorPercent,
        int Space,
        double Expected,
        int Rejections);

    /// <summary>
    /// Modulo bias over 64 bits is real and invisible. Narrowing the draw to a handful of
    /// bits keeps the arithmetic identical and makes the skew big enough to see on a chart.
    /// </summary>
    private static IResult Bias(BiasRequest request, StructuredLogger log)
    {
        if (request.Bits is < 4 or > 16 || request.Bound < 2 || request.Samples is < 100 or > 2_000_000)
            return Results.BadRequest(new { error = "Bits 4-16, Bound >= 2, Samples 100-2,000,000." });

        var space = 1 << request.Bits;
        if (request.Bound > space)
            return Results.BadRequest(new { error = $"Bound must be <= {space} for a {request.Bits}-bit draw space." });

        var shift = 64 - request.Bits;
        var modulo = new int[request.Bound];
        var lemire = new int[request.Bound];
        var rejections = 0;

        var threshold = (ulong)(space % request.Bound);   // values below this cause the skew

        // Two passes from the same seed rather than one interleaved pass. Lemire discards
        // draws and modulo does not, so a shared loop would give the two histograms
        // different sample counts.
        var moduloRng = SpinRng.ForWorker(request.Seed, 0);
        for (var i = 0; i < request.Samples; i++)
        {
            var draw = moduloRng.NextUInt64() >> shift;
            modulo[(int)(draw % (ulong)request.Bound)]++;
        }

        var lemireRng = SpinRng.ForWorker(request.Seed, 0);
        for (var accepted = 0; accepted < request.Samples;)
        {
            // Lemire on the same narrow space: multiply, take the high half, reject the
            // short tail that would otherwise land unevenly.
            var draw = lemireRng.NextUInt64() >> shift;
            var product = draw * (ulong)request.Bound;
            var low = product & (ulong)(space - 1);
            if (low < threshold)
            {
                rejections++;
                continue;
            }
            lemire[(int)(product >> request.Bits)]++;
            accepted++;
        }

        var expected = (double)request.Samples / request.Bound;
        var moduloWorst = WorstErrorPercent(modulo, expected);
        var lemireWorst = WorstErrorPercent(lemire, expected);

        log.Information(Category,
            "Bias lab: {Space}-value space into {Bound} buckets — modulo worst error {ModuloError}%, Lemire worst {LemireError}%, {Rejections} rejections",
            new LogProperty("Space", space),
            new LogProperty("Bound", request.Bound),
            new LogProperty("ModuloError", Math.Round(moduloWorst, 2)),
            new LogProperty("LemireError", Math.Round(lemireWorst, 2)),
            new LogProperty("Rejections", rejections));

        return Results.Ok(new BiasResponse(
            modulo, lemire, moduloWorst, lemireWorst, space, expected, rejections));
    }

    private static double WorstErrorPercent(int[] counts, double expected)
    {
        var worst = 0.0;
        foreach (var count in counts)
            worst = Math.Max(worst, Math.Abs(count - expected) / expected * 100.0);
        return worst;
    }

    // ---- display helpers ----------------------------------------------------------

    private static string Bits(ulong value) => Convert.ToString((long)value, 2).PadLeft(64, '0');

    private static string Hex(ulong value) => value.ToString("X16");

    private static int SharedLeadingBits(ulong a, ulong b)
    {
        var diff = a ^ b;
        return diff == 0 ? 64 : System.Numerics.BitOperations.LeadingZeroCount(diff);
    }
}
