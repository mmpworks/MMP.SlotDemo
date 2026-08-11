using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Games;

/// <summary>
/// The winning interpretation of one payline: which pay category, how long a run, what it
/// pays. <paramref name="Multiplier"/> is in hundredths of the TOTAL SPIN BET (225 =
/// 2.25X of the whole wager, not of this line's share of it — see
/// <see cref="EvaluateWindow"/>'s doc comment for why); see
/// <see cref="Definition.PayCategory.PayFor"/>.
/// </summary>
public readonly record struct LineWin(int CategoryIndex, int Count, int Multiplier)
{
    public static readonly LineWin None = new(-1, 0, 0);

    public bool IsWin => Multiplier > 0;
}

/// <summary>
/// Turns one payline into one win, for ANY game definition. It has no idea what a wild, a
/// seven or a fruit is; it walks the compiled pay categories and takes the best.
///
/// Two engine-wide rules, and they are the only game knowledge in here:
///
///  1. A run is left-aligned and continues while the category says the symbol continues it.
///     A run only counts if at least one symbol in it satisfies the category. That second
///     clause is what keeps an all-substitute line with the substitute rather than with the
///     symbol it was standing in for, and it is vacuous for games without wilds.
///  2. Best win per line: highest pay wins, and equal pays go to the LONGER run. The tie
///     rule is not decoration. In Orca Dive a Red-7 four-of-a-kind and a Mixed-7
///     five-of-a-kind both pay 100, and the public reconstruction assigns that line to Mixed 7.
///     Ties are otherwise a coin flip, and a coin flip is not reproducible.
///
/// Minimum run length is not a rule here either. A category pays at a length exactly when
/// its pay table has a non-zero entry there, which is how Orca Dive pays a lone wild at
/// one of a kind while everything else needs three.
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
    /// Total pay multiplier over every payline, in hundredths of the TOTAL SPIN BET. Lines
    /// are independent pays that add, which is standard and is also what makes the analytic
    /// EV a plain sum over lines.
    ///
    /// BASIS, stated once here because it is load-bearing everywhere else that reads a
    /// <see cref="LineWin.Multiplier"/>: every payline's compiled multiplier is scaled
    /// against the SAME total spin wager (<see cref="Money.Millicents.ScaledMultiply"/> is
    /// always called with this sum, never a per-line share of the wager), and this method
    /// sums every winning line's multiplier before that one scaling happens. Traditional
    /// multiline paytables often state pays as multiples of one line's bet; this engine
    /// does not model that division, so a
    /// declared multiplier of 5000 always means 5000X the TOTAL spin wager, on every line,
    /// with no per-line share taken first. For a single-payline game (every shipped game
    /// today) the two are the same number, which is why this basis has never visibly
    /// mattered — it will the moment a multi-payline JSON game is authored.
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
