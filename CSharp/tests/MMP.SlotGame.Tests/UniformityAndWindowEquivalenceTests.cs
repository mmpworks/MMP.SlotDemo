using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;
using Xunit;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The two facts this series rests on that the suite did not previously pin.
///
/// The engine draws a stop with Lemire rejection, and it draws a window through an extended
/// strip instead of a modulo. Both are optimizations of an obvious correct version, and both
/// are the kind of code a later reader will "tidy" without noticing what breaks: a rejection
/// threshold set to the range instead of 2^64 mod range is still roughly uniform, and an
/// extended-strip off-by-one only shows at the wrap. Neither would fail a smoke test.
/// </summary>
public sealed class UniformityAndWindowEquivalenceTests
{
    /// <summary>
    /// The extended strip must produce the SAME window as the modulo version.
    ///
    /// 64 seeds by 512 spins is 32,768 draws per reel against strips of at most 128 stops,
    /// so every stop is exercised many times over, including the wrap positions where an
    /// off-by-one would show. This is a heavy sample rather than a proof: the type exposes
    /// no way to ask for a chosen stop, and adding one purely for a test would widen the
    /// production surface to prove something the sample already reaches.
    /// </summary>
    [Theory]
    [InlineData("Classic3")]
    [InlineData("Video3")]
    [InlineData("Line4")]
    [InlineData("Video5x64")]
    [InlineData("Video5x128")]
    public void The_extended_strip_draws_the_same_window_as_the_modulo_version(string presetName)
    {
        var preset = StandardReelPresets.All[presetName];
        var reels = preset.BuildReels();

        var fast = new Symbol[reels.WindowSize];
        var baseline = new Symbol[reels.WindowSize];

        // Same seed on both sides, so the two paths draw the same stops and any difference
        // is the window construction rather than the randomness.
        for (ulong seed = 1; seed <= 64; seed++)
        {
            var fastRng = SpinRng.ForWorker(seed, 0);
            var baselineRng = SpinRng.ForWorker(seed, 0);

            for (var spin = 0; spin < 512; spin++)
            {
                reels.DrawWindow(ref fastRng, fast);
                reels.DrawWindowBaseline(ref baselineRng, baseline);

                for (var cell = 0; cell < fast.Length; cell++)
                {
                    Assert.Equal(baseline[cell].Id, fast[cell].Id);
                }
            }
        }
    }

    /// <summary>
    /// A chi-square uniformity check at bounds that are NOT powers of two. 26 and 29 are
    /// the strip lengths Orca Dive actually uses.
    ///
    /// WHAT THIS DOES NOT PROVE, stated because it is easy to assume otherwise: it cannot
    /// detect a wrong rejection threshold. Verified by experiment on 2026-08-19 — replacing
    /// `2^64 mod range` with `range` leaves this test green. The reason is the arithmetic
    /// of multiply-shift on a 64-bit source: the reject zone is 16 values out of 2^64 for a
    /// 26-stop reel, and the worst-case bias with NO rejection at all is range/2^64, about
    /// 1.4e-18. Seeing that would take on the order of 1e35 draws. At 64 bits the rejection
    /// step is insurance whose absence is unobservable; the bias article 2 demonstrates is
    /// real and visible only because it uses an 8-bit source.
    ///
    /// What this DOES pin is the bin arithmetic: an out-of-range return, an off-by-one in
    /// the high-bits shift, or a draw that favours part of the range would all fail here.
    /// The critical values are upper 0.1% points, and the run is seeded, so a correct
    /// generator passes deterministically rather than usually.
    /// </summary>
    [Theory]
    [InlineData(26, 38.885)]   // 25 degrees of freedom, upper 0.1% point
    [InlineData(29, 42.796)]   // 28 degrees of freedom
    [InlineData(36, 52.620)]   // 35 degrees of freedom
    [InlineData(3, 13.816)]    // 2 degrees of freedom, a tiny bound
    public void Bounded_draws_are_uniform_at_a_non_power_of_two_bound(int bound, double critical)
    {
        const int draws = 2_000_000;
        var counts = new int[bound];
        var rng = SpinRng.ForWorker(20260819, 0);

        for (var i = 0; i < draws; i++)
        {
            var value = rng.NextInt(bound);
            Assert.InRange(value, 0, bound - 1);
            counts[value]++;
        }

        var expected = (double)draws / bound;
        var chiSquare = 0.0;
        foreach (var observed in counts)
        {
            var delta = observed - expected;
            chiSquare += delta * delta / expected;
        }

        Assert.True(
            chiSquare < critical,
            $"chi-square {chiSquare:0.###} exceeded {critical} for bound {bound}; the draw is biased.");
    }

    /// <summary>
    /// A power-of-two bound rejects nothing, so it is the case where a wrong threshold hides.
    /// Checking it separately keeps the previous test honest about what it proves.
    /// </summary>
    [Fact]
    public void A_power_of_two_bound_is_also_uniform()
    {
        const int bound = 64;
        const int draws = 1_280_000;
        var counts = new int[bound];
        var rng = SpinRng.ForWorker(7, 3);

        for (var i = 0; i < draws; i++) counts[rng.NextInt(bound)]++;

        var expected = (double)draws / bound;
        var chiSquare = 0.0;
        foreach (var observed in counts)
        {
            var delta = observed - expected;
            chiSquare += delta * delta / expected;
        }

        // 63 degrees of freedom, upper 0.1% point.
        Assert.True(chiSquare < 103.442, $"chi-square {chiSquare:0.###} exceeded 103.442.");
    }
}
