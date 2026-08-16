using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// AC-2 / RT-10 / RT-11 — the solver hits the configured target, and the number it
/// claims is the number the ROUNDED integer paytable actually pays.
///
/// The failure mode this suite exists for: a solver that reports its target back as its
/// analytic RTP. That solver looks perfect and is lying. Every assertion here recomputes
/// RTP from the realized ScaledPaytable via RtpCalculator.Analyze and compares THAT
/// against the target.
///
/// Budgets (RT-10): realized-vs-target ≤ 0.005 pp per preset (half of AC-2's 0.01 pp).
/// </summary>
[Trait("Category", "Fast")]
public sealed class SolverTests
{
    /// <summary>0.01 percentage points, expressed as a fraction of 1.</summary>
    private const double Ac2Budget = 0.0001;

    /// <summary>0.005 percentage points — the tighter per-preset rounding residual (RT-10).</summary>
    private const double ResidualBudget = 0.00005;

    [Theory]
    [MemberData(nameof(TestGame.AllPresetNames), MemberType = typeof(TestGame))]
    public void DefaultConfig_RealizedRtp_IsWithinBudgetOf98Percent(string preset)
    {
        var game = TestGame.Build(preset);
        var breakdown = game.Analyze();

        AssertRealized(preset, game, breakdown, target: 0.98);
    }

    [Theory]
    [MemberData(nameof(TestGame.AllPresetNames), MemberType = typeof(TestGame))]
    public void AtTheCap_RealizedRtp_IsWithinBudget_AndStaysUnder99Percent(string preset)
    {
        // 7600 + 1300 + 1000 = 9900 bp exactly.
        var game = TestGame.Build(preset, baseBp: 7600, freeSpinsBp: 1300, pickBonusBp: 1000);
        var breakdown = game.Analyze();

        AssertRealized(preset, game, breakdown, target: 0.99);

        // RT-11: the post-solve re-validation must still see a game at or under the cap.
        Assert.True(
            breakdown.TotalRtp <= 0.99 + ResidualBudget,
            $"[{preset}] realized RTP {breakdown.TotalRtp:R} breached the 99.00% cap after rounding.");
    }

    [Theory]
    [MemberData(nameof(TestGame.AllPresetNames), MemberType = typeof(TestGame))]
    public void LowConfigWithNoFeatures_RealizedRtp_IsWithinBudget(string preset)
    {
        // 7500 bp is the lowest target the solver's RTP limits admit (the floor).
        var game = TestGame.Build(preset, baseBp: 7500, freeSpinsBp: 0, pickBonusBp: 0);
        var breakdown = game.Analyze();

        Assert.Empty(breakdown.Features);
        AssertRealized(preset, game, breakdown, target: 0.75);
    }

    [Theory]
    [MemberData(nameof(TestGame.AllPresetNames), MemberType = typeof(TestGame))]
    public void Breakdown_TermsSumToTotal_AndFeaturesMatchTheirConfiguredShare(string preset)
    {
        var game = TestGame.Build(preset);
        var breakdown = game.Analyze();

        var sum = breakdown.BaseRtp + breakdown.Features.Sum(f => f.Rtp);
        Assert.Equal(breakdown.TotalRtp, sum, 12);

        Assert.Equal(2, breakdown.Features.Count);
        foreach (var (name, rtp) in breakdown.Features)
        {
            var schedule = game.Config.Features.Single(f => f.Name == name);
            var target = schedule.ContributionBasisPoints / 10_000.0;
            Assert.True(
                Math.Abs(rtp - target) <= ResidualBudget,
                $"[{preset}] feature '{name}' realized {rtp:R} vs target {target:R}.");
        }
    }

    /// <summary>
    /// The solver's job is one scalar applied once. Raising the base target by ×1.25 must
    /// very nearly raise the realized base RTP by ×1.25 — a solver that quietly
    /// renormalizes, clamps, or re-solves per symbol fails this. (The pair 7600/9500 bp
    /// is an exact ×1.25 that fits inside the solver's RTP limits, floor 7500 bp.)
    ///
    /// "Very nearly", not exactly: each realized RTP carries its own half-millicent
    /// rounding residual (≤ 5e-5 absolute, RT-10), so the ratio can drift by a few parts
    /// in 10,000. The budget below is that rounding budget, not a fudge factor — a solver
    /// that scaled non-linearly would miss by percent, not by 1e-4.
    /// </summary>
    [Fact]
    public void BaseRtp_ScalesLinearlyWithTheBaseTarget()
    {
        var low = TestGame.Build("Video5x64", baseBp: 7600, freeSpinsBp: 0, pickBonusBp: 0).Analyze();
        var high = TestGame.Build("Video5x64", baseBp: 9500, freeSpinsBp: 0, pickBonusBp: 0).Analyze();

        var ratio = high.BaseRtp / low.BaseRtp;
        Assert.True(
            Math.Abs(ratio - 1.25) <= 1e-3,
            $"Scaling the base target ×1.25 changed realized base RTP by ×{ratio:R}, not ×1.25 " +
            $"(low {low.BaseRtp:R}, high {high.BaseRtp:R}).");
    }

    [Fact]
    public void ZeroEvCanonicalPaytable_ThrowsInsteadOfDividingByZero()
    {
        var preset = StandardReelPresets.All["Classic3"];
        var reels = preset.BuildReels();
        var empty = new Paytable(new Dictionary<(byte, int), double>());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PaytableSolver.Solve(reels, preset.Paylines, empty, 0.75, SimulationConfig.Wager));

        Assert.Contains("zero EV", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Solve_IsDeterministic_SamePresetSameTargetSamePays()
    {
        var a = TestGame.Build("Video5x128");
        var b = TestGame.Build("Video5x128");

        Assert.Equal(a.Paytable.Pays.Count, b.Paytable.Pays.Count);
        foreach (var (key, value) in a.Paytable.Pays)
            Assert.Equal(value, b.Paytable.Pays[key]);
    }

    [Theory]
    [MemberData(nameof(TestGame.AllPresetNames), MemberType = typeof(TestGame))]
    public void EveryScaledPay_IsAWholePositiveMillicentCount(string preset)
    {
        var game = TestGame.Build(preset);

        Assert.NotEmpty(game.Paytable.Pays);
        Assert.All(game.Paytable.Pays.Values, pay =>
            Assert.True(pay.Value > 0, $"[{preset}] a scaled pay rounded to {pay.Value} — a dead paytable entry."));
    }

    private static void AssertRealized(string preset, PresetGame game, RtpBreakdown breakdown, double target)
    {
        var delta = Math.Abs(breakdown.TotalRtp - target);

        Assert.True(
            delta <= Ac2Budget,
            $"""
             [{preset}] AC-2: realized RTP is outside the 0.01 pp budget.
               target     = {target:R}
               realized   = {breakdown.TotalRtp:R}   (base {breakdown.BaseRtp:R})
               delta      = {delta:R} ({delta * 100:F6} pp)
             """);

        Assert.True(
            delta <= ResidualBudget,
            $"""
             [{preset}] RT-10: rounding residual exceeds the 0.005 pp per-preset budget.
               target   = {target:R}
               realized = {breakdown.TotalRtp:R}
               residual = {delta * 100:F6} pp
             """);

        Assert.True(
            breakdown.SigmaPerUnitWagered > 0,
            $"[{preset}] analytic sigma is {breakdown.SigmaPerUnitWagered} — the AC-1 band would be vacuous.");

        Assert.Equal(target, game.Config.TargetTotalRtp, 12);
    }
}
