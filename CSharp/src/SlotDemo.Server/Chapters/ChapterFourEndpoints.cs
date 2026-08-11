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
    }

    public sealed record SolveRequest(string PresetName, int TargetBaseRtpBasisPoints);

    /// <summary>
    /// Solve a preset's canonical table to a target base RTP and show the whole path:
    /// canonical ratios, the single scale factor, the rounded integer paytable, and the
    /// realized RTP with its drift from the target. The drift is the lesson - rounding
    /// each pay independently means the target is a target, and only the analytic
    /// recomputation is authoritative.
    /// </summary>
    private static IResult Solve(SolveRequest request, StructuredLogger log)
    {
        if (!ReelPreset.All.TryGetValue(request.PresetName ?? "", out var preset))
            return Results.BadRequest(new { error = $"Unknown preset '{request.PresetName}'." });
        if (request.TargetBaseRtpBasisPoints is < 100 or > 9_900)
            return Results.BadRequest(new { error = "Target base RTP 100-9900 basis points." });

        var reels = preset.BuildReels();
        var canonical = Paytable.CanonicalFor(preset.ReelCount, preset.SymbolWeights.Count);
        var target = request.TargetBaseRtpBasisPoints / 10_000.0;

        var unscaledEv = AnalyticMath.BaseEvMultiplier(reels, preset.Paylines, canonical);
        var scaled = PaytableSolver.Solve(reels, preset.Paylines, canonical, target, SimulationConfig.Wager);
        var realized = AnalyticMath.RealizedBaseRtp(reels, preset.Paylines, scaled, SimulationConfig.Wager);

        var symbolNames = preset.SymbolWeights.ToDictionary(sw => sw.Symbol.Id, sw => sw.Symbol.Name);
        var rows = canonical.Pays
            .OrderBy(p => p.Key.SymbolId).ThenBy(p => p.Key.Count)
            .Select(p => new
            {
                symbolId = p.Key.SymbolId,
                symbol = symbolNames[p.Key.SymbolId],
                count = p.Key.Count,
                canonical = p.Value,
                // The probability that prices this row: exactly-k leading on the first line.
                probability = AnalyticMath.ExactlyKLeading(reels, preset.Paylines[0], p.Key.SymbolId, p.Key.Count),
                scaledMillicents = scaled.PayFor(p.Key.SymbolId, p.Key.Count).Value,
                scaledCredits = scaled.PayFor(p.Key.SymbolId, p.Key.Count).ToCredits(),
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

    public sealed record BandRequest(
        string PresetName,
        int BaseRtpBasisPoints,
        int FreeSpinsRtpBasisPoints,
        int PickBonusRtpBasisPoints);

    /// <summary>
    /// The confidence band, priced from the strips alone: sigma per unit wagered and the
    /// half-width at a ladder of spin counts. The funnel the finale page draws live is
    /// this table - z times sigma over root N, nothing more.
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
        var canonical = Paytable.CanonicalFor(valid.Preset.ReelCount, valid.Preset.SymbolWeights.Count);
        var scaled = PaytableSolver.Solve(
            reels, valid.Preset.Paylines, canonical,
            valid.BaseRtpBasisPoints / 10_000.0, SimulationConfig.Wager);
        var breakdown = RtpCalculator.Analyse(reels, valid.Preset.Paylines, scaled, valid.Features, SimulationConfig.Wager);

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
}
