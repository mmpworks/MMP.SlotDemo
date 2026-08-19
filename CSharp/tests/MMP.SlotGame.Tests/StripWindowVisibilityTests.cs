using MMP.SlotGame.Core.Reels;
using Xunit;

namespace MMP.SlotGame.Tests;

public sealed class StripWindowVisibilityTests
{
    private static readonly Symbol Blank = new(0, "Blank");
    private static readonly Symbol Star = new(1, "Star", IsScatter: true);

    [Fact]
    public void Separated_symbols_contribute_one_visible_start_per_window_position()
    {
        var reels = BuildReelSet(0, 10);

        Assert.Equal(10, reels.VisibleStopCount(0, Star.Id));
        Assert.Equal(0.5, reels.WindowVisibilityOf(0, Star.Id));
    }

    [Fact]
    public void Overlapping_symbol_windows_count_each_starting_stop_once()
    {
        var reels = BuildReelSet(0, 2);

        // Two Stars times five positions suggests 10 starts, but three starts show
        // both Stars. The union contains only seven distinct starting stops.
        Assert.Equal(7, reels.VisibleStopCount(0, Star.Id));
        Assert.Equal(0.35, reels.WindowVisibilityOf(0, Star.Id));
    }

    [Fact]
    public void Visibility_wraps_across_the_end_of_the_strip()
    {
        var reels = BuildReelSet(0);

        Assert.True(reels.WindowContains(0, 18, Star.Id));
        Assert.Equal(5, reels.VisibleStopCount(0, Star.Id));
    }

    private static StripReelSet BuildReelSet(params int[] starPositions)
    {
        var strip = Enumerable.Repeat(Blank, 20).ToArray();
        foreach (var position in starPositions) strip[position] = Star;
        return new StripReelSet([strip], rows: 5);
    }
}
