using System.Reflection;
using SlotDemo.Server.Chapters.Money;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// The money guarantees episode 2 claims, checked here rather than asserted on camera.
/// These are the demo site's copy of the engine type, so a drift between this copy and
/// MMP.SlotGame.Core shows up as a failure in this suite.
/// </summary>
public sealed class MillicentsTests
{
    [Fact]
    public void FromCredits_scales_by_the_declared_resolution()
    {
        Assert.Equal(100_000, Millicents.FromCredits(1).Value);
        Assert.Equal(2_500_000, Millicents.FromCredits(25).Value);
    }

    [Fact]
    public void M1_the_type_exposes_no_conversion_to_a_floating_point_number()
    {
        // The invariant is the absence of a feature, so the test looks for the feature and
        // expects to find nothing. An implicit conversion added later fails here first.
        var conversions = typeof(Millicents)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name is "op_Implicit" or "op_Explicit")
            .Where(m => m.ReturnType == typeof(double) || m.ReturnType == typeof(float)
                        || m.ReturnType == typeof(decimal))
            .ToArray();

        Assert.Empty(conversions);
    }

    [Fact]
    public void M2_a_total_does_not_depend_on_the_order_the_parts_arrive_in()
    {
        var parts = Enumerable.Range(1, 500).Select(i => new Millicents(i * 37L)).ToArray();
        var forward = parts.Aggregate(Millicents.Zero, (sum, part) => sum + part);
        var backward = parts.Reverse().Aggregate(Millicents.Zero, (sum, part) => sum + part);

        var shuffled = parts.OrderBy(p => p.Value % 7).ThenByDescending(p => p.Value).ToArray();
        var scrambled = shuffled.Aggregate(Millicents.Zero, (sum, part) => sum + part);

        Assert.Equal(forward, backward);
        Assert.Equal(forward, scrambled);
    }

    [Fact]
    public void M2_holds_where_the_same_sum_in_double_drifts()
    {
        // 1.1 credits has no exact binary form; the integer path is unbothered by that.
        const int repeats = 1_000_000;
        var exact = new Millicents(110_000) * repeats;

        double drifting = 0;
        for (var i = 0; i < repeats; i++) drifting += 1.1;

        Assert.Equal(110_000_000_000L, exact.Value);
        Assert.NotEqual(1_100_000.0, drifting);
    }

    [Theory]
    [InlineData(225, 225_000)]     // 2.25x of one credit
    [InlineData(100, 100_000)]     // 1.00x
    [InlineData(0, 0)]             // a losing spin
    public void ScaledMultiply_converts_a_scaled_multiplier_without_a_remainder(
        int scaledMultiplier, long expected)
    {
        var wager = Millicents.FromCredits(1);
        Assert.Equal(expected, wager.ScaledMultiply(scaledMultiplier).Value);
    }

    [Fact]
    public void ScaledMultiply_refuses_an_amount_it_cannot_convert_exactly()
    {
        var odd = new Millicents(12_345);   // not a multiple of the pay scale

        var error = Assert.Throws<InvalidOperationException>(() => odd.ScaledMultiply(110));

        // The message has to name the amount and the scale: it is the only explanation the
        // next person gets.
        Assert.Contains("12345", error.Message);
        Assert.Contains(Millicents.ScaleFactor.ToString(), error.Message);
    }

    [Fact]
    public void Multiplication_takes_a_whole_count_of_an_amount()
    {
        var bet = Millicents.FromCredits(2);
        Assert.Equal(Millicents.FromCredits(40), bet * 20);
    }

    [Fact]
    public void Comparison_and_equality_follow_the_underlying_count()
    {
        var small = Millicents.FromCredits(1);
        var large = Millicents.FromCredits(2);

        Assert.True(small < large);
        Assert.True(large >= small);
        Assert.Equal(small, Millicents.FromCredits(1));
        Assert.Equal(0, small.CompareTo(Millicents.FromCredits(1)));
    }

    [Fact]
    public void ToCredits_is_the_display_exit_and_round_trips_whole_credits()
    {
        Assert.Equal(2.25, Millicents.FromCredits(1).ScaledMultiply(225).ToCredits());
        Assert.Equal("2.25cr", Millicents.FromCredits(1).ScaledMultiply(225).ToString());
    }
}
