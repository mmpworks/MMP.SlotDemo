namespace MMP.SlotGame.Core.Rtp;

/// <summary>
/// Two-sided normal quantiles for convergence-band assertions: measured vs. analytic
/// agree when they land within Z * sigma / sqrt(spins) of each other. One home, because a
/// statistical constant declared in more than one place drifts silently. It lives in Core
/// because RunCoordinator (Server) reads it alongside the tests.
/// </summary>
public static class NormalQuantile
{
    /// <summary>
    /// Two-sided 99% quantile. RunCoordinator's live "WITHIN BAND" / "OUTSIDE BAND"
    /// verdict and StatisticalConvergenceTests' coverage-band test both gate on this.
    /// Raising it widens the band both places accept as "converged"; lowering it
    /// tightens it.
    /// </summary>
    public const double TwoSided99 = 2.5758293035489004;

    /// <summary>
    /// Two-sided 99.9% quantile, the headline convergence band. Every convergence test
    /// across both the JSON GameDefinition pipeline and the preset/solver pipeline asserts
    /// against this quantile.
    /// </summary>
    public const double TwoSided999 = 3.290527;
}
