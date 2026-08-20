namespace MMP.SlotGame.Core.Rtp;

/// <summary>
/// Constants used to build confidence bands around the analytic RTP. The half-width is
/// Z × standard deviation ÷ square root of the spin count. The server and tests use these
/// shared values so they calculate identical bands.
/// </summary>
public static class NormalQuantile
{
    /// <summary>
    /// Multiplier for a two-sided 99% confidence band. The middle 99% leaves 0.5% in each
    /// tail, so this is the standard normal distribution's 99.5th percentile. A larger
    /// multiplier makes the band wider; a smaller multiplier makes it narrower.
    /// </summary>
    public const double TwoSided99 = 2.5758293035489004;

    /// <summary>
    /// Multiplier for a two-sided 99.9% confidence band used by the long convergence tests.
    /// </summary>
    public const double TwoSided999 = 3.290527;

    /// <summary>
    /// Multiplier for a two-sided 90% band, the level the volatility index is quoted at.
    /// </summary>
    public const double TwoSided90 = 1.6448536269514722;

    /// <summary>Multiplier for a two-sided 95% band.</summary>
    public const double TwoSided95 = 1.9599639845400545;
}
