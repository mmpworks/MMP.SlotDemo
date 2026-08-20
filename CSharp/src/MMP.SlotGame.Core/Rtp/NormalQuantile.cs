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

    /// <summary>Multiplier for a two-sided 90% band.</summary>
    public const double TwoSided90 = 1.6448536269514722;

    /// <summary>
    /// The z published PAR sheets use for the volatility index. Harrigan and Dixon's Table 2
    /// states it outright: "Volatility index = (z-score for confidence interval) * (standard
    /// deviation of the game)", with "z-score for a 90% confidence interval is: 1.65", giving
    /// "10.476 i.e., 6.349285 x 1.65" for Double Diamond Deluxe.
    ///
    /// It is the rounded 90% value, not the exact one above. The VI column exists to be read
    /// against their published figures, so it uses their constant; the exact z would shift
    /// Double Diamond's VI from 10.476 to 10.444, enough to change the printed digit.
    /// Confidence bands elsewhere use the exact values.
    /// </summary>
    public const double VolatilityIndexZ = 1.65;

    /// <summary>Multiplier for a two-sided 95% band.</summary>
    public const double TwoSided95 = 1.9599639845400545;
}
