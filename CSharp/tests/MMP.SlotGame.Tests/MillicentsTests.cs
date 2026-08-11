using MMP.SlotGame.Core.Money;

namespace MMP.SlotGame.Tests;

/// <summary>
/// <see cref="Millicents.ScaledMultiply"/> is the one place a scaled pay multiplier
/// (<see cref="Core.Games.Definition.PayCategory.PayFor"/>, the real multiplier ×
/// <see cref="Millicents.ScaleFactor"/>) turns into money. These pin the arithmetic and the
/// guard that keeps the division exact.
/// </summary>
[Trait("Category", "Fast")]
public sealed class MillicentsTests
{
    /// <summary>2.25X at a 1-credit wager: the number quoted throughout the fractional-pay design.</summary>
    [Fact]
    public void ScaledMultiply_TwoAndAQuarterXAtOneCredit_IsExactMillicents()
    {
        var wager = Millicents.FromCredits(1);

        var pay = wager.ScaledMultiply(225);

        Assert.Equal(225_000L, pay.Value);
    }

    [Theory]
    [InlineData(50, 50_000L)] // 0.5X
    [InlineData(90, 90_000L)] // 0.9X
    [InlineData(100, 100_000L)] // 1.0X
    [InlineData(125, 125_000L)] // 1.25X
    [InlineData(175, 175_000L)] // 1.75X
    [InlineData(225, 225_000L)] // 2.25X
    public void ScaledMultiply_IsExactForEveryScaleStep(int scaledMultiplier, long expectedMillicents)
    {
        var wager = Millicents.FromCredits(1);

        var pay = wager.ScaledMultiply(scaledMultiplier);

        Assert.Equal(expectedMillicents, pay.Value);
    }

    /// <summary>
    /// A wager that is not a multiple of <see cref="Millicents.ScaleFactor"/> millicents cannot
    /// divide evenly, so this fails loud instead of silently truncating a fraction of a millicent.
    /// </summary>
    [Fact]
    public void ScaledMultiply_WagerNotDivisibleByScaleFactor_ThrowsWithAClearMessage()
    {
        var wager = new Millicents(100_050); // divisible by 10, not by Millicents.ScaleFactor (100)

        var ex = Assert.Throws<InvalidOperationException>(() => wager.ScaledMultiply(225));

        Assert.Contains($"multiple of {Millicents.ScaleFactor}", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A one-cent wager (1,000 millicents) is the finest real denomination, and it still divides evenly.</summary>
    [Fact]
    public void ScaledMultiply_OneCentWager_IsExact()
    {
        var wager = new Millicents(1_000);

        var pay = wager.ScaledMultiply(225);

        Assert.Equal(2_250L, pay.Value);
    }
}
