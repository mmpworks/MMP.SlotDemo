using MMP.SlotGame.Core.Money;

namespace MMP.SlotGame.Core.Paytables;

/// <summary>
/// Canonical paytable: pay multipliers (of the TOTAL spin bet, not a single line's
/// share of it — see <see cref="PaytableSolver.Solve"/>'s wager doc comment) for
/// k-of-a-kind runs, k ≥ 3, left-to-right. Dimensionless — the solver turns this into
/// integer millicents via one scalar, <c>paytableScaleFactor</c>.
/// </summary>
public sealed record Paytable
{
    public Paytable(IReadOnlyDictionary<(byte SymbolId, int Count), double> pays)
    {
        ArgumentNullException.ThrowIfNull(pays);
        Pays = new System.Collections.ObjectModel.ReadOnlyDictionary<(byte SymbolId, int Count), double>(
            new Dictionary<(byte SymbolId, int Count), double>(pays));
    }

    public IReadOnlyDictionary<(byte SymbolId, int Count), double> Pays { get; }

    /// <summary>
    /// The v1 preset/solver pipeline's minimum k-of-a-kind that pays anything — the
    /// "no pair" rule of a classic/video slot. <see cref="CanonicalFor"/> never
    /// generates an entry below this, and <see cref="Paylines.LinePayEvaluator"/>'s
    /// own run-length gate must stay at the same value: lowering one without the
    /// other either creates pay entries that can never be reached (paytable ahead of
    /// the evaluator) or a run length the evaluator would pay with nothing in the
    /// table to look up (evaluator ahead of the paytable). This governs only the
    /// preset/solver pipeline, not the JSON <c>GameDefinition</c> path, where a pay
    /// table entry pays at whatever run length its own data declares.
    /// </summary>
    public const int MinimumWinningRun = 3;

    public double PayFor(byte symbolId, int count) =>
        Pays.GetValueOrDefault((symbolId, count));

    /// <summary>
    /// Canonical multipliers per symbol set: premiums pay steep, commons pay shallow.
    /// <see cref="PaytableSolver"/> scales the whole table to hit target RTP, so the
    /// ratios between these entries are what carry over.
    /// </summary>
    public static Paytable CanonicalFor(int reelCount, int symbolCount)
    {
        var pays = new Dictionary<(byte, int), double>();
        for (byte s = 0; s < symbolCount; s++)
        {
            // Teaching curve: symbol 0 is the premium and later symbols pay less.
            var basePay = 60.0 / Math.Pow(2.2, s);
            for (var k = MinimumWinningRun; k <= reelCount; k++)
            {
                // each extra matching reel roughly 5×
                pays[(s, k)] = basePay * Math.Pow(5, k - MinimumWinningRun);
            }
        }
        return new Paytable(pays);
    }
}

/// <summary>
/// Payout transform produced by the solver as a closure over
/// <c>paytableScaleFactor</c>.
/// </summary>
public delegate Millicents PayoutScaler(double rawPayMultiplier);

/// <summary>
/// The realized game: integer-millicent pays. The analytic calculator and the spin
/// evaluator both read this instance, so they share one rounding residual.
/// </summary>
public sealed record ScaledPaytable
{
    private readonly Millicents[] _densePays;
    private readonly int _countStride;

    public ScaledPaytable(IReadOnlyDictionary<(byte SymbolId, int Count), Millicents> pays)
    {
        ArgumentNullException.ThrowIfNull(pays);
        Pays = new System.Collections.ObjectModel.ReadOnlyDictionary<(byte SymbolId, int Count), Millicents>(
            new Dictionary<(byte SymbolId, int Count), Millicents>(pays));

        var maxSymbol = pays.Count == 0 ? 0 : pays.Keys.Max(key => key.SymbolId);
        var maxCount = pays.Count == 0 ? 0 : pays.Keys.Max(key => key.Count);
        _countStride = maxCount + 1;
        _densePays = new Millicents[(maxSymbol + 1) * _countStride];
        foreach (var (key, value) in pays)
            _densePays[key.SymbolId * _countStride + key.Count] = value;
    }

    public IReadOnlyDictionary<(byte SymbolId, int Count), Millicents> Pays { get; }

    public Millicents PayFor(byte symbolId, int count)
    {
        if ((uint)count >= (uint)_countStride) return Millicents.Zero;
        var index = symbolId * _countStride + count;
        return (uint)index < (uint)_densePays.Length ? _densePays[index] : Millicents.Zero;
    }
}
