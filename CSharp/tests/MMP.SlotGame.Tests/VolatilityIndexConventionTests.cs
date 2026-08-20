using MMP.SlotGame.Core.Rtp;
using Xunit;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The volatility index column on the PAR page exists to be read against Harrigan and
/// Dixon's published figures, so it has to use their arithmetic rather than merely
/// something defensible.
///
/// Their Table 2 states the calculation outright: "Volatility index = (z-score for
/// confidence interval) * (standard deviation of the game)", "z-score for a 90% confidence
/// interval is: 1.65", and "Volatility index: 10.476 i.e., 6.349285 x 1.65" for the 92.5%
/// version of Double Diamond Deluxe. Table 1 prints that game's VI as 10.5.
///
/// This reproduces their worked example. The 90% level was checked against the paper on
/// 2026-08-19 after it was questioned as possibly invented; it is verbatim from the source.
/// </summary>
public sealed class VolatilityIndexConventionTests
{
    /// <summary>The standard deviation Harrigan and Dixon's Table 2 works from.</summary>
    private const double DoubleDiamondDeluxeSigma = 6.349285;

    [Fact]
    public void The_published_worked_example_reproduces()
    {
        var vi = NormalQuantile.VolatilityIndexZ * DoubleDiamondDeluxeSigma;

        Assert.Equal(10.476, vi, 3);
        Assert.Equal(10.5, Math.Round(vi, 1));
    }

    /// <summary>
    /// Their z is the ROUNDED 90% value. Swapping in the exact one changes the printed
    /// digit, which is why the two constants are kept apart rather than merged.
    /// </summary>
    [Fact]
    public void The_exact_z_would_print_a_different_digit()
    {
        var published = NormalQuantile.VolatilityIndexZ * DoubleDiamondDeluxeSigma;
        var exact = NormalQuantile.TwoSided90 * DoubleDiamondDeluxeSigma;

        Assert.NotEqual(Math.Round(published, 1), Math.Round(exact, 1));
        Assert.Equal(1.65, NormalQuantile.VolatilityIndexZ);
    }

    /// <summary>
    /// The confidence bands are a different job and keep the exact quantiles, so a future
    /// tidy-up that unified the constants would fail here.
    /// </summary>
    [Fact]
    public void Confidence_bands_keep_the_exact_quantiles()
    {
        Assert.Equal(1.6448536269514722, NormalQuantile.TwoSided90, 12);
        Assert.Equal(1.9599639845400545, NormalQuantile.TwoSided95, 12);
        Assert.Equal(2.5758293035489004, NormalQuantile.TwoSided99, 12);
    }
}
