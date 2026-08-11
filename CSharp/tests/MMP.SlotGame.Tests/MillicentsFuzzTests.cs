using MMP.SlotGame.Core.Money;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Randomized property tests over <see cref="Millicents"/> arithmetic. Fixed seeds keep every
/// run reproducible; a failure here always points at the same input on a re-run.
/// </summary>
[Trait("Category", "Fast")]
public sealed class MillicentsFuzzTests
{
    private const int Iterations = 5_000;

    // Bounded to keep every intermediate product well inside long range: at most ~1e11
    // millicents (a million credits) times a multiplier up to 10,000 is ~1e15, nowhere near
    // long.MaxValue (~9.2e18). This is the "realistic range" band; extreme values get their
    // own property below.
    private const long MaxRealisticValue = 100_000_000_000L;
    private const long MaxRealisticMultiple = 10_000L;

    [Fact]
    public void AddThenSubtract_RoundTripsToTheOriginalValue()
    {
        var rng = new Random(1001);
        for (var i = 0; i < Iterations; i++)
        {
            var a = new Millicents(rng.NextInt64(-MaxRealisticValue, MaxRealisticValue));
            var b = new Millicents(rng.NextInt64(-MaxRealisticValue, MaxRealisticValue));

            var result = a + b - b;

            Assert.Equal(a, result);
        }
    }

    [Fact]
    public void Add_MatchesLongAdditionOfTheUnderlyingValues()
    {
        var rng = new Random(1002);
        for (var i = 0; i < Iterations; i++)
        {
            var aValue = rng.NextInt64(-MaxRealisticValue, MaxRealisticValue);
            var bValue = rng.NextInt64(-MaxRealisticValue, MaxRealisticValue);

            var sum = new Millicents(aValue) + new Millicents(bValue);

            Assert.Equal(aValue + bValue, sum.Value);
        }
    }

    [Fact]
    public void MultiplyByLong_MatchesLongMultiplicationOfTheUnderlyingValue()
    {
        var rng = new Random(1003);
        for (var i = 0; i < Iterations; i++)
        {
            var value = rng.NextInt64(-MaxRealisticValue, MaxRealisticValue);
            var multiple = rng.NextInt64(-MaxRealisticMultiple, MaxRealisticMultiple);

            var product = new Millicents(value) * multiple;

            Assert.Equal(value * multiple, product.Value);
        }
    }

    /// <summary>
    /// <see cref="Millicents.ScaledMultiply"/> divides first, so it is only exact when the
    /// wager is credit-aligned to <see cref="Millicents.ScaleFactor"/>. This fuzzes random
    /// aligned wagers and random scaled multipliers, comparing against an independent decimal
    /// reference computation rather than re-deriving the same integer arithmetic under test.
    /// </summary>
    [Fact]
    public void ScaledMultiply_OnAlignedValues_MatchesTheDecimalReferenceExactly()
    {
        var rng = new Random(1004);
        for (var i = 0; i < Iterations; i++)
        {
            // Force alignment to ScaleFactor by construction, rather than filtering random
            // values, so every iteration exercises the property instead of being skipped.
            var alignedValue = rng.NextInt64(-1_000_000_000L, 1_000_000_000L) * Millicents.ScaleFactor;
            var scaledMultiplier = rng.Next(-100_000, 100_000);
            var wager = new Millicents(alignedValue);

            var pay = wager.ScaledMultiply(scaledMultiplier);

            var reference = (decimal)alignedValue / Millicents.ScaleFactor * scaledMultiplier;
            Assert.Equal(reference, (decimal)pay.Value);
        }
    }

    [Fact]
    public void ScaledMultiply_OnEveryNonAlignedValue_ThrowsTheGuard()
    {
        var rng = new Random(1005);
        for (var i = 0; i < Iterations; i++)
        {
            // Build a value guaranteed NOT divisible by ScaleFactor: any nonzero remainder in
            // [1, ScaleFactor - 1] added to an aligned base.
            var basis = rng.NextInt64(-1_000_000_000L, 1_000_000_000L) * Millicents.ScaleFactor;
            var remainder = rng.NextInt64(1, Millicents.ScaleFactor);
            var wager = new Millicents(basis + remainder);

            Assert.Throws<InvalidOperationException>(() => wager.ScaledMultiply(rng.Next(-1000, 1000)));
        }
    }

    [Fact]
    public void Comparison_IComparableAgreesWithTheRelationalOperators()
    {
        var rng = new Random(1006);
        for (var i = 0; i < Iterations; i++)
        {
            var a = new Millicents(rng.NextInt64(-MaxRealisticValue, MaxRealisticValue));
            var b = new Millicents(rng.NextInt64(-MaxRealisticValue, MaxRealisticValue));

            var compareSign = Math.Sign(a.CompareTo(b));

            Assert.Equal(a > b, compareSign > 0);
            Assert.Equal(a < b, compareSign < 0);
            Assert.Equal(a >= b, compareSign >= 0);
            Assert.Equal(a <= b, compareSign <= 0);
            Assert.Equal(a == b, compareSign == 0);
        }
    }

    /// <summary>
    /// Documents the operator's actual behavior at the edges of <see cref="long"/> rather than
    /// asserting an exception it does not throw: <see cref="Millicents"/> arithmetic runs in an
    /// unchecked context, so a product that exceeds <see cref="long.MaxValue"/> wraps rather
    /// than throwing. This pins that wraparound as deterministic (matching an explicit
    /// <c>unchecked</c> reference computation) so a future switch to checked arithmetic is a
    /// visible, deliberate diff instead of a silent behavior change.
    /// </summary>
    [Fact]
    public void Multiply_AtExtremeValues_WrapsDeterministicallyRatherThanCorrupting()
    {
        var rng = new Random(1007);
        for (var i = 0; i < Iterations; i++)
        {
            var value = rng.NextInt64(long.MinValue, long.MaxValue);
            var multiple = rng.NextInt64(long.MinValue, long.MaxValue);

            var product = new Millicents(value) * multiple;

            Assert.Equal(unchecked(value * multiple), product.Value);
        }
    }
}
