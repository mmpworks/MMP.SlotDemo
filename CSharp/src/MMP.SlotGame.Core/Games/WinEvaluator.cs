using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Games;

/// <summary>
/// The winning interpretation of one payline: which pay category, how long a run, what it
/// pays. <paramref name="Multiplier"/> is in hundredths of the total spin bet (225 =
/// 2.25X of the whole wager, not of this line's share of it; <see cref="EvaluateWindow"/>
/// documents the basis). See <see cref="Definition.PayCategory.PayFor"/>.
/// </summary>
public readonly record struct LineWin(int CategoryIndex, int Count, int Multiplier)
{
    public static readonly LineWin None = new(-1, 0, 0);

    public bool IsWin => Multiplier > 0;
}

/// <summary>
/// Turns one payline into one win, for any game definition. It walks the compiled pay
/// categories and takes the best; the definition supplies every symbol meaning.
///
/// Two engine-wide rules live here, and they are the whole of this class's game knowledge:
///
///  1. A run is left-aligned and continues while the category says the symbol continues it.
///     A run counts only if at least one symbol in it satisfies the category. That second
///     clause keeps an all-substitute line with the substitute rather than the symbol it
///     stands in for. It matters only where wilds exist.
///  2. Best win per line: highest pay wins, and equal pays go to the longer run. In Orca
///     Dive a Red-7 four-of-a-kind and a Mixed-7 five-of-a-kind both pay 100, and the
///     source combination table assigns that line to Mixed 7. Fixing the tie on run
///     length keeps the assignment reproducible.
///
/// Run length has no minimum here. A category pays at a length exactly when its pay table
/// has a non-zero entry there, which is how Orca Dive pays a lone wild at one of a kind
/// while everything else needs three.
/// </summary>
public sealed class WinEvaluator(GameDefinition definition)
{
    private readonly PayCategory[] _categories = [.. definition.Categories];
    private readonly Payline[] _paylines = [.. definition.Paylines];
    private readonly int _reelCount = definition.ReelCount;
    private readonly int _rows = definition.Reels.Rows;

    public int PaylineCount => _paylines.Length;

    /// <summary><paramref name="cells"/> is the payline cell of each reel, left to right.</summary>
    public LineWin Evaluate(ReadOnlySpan<byte> cells)
    {
        var best = LineWin.None;

        foreach (var category in _categories)
        {
            var run = 0;
            var satisfied = false;
            while (run < cells.Length && category.Continues(cells[run]))
            {
                satisfied |= category.IsRequired(cells[run]);
                run++;
            }
            if (!satisfied) continue;

            var pay = category.PayFor(run);
            if (pay == 0 || pay < best.Multiplier) continue;
            if (pay == best.Multiplier && run <= best.Count) continue;

            best = new LineWin(category.Index, run, pay);
        }

        return best;
    }

    /// <summary>Copies one payline out of a drawn window, left to right.</summary>
    public void ReadLine(ReadOnlySpan<Symbol> window, int payline, Span<byte> destination)
    {
        var rows = _paylines[payline].Rows;
        for (var reel = 0; reel < _reelCount; reel++)
            destination[reel] = window[reel * _rows + rows[reel]].Id;
    }

    /// <summary>
    /// Total pay multiplier over every payline, in hundredths of the total spin bet. Lines
    /// are independent pays that add, which is also what makes the analytic EV a plain sum
    /// over lines.
    ///
    /// Every payline's compiled multiplier is scaled against the same total spin wager:
    /// <see cref="Money.Millicents.ScaledMultiply"/> is always called with this sum, never
    /// a per-line share. This method sums every winning line's multiplier before that one
    /// scaling happens. Traditional multiline paytables often state pays as multiples of
    /// one line's bet; this engine has no such division, so a declared multiplier of 5000
    /// means 5000X the total spin wager on every line. Every game shipped today has a
    /// single payline, where the two bases give the same number; a multi-payline JSON game
    /// would separate them.
    /// </summary>
    public int EvaluateWindow(ReadOnlySpan<Symbol> window, Span<byte> cells)
    {
        var total = 0;
        for (var payline = 0; payline < _paylines.Length; payline++)
        {
            ReadLine(window, payline, cells);
            total += Evaluate(cells).Multiplier;
        }
        return total;
    }

    /// <summary>True when the scatter shows anywhere in the window on every required reel.</summary>
    public static bool IsTriggered(ReadOnlySpan<Symbol> window, int rows, ScatterPickBonus bonus)
    {
        foreach (var reel in bonus.RequiredReels)
        {
            var seen = false;
            for (var row = 0; row < rows && !seen; row++)
                seen = window[reel * rows + row].Id == bonus.ScatterSymbolId;
            if (!seen) return false;
        }
        return true;
    }
}
