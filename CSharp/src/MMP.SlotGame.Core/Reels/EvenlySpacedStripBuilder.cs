namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// Creates a deterministic demo strip when only symbol counts are known. Do not use this
/// builder when a PAR sheet gives the exact stop order; preserve that published order and
/// pass it directly to <see cref="StripReelSet"/>.
/// </summary>
public static class EvenlySpacedStripBuilder
{
    /// <summary>
    /// Turns symbol counts into an ordered strip by spreading each symbol's copies around
    /// the reel as evenly as possible.
    ///
    /// Counts determine single-position probabilities: 2 Pearls on a 10-stop strip give
    /// Pearl a 2/10 chance at one visible position. Counts do not determine neighboring
    /// symbols. Neighbor order matters when two or more positions from the same reel are
    /// visible, so inventing an order is a real modeling choice.
    ///
    /// For the built-in demos, even spacing avoids putting all copies of a symbol into one
    /// accidental cluster. Each copy receives a temporary position between 0 and 1. Two
    /// Pearls land at 0.25 and 0.75; one Shell lands at 0.50. Sorting those marks produces
    /// Pearl, Shell, Pearl. The marks only establish order. They are not probabilities and
    /// are not stored in the finished strip.
    ///
    /// This policy is deterministic: the same counts always produce the same order. That
    /// makes seeded demonstrations repeatable. It is not evidence that a real game's reel
    /// used this spacing, which is why exact PAR strips take the separate direct path.
    /// </summary>
    /// <returns>A new array containing one symbol for every generated reel stop.</returns>
    public static Symbol[] Build(IReadOnlyList<(Symbol Symbol, int Count)> symbolCounts)
    {
        ArgumentNullException.ThrowIfNull(symbolCounts);
        if (symbolCounts.Count == 0)
            throw new ArgumentException("A generated strip needs at least one symbol.", nameof(symbolCounts));
        if (symbolCounts.Any(entry => entry.Count <= 0))
            throw new ArgumentException("Every symbol count must be positive.", nameof(symbolCounts));

        var stopCount = symbolCounts.Sum(entry => entry.Count);
        var placements = new List<(double Position, Symbol Symbol)>(stopCount);

        foreach (var (symbol, count) in symbolCounts)
        {
            for (var copy = 0; copy < count; copy++)
            {
                // The half-step centers each copy in its equal share of the 0..1 ruler.
                // Three copies land at 1/6, 3/6, and 5/6 instead of at the boundaries.
                var position = (copy + 0.5) / count;
                placements.Add((position, symbol));
            }
        }

        return placements
            .OrderBy(placement => placement.Position)
            .ThenBy(placement => placement.Symbol.Id)
            .Select(placement => placement.Symbol)
            .ToArray();
    }
}
