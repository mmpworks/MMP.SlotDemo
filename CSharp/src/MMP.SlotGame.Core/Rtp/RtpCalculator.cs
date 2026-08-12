using MMP.SlotGame.Core.Features;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Rtp;

/// <summary>
/// The analytic verdict for a realized game: what the integer paytable and feature
/// schedules actually pay (not what was requested), plus the analytic per-spin σ the
/// confidence band is computed from.
/// </summary>
public sealed record RtpBreakdown(
    double BaseRtp,
    IReadOnlyList<(string Name, double Rtp)> Features,
    double TotalRtp,
    double SigmaPerUnitWagered);

public sealed class RtpCalculator
{
    /// <summary>
    /// Analyse the realized game: recomputes RTP from the integer millicent pays via the
    /// enumeration path, so the solver's own scalar is never carried forward.
    /// </summary>
    public static RtpBreakdown Analyse(
        StripReelSet reels,
        IReadOnlyList<Payline> lines,
        ScaledPaytable scaled,
        IReadOnlyList<FeatureSchedule> features,
        Millicents wager)
    {
        var baseRtp = AnalyticMath.RealizedBaseRtp(reels, lines, scaled, wager);
        var featureRtps = features
            .Select(f => (f.Name, Rtp: f.RealizedContribution(wager)))
            .ToList();
        var total = baseRtp + featureRtps.Sum(f => f.Rtp);
        var sigma = AnalyticMath.SigmaPerUnitWagered(reels, lines, scaled, features, wager);
        return new RtpBreakdown(baseRtp, featureRtps, total, sigma);
    }
}
