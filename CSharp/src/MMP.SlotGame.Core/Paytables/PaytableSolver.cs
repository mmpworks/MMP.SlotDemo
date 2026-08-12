using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Rtp;

namespace MMP.SlotGame.Core.Paytables;

/// <summary>
/// Finds the scalar paytableScaleFactor = targetBaseRtp / unscaledBaseGameEv and applies
/// it once, at paytable construction, producing integer millicents. Rounding is half-even,
/// which removes the low bias uniform truncation would introduce.
///
/// Each pay rounds independently, so the realized total can drift a hair from
/// targetBaseRtp. Read <see cref="AnalyticMath.RealizedBaseRtp"/>, recomputed from the
/// rounded table, for the number the game pays.
/// </summary>
public sealed class PaytableSolver
{
    /// <summary>
    /// <paramref name="targetBaseRtp"/> is a fraction (e.g. 0.75) derived from integer
    /// basis points upstream. <paramref name="wager"/> is the total spin bet: every line's
    /// award is scaled against that same total, and a spin's payout is the sum across all
    /// paylines. RTP throughout this pipeline therefore means return relative to the total
    /// amount wagered per spin, not relative to one line's share of it.
    /// </summary>
    public static ScaledPaytable Solve(StripReelSet reels, IReadOnlyList<Payline> lines, Paytable canonical, double targetBaseRtp, Millicents wager)
    {
        var unscaledBaseGameEv = AnalyticMath.BaseEvMultiplier(reels, lines, canonical);
        if (unscaledBaseGameEv <= 0)
            throw new InvalidOperationException("Canonical paytable has zero EV; cannot scale.");

        var paytableScaleFactor = targetBaseRtp / unscaledBaseGameEv;
        PayoutScaler scale = raw => new Millicents(
            (long)Math.Round(raw * paytableScaleFactor * wager.Value, MidpointRounding.ToEven));

        var scaled = canonical.Pays.ToDictionary(kv => kv.Key, kv => scale(kv.Value));
        return new ScaledPaytable(scaled);
    }
}
