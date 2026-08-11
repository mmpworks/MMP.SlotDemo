using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Rtp;

/// <summary>
/// Analytic paytable calculations. They enumerate or combine the modeled outcomes rather
/// than sampling them; probabilities and moments are represented as <see cref="double"/>:
///
///  - Line EV uses the closed form over per-reel marginals. Rows of one line sit on
///    different reels, and reels are independent, so marginals suffice for EV.
///  - Line VARIANCE needs more: two lines share reels, and rows within a reel are
///    correlated by strip adjacency (RT-1). Cov(line i, line j) therefore uses the
///    per-reel JOINT row-pair distribution, obtained by enumerating the S stops per
///    reel (RT-2's method). Joint probability across reels still factorizes, because
///    reels are independent.
///
/// σ here is the analytic, configuration-derived band source for AC-1 (RT-7). The
/// empirical Welford estimate is a cross-check, never the authority.
/// </summary>
public static class AnalyticMath
{
    /// <summary>
    /// The unscaled base-game EV: the canonical (dimensionless) paytable's expected
    /// payout, summed across every payline, in wager-multiplier units. "Unscaled"
    /// because this reads the canonical table directly, before <c>paytableScaleFactor</c>
    /// (<see cref="Paytables.PaytableSolver.Solve"/>) turns it into real millicents.
    /// Summing across lines here — and <see cref="RealizedBaseRtp"/> doing the same
    /// on the scaled table — is what fixes the basis for every RTP number this
    /// pipeline produces: relative to the TOTAL spin wager, not a single line's
    /// share of it.
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
    /// Realized base RTP from the integer paytable actually shipped, recomputed here
    /// rather than trusted from <c>paytableScaleFactor</c>: round-half-even
    /// (<see cref="Paytables.PaytableSolver.Solve"/>) removes systematic rounding bias,
    /// but it does not guarantee the rounded table lands exactly on the target — each
    /// pay rounds independently, so the realized total can drift a hair. This recompute
    /// is the authoritative number; the target RTP is only ever a target.
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
    /// P(line shows exactly k leading copies of symbol s): match reels 0..k-1,
    /// mismatch reel k (or k == ReelCount). Reels independent → product of marginals.
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
    /// Variance of the total per-spin return (base + features), per unit wagered.
    /// Base: Var(Σ lines) = Σ Var + 2 Σ Cov over line pairs.
    /// Features trigger independently of the window and of each other (v1 model,
    /// RT-5 resolution), so their variances simply add.
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

        // Per-line pay distributions in millicents.
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
    /// E[pay_i · pay_j]: sum over both lines' (symbol, exact-run) outcomes of
    /// pay·pay·P(joint). Joint P = product over reels of the per-reel probability that
    /// BOTH lines' cell conditions hold, read from the joint row-pair tables.
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

    private static double JointRunProbability(
        StripReelSet reels,
        JointRowSymbolTables joints,
        Payline lineA, byte symA, int runA,
        Payline lineB, byte symB, int runB)
    {
        var p = 1.0;
        for (var reel = 0; reel < reels.ReelCount && p > 0; reel++)
        {
            // Cell condition per line on this reel: Match (reel < run),
            // Mismatch (reel == run), or Any (reel > run).
            var condA = reel < runA ? Cond.Match : reel == runA ? Cond.Mismatch : Cond.Any;
            var condB = reel < runB ? Cond.Match : reel == runB ? Cond.Mismatch : Cond.Any;
            p *= joints.Probability(reel, lineA.Rows[reel], lineB.Rows[reel], condA, symA, condB, symB);
        }
        return p;
    }

    internal enum Cond { Match, Mismatch, Any }

    /// <summary>
    /// Per reel, per (rowA, rowB) pair: the joint distribution of the two window
    /// cells' symbols, built by one O(S) stop enumeration each. 3×3 row pairs × R
    /// reels, tiny and exact. When rowA == rowB the two cells are the same cell and
    /// the table is automatically diagonal — no special case needed.
    /// </summary>
    internal sealed class JointRowSymbolTables
    {
        private readonly double[][,][,] _tables; // [reel][rowA,rowB][symA,symB] — jagged over reels
        private readonly double[][][] _marginals; // [reel][row][sym]
        private readonly int _symbolCount;

        private JointRowSymbolTables(double[][,][,] tables, double[][][] marginals, int symbolCount)
        {
            _tables = tables;
            _marginals = marginals;
            _symbolCount = symbolCount;
        }

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

        /// <summary>P(cell@rowA satisfies condA vs symA AND cell@rowB satisfies condB vs symB) on one reel.</summary>
        public double Probability(int reel, int rowA, int rowB, Cond condA, byte symA, Cond condB, byte symB)
        {
            // Inclusion–exclusion over the joint == table; marginals cover the Any/Mismatch sides.
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

        private double Single(int reel, int row, Cond cond, byte sym) =>
            cond == Cond.Match ? _marginals[reel][row][sym] : 1.0 - _marginals[reel][row][sym];

        private double Joint(int reel, int rowA, int rowB, byte symA, byte symB) =>
            symA < _symbolCount && symB < _symbolCount ? _tables[reel][rowA, rowB][symA, symB] : 0.0;
    }
}
