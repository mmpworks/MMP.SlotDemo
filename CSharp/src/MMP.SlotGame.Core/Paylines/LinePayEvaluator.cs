using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Paylines;

/// <summary>
/// Turns a drawn window into base-game money: left-to-right k-of-a-kind per payline,
/// k ≥ 3. The preset strips carry no wilds, so substitution never enters here.
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
