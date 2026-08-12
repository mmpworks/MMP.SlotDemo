using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Core.Features;

public enum FeatureKind
{
    FreeSpins,
    PickBonus,
}

/// <summary>
/// A side game as an independent RTP term: the preset kind fixes trigger probability p;
/// the target contribution c derives the mean award m = c · wager / p. The feature pays
/// from its own 3-point award table. It never re-runs the base game and never retriggers.
///
/// The kind is a skin: FreeSpins presents its award as a spin session, PickBonus as picks.
/// Both settle the same money contract, so one class covers both.
///
/// Award table {½m, m, 2m − ½m}: the mean is the middle value in integer
/// millicents, so the realized contribution is p·M/wager with no distribution
/// rounding drift beyond M itself.
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
    /// <summary>Preset trigger probabilities used by the stock games.</summary>
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
        var high = new Millicents(2 * mid.Value - low.Value); // keeps the three-point mean at mid

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
        var mean = p * mid;                                  // table mean is mid, so E[award] = p·mid
        var meanSq = p * (low * low + mid * mid + high * high) / 3.0;
        return meanSq - mean * mean;
    }

    /// <summary>
    /// Play the feature for one base spin: Bernoulli(p) trigger, then one uniform pick
    /// from the 3-point table. This method advances the caller's RNG stream.
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
