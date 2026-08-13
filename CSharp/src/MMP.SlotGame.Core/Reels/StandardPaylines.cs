namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// Convenience paylines for the demo presets. These shapes are defaults, not rules of
/// slot design. A PAR-sheet transcription may supply any valid position list directly
/// to <see cref="Payline"/>.
/// </summary>
public static class StandardPaylines
{
    /// <summary>
    /// Builds the demo's five- or nine-line set. Position 0 is the top of each reel's
    /// visible window; <paramref name="visiblePositions"/> minus one is the bottom.
    /// </summary>
    public static IReadOnlyList<Payline> For(int reels, int lineCount, int visiblePositions)
    {
        var top = 0;
        var bottom = visiblePositions - 1;
        var middle = visiblePositions / 2;

        Payline[] lines =
        [
            new("Center", Repeat(reels, middle)),
            new("Top", Repeat(reels, top)),
            new("Bottom", Repeat(reels, bottom)),
            new("V", Bend(reels, top, bottom)),
            new("Hat", Bend(reels, bottom, top)),
            new("ZigTop", Alternate(reels, top, middle)),
            new("ZigBottom", Alternate(reels, bottom, middle)),
            new("ZagTop", Alternate(reels, middle, top)),
            new("ZagBottom", Alternate(reels, middle, bottom)),
        ];

        return lineCount switch
        {
            5 => lines[..5],
            9 => lines,
            _ => throw new ArgumentException($"Unsupported line count {lineCount}; the standard catalog contains 5 or 9."),
        };
    }

    /// <summary>Builds one straight line through the middle visible position.</summary>
    public static Payline Center(int reels, int visiblePositions) =>
        new("Center", Repeat(reels, visiblePositions / 2));

    private static int[] Repeat(int reels, int position) => [.. Enumerable.Repeat(position, reels)];

    private static int[] Bend(int reels, int edgePosition, int middlePosition)
    {
        var positions = new int[reels];
        var middleReel = reels / 2;
        for (var reel = 0; reel < reels; reel++)
        {
            var distance = Math.Abs(reel - middleReel);
            var farthestEdge = Math.Max(middleReel, reels - 1 - middleReel);
            positions[reel] = farthestEdge == 0
                ? middlePosition
                : (int)Math.Round(middlePosition
                    + (edgePosition - middlePosition) * (double)distance / farthestEdge);
        }
        return positions;
    }

    private static int[] Alternate(int reels, int evenPosition, int oddPosition)
    {
        var positions = new int[reels];
        for (var reel = 0; reel < reels; reel++)
            positions[reel] = reel % 2 == 0 ? evenPosition : oddPosition;
        return positions;
    }
}
