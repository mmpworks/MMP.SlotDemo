using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Money;

namespace MMP.SlotGame.Core.Games;

/// <summary>
/// Calculates the exact return of a loaded, single-payline game without running random spins.
/// It tries every symbol combination that can appear on the payline. Each combination is
/// counted as many times as it appears on the physical reel strips.
///
/// For example, suppose the first reel has four cherry stops and two bell stops. A combination
/// beginning with a cherry represents four times as many reel stops as one beginning with a
/// bell. Keeping that count produces the same result as checking every stop, with less work.
///
/// The analyzer also counts stops that show the bonus scatter anywhere in the visible window.
/// This is necessary because the line win and the bonus trigger can happen on the same spin.
///
/// This class supports any number of reels, but stores one payline result and its overlap with
/// the bonus. Games with several paylines still simulate correctly. Their exact variance also
/// needs every line-to-line and line-to-bonus covariance pair, which this analyzer does not yet
/// store.
/// </summary>
public static class GameAnalyzer
{
    /// <summary>
    /// Maximum number of payline-symbol combinations the analyzer will check. Larger inputs
    /// are rejected because they are likely to take too long or indicate a bad game definition.
    /// </summary>
    public const long MaxEnumeration = 200_000_000;

    /// <summary>
    /// Validates that the game has one payline, then calculates its exact return and variance.
    /// No random-number generator is used.
    /// </summary>
    public static GameAnalysis Analyze(GameDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Paylines.Count != 1)
            throw new NotSupportedException(
                $"Game '{definition.Name}' has {definition.Paylines.Count} paylines; exact analysis covers "
                + "single-payline games. Multi-line games simulate correctly but are not analyzed here yet.");

        return new Enumeration(definition).Run();
    }

    /// <summary>
    /// Counts one game's supported outcomes and stores the tallies until the report is ready.
    /// Keeping the tallies here avoids passing wins, payouts, and trigger counts through every
    /// recursive call.
    /// </summary>
    private sealed class Enumeration
    {
        private readonly GameDefinition _definition;
        private readonly WinEvaluator _evaluator;
        private readonly int[][] _anyStop;
        private readonly int[][] _triggerStop;
        private readonly byte[] _cells;
        private readonly Dictionary<(int CategoryIndex, int Count), long> _counts = [];

        private long _hits, _payUnits, _paySquareUnits, _payTriggerUnits, _triggerWeight;

        /// <summary>
        /// Prepares the win evaluator, reusable payline buffer, and per-reel symbol counts.
        /// It also rejects a game whose analysis would exceed <see cref="MaxEnumeration"/>.
        /// </summary>
        public Enumeration(GameDefinition definition)
        {
            _definition = definition;
            _evaluator = new WinEvaluator(definition);
            _cells = new byte[definition.ReelCount];
            (_anyStop, _triggerStop) = BuildWeights(definition);
            GuardEnumerationSize();
        }

        /// <summary>
        /// Checks every possible payline-symbol combination and converts the resulting counts
        /// into RTP, hit frequency, trigger frequency, and standard deviation.
        /// </summary>
        public GameAnalysis Run()
        {
            Descend(reel: 0, weight: 1, triggerWeight: 1);
            return Summarize();
        }

        /// <summary>
        /// Multiplies the number of distinct symbols on each reel to estimate how many
        /// combinations <see cref="Descend"/> must visit. Stops that show the same symbol count
        /// as one branch because their frequency is stored in that branch's weight.
        /// </summary>
        private void GuardEnumerationSize()
        {
            long space = 1;
            foreach (var reel in _anyStop)
            {
                var present = reel.Count(count => count > 0);
                space *= present;
                if (space > MaxEnumeration)
                    throw new NotSupportedException(
                        $"Game '{_definition.Name}' would enumerate more than {MaxEnumeration:N0} symbol "
                        + "combinations. Check the reel and symbol counts.");
            }
        }

        /// <summary>
        /// Chooses one possible payline symbol for the current reel, then moves to the next reel.
        /// <paramref name="weight"/> records how many physical stop combinations produce the
        /// choices made so far. For example, choosing a symbol that appears four times multiplies
        /// the current weight by four. After every reel has a choice, <see cref="Accumulate"/>
        /// evaluates that completed payline.
        /// </summary>
        private void Descend(int reel, long weight, long triggerWeight)
        {
            if (reel == _definition.ReelCount)
            {
                Accumulate(weight, triggerWeight);
                return;
            }

            var any = _anyStop[reel];
            var trigger = _triggerStop[reel];
            for (byte symbol = 0; symbol < any.Length; symbol++)
            {
                if (any[symbol] == 0) continue;
                _cells[reel] = symbol;
                Descend(reel + 1, weight * any[symbol], triggerWeight * trigger[symbol]);
            }
        }

        /// <summary>
        /// Evaluates one completed payline and adds its weighted results to the running totals.
        /// A combination with weight 12 contributes the same result as 12 individual reel-stop
        /// combinations. Payout multipliers remain scaled integers here to avoid rounding.
        /// </summary>
        private void Accumulate(long weight, long triggerWeight)
        {
            _triggerWeight += triggerWeight;

            var win = _evaluator.Evaluate(_cells);
            if (!win.IsWin) return;

            var key = (win.CategoryIndex, win.Count);
            _counts[key] = _counts.GetValueOrDefault(key) + weight;
            _hits += weight;

            // win.Multiplier is the real multiplier x Millicents.ScaleFactor (PayCategory.PayFor),
            // so every tally below is at that scale too. Summarize() divides that back out
            // once, at the end, rather than converting per combination.
            //
            // Overflow check for _paySquareUnits, since it is quadratic in the multiplier: at
            // the current ScaleFactor of 100, Orca Dive's richest pay is 5000X (500,000
            // scaled), and no category can win on more combinations than the game has
            // (14,781,416), so even the impossible worst case of every combination paying the
            // top prize bounds this accumulator at 500,000^2 * 14,781,416 ~= 3.70e18 — about
            // 2.5x under long.MaxValue (~9.22e18). The real accumulation is far smaller (only 4
            // combinations pay 5000X at all); this is the conservative bound, and it still
            // clears with room to spare. Raising ScaleFactor narrows this margin quadratically
            // (a 10x scale increase is a 100x tighter bound), so re-check this comment's numbers
            // against the new ScaleFactor before changing it.
            _payUnits += (long)win.Multiplier * weight;
            _paySquareUnits += (long)win.Multiplier * win.Multiplier * weight;
            _payTriggerUnits += (long)win.Multiplier * triggerWeight;
        }

        /// <summary>
        /// Converts the weighted integer totals into the values returned to callers. The average
        /// line pay is total weighted line pay divided by all reel-stop combinations. Bonus RTP
        /// is trigger probability multiplied by the bonus's average award.
        ///
        /// Variance also needs the average squared payout. The formula includes the case where
        /// a line win and bonus trigger occur together; treating them as unrelated would give the
        /// wrong standard deviation. Scaled integer multipliers are converted to doubles only here.
        /// </summary>
        private GameAnalysis Summarize()
        {
            var total = (double)_definition.StopCombinations;
            var scale = (double)Millicents.ScaleFactor;
            var meanLine = _payUnits / scale / total;
            var meanLineSquared = _paySquareUnits / (scale * scale) / total;
            var meanLineTimesTrigger = _payTriggerUnits / scale / total;

            var bonus = _definition.Bonus?.Bonus;
            var bonusMean = bonus?.Mean ?? 0.0;
            var bonusMeanSquared = bonus?.MeanSquared ?? 0.0;
            var triggerProbability = bonus is null ? 0.0 : _triggerWeight / total;

            var bonusRtp = triggerProbability * bonusMean;
            var mean = meanLine + bonusRtp;
            var meanSquared = meanLineSquared
                + 2.0 * meanLineTimesTrigger * bonusMean
                + triggerProbability * bonusMeanSquared;

            return new GameAnalysis
            {
                Definition = _definition,
                CombinationCounts = _counts,
                StopCombinations = _definition.StopCombinations,
                HitCombinations = _hits,
                TriggerCombinations = bonus is null ? 0 : _triggerWeight,
                LineRtp = meanLine,
                BonusRtp = bonusRtp,
                LineSigma = Math.Sqrt(meanLineSquared - meanLine * meanLine),
                BonusSigma = Math.Sqrt(Math.Max(0.0, triggerProbability * bonusMeanSquared - bonusRtp * bonusRtp)),
                SigmaPerUnitWagered = Math.Sqrt(Math.Max(0.0, meanSquared - mean * mean)),
            };
        }

        /// <summary>
        /// Counts two things for every symbol on every reel: stops that place the symbol on the
        /// payline, and stops that also show the bonus scatter somewhere in the visible window.
        /// <see cref="Descend"/> uses these counts as weights instead of visiting identical stops
        /// one at a time.
        /// </summary>
        private static (int[][] AnyStop, int[][] TriggerStop) BuildWeights(GameDefinition definition)
        {
            var reels = definition.Reels;
            var symbolCount = definition.Symbols.Count;
            var payline = definition.Paylines[0];
            var bonus = definition.Bonus;

            var anyStop = new int[reels.ReelCount][];
            var triggerStop = new int[reels.ReelCount][];

            for (var reel = 0; reel < reels.ReelCount; reel++)
            {
                anyStop[reel] = new int[symbolCount];
                triggerStop[reel] = new int[symbolCount];
                var required = bonus is not null && bonus.RequiredReels.Contains(reel);

                for (var stop = 0; stop < reels.StopCount(reel); stop++)
                {
                    var onPayline = reels.At(reel, stop, payline.Rows[reel]).Id;
                    anyStop[reel][onPayline]++;

                    if (required && !reels.WindowContains(reel, stop, bonus!.ScatterSymbolId)) continue;
                    triggerStop[reel][onPayline]++;
                }
            }

            return (anyStop, triggerStop);
        }
    }
}
