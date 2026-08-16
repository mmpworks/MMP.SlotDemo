using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// AC-3 / RT-12 — the aggregate RTP cap and the rest of the validation boundary.
///
/// The cap is an INTEGER comparison on basis points, so 9900 is exactly acceptable and
/// 9901 is exactly rejected; there is no floating-point boundary to argue about. A
/// rejected config must be rejected loudly and never silently clamped, and every
/// rejection must carry a message the SPA can show a human.
/// </summary>
[Trait("Category", "Fast")]
public sealed class ConfigValidationTests
{
    private static ConfigDraft Draft(
        int baseBp = 7500,
        int freeSpinsBp = 1300,
        int pickBonusBp = 1000,
        string preset = TestGame.DefaultPreset,
        int workers = 4,
        long spins = 1_000) =>
        new(preset, baseBp, freeSpinsBp, pickBonusBp, 42UL, workers, spins);

    private static (bool Ok, SimulationConfig? Config, IReadOnlyList<string> Errors) Try(ConfigDraft draft)
    {
        var ok = SimulationConfig.TryCreate(draft, out var config, out var errors);
        return (ok, config, errors);
    }

    // ---- the cap (AC-3) -----------------------------------------------------

    [Fact]
    public void Aggregate_9900_IsAccepted_BoundaryIsInclusive()
    {
        var (ok, config, errors) = Try(Draft(baseBp: 7600, freeSpinsBp: 1300, pickBonusBp: 1000));

        Assert.True(ok, $"9900 bp must be accepted; got: {string.Join(" | ", errors)}");
        Assert.NotNull(config);
        Assert.Equal(9_900, config!.AggregateBasisPoints);
        Assert.Equal(SimulationConfig.MaxAggregateBasisPoints, config.AggregateBasisPoints);
    }

    [Fact]
    public void Aggregate_9901_IsRejected_AndNeverClamped()
    {
        var draft = Draft(baseBp: 7601, freeSpinsBp: 1300, pickBonusBp: 1000);
        var (ok, config, errors) = Try(draft);

        Assert.False(ok);
        Assert.Null(config);
        Assert.Contains(errors, e => e.Contains("9900") && e.Contains("ceiling", StringComparison.OrdinalIgnoreCase));

        // No silent clamping: the caller's draft is untouched (PRD explicit).
        Assert.Equal(7601, draft.BaseRtpBasisPoints);
        Assert.Equal(1300, draft.FreeSpinsRtpBasisPoints);
        Assert.Equal(1000, draft.PickBonusRtpBasisPoints);
    }

    [Fact]
    public void Aggregate_9899_IsAccepted()
    {
        var (ok, _, errors) = Try(Draft(baseBp: 7599, freeSpinsBp: 1300, pickBonusBp: 1000));
        Assert.True(ok, string.Join(" | ", errors));
    }

    // ---- the floor of the solver's RTP limits --------------------------------

    [Fact]
    public void Aggregate_7500_IsAccepted_FloorIsInclusive()
    {
        var (ok, config, errors) = Try(Draft(baseBp: 7500, freeSpinsBp: 0, pickBonusBp: 0));

        Assert.True(ok, $"7500 bp must be accepted; got: {string.Join(" | ", errors)}");
        Assert.Equal(SimulationConfig.MinAggregateBasisPoints, config!.AggregateBasisPoints);
    }

    [Fact]
    public void Aggregate_7499_IsRejected_AndNeverRaised()
    {
        var draft = Draft(baseBp: 7499, freeSpinsBp: 0, pickBonusBp: 0);
        var (ok, config, errors) = Try(draft);

        Assert.False(ok);
        Assert.Null(config);
        Assert.Contains(errors, e => e.Contains("7500") && e.Contains("floor", StringComparison.OrdinalIgnoreCase));

        // No silent raising: the caller's draft is untouched.
        Assert.Equal(7499, draft.BaseRtpBasisPoints);
    }

    // ---- negatives and term bounds ------------------------------------------

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(-500, -500)]
    public void NegativeFeatureTerms_AreRejected(int freeSpinsBp, int pickBonusBp)
    {
        var (ok, config, errors) = Try(Draft(baseBp: 7500, freeSpinsBp: freeSpinsBp, pickBonusBp: pickBonusBp));

        Assert.False(ok);
        Assert.Null(config);
        Assert.Contains(errors, e => e.Contains("negative", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9_901)]
    public void BaseRtpOutsideOneToCap_IsRejected(int baseBp)
    {
        var (ok, config, errors) = Try(Draft(baseBp: baseBp, freeSpinsBp: 0, pickBonusBp: 0));

        Assert.False(ok);
        Assert.Null(config);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ZeroBpFeatures_ProduceNoFeatureSchedules()
    {
        var (ok, config, _) = Try(Draft(baseBp: 7500, freeSpinsBp: 0, pickBonusBp: 0));

        Assert.True(ok);
        Assert.Empty(config!.Features);
        Assert.Equal(7_500, config.AggregateBasisPoints);
    }

    // ---- preset -------------------------------------------------------------

    [Theory]
    [InlineData("NotAPreset")]
    [InlineData("classic3")] // case-sensitive lookup: near-misses must not silently resolve
    [InlineData("")]
    public void UnknownPreset_IsRejected_WithTheValidListInTheMessage(string preset)
    {
        var (ok, config, errors) = Try(Draft(preset: preset));

        Assert.False(ok);
        Assert.Null(config);
        var message = Assert.Single(errors, e => e.Contains("preset", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Classic3", message);
        Assert.Contains(TestGame.DefaultPreset, message);
    }

    [Theory]
    [MemberData(nameof(TestGame.AllPresetNames), MemberType = typeof(TestGame))]
    public void EveryShippedPreset_IsAccepted(string preset)
    {
        var (ok, config, errors) = Try(Draft(preset: preset));

        Assert.True(ok, string.Join(" | ", errors));
        Assert.Equal(preset, config!.Preset.Name);
    }

    // ---- worker and spin bounds ---------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(64)]
    public void WorkerCountInsideBounds_IsAccepted(int workers)
    {
        var (ok, _, errors) = Try(Draft(workers: workers));
        Assert.True(ok, string.Join(" | ", errors));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65)]
    public void WorkerCountOutsideBounds_IsRejected(int workers)
    {
        var (ok, config, errors) = Try(Draft(workers: workers));

        Assert.False(ok);
        Assert.Null(config);
        Assert.Contains(errors, e => e.Contains("WorkerCount", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TargetSpinsBelowOne_IsRejected(long spins)
    {
        var (ok, config, errors) = Try(Draft(spins: spins));

        Assert.False(ok);
        Assert.Null(config);
        Assert.Contains(errors, e => e.Contains("TargetSpins", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TargetSpinsOfOne_IsAccepted()
    {
        var (ok, _, errors) = Try(Draft(spins: 1));
        Assert.True(ok, string.Join(" | ", errors));
    }

    // ---- error reporting shape ----------------------------------------------

    [Fact]
    public void MultipleProblems_AreAllReported_NotJustTheFirst()
    {
        var (ok, config, errors) = Try(Draft(preset: "Nope", baseBp: 9_901, workers: 0, spins: 0));

        Assert.False(ok);
        Assert.Null(config);
        Assert.True(errors.Count >= 4, $"Expected every problem reported; got {errors.Count}: {string.Join(" | ", errors)}");
        Assert.All(errors, e => Assert.False(string.IsNullOrWhiteSpace(e)));
    }

    [Fact]
    public void AcceptedConfig_ReportsNoErrors()
    {
        var (ok, _, errors) = Try(Draft());

        Assert.True(ok);
        Assert.Empty(errors);
    }
}
