using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// Each reel owns one ordered cyclic strip. A spin chooses one stop on each reel. The visible
/// symbol positions for that reel then read neighboring locations from that same strip: s, s+1, and so on,
/// wrapping at the end. Separate reels choose their stops independently.
///
/// Symbol counts can give the chance for one cell, but they do not record which symbols are
/// neighbors. Calculations that inspect two visible positions on the same reel need the ordered strip.
///
/// Reel count, per-reel stop count and window height all arrive as arguments. Strips of
/// differing lengths on the same machine are normal; Orca Dive, the fictional game this
/// project ships, has 26/29/26/29/26 stops, so each reel's length is read separately.
/// </summary>
public sealed class StripReelSet
{
    /// <summary>The window height every stock preset uses. A game definition may declare another.</summary>
    public const int DefaultRows = 3;

    /// <summary>The shortest window this version supports and tests.</summary>
    public const int MinRows = 3;

    /// <summary>
    /// The tallest window this version supports and tests. Raising the limit requires
    /// reviewing generated payline shapes and tests for the new height.
    /// </summary>
    public const int MaxRows = 5;

    private readonly Symbol[][] _strips;
    private readonly Symbol[][] _drawStrips;
    private readonly byte[][] _drawIds;
    private readonly ulong[] _rngRanges;
    private readonly ulong[] _rngThresholds;

    /// <summary>
    /// Copies one ordered symbol list per reel. Inner lists may have different lengths.
    /// Create a new reel set to change strips for a later run; this snapshot never changes.
    /// </summary>
    public StripReelSet(IReadOnlyList<IReadOnlyList<Symbol>> strips, int rows = DefaultRows)
    {
        ArgumentNullException.ThrowIfNull(strips);
        if (strips.Count < 1)
            throw new ArgumentException("A reel set needs at least one reel.", nameof(strips));
        if (rows < MinRows || rows > MaxRows)
            throw new ArgumentOutOfRangeException(
                nameof(rows), rows, $"A window must have {MinRows}..{MaxRows} rows.");

        for (var reel = 0; reel < strips.Count; reel++)
        {
            if (strips[reel] is null || strips[reel].Count == 0)
                throw new ArgumentException($"Reel {reel + 1} has no stops.", nameof(strips));
        }

        // The reel set is shared by workers and analytic code. Copy the caller's arrays so
        // later mutations cannot change a game that is already running.
        _strips = strips.Select(strip => strip.ToArray()).ToArray();
        Rows = rows;

        // A window may begin at the last stop and continue past the physical end of the
        // array. Append Rows - 1 wrapped symbols once during construction so DrawWindow
        // can read a contiguous slice without calculating modulo for every visible cell.
        // Rows is limited to 3..5, so this costs only two to four extra references per reel.
        _drawStrips = new Symbol[_strips.Length][];
        _drawIds = new byte[_strips.Length][];
        _rngRanges = new ulong[_strips.Length];
        _rngThresholds = new ulong[_strips.Length];
        for (var reel = 0; reel < _strips.Length; reel++)
        {
            var strip = _strips[reel];
            var range = (ulong)strip.Length;
            _rngRanges[reel] = range;
            _rngThresholds[reel] = unchecked(0UL - range) % range;

            var drawStrip = new Symbol[strip.Length + Rows - 1];
            strip.CopyTo(drawStrip, 0);
            for (var extra = 0; extra < Rows - 1; extra++)
                drawStrip[strip.Length + extra] = strip[extra % strip.Length];
            _drawStrips[reel] = drawStrip;
            _drawIds[reel] = drawStrip.Select(symbol => symbol.Id).ToArray();
        }
    }

    public int ReelCount => _strips.Length;

    /// <summary>Number of visible symbol positions in each reel's column. Also the screen-row count. The window is laid out [reel * Rows + row].</summary>
    public int Rows { get; }

    public int WindowSize => ReelCount * Rows;

    public int StopCount(int reel) => _strips[reel].Length;

    public ReadOnlySpan<Symbol> Strip(int reel) => _strips[reel];

    /// <summary>
    /// Marginal probability that a given window row on <paramref name="reel"/> shows
    /// <paramref name="symbolId"/>. By cyclicity every row has the same marginal:
    /// count-on-strip / S. Exact rational, exposed as double for the analytic layer.
    /// </summary>
    public double ProbabilityOf(int reel, byte symbolId)
    {
        var strip = _strips[reel];
        var count = 0;
        foreach (var s in strip)
        {
            if (s.Id == symbolId) count++;
        }
        return (double)count / strip.Length;
    }

    /// <summary>
    /// Joint probability that on <paramref name="reel"/> the window shows
    /// <paramref name="aId"/> at <paramref name="rowA"/> AND <paramref name="bId"/> at
    /// <paramref name="rowB"/>. Enumerates all S stops; the count is exact and the
    /// returned <see cref="double"/> is its floating-point ratio.
    /// </summary>
    public double JointProbabilityOf(int reel, int rowA, byte aId, int rowB, byte bId)
    {
        var strip = _strips[reel];
        var n = strip.Length;
        var count = 0;
        for (var stop = 0; stop < n; stop++)
        {
            if (strip[(stop + rowA) % n].Id == aId && strip[(stop + rowB) % n].Id == bId)
                count++;
        }
        return (double)count / n;
    }

    /// <summary>Draws one stop per reel, then fills that reel's column from neighboring strip positions.</summary>
    public void DrawWindow(ref SpinRng rng, Span<Symbol> window)
    {
        // window layout: [reel * Rows + row]
        for (var reel = 0; reel < _strips.Length; reel++)
        {
            // Range and rejection threshold depend only on strip length. Construction
            // calculates both once so this call performs no setup division per spin.
            var stop = rng.NextInt(_rngRanges[reel], _rngThresholds[reel]);
            var drawStrip = _drawStrips[reel];
            var windowOffset = reel * Rows;
            for (var row = 0; row < Rows; row++)
                window[windowOffset + row] = drawStrip[stop + row];
        }
    }

    /// <summary>
    /// Teaching baseline for the optimization lab. It uses the direct cyclic formula for
    /// every visible cell and copies the full <see cref="Symbol"/> value. Production runs
    /// use <see cref="DrawWindowIds"/>. Keeping both implementations lets the lab compare
    /// equal work and equal output instead of comparing against a synthetic loop.
    /// </summary>
    public void DrawWindowBaseline(ref SpinRng rng, Span<Symbol> window)
    {
        for (var reel = 0; reel < _strips.Length; reel++)
        {
            var strip = _strips[reel];
            var stop = rng.NextInt(strip.Length);
            var windowOffset = reel * Rows;
            for (var row = 0; row < Rows; row++)
                window[windowOffset + row] = strip[(stop + row) % strip.Length];
        }
    }

    /// <summary>
    /// Draws the same window as <see cref="DrawWindow"/>, but writes only symbol ids.
    /// Simulation evaluators use ids, so this avoids copying each symbol's name and flags
    /// through the hot loop. The full-symbol overload remains available to teaching tools.
    /// </summary>
    public void DrawWindowIds(ref SpinRng rng, Span<byte> window)
    {
        for (var reel = 0; reel < _strips.Length; reel++)
        {
            var stop = rng.NextInt(_rngRanges[reel], _rngThresholds[reel]);
            var drawIds = _drawIds[reel];
            var windowOffset = reel * Rows;
            for (var row = 0; row < Rows; row++)
                window[windowOffset + row] = drawIds[stop + row];
        }
    }

    /// <summary>The symbol shown at (reel, row) for a given stop, wrapping cyclically.</summary>
    public Symbol At(int reel, int stop, int row)
    {
        var strip = _strips[reel];
        return strip[(stop + row) % strip.Length];
    }
}
