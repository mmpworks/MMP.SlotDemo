using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Rtp;

namespace MMP.SlotGame.Core.Paytables;

/// <summary>
/// Finds the one scalar paytableScaleFactor = targetBaseRtp / unscaledBaseGameEv and
/// applies it ONCE, at paytable construction, producing integer millicents
/// (round-half-even — uniform truncation would bias RTP low systematically, RT-10).
/// Round-half-even removes that systematic bias; it does NOT guarantee the rounded
/// table lands exactly on targetBaseRtp — individual pays round up or down independently,
/// so the realized total can drift a hair from the target. <see cref="AnalyticMath.RealizedBaseRtp"/>,
/// recomputed from THIS rounded table, is the authoritative number; targetBaseRtp is only
/// ever a target.
/// </summary>
public sealed class PaytableSolver
{
    /// <summary>
    /// <paramref name="targetBaseRtp"/> is a fraction (e.g. 0.75) derived from integer
    /// basis points upstream. <paramref name="wager"/> is the total spin bet: every line's
    /// award is scaled against this SAME total, and a spin's payout is the sum across all
    /// paylines, so RTP throughout this pipeline means "return relative to the total amount
    /// wagered per spin," not "relative to a single line's share of it."
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
