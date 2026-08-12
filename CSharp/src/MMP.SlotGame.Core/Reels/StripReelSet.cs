using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// A reel is an ordered cyclic strip. A spin draws one uniform stop index per reel; the
/// visible window shows adjacent strip positions {s, s+1, ... s+Rows-1} mod S. Rows within
/// a reel are therefore correlated by strip adjacency, while different reels are
/// independent. A weighted multiset loses that adjacency, so it stops being equivalent the
/// moment a multi-row window exists.
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

    public StripReelSet(Symbol[][] strips, int rows = DefaultRows)
    {
        ArgumentNullException.ThrowIfNull(strips);
        if (strips.Length < 1)
            throw new ArgumentException("A reel set needs at least one reel.", nameof(strips));
        if (rows < MinRows || rows > MaxRows)
            throw new ArgumentOutOfRangeException(
                nameof(rows), rows, $"A window must have {MinRows}..{MaxRows} rows.");

        for (var reel = 0; reel < strips.Length; reel++)
        {
            if (strips[reel] is null || strips[reel].Length == 0)
                throw new ArgumentException($"Reel {reel + 1} has no stops.", nameof(strips));
        }

        // The reel set is shared by workers and analytic code. Copy the caller's arrays so
        // later mutations cannot change a game that is already running.
        _strips = strips.Select(strip => strip.ToArray()).ToArray();
        Rows = rows;
    }

    public int ReelCount => _strips.Length;

    /// <summary>Visible rows per reel. The window is laid out [reel * Rows + row].</summary>
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

    /// <summary>Draw one spin window. One uniform stop per reel; rows are strip-adjacent.</summary>
    public void DrawWindow(ref SpinRng rng, Span<Symbol> window)
    {
        // window layout: [reel * Rows + row]
        for (var reel = 0; reel < _strips.Length; reel++)
        {
            var strip = _strips[reel];
            var stop = rng.NextInt(strip.Length);
            for (var row = 0; row < Rows; row++)
            {
                var pos = (stop + row) % strip.Length;
                window[reel * Rows + row] = strip[pos];
            }
        }
    }

    /// <summary>The symbol shown at (reel, row) for a given stop, wrapping cyclically.</summary>
    public Symbol At(int reel, int stop, int row)
    {
        var strip = _strips[reel];
        return strip[(stop + row) % strip.Length];
    }
}
