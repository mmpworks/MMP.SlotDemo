using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Games;

/// <summary>
/// Resolves a precomputed outcome one reel at a time. Each level is one flat integer
/// array. Its index is (current state × this reel's stop count) + stop number. A value of
/// -1 means no later reel can produce a line pay or feature trigger from that prefix.
/// </summary>
public sealed class ProgressiveOutcomeTable
{
    private readonly int[][] _transitions;
    private readonly int[]? _firstPairStates;
    private readonly WinningOutcome?[] _terminalOutcomes;
    private readonly int[] _stopCounts;

    private ProgressiveOutcomeTable(
        int[][] transitions,
        int[]? firstPairStates,
        WinningOutcome?[] terminalOutcomes,
        int[] stopCounts,
        int[] survivingPrefixCounts)
    {
        _transitions = transitions;
        _firstPairStates = firstPairStates;
        _terminalOutcomes = terminalOutcomes;
        _stopCounts = stopCounts;
        SurvivingPrefixCounts = Array.AsReadOnly(survivingPrefixCounts);
    }

    /// <summary>
    /// Number of prefixes that can still pay or trigger after each reel. Index zero is the
    /// count after reel 0; the last value is the number of stored final outcomes.
    /// </summary>
    public IReadOnlyList<int> SurvivingPrefixCounts { get; }

    /// <summary>
    /// Walks the prefix table. False means this stop combination neither pays nor starts a
    /// feature. The caller still draws every reel before calling this method, preserving the
    /// random stream even when an early prefix is dead.
    /// </summary>
    public bool TryGetValue(ReadOnlySpan<byte> stops, out WinningOutcome? outcome)
    {
        if (stops.Length != _stopCounts.Length)
            throw new ArgumentException(
                $"Expected {_stopCounts.Length} reel stops; received {stops.Length}.", nameof(stops));

        var state = 0;
        var firstTransitionReel = 0;
        if (_firstPairStates is not null)
        {
            if (stops[0] >= _stopCounts[0])
                throw new ArgumentOutOfRangeException(nameof(stops), stops[0], "Stop is outside reel 1.");
            if (stops[1] >= _stopCounts[1])
                throw new ArgumentOutOfRangeException(nameof(stops), stops[1], "Stop is outside reel 2.");

            state = _firstPairStates[stops[0] * _stopCounts[1] + stops[1]];
            if (state < 0)
            {
                outcome = null;
                return false;
            }
            firstTransitionReel = 2;
        }

        for (var reel = firstTransitionReel; reel < _transitions.Length; reel++)
        {
            var stop = stops[reel];
            if (stop >= _stopCounts[reel])
                throw new ArgumentOutOfRangeException(nameof(stops), stop, $"Stop is outside reel {reel + 1}.");

            state = _transitions[reel][state * _stopCounts[reel] + stop];
            if (state < 0)
            {
                outcome = null;
                return false;
            }
        }

        var lastReel = _stopCounts.Length - 1;
        var lastStop = stops[lastReel];
        if (lastStop >= _stopCounts[lastReel])
            throw new ArgumentOutOfRangeException(nameof(stops), lastStop, $"Stop is outside reel {lastReel + 1}.");

        outcome = _terminalOutcomes[state * _stopCounts[lastReel] + lastStop];
        return outcome is not null;
    }

    internal static ProgressiveOutcomeTable Build(WinningOutcomeTable source, StripReelSet reels)
    {
        var reelCount = reels.ReelCount;
        var stopCounts = new int[reelCount];
        for (var reel = 0; reel < reelCount; reel++) stopCounts[reel] = reels.StopCount(reel);

        // A map exists for every non-terminal prefix depth. It is used only while building;
        // runtime reads the compact flat arrays below.
        var prefixMaps = new Dictionary<ulong, int>[reelCount];
        prefixMaps[0] = new Dictionary<ulong, int> { [0] = 0 };
        for (var depth = 1; depth < reelCount; depth++) prefixMaps[depth] = [];

        foreach (var entry in source.Entries)
        {
            ulong prefix = 0;
            for (var depth = 1; depth < reelCount; depth++)
            {
                prefix = (prefix << 8) | StopAt(entry.Key, reelCount, depth - 1);
                var map = prefixMaps[depth];
                if (!map.ContainsKey(prefix)) map.Add(prefix, map.Count);
            }
        }

        var transitions = new int[reelCount - 1][];
        for (var reel = 0; reel < transitions.Length; reel++)
        {
            transitions[reel] = new int[checked(prefixMaps[reel].Count * stopCounts[reel])];
            Array.Fill(transitions[reel], -1);
        }

        var terminalOutcomes = new WinningOutcome?[checked(prefixMaps[^1].Count * stopCounts[^1])];
        foreach (var entry in source.Entries)
        {
            ulong prefix = 0;
            var state = 0;
            for (var reel = 0; reel < reelCount - 1; reel++)
            {
                var stop = StopAt(entry.Key, reelCount, reel);
                prefix = (prefix << 8) | stop;
                var nextState = prefixMaps[reel + 1][prefix];
                transitions[reel][state * stopCounts[reel] + stop] = nextState;
                state = nextState;
            }

            var lastStop = StopAt(entry.Key, reelCount, reelCount - 1);
            terminalOutcomes[state * stopCounts[^1] + lastStop] = entry.Value;
        }

        int[]? firstPairStates = null;
        if (reelCount >= 3)
        {
            firstPairStates = new int[checked(stopCounts[0] * stopCounts[1])];
            Array.Fill(firstPairStates, -1);
            for (var firstStop = 0; firstStop < stopCounts[0]; firstStop++)
            {
                var afterFirst = transitions[0][firstStop];
                if (afterFirst < 0) continue;
                for (var secondStop = 0; secondStop < stopCounts[1]; secondStop++)
                {
                    firstPairStates[firstStop * stopCounts[1] + secondStop] =
                        transitions[1][afterFirst * stopCounts[1] + secondStop];
                }
            }
        }

        var surviving = new int[reelCount];
        for (var depth = 1; depth < reelCount; depth++) surviving[depth - 1] = prefixMaps[depth].Count;
        surviving[^1] = source.StoredOutcomeCount;

        return new ProgressiveOutcomeTable(
            transitions,
            firstPairStates,
            terminalOutcomes,
            stopCounts,
            surviving);
    }

    private static byte StopAt(ulong key, int reelCount, int reel) =>
        (byte)(key >> (8 * (reelCount - reel - 1)));
}
