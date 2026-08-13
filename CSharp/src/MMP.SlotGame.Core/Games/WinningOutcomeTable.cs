using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Games;

/// <summary>
/// The result already calculated for one useful combination of reel stops. An entry may
/// pay immediately, trigger a feature, or do both. Combinations that do neither are absent.
/// </summary>
public sealed record WinningOutcome(
    int TotalMultiplier,
    IReadOnlyList<Payline> Paylines,
    IReadOnlyList<ScatterPickBonus> TriggeredFeatures);

/// <summary>
/// Precomputes the line wins implied by a validated PAR definition. A key contains one
/// byte per reel stop, in reel order. For five reels, stops 12/28/4/17/25 become
/// 0x0C1C041119. This is an exact encoding, not a hash: different stop combinations cannot
/// produce the same key.
///
/// Construction visits the complete stop cycle once. It stores only wins, so a normal
/// losing spin becomes one failed dictionary lookup. Outcomes with the same payout and
/// winning paylines share one value object instead of allocating the same list repeatedly.
/// </summary>
public sealed class WinningOutcomeTable
{
    /// <summary>One byte per reel in a 64-bit key.</summary>
    public const int MaximumReels = sizeof(ulong);

    /// <summary>A byte can identify stop numbers 0 through 255.</summary>
    public const int MaximumStopsPerReel = byte.MaxValue + 1;

    /// <summary>
    /// Construction enumerates the full physical cycle. Larger games need another table
    /// representation or an offline build step instead of an unbounded startup allocation.
    /// </summary>
    public const long MaximumCombinations = 100_000_000;

    private readonly Dictionary<ulong, WinningOutcome> _outcomes;

    internal IEnumerable<KeyValuePair<ulong, WinningOutcome>> Entries => _outcomes;

    private WinningOutcomeTable(
        Dictionary<ulong, WinningOutcome> outcomes,
        long combinations,
        int winningCombinations,
        int featureTriggerCombinations)
    {
        _outcomes = outcomes;
        CombinationCount = combinations;
        WinningCombinationCount = winningCombinations;
        FeatureTriggerCombinationCount = featureTriggerCombinations;
    }

    /// <summary>Number of physical stop combinations examined during construction.</summary>
    public long CombinationCount { get; }

    /// <summary>Number of stop combinations that pay on at least one payline.</summary>
    public int WinningCombinationCount { get; }

    /// <summary>Number of stop combinations that start the configured feature.</summary>
    public int FeatureTriggerCombinationCount { get; }

    /// <summary>Number of stored keys. A key that both pays and triggers is counted once.</summary>
    public int StoredOutcomeCount => _outcomes.Count;

    /// <summary>Returns the precomputed result; false means no payline pays and no feature starts.</summary>
    public bool TryGetValue(ulong key, out WinningOutcome? outcome) =>
        _outcomes.TryGetValue(key, out outcome);

    /// <summary>
    /// Packs stop numbers from left to right, using one byte for each reel. Reel lengths
    /// may differ, but each individual strip must contain no more than 256 stops.
    /// </summary>
    public static ulong PackKey(ReadOnlySpan<int> stops)
    {
        if (stops.Length is < 1 or > MaximumReels)
            throw new ArgumentOutOfRangeException(
                nameof(stops), $"A packed stop key needs 1..{MaximumReels} reels.");

        ulong key = 0;
        foreach (var stop in stops)
        {
            if ((uint)stop > byte.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(stops), stop, "Each stop number must fit in one byte (0..255).");
            key = (key << 8) | (byte)stop;
        }
        return key;
    }

    internal static WinningOutcomeTable Build(GameDefinition definition)
    {
        var reels = definition.Reels;
        if (reels.ReelCount > MaximumReels)
            throw new ArgumentException(
                $"Game '{definition.Name}' has {reels.ReelCount} reels; the packed outcome key supports at most {MaximumReels}.");
        if (definition.Paylines.Count > sizeof(ulong) * 8)
            throw new ArgumentException(
                $"Game '{definition.Name}' has {definition.Paylines.Count} paylines; the winning-line mask supports at most 64.");

        for (var reel = 0; reel < reels.ReelCount; reel++)
        {
            if (reels.StopCount(reel) > MaximumStopsPerReel)
                throw new ArgumentException(
                    $"Game '{definition.Name}' reel {reel + 1} has {reels.StopCount(reel)} stops; "
                    + $"a one-byte stop key supports at most {MaximumStopsPerReel}.");
        }

        var combinations = definition.StopCombinations;
        if (combinations > MaximumCombinations)
            throw new ArgumentException(
                $"Game '{definition.Name}' has {combinations:N0} stop combinations; this version limits "
                + $"a precomputed outcome table to {MaximumCombinations:N0} combinations.");

        var estimatedWins = (int)Math.Min(combinations / 8, 2_000_000);
        var outcomes = new Dictionary<ulong, WinningOutcome>(estimatedWins);
        var sharedOutcomes = new Dictionary<(int Total, ulong Lines, bool Feature), WinningOutcome>();
        var evaluator = new WinEvaluator(definition);
        var stops = new int[reels.ReelCount];
        var cells = new byte[reels.ReelCount];
        var winningCombinationCount = 0;
        var featureTriggerCombinationCount = 0;

        while (true)
        {
            var total = 0;
            ulong winningLines = 0;

            for (var paylineIndex = 0; paylineIndex < definition.Paylines.Count; paylineIndex++)
            {
                var positions = definition.Paylines[paylineIndex].Rows;
                for (var reel = 0; reel < reels.ReelCount; reel++)
                    cells[reel] = reels.At(reel, stops[reel], positions[reel]).Id;

                var multiplier = evaluator.Evaluate(cells).Multiplier;
                if (multiplier == 0) continue;

                total = checked(total + multiplier);
                winningLines |= 1UL << paylineIndex;
            }

            var triggersFeature = definition.Bonus is not null
                && IsFeatureTriggered(reels, stops, definition.Bonus);
            if (total > 0) winningCombinationCount++;
            if (triggersFeature) featureTriggerCombinationCount++;

            if (total > 0 || triggersFeature)
            {
                var outcomeKey = (total, winningLines, triggersFeature);
                if (!sharedOutcomes.TryGetValue(outcomeKey, out var outcome))
                {
                    var hitLines = new List<Payline>();
                    for (var line = 0; line < definition.Paylines.Count; line++)
                    {
                        if ((winningLines & (1UL << line)) != 0)
                            hitLines.Add(definition.Paylines[line]);
                    }
                    IReadOnlyList<ScatterPickBonus> features = triggersFeature
                        ? Array.AsReadOnly([definition.Bonus!])
                        : Array.Empty<ScatterPickBonus>();
                    outcome = new WinningOutcome(
                        total,
                        Array.AsReadOnly([.. hitLines]),
                        features);
                    sharedOutcomes.Add(outcomeKey, outcome);
                }

                outcomes.Add(PackKey(stops), outcome);
            }

            var reelToAdvance = stops.Length - 1;
            while (reelToAdvance >= 0)
            {
                stops[reelToAdvance]++;
                if (stops[reelToAdvance] < reels.StopCount(reelToAdvance)) break;
                stops[reelToAdvance] = 0;
                reelToAdvance--;
            }
            if (reelToAdvance < 0) break;
        }

        return new WinningOutcomeTable(
            outcomes,
            combinations,
            winningCombinationCount,
            featureTriggerCombinationCount);
    }

    private static bool IsFeatureTriggered(
        StripReelSet reels,
        ReadOnlySpan<int> stops,
        ScatterPickBonus feature)
    {
        foreach (var reel in feature.RequiredReels)
        {
            var seen = false;
            for (var row = 0; row < reels.Rows && !seen; row++)
                seen = reels.At(reel, stops[reel], row).Id == feature.ScatterSymbolId;
            if (!seen) return false;
        }
        return true;
    }
}
