using MMP.SlotGame.Core.Features;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Rtp;

/// <summary>
/// The calculated return of the finished game: base-game RTP, each feature's RTP, total RTP,
/// and the per-spin standard deviation used by the confidence band.
/// </summary>
public sealed record RtpBreakdown(
    double BaseRtp,
    IReadOnlyList<(string Name, double Rtp)> Features,
    double TotalRtp,
    double SigmaPerUnitWagered);

public sealed class RtpCalculator
{
    /// <summary>
    /// Calculates the RTP and standard deviation from the rounded paytable and feature
    /// schedules that the game will use. It does not reuse the solver's requested RTP.
    /// </summary>
    public static RtpBreakdown Analyze(
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
