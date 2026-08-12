namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// A payline: one window row index per reel, evaluated left-to-right.
/// </summary>
public sealed record Payline
{
    public Payline(string name, IReadOnlyList<int> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rows);
        Name = name;
        Rows = Array.AsReadOnly([.. rows]);
    }

    public string Name { get; }

    /// <summary>A construction-time snapshot of the row selected on each reel.</summary>
    public IReadOnlyList<int> Rows { get; }

    /// <summary>
    /// Standard line patterns used by the stock presets: 1 center, 3 horizontals,
    /// 5 adds V/Λ, and 9 adds zig-zags. This is a project convention rather than a
    /// universal slot-game payline set. v1 supports 5 or 9 lines and windows of
    /// <see cref="StripReelSet.MinRows"/>..<see cref="StripReelSet.MaxRows"/> rows
    /// (validated by the caller, not here).
    ///
    /// Row geometry is derived from <paramref name="rows"/> on every call:
    /// top = 0, bottom = rows - 1, middle = rows / 2 (integer division).
    ///
    /// An odd row count puts the middle at the true center: 5 rows give 0, 2, 4, a V/hat
    /// spans the full window height, and both zig-zags swing by 2 rows. An even row count
    /// has no center, and integer division rounds toward the bottom half: 4 rows put the
    /// middle at index 2. A V/hat still spans 0 to rows-1, since middle is only an
    /// intermediate ramp point, but the zig-zags become asymmetric. In a four-row window
    /// ZigTop/ZagTop swing two rows (0 to 2) and ZigBottom/ZagBottom swing one (2 to 3).
    /// </summary>
    public static IReadOnlyList<Payline> For(int reels, int lineCount, int rows)
    {
        var topRow = 0;
        var bottomRow = rows - 1;
        var middleRow = rows / 2;

        var mid = Repeat(reels, middleRow);
        var top = Repeat(reels, topRow);
        var bottom = Repeat(reels, bottomRow);
        var vee = Bend(reels, topRow, bottomRow);      // top → bottom → top
        var hat = Bend(reels, bottomRow, topRow);      // bottom → top → bottom
        var zigTop = Alternate(reels, topRow, middleRow);
        var zigBottom = Alternate(reels, bottomRow, middleRow);
        var zagTop = Alternate(reels, middleRow, topRow);
        var zagBottom = Alternate(reels, middleRow, bottomRow);

        Payline[] lines =
        [
            new("Center", mid), new("Top", top), new("Bottom", bottom),
            new("V", vee), new("Hat", hat),
            new("ZigTop", zigTop), new("ZigBottom", zigBottom),
            new("ZagTop", zagTop), new("ZagBottom", zagBottom),
        ];
        return lineCount switch
        {
            5 => lines[..5],
            9 => lines,
            _ => throw new ArgumentException($"Unsupported line count {lineCount}; v1 supports 5 or 9."),
        };
    }

    /// <summary>The center-row payline used by classic one-line games.</summary>
    public static Payline Center(int reels, int rows) => new("Center", Repeat(reels, rows / 2));

    private static int[] Repeat(int reels, int row) =>
        [.. Enumerable.Repeat(row, reels)];

    /// <summary>
    /// V-shape: start row, dip/peak to the far row at the middle reel, back. It interpolates
    /// between the two row values it is given, so it works at any window height. The caller
    /// derives <paramref name="edgeRow"/> and <paramref name="midRow"/>.
    /// </summary>
    private static int[] Bend(int reels, int edgeRow, int midRow)
    {
        var rows = new int[reels];
        var middle = reels / 2;
        for (var r = 0; r < reels; r++)
        {
            // linear ramp toward the middle then back; rounds to the nearest whole row
            var distance = Math.Abs(r - middle);
            var maxDistance = Math.Max(middle, reels - 1 - middle);
            rows[r] = maxDistance == 0
                ? midRow
                : (int)Math.Round(midRow + (edgeRow - midRow) * (double)distance / maxDistance);
        }
        return rows;
    }

    private static int[] Alternate(int reels, int rowEven, int rowOdd)
    {
        var rows = new int[reels];
        for (var r = 0; r < reels; r++) rows[r] = r % 2 == 0 ? rowEven : rowOdd;
        return rows;
    }
}
