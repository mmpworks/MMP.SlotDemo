namespace MMP.SlotGame.Core.Rtp;

/// <summary>
/// Two-sided normal quantiles for convergence-band assertions: measured vs. analytic
/// agree when they land within Z * sigma / sqrt(spins) of each other. One home because
/// both values were independently declared, at different precisions, in production code
/// (RunCoordinator's live convergence verdict) and in test code (three separate test
/// files) — exactly the silent-drift risk a shared statistical constant should never
/// have. Lives in Core, not test Support, because RunCoordinator (Server) needs it too.
/// </summary>
public static class NormalQuantile
{
    /// <summary>
    /// Two-sided 99% quantile. RunCoordinator's live "WITHIN BAND" / "OUTSIDE BAND"
    /// verdict and StatisticalConvergenceTests' coverage-band test both gate on this.
    /// Raising it widens the band both places accept as "converged"; lowering it
    /// tightens it. The two call sites previously carried this at different rounding
    /// precisions (2.575829 vs 2.5758293035489004) — consolidating picks the fuller
    /// precision; the difference is around 1e-7 relative, far below anything either
    /// site's band tolerance can distinguish.
    /// </summary>
    public const double TwoSided99 = 2.5758293035489004;

    /// <summary>
    /// Two-sided 99.9% quantile — the AC-1 headline band. Every convergence test across
    /// both the JSON GameDefinition pipeline and the preset/solver pipeline asserts
    /// against this same quantile.
    /// </summary>
    public const double TwoSided999 = 3.290527;
}
