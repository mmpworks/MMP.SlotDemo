using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Paylines;

/// <summary>
/// Turns a drawn window into base-game money: left-to-right k-of-a-kind per payline,
/// k ≥ 3. The preset strips carry no wilds, so substitution never enters here.
///
/// PAY SEMANTICS, and why they differ from <see cref="Games.WinEvaluator"/>: this path pays
/// the entry for the run it reached, with no search for a shorter paying prefix. WinEvaluator
/// does search, because it reads paytables transcribed from real PAR sheets, which may pay at
/// three of a kind and nothing above it.
///
/// The two agree whenever a category's pays are contiguous and non-decreasing in run length,
/// and this path only ever sees tables that are: they come from
/// <see cref="Paytable.CanonicalFor"/>, which generates an increasing curve, scaled by
/// <see cref="Paytables.PaytableSolver"/>, and scaling by a positive factor preserves the
/// ordering. CanonicalPaytableShapeTests pins that. A hand-written table reaching this path
/// would break the equivalence, so it must not.
/// </summary>
public sealed class LinePayEvaluator(IReadOnlyList<Payline> lines, ScaledPaytable paytable)
{
    private readonly Payline[] _lines = [.. lines];

    /// <summary>Window layout: [reel * rows + row], as DrawWindow fills it.</summary>
    public Millicents Evaluate(ReadOnlySpan<Symbol> window, int reelCount, int rows)
    {
        var total = Millicents.Zero;
        foreach (var line in _lines)
        {
            var first = window[0 * rows + line.Rows[0]];
            var run = 1;
            for (var reel = 1; reel < reelCount; reel++)
            {
                if (window[reel * rows + line.Rows[reel]].Id != first.Id)
                    break;
                run++;
            }
            if (run >= Paytable.MinimumWinningRun)
                total += paytable.PayFor(first.Id, run);
        }
        return total;
    }

    /// <summary>Allocation-free simulation path for a window that already contains symbol ids.</summary>
    public Millicents EvaluateIds(ReadOnlySpan<byte> window, int reelCount, int rows)
    {
        var total = Millicents.Zero;
        foreach (var line in _lines)
        {
            var first = window[line.Rows[0]];
            var run = 1;
            for (var reel = 1; reel < reelCount; reel++)
            {
                if (window[reel * rows + line.Rows[reel]] != first)
                    break;
                run++;
            }
            if (run >= Paytable.MinimumWinningRun)
                total += paytable.PayFor(first, run);
        }
        return total;
    }
}
