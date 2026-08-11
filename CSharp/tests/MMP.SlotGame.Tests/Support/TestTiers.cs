namespace MMP.SlotGame.Tests.Support;

/// <summary>
/// Tier gating. Fast runs on every commit; Slow and Stress run only when
/// SLOTGAME_SLOW_TESTS=1 is set, because they burn 100M+ spins.
///
/// The gate is a SKIP, never a weakened assertion (test-plan §6 honesty rule):
/// a gated test that runs asserts exactly what it would assert in CI.
/// </summary>
public static class TestTiers
{
    public const string EnvVar = "SLOTGAME_SLOW_TESTS";

    public static bool LongRunningEnabled =>
        Environment.GetEnvironmentVariable(EnvVar) == "1";

    public const string SkipReason =
        "Long-running tier. Set SLOTGAME_SLOW_TESTS=1 to run.";
}

/// <summary>A Fact in the Slow tier — skipped unless SLOTGAME_SLOW_TESTS=1.</summary>
public sealed class SlowFactAttribute : FactAttribute
{
    public SlowFactAttribute()
    {
        if (!TestTiers.LongRunningEnabled) Skip = TestTiers.SkipReason;
    }
}

/// <summary>A Fact in the Stress tier — skipped unless SLOTGAME_SLOW_TESTS=1.</summary>
public sealed class StressFactAttribute : FactAttribute
{
    public StressFactAttribute()
    {
        if (!TestTiers.LongRunningEnabled) Skip = TestTiers.SkipReason;
    }
}
