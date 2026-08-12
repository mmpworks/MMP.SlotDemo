using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Money;

namespace MMP.SlotGame.Core.Games;

/// <summary>
/// Exact analysis of a loaded game by enumeration over symbol tuples rather than stop
/// tuples: every combination of one symbol per reel, weighted by how many stops produce it.
/// A payline reads one cell per reel, so both enumerations give the same answer. For Orca
/// Dive that is tens of thousands of tuples instead of 14,781,416 stops.
///
/// The scatter reads the whole window rather than a single cell, so it rides through the
/// same enumeration as a second weight: beside each reel's plain symbol counts sits the
/// count of stops that show that symbol on the payline and the scatter somewhere in the
/// window. Multiplying the second weight on the required reels and the first elsewhere
/// gives the joint distribution of (line pay, feature triggered). Line pay and the feature
/// are correlated, because a scatter in the window costs that reel a payline symbol, so a
/// sigma built by adding their variances would be wrong.
///
/// Reel count is a loop bound, not a constant: the enumeration is a recursive descent over
/// however many reels the definition has, and the same code analyses a 3-reel classic and a
/// 5-reel video game.
///
/// Known limit: this analyses single-payline games. Multi-line EV is a plain sum over
/// lines, but multi-line sigma needs the line-pair covariance that
/// <see cref="Rtp.AnalyticMath"/> computes for the wild-free preset games, and combining
/// that with wilds and a window-coupled feature is a separate piece of work. Multi-line
/// definitions still simulate correctly; analysis here does not yet reach them.
/// </summary>
public static class GameAnalyzer
{
    /// <summary>
    /// Enumeration is the product of the distinct symbols present on each reel. A definition
    /// far past this is likely a mistake; the analyzer throws instead of appearing to hang.
    /// </summary>
    public const long MaxEnumeration = 200_000_000;

    public static GameAnalysis Analyse(GameDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Paylines.Count != 1)
            throw new NotSupportedException(
                $"Game '{definition.Name}' has {definition.Paylines.Count} paylines; exact analysis covers "
                + "single-payline games. Multi-line games simulate correctly but are not analysed here yet.");

        return new Enumeration(definition).Run();
    }

    /// <summary>
    /// One analysis in flight. A class rather than a pile of ref parameters because the
    /// descent carries eight accumulators, and a recursive signature would have to thread
    /// all eight through every call.
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

        public Enumeration(GameDefinition definition)
        {
            _definition = definition;
            _evaluator = new WinEvaluator(definition);
            _cells = new byte[definition.ReelCount];
            (_anyStop, _triggerStop) = BuildWeights(definition);
            GuardEnumerationSize();
        }

        public GameAnalysis Run()
        {
            Descend(reel: 0, weight: 1, triggerWeight: 1);
            return Summarise();
        }

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
        /// Walk one reel deeper, multiplying in this symbol weight. Symbols absent from a
        /// reel are skipped, so a game whose reels carry different symbol sets costs nothing
        /// for the ones it does not use.
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

        private void Accumulate(long weight, long triggerWeight)
        {
            _triggerWeight += triggerWeight;

            var win = _evaluator.Evaluate(_cells);
            if (!win.IsWin) return;

            var key = (win.CategoryIndex, win.Count);
            _counts[key] = _counts.GetValueOrDefault(key) + weight;
            _hits += weight;

            // win.Multiplier is the real multiplier x Millicents.ScaleFactor (PayCategory.PayFor),
            // so every tally below is at that scale too. Summarise() divides that back out
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
        /// Turn the integer tallies into returns and sigmas. X = L + T*W, where L is the line
        /// pay, T is the trigger indicator and W is the feature award, independent of
        /// everything once triggered. So E[X] = E[L] + P(T)*E[W] and
        /// E[X^2] = E[L^2] + 2*E[L*T]*E[W] + P(T)*E[W^2]. The middle term is the coupling,
        /// and the enumeration carries its second weight to supply it.
        ///
        /// The tallies accumulate at <see cref="Millicents.ScaleFactor"/> (see
        /// <see cref="Accumulate"/>); L is linear in the multiplier so a single /ScaleFactor
        /// recovers it, and L^2 is quadratic so it needs /ScaleFactor^2. That division happens
        /// here and nowhere else, which keeps <see cref="Games.GameRunner"/>'s simulated pay
        /// and this analytic pay in the same unit throughout.
        /// </summary>
        private GameAnalysis Summarise()
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
        /// Per reel and per symbol: how many stops put that symbol on the payline (anyStop),
        /// and how many of those ALSO show the scatter in the window (triggerStop). Reels the
        /// feature does not require keep the plain count, which is what makes the product in
        /// <see cref="Descend"/> apply the scatter condition to the required reels only.
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

                    if (required && !ScatterInWindow(reels, reel, stop, bonus!.ScatterSymbolId)) continue;
                    triggerStop[reel][onPayline]++;
                }
            }

            return (anyStop, triggerStop);
        }

        private static bool ScatterInWindow(Reels.StripReelSet reels, int reel, int stop, byte scatterId)
        {
            for (var row = 0; row < reels.Rows; row++)
            {
                if (reels.At(reel, stop, row).Id == scatterId) return true;
            }
            return false;
        }
    }
}
