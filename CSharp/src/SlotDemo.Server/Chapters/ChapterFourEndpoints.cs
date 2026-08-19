using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;
using MMP.SlotGame.Core.Features;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Chapters;

/// <summary>
/// Episode 4's lab — paytable math. The solver is one closed-form scalar over the
/// canonical table, and the analytic twin prices the result without simulating a spin.
/// </summary>
public static class ChapterFourEndpoints
{
    private static readonly LogCategory Category = new("Chapter04");

    public static void MapChapterFour(this WebApplication app, StructuredLogger log)
    {
        app.MapPost("/api/ch4/solve", (SolveRequest request) => Solve(request, log));
        app.MapPost("/api/ch4/band", (BandRequest request) => Band(request, log));
        app.MapPost("/api/ch4/published", (PublishedRequest request) => Published(request, log));
    }

    /// <summary>
    /// A non-empty <c>GameFile</c> solves the shipped game instead of a preset:
    /// its published pays become the canonical ratios, its enumerated probabilities
    /// (wilds, groups, and tie-breaks included) price every row, and the target scales
    /// the line RTP. The bonus is left as shipped: a real cabinet ships several approved
    /// payback versions that vary the line table and share one bonus.
    /// </summary>
    public sealed record SolveRequest(
        string PresetName, int TargetBaseRtpBasisPoints, string GameFile = "");

    /// <summary>
    /// Solve a preset's canonical table to a target base RTP and show the whole path:
    /// canonical ratios, the single scale factor, the rounded integer paytable, and the
    /// realized RTP with its drift from the target. Each pay rounds independently, so
    /// the response reports the RTP recomputed from the rounded table.
    /// </summary>
    private static IResult Solve(SolveRequest request, StructuredLogger log)
    {
        if (!string.IsNullOrWhiteSpace(request.GameFile))
            return SolveGame(request, log);
        if (!StandardReelPresets.All.TryGetValue(request.PresetName ?? "", out var preset))
            return Results.BadRequest(new { error = $"Unknown preset '{request.PresetName}'." });
        if (request.TargetBaseRtpBasisPoints is < 100 or > 9_900)
            return Results.BadRequest(new { error = "Target base RTP 100-9900 basis points." });

        var reels = preset.BuildReels();
        var canonical = Paytable.CanonicalFor(preset.ReelCount, preset.Symbols.Count);
        var target = request.TargetBaseRtpBasisPoints / 10_000.0;

        var unscaledEv = AnalyticMath.BaseEvMultiplier(reels, preset.Paylines, canonical);
        var scaled = PaytableSolver.Solve(reels, preset.Paylines, canonical, target, SimulationConfig.Wager);
        var realized = AnalyticMath.RealizedBaseRtp(reels, preset.Paylines, scaled, SimulationConfig.Wager);

        var symbolNames = preset.Symbols.ToDictionary(symbol => symbol.Id, symbol => symbol.Name);
        var rows = canonical.Pays
            .OrderBy(p => p.Key.SymbolId).ThenBy(p => p.Key.Count)
            .Select(p =>
            {
                var scaledPay = scaled.PayFor(p.Key.SymbolId, p.Key.Count);
                // A row can pay on several paylines. Show one line's probability for
                // inspection, but add every line when reporting this row's RTP share.
                var lineProbabilities = preset.Paylines
                    .Select(line => AnalyticMath.ExactlyKLeading(
                        reels, line, p.Key.SymbolId, p.Key.Count))
                    .ToArray();
                return new
                {
                    symbolId = p.Key.SymbolId,
                    symbol = symbolNames[p.Key.SymbolId],
                    count = p.Key.Count,
                    canonical = p.Value,
                    probability = lineProbabilities[0],
                    scaledMillicents = scaledPay.Value,
                    scaledCredits = scaledPay.ToCredits(),
                    rtpContribution = lineProbabilities.Sum() * scaledPay.Value / SimulationConfig.Wager.Value,
                };
            });

        log.Information(Category,
            "Solve {Preset} to {Target} bp: unscaled EV {UnscaledEv}, realized {Realized}, drift {Drift} bp",
            new LogProperty("Preset", preset.Name),
            new LogProperty("Target", request.TargetBaseRtpBasisPoints),
            new LogProperty("UnscaledEv", unscaledEv),
            new LogProperty("Realized", realized),
            new LogProperty("Drift", (realized - target) * 10_000));

        return Results.Ok(new
        {
            preset = preset.Name,
            targetBaseRtp = target,
            unscaledEvMultiplier = unscaledEv,
            scaleFactor = target / unscaledEv,
            realizedBaseRtp = realized,
            driftBasisPoints = (realized - target) * 10_000,
            paytable = rows,
        });
    }

    private static IResult SolveGame(SolveRequest request, StructuredLogger log)
    {
        if (request.TargetBaseRtpBasisPoints is < 100 or > 9_900)
            return Results.BadRequest(new { error = "Target line RTP 100-9900 basis points." });

        var path = Path.Combine(AppContext.BaseDirectory, "games", Path.GetFileName(request.GameFile));
        if (!File.Exists(path))
            return Results.BadRequest(new { error = $"No shipped game named '{request.GameFile}'." });
        if (!MMP.SlotGame.Core.Games.Definition.GameDefinitionLoader.TryLoad(
                File.ReadAllText(path), out var definition, out var loadErrors))
            return Results.BadRequest(new { error = "Definition failed to load.", errors = loadErrors });

        var game = definition!;
        MMP.SlotGame.Core.Games.GameAnalysis analysis;
        try { analysis = MMP.SlotGame.Core.Games.GameAnalyzer.Analyze(game); }
        catch (NotSupportedException ex) { return Results.BadRequest(new { error = ex.Message }); }

        var target = request.TargetBaseRtpBasisPoints / 10_000.0;
        // Line RTP is linear in the pays, so one scalar re-prices the whole table; each
        // re-priced pay then rounds to a whole hundredth of the bet, and the realized
        // figure is recomputed from the enumerated probabilities.
        var scale = target / analysis.LineRtp;

        var rows = analysis.CombinationCounts
            .OrderBy(c => c.Key.CategoryIndex).ThenBy(c => c.Key.Count)
            .Select(c =>
            {
                var category = game.Categories[c.Key.CategoryIndex];
                var shippedPay = category.PayFor(c.Key.Count) / 100.0;
                var probability = (double)c.Value / analysis.StopCombinations;
                var scaledHundredths = (long)Math.Round(
                    category.PayFor(c.Key.Count) * scale, MidpointRounding.ToEven);
                return new
                {
                    symbolId = c.Key.CategoryIndex,
                    symbol = category.Name,
                    count = c.Key.Count,
                    canonical = shippedPay,
                    probability,
                    scaledMillicents = scaledHundredths * 1_000,   // hundredth of a 100,000 mc bet
                    scaledCredits = scaledHundredths / 100.0,
                    rtpContribution = scaledHundredths / 100.0 * probability,
                };
            })
            .ToArray();

        var realized = rows.Sum(r => r.scaledCredits * r.probability);

        log.Information(Category,
            "Solve {Game} line table to {Target} bp: shipped {Shipped}, realized {Realized}, drift {Drift} bp",
            new LogProperty("Game", game.Name),
            new LogProperty("Target", request.TargetBaseRtpBasisPoints),
            new LogProperty("Shipped", analysis.LineRtp),
            new LogProperty("Realized", realized),
            new LogProperty("Drift", (realized - target) * 10_000));

        return Results.Ok(new
        {
            preset = game.Name,
            isGame = true,
            shippedLineRtp = analysis.LineRtp,
            bonusRtp = analysis.BonusRtp,
            targetBaseRtp = target,
            unscaledEvMultiplier = analysis.LineRtp,
            scaleFactor = scale,
            realizedBaseRtp = realized,
            driftBasisPoints = (realized - target) * 10_000,
            paytable = rows,
        });
    }

    public sealed record BandRequest(
        string PresetName,
        int BaseRtpBasisPoints,
        int FreeSpinsRtpBasisPoints,
        int PickBonusRtpBasisPoints);

    /// <summary>
    /// The confidence band calculated from the strips: sigma per unit wagered and the
    /// half-width at a ladder of spin counts. The finale page plots this table.
    /// </summary>
    private static IResult Band(BandRequest request, StructuredLogger log)
    {
        var draft = new ConfigDraft(
            request.PresetName,
            request.BaseRtpBasisPoints,
            request.FreeSpinsRtpBasisPoints,
            request.PickBonusRtpBasisPoints,
            MasterSeed: 1, WorkerCount: 1, TargetSpins: 1);

        if (!SimulationConfig.TryCreate(draft, out var config, out var errors))
            return Results.BadRequest(new { error = string.Join(" ", errors), errors });

        var valid = config!;
        var reels = valid.Preset.BuildReels();
        var canonical = Paytable.CanonicalFor(valid.Preset.ReelCount, valid.Preset.Symbols.Count);
        var scaled = PaytableSolver.Solve(
            reels, valid.Preset.Paylines, canonical,
            valid.BaseRtpBasisPoints / 10_000.0, SimulationConfig.Wager);
        var breakdown = RtpCalculator.Analyze(reels, valid.Preset.Paylines, scaled, valid.Features, SimulationConfig.Wager);

        long[] ladder = [10_000, 100_000, 1_000_000, 10_000_000, 100_000_000];
        var bands = ladder.Select(n => new
        {
            spins = n,
            halfWidth99 = NormalQuantile.TwoSided99 * breakdown.SigmaPerUnitWagered / Math.Sqrt(n),
            halfWidth999 = NormalQuantile.TwoSided999 * breakdown.SigmaPerUnitWagered / Math.Sqrt(n),
        });

        log.Information(Category,
            "Band for {Preset}: total RTP {TotalRtp}, sigma {Sigma}",
            new LogProperty("Preset", valid.Preset.Name),
            new LogProperty("TotalRtp", breakdown.TotalRtp),
            new LogProperty("Sigma", breakdown.SigmaPerUnitWagered));

        return Results.Ok(new
        {
            preset = valid.Preset.Name,
            analytic = new
            {
                baseRtp = breakdown.BaseRtp,
                features = breakdown.Features.Select(f => new { name = f.Name, rtp = f.Rtp }),
                totalRtp = breakdown.TotalRtp,
                sigma = breakdown.SigmaPerUnitWagered,
            },
            bands,
        });
    }

    public sealed record PublishedRequest(string GameFile);

    /// <summary>
    /// The other way a paytable comes to exist: published and fixed, the way Orca Dive
    /// ships. No solver runs — the enumeration prices each category exactly, and the sum
    /// of pay × probability per row is the same arithmetic the solver lab teaches, read
    /// in the other direction.
    /// </summary>
    private static IResult Published(PublishedRequest request, StructuredLogger log)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "games", Path.GetFileName(request.GameFile ?? ""));
        if (!File.Exists(path))
            return Results.BadRequest(new { error = $"No shipped game named '{request.GameFile}'." });
        if (!MMP.SlotGame.Core.Games.Definition.GameDefinitionLoader.TryLoad(
                File.ReadAllText(path), out var definition, out var loadErrors))
            return Results.BadRequest(new { error = "Definition failed to load.", errors = loadErrors });

        var game = definition!;
        MMP.SlotGame.Core.Games.GameAnalysis analysis;
        try
        {
            analysis = MMP.SlotGame.Core.Games.GameAnalyzer.Analyze(game);
        }
        catch (NotSupportedException ex)
        {
            return Results.Ok(new { supported = false, reason = ex.Message });
        }

        var rows = analysis.CombinationCounts
            .OrderByDescending(c =>
                game.Categories[c.Key.CategoryIndex].PayFor(c.Key.Count) / 100.0
                * c.Value / analysis.StopCombinations)
            .Select(c =>
            {
                var category = game.Categories[c.Key.CategoryIndex];
                var payMultiplier = category.PayFor(c.Key.Count) / 100.0;
                var probability = (double)c.Value / analysis.StopCombinations;
                return new
                {
                    category = category.Name,
                    count = c.Key.Count,
                    payMultiplier,
                    combinations = c.Value,
                    probability,
                    // pay × probability: this row's slice of the line RTP.
                    rtpContribution = payMultiplier * probability,
                };
            });

        log.Information(Category,
            "Published paytable priced for {Game}: line {LineRtp}, bonus {BonusRtp}, total {TotalRtp}",
            new LogProperty("Game", game.Name),
            new LogProperty("LineRtp", analysis.LineRtp),
            new LogProperty("BonusRtp", analysis.BonusRtp),
            new LogProperty("TotalRtp", analysis.TotalRtp));

        return Results.Ok(new
        {
            supported = true,
            game = game.Name,
            stopCombinations = analysis.StopCombinations,
            lineRtp = analysis.LineRtp,
            bonusRtp = analysis.BonusRtp,
            totalRtp = analysis.TotalRtp,
            sigma = analysis.SigmaPerUnitWagered,
            rows,
        });
    }
}
