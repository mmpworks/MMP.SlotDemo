using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Rtp;

/// <summary>
/// Calculates RTP and standard deviation from the reel strips and paytable. These methods
/// use probabilities instead of random spins, so they return the same answer on every call.
///
/// A single line can be calculated from each reel's symbol frequencies. Several lines need
/// an extra calculation because two lines can read different rows of the same reel. Those
/// two cells are connected by their positions on the strip and are not independent.
/// </summary>
public static class AnalyticMath
{
    /// <summary>
    /// Calculates the average base-game payout from the original multiplier table, before
    /// the solver rounds pays to millicents. The result is a multiple of the total spin wager.
    /// For example, 0.72 means an average base-game return of 72%.
    /// </summary>
    public static double BaseEvMultiplier(StripReelSet reels, IReadOnlyList<Payline> lines, Paytable canonical)
    {
        var ev = 0.0;
        foreach (var line in lines)
        {
            foreach (var ((symbolId, count), pay) in canonical.Pays)
                ev += pay * ExactlyKLeading(reels, line, symbolId, count);
        }
        return ev;
    }

    /// <summary>
    /// Calculates base-game RTP from the rounded millicent pays that the game will actually
    /// use. It recalculates the result because rounding each pay can move the final RTP slightly
    /// away from the requested target.
    /// </summary>
    public static double RealizedBaseRtp(StripReelSet reels, IReadOnlyList<Payline> lines, ScaledPaytable scaled, Millicents wager)
    {
        var ev = 0.0;
        foreach (var line in lines)
        {
            foreach (var ((symbolId, count), pay) in scaled.Pays)
                ev += pay.Value * ExactlyKLeading(reels, line, symbolId, count);
        }
        return ev / wager.Value;
    }

    /// <summary>
    /// Returns the chance that a line begins with exactly <paramref name="k"/> copies of one
    /// symbol. The first k reels must match. If another reel follows, it must not match.
    /// Because the reels spin independently, their probabilities are multiplied.
    /// </summary>
    public static double ExactlyKLeading(StripReelSet reels, Payline line, byte symbolId, int k)
    {
        var p = 1.0;
        for (var reel = 0; reel < k; reel++)
            p *= reels.ProbabilityOf(reel, symbolId);
        if (k < reels.ReelCount)
            p *= 1.0 - reels.ProbabilityOf(k, symbolId);
        return p;
    }

    /// <summary>
    /// Calculates the standard deviation of one spin's total return, divided by the wager.
    /// It includes each line's variance and the covariance between line pairs. Covariance is
    /// needed because two paylines can read cells from the same reel. Version 1 features are
    /// independent of the reel window and of each other, so their variances can be added.
    /// </summary>
    public static double SigmaPerUnitWagered(
        StripReelSet reels,
        IReadOnlyList<Payline> lines,
        ScaledPaytable scaled,
        IReadOnlyList<Features.FeatureSchedule> features,
        Millicents wager)
    {
        var joints = JointRowSymbolTables.Build(reels);
        var w = (double)wager.Value;

        // Record the average payout and average squared payout for each line.
        var lineMean = new double[lines.Count];
        var lineMeanSq = new double[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            foreach (var ((symbolId, count), pay) in scaled.Pays)
            {
                var p = ExactlyKLeading(reels, lines[i], symbolId, count);
                lineMean[i] += pay.Value * p;
                lineMeanSq[i] += (double)pay.Value * pay.Value * p;
            }
        }

        var variance = 0.0;
        for (var i = 0; i < lines.Count; i++)
            variance += lineMeanSq[i] - lineMean[i] * lineMean[i];

        for (var i = 0; i < lines.Count; i++)
        {
            for (var j = i + 1; j < lines.Count; j++)
            {
                var eProduct = ExpectedPairProduct(reels, joints, lines[i], lines[j], scaled);
                variance += 2.0 * (eProduct - lineMean[i] * lineMean[j]);
            }
        }

        foreach (var f in features)
            variance += f.VarianceMillicentsSquared();

        return Math.Sqrt(variance) / w;
    }

    /// <summary>
    /// Calculates the average product of two lines' payouts. This value is used to find
    /// their covariance. Every pair of paying outcomes is multiplied by the chance that
    /// both outcomes occur on the same spin.
    /// </summary>
    private static double ExpectedPairProduct(
        StripReelSet reels,
        JointRowSymbolTables joints,
        Payline lineA,
        Payline lineB,
        ScaledPaytable scaled)
    {
        var total = 0.0;
        foreach (var ((symA, runA), payA) in scaled.Pays)
        {
            if (payA.Value == 0) continue;
            foreach (var ((symB, runB), payB) in scaled.Pays)
            {
                if (payB.Value == 0) continue;
                var p = JointRunProbability(reels, joints, lineA, symA, runA, lineB, symB, runB);
                if (p > 0)
                    total += (double)payA.Value * payB.Value * p;
            }
        }
        return total;
    }

    /// <summary>
    /// Returns the chance that two specified line wins occur together. On each reel, each
    /// line may need its symbol to match, not match, or no longer matter after its run ends.
    /// The per-reel chances are multiplied because separate reels are independent.
    /// </summary>
    private static double JointRunProbability(
        StripReelSet reels,
        JointRowSymbolTables joints,
        Payline lineA, byte symA, int runA,
        Payline lineB, byte symB, int runB)
    {
        var p = 1.0;
        for (var reel = 0; reel < reels.ReelCount && p > 0; reel++)
        {
            // Before a run ends, the cell must match. The next cell must differ.
            // Cells after the run ends do not affect that line's win.
            var condA = reel < runA ? Cond.Match : reel == runA ? Cond.Mismatch : Cond.Any;
            var condB = reel < runB ? Cond.Match : reel == runB ? Cond.Mismatch : Cond.Any;
            p *= joints.Probability(reel, lineA.Rows[reel], lineB.Rows[reel], condA, symA, condB, symB);
        }
        return p;
    }

    internal enum Cond { Match, Mismatch, Any }

    /// <summary>
    /// Stores the chance of seeing two symbols at two visible rows of the same reel.
    /// The tables are built by checking every stop on each reel. When both paylines use
    /// the same row, both symbols come from the same cell and the table records that naturally.
    /// </summary>
    internal sealed class JointRowSymbolTables
    {
        private readonly double[][,][,] _tables; // [reel][rowA,rowB][symbolA,symbolB]
        private readonly double[][][] _marginals; // [reel][row][sym]
        private readonly int _symbolCount;

        /// <summary>Stores the completed probability tables and the number of symbol ids they cover.</summary>
        private JointRowSymbolTables(double[][,][,] tables, double[][][] marginals, int symbolCount)
        {
            _tables = tables;
            _marginals = marginals;
            _symbolCount = symbolCount;
        }

        /// <summary>
        /// Checks every stop on every reel and builds the single-cell and two-cell probability
        /// tables used by the line-pair calculation.
        /// </summary>
        public static JointRowSymbolTables Build(StripReelSet reels)
        {
            var symbolCount = 0;
            for (var reel = 0; reel < reels.ReelCount; reel++)
            {
                foreach (var s in reels.Strip(reel))
                    symbolCount = Math.Max(symbolCount, s.Id + 1);
            }

            var rows = reels.Rows;
            var tables = new double[reels.ReelCount][,][,];
            var marginals = new double[reels.ReelCount][][];
            for (var reel = 0; reel < reels.ReelCount; reel++)
            {
                var strip = reels.Strip(reel);
                var n = strip.Length;
                tables[reel] = new double[rows, rows][,];
                marginals[reel] = new double[rows][];
                for (var rowA = 0; rowA < rows; rowA++)
                {
                    marginals[reel][rowA] = new double[symbolCount];
                    for (var rowB = 0; rowB < rows; rowB++)
                        tables[reel][rowA, rowB] = new double[symbolCount, symbolCount];
                }

                for (var stop = 0; stop < n; stop++)
                {
                    for (var rowA = 0; rowA < rows; rowA++)
                    {
                        var a = strip[(stop + rowA) % n].Id;
                        marginals[reel][rowA][a] += 1.0 / n;
                        for (var rowB = 0; rowB < rows; rowB++)
                        {
                            var b = strip[(stop + rowB) % n].Id;
                            tables[reel][rowA, rowB][a, b] += 1.0 / n;
                        }
                    }
                }
            }
            return new JointRowSymbolTables(tables, marginals, symbolCount);
        }

        /// <summary>
        /// Returns the chance that two cells on one reel satisfy their requested match rules.
        /// Each rule may require a symbol, require a different symbol, or accept any symbol.
        /// </summary>
        public double Probability(int reel, int rowA, int rowB, Cond condA, byte symA, Cond condB, byte symB)
        {
            // Derive mismatch cases by subtracting matching cases from the stored probabilities.
            return (condA, condB) switch
            {
                (Cond.Any, Cond.Any) => 1.0,
                (Cond.Any, _) => Single(reel, rowB, condB, symB),
                (_, Cond.Any) => Single(reel, rowA, condA, symA),
                (Cond.Match, Cond.Match) => Joint(reel, rowA, rowB, symA, symB),
                (Cond.Match, Cond.Mismatch) => _marginals[reel][rowA][symA] - Joint(reel, rowA, rowB, symA, symB),
                (Cond.Mismatch, Cond.Match) => _marginals[reel][rowB][symB] - Joint(reel, rowA, rowB, symA, symB),
                (Cond.Mismatch, Cond.Mismatch) =>
                    1.0 - _marginals[reel][rowA][symA] - _marginals[reel][rowB][symB] + Joint(reel, rowA, rowB, symA, symB),
                _ => throw new ArgumentOutOfRangeException(nameof(condA)),
            };
        }

        /// <summary>Returns the chance that one cell matches, or does not match, a symbol.</summary>
        private double Single(int reel, int row, Cond cond, byte sym) =>
            cond == Cond.Match ? _marginals[reel][row][sym] : 1.0 - _marginals[reel][row][sym];

        /// <summary>Returns the chance of seeing both requested symbols at the two rows.</summary>
        private double Joint(int reel, int rowA, int rowB, byte symA, byte symB) =>
            symA < _symbolCount && symB < _symbolCount ? _tables[reel][rowA, rowB][symA, symB] : 0.0;
    }
}
