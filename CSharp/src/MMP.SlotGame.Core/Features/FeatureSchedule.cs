using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Core.Features;

public enum FeatureKind
{
    FreeSpins,
    PickBonus,
}

/// <summary>
/// A side game as an independent RTP term (RT-5 resolution, binding): the preset kind
/// fixes trigger probability p; the target contribution c derives the mean award
/// m = c · wager / p. The feature pays from its OWN 3-point award table — it never
/// re-runs the base game and never retriggers (PRD NG-2). The kind is a *skin*
/// (FreeSpins presents its award as a spin session; PickBonus as picks) — the money
/// contract is identical, which is why there is ONE class, not an interface with two
/// near-identical implementations (DRY; merging their internals would need no flag
/// because there is no divergent internal to merge).
///
/// Award table {½m, m, 2m − ½m}: the mean is the middle value in integer
/// millicents, so the realized contribution is p·M/wager with no distribution
/// rounding drift beyond M itself (RT-10 discipline).
/// </summary>
public sealed record FeatureSchedule(
    FeatureKind Kind,
    string Name,
    double TriggerProbability,
    int ContributionBasisPoints,
    Millicents AwardLow,
    Millicents AwardMid,
    Millicents AwardHigh)
{
    /// <summary>Preset trigger probabilities per kind (popular shapes: rare-ish, chunky awards).</summary>
    public const double FreeSpinsTriggerP = 1.0 / 120;
    public const double PickBonusTriggerP = 1.0 / 150;

    public static FeatureSchedule Create(FeatureKind kind, int contributionBp, Millicents wager)
    {
        var p = kind == FeatureKind.FreeSpins ? FreeSpinsTriggerP : PickBonusTriggerP;
        var name = kind == FeatureKind.FreeSpins ? "FreeSpins" : "PickBonus";

        // m = c · wager / p, rounded half-even to integer millicents.
        var mid = new Millicents((long)Math.Round(
            contributionBp / 10_000.0 * wager.Value / p, MidpointRounding.ToEven));
        var low = new Millicents((long)Math.Round(mid.Value * 0.5, MidpointRounding.ToEven));
        var high = new Millicents(2 * mid.Value - low.Value); // keeps the mean exactly mid

        return new FeatureSchedule(kind, name, p, contributionBp, low, mid, high);
    }

    /// <summary>Realized analytic contribution of this feature, per unit wagered.</summary>
    public double RealizedContribution(Millicents wager) =>
        TriggerProbability * AwardMid.Value / wager.Value;

    /// <summary>Exact per-spin variance of this feature's payout, in millicents².</summary>
    public double VarianceMillicentsSquared()
    {
        double low = AwardLow.Value, mid = AwardMid.Value, high = AwardHigh.Value;
        var p = TriggerProbability;
        var mean = p * mid;                                  // table mean is exactly mid
        var meanSq = p * (low * low + mid * mid + high * high) / 3.0;
        return meanSq - mean * mean;
    }

    /// <summary>
    /// Play the feature for one base spin: Bernoulli(p) trigger, then one uniform pick
    /// from the 3-point table. RNG only ever arrives by ref (invariant R3).
    /// </summary>
    public Millicents Play(ref SpinRng rng)
    {
        if (rng.NextDouble() >= TriggerProbability)
            return Millicents.Zero;
        return rng.NextInt(3) switch
        {
            0 => AwardLow,
            1 => AwardMid,
            _ => AwardHigh,
        };
    }
}
