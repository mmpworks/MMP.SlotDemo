using MMP.SlotGame.Core.Features;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// RT-5 / RT-21 — features are independent RTP terms paying from their own award table.
///
/// The award table is {½m, m, 2m − ½m}. Its arithmetic mean is EXACTLY m in integer
/// millicents, which is the whole reason the realized contribution is p·m/wager with no
/// distribution rounding drift. That exactness is the first thing pinned here — if the
/// high award is ever computed as 1.5m instead of 2m − ½m, the mean drifts and every
/// feature RTP is silently wrong by a few basis points.
/// </summary>
[Trait("Category", "Fast")]
public sealed class FeatureScheduleTests
{
    private static readonly Millicents Wager = SimulationConfig.Wager;

    public static IEnumerable<object[]> KindsAndContributions() =>
        from kind in new[] { FeatureKind.FreeSpins, FeatureKind.PickBonus }
        from bp in new[] { 1, 100, 1000, 1300, 2500 }
        select new object[] { kind, bp };

    [Theory]
    [MemberData(nameof(KindsAndContributions))]
    public void AwardTableMean_IsExactlyAwardMid(FeatureKind kind, int contributionBp)
    {
        var f = FeatureSchedule.Create(kind, contributionBp, Wager);

        // Integer identity, not a tolerance: low + mid + high must be exactly 3·mid.
        Assert.Equal(3 * f.AwardMid.Value, f.AwardLow.Value + f.AwardMid.Value + f.AwardHigh.Value);
        Assert.True(f.AwardLow < f.AwardMid && f.AwardMid < f.AwardHigh,
            $"Award table is not ordered: {f.AwardLow} / {f.AwardMid} / {f.AwardHigh}");
    }

    [Theory]
    [MemberData(nameof(KindsAndContributions))]
    public void RealizedContribution_MatchesTheConfiguredBasisPoints(FeatureKind kind, int contributionBp)
    {
        var f = FeatureSchedule.Create(kind, contributionBp, Wager);

        var target = contributionBp / 10_000.0;
        var realized = f.RealizedContribution(Wager);

        // Budget: the only rounding is AwardMid to a whole millicent, i.e. ≤ 0.5 mc,
        // which is p·0.5/wager ≤ 5e-6 of a unit wager — two orders under AC-2 (RT-10).
        Assert.True(
            Math.Abs(realized - target) <= 1e-5,
            $"{kind} @ {contributionBp} bp: realized {realized:R} vs target {target:R}");
    }

    /// <summary>
    /// Hand-computed variance for the shipped default FreeSpins term.
    ///
    ///   p   = 1/120, wager = 100,000 mc, c = 1300 bp = 0.13
    ///   m   = 0.13 · 100,000 · 120           = 1,560,000 mc
    ///   low = m/2                            =   780,000 mc
    ///   high= 2m − low                       = 2,340,000 mc
    ///
    ///   E[X]   = p·m                                        = 13,000
    ///   E[X²]  = p·(low² + m² + high²)/3
    ///          = (1/120)·(6.084e11 + 2.4336e12 + 5.4756e12)/3
    ///          = (1/120)·2.8392e12                          = 23,660,000,000
    ///   Var    = E[X²] − E[X]²  = 23,660,000,000 − 169,000,000 = 23,491,000,000
    /// </summary>
    [Fact]
    public void Variance_MatchesTheHandComputedValue()
    {
        var f = FeatureSchedule.Create(FeatureKind.FreeSpins, 1300, Wager);

        Assert.Equal(1_560_000L, f.AwardMid.Value);
        Assert.Equal(780_000L, f.AwardLow.Value);
        Assert.Equal(2_340_000L, f.AwardHigh.Value);

        const double expected = 23_491_000_000d;
        var actual = f.VarianceMillicentsSquared();

        Assert.True(
            Math.Abs(actual - expected) / expected <= 1e-9,
            $"Feature variance {actual:R} vs hand-computed {expected:R}");
    }

    [Fact]
    public void PickBonusDefault_HasTheExpectedAwardTable()
    {
        // p = 1/150, c = 0.10 → m = 0.10 · 100,000 · 150 = 1,500,000 mc.
        var f = FeatureSchedule.Create(FeatureKind.PickBonus, 1000, Wager);

        Assert.Equal(1_500_000L, f.AwardMid.Value);
        Assert.Equal(750_000L, f.AwardLow.Value);
        Assert.Equal(2_250_000L, f.AwardHigh.Value);
        Assert.Equal(FeatureSchedule.PickBonusTriggerP, f.TriggerProbability);
    }

    /// <summary>
    /// RT-4b REGRESSION LOCK — a single fixed seed, not a statistical proof.
    ///
    /// N = 2,000,000 draws of the default FreeSpins term. Expected mean payout is
    /// 13,000 mc/spin; sigma is sqrt(23,491,000,000) ≈ 153,268 mc, so the standard
    /// error at this N is ≈ 108 mc and the assertion band below is ≈ 4.6 SE. The seed is
    /// a constant, so this verdict is a constant: if it ever fails, Play() changed, not
    /// the weather.
    /// </summary>
    [Fact]
    public void Play_EmpiricalMean_MatchesContribution_FixedSeedRegressionLock()
    {
        const long n = 2_000_000;
        var f = FeatureSchedule.Create(FeatureKind.FreeSpins, 1300, Wager);
        var rng = SpinRng.ForWorker(0xFEED_BEEF_CAFEUL, workerId: 3);

        long total = 0;
        long triggers = 0;
        for (long i = 0; i < n; i++)
        {
            var award = f.Play(ref rng);
            if (award.Value <= 0) continue;
            total += award.Value;
            triggers++;
        }

        var mean = (double)total / n;
        var expectedMean = f.RealizedContribution(Wager) * Wager.Value;
        Assert.True(
            Math.Abs(mean - expectedMean) <= 500,
            $"Empirical feature mean {mean:F2} mc/spin vs expected {expectedMean:F2} (band ±500, ≈4.6 SE).");

        var triggerRate = (double)triggers / n;
        Assert.True(
            Math.Abs(triggerRate - f.TriggerProbability) <= 5e-4,
            $"Empirical trigger rate {triggerRate:R} vs p = {f.TriggerProbability:R}");
    }

    [Fact]
    public void Play_OnlyEverReturnsZeroOrAnAwardTableValue()
    {
        var f = FeatureSchedule.Create(FeatureKind.PickBonus, 1000, Wager);
        var rng = SpinRng.ForWorker(7UL, workerId: 0);

        var seen = new HashSet<long>();
        for (var i = 0; i < 200_000; i++) seen.Add(f.Play(ref rng).Value);

        Assert.Equal(
            new HashSet<long> { 0, f.AwardLow.Value, f.AwardMid.Value, f.AwardHigh.Value },
            seen);
    }
}
