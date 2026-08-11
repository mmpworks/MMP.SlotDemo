namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// The v1 configuration surface for slot geometry is preset selection (RT-25): a fixed,
/// enumerable set of popular real-world shapes. This record is the AIF seam for custom
/// shapes — the shape is extensible; the free-form editing surface is not built.
///
/// Strips are built DETERMINISTICALLY from the weighted spec: each symbol's weight
/// count is spread round-robin across the strip (even spacing, fixed layout). Same
/// preset → same strip, always — the strip layout is part of the reproducibility
/// contract, not a random artifact.
/// </summary>
public sealed record ReelPreset(
    string Name,
    int ReelCount,
    int StopsPerReel,
    IReadOnlyList<(Symbol Symbol, int Weight)> SymbolWeights,
    IReadOnlyList<Payline> Paylines)
{
    /// <summary>All v1 presets, keyed by name. DRY: one table, no per-preset classes.</summary>
    public static IReadOnlyDictionary<string, ReelPreset> All { get; } = BuildAll();

    public StripReelSet BuildReels()
    {
        var strips = new Symbol[ReelCount][];
        for (var reel = 0; reel < ReelCount; reel++)
            strips[reel] = BuildStrip();
        return new StripReelSet(strips);
    }

    /// <summary>
    /// Even-interleave expansion: lay each symbol's copies at evenly spaced fractional
    /// positions, then stable-sort by position. Deterministic, spreads every symbol
    /// around the cycle (no accidental adjacency clumps), no RNG involved.
    /// </summary>
    private Symbol[] BuildStrip()
    {
        var totalWeight = SymbolWeights.Sum(sw => sw.Weight);
        if (totalWeight != StopsPerReel)
            throw new InvalidOperationException(
                $"Preset '{Name}': weights sum to {totalWeight}, expected {StopsPerReel}.");

        return SymbolWeights
            .SelectMany(sw => Enumerable.Range(0, sw.Weight)
                .Select(i => (Position: (i + 0.5) / sw.Weight, sw.Symbol)))
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Symbol.Id)   // stable tie-break → fully deterministic
            .Select(x => x.Symbol)
            .ToArray();
    }

    private static Dictionary<string, ReelPreset> BuildAll()
    {
        // Classic symbol set (8 distinct: 7s, bars, bells, cherries, blanks).
        var seven = new Symbol(0, "Seven");
        var bar3 = new Symbol(1, "TripleBar");
        var bar2 = new Symbol(2, "DoubleBar");
        var bar1 = new Symbol(3, "Bar");
        var bell = new Symbol(4, "Bell");
        var cherry = new Symbol(5, "Cherry");
        var lemon = new Symbol(6, "Lemon");
        var blank = new Symbol(7, "Blank");

        // Video symbol set (9 distinct: 4 premiums + 5 royals). No wild, no scatter in v1.
        var gem = new Symbol(0, "Gem");
        var crown = new Symbol(1, "Crown");
        var ring = new Symbol(2, "Ring");
        var coin = new Symbol(3, "Coin");
        var ace = new Symbol(4, "A");
        var king = new Symbol(5, "K");
        var queen = new Symbol(6, "Q");
        var jack = new Symbol(7, "J");
        var ten = new Symbol(8, "T");

        // Classic weights for a 22-stop strip (canonical mechanical count).
        var classic22 = new (Symbol, int)[]
        {
            (seven, 1), (bar3, 2), (bar2, 2), (bar1, 3), (bell, 3), (cherry, 3), (lemon, 3), (blank, 5),
        };
        var classic32 = new (Symbol, int)[]
        {
            (seven, 1), (bar3, 2), (bar2, 3), (bar1, 5), (bell, 5), (cherry, 5), (lemon, 5), (blank, 6),
        };
        // Video weights: premiums rare, royals common.
        var video64 = new (Symbol, int)[]
        {
            (gem, 3), (crown, 4), (ring, 5), (coin, 6), (ace, 8), (king, 9), (queen, 9), (jack, 10), (ten, 10),
        };
        var video72 = new (Symbol, int)[]
        {
            (gem, 3), (crown, 4), (ring, 6), (coin, 7), (ace, 9), (king, 10), (queen, 10), (jack, 11), (ten, 12),
        };
        var video128 = new (Symbol, int)[]
        {
            (gem, 5), (crown, 7), (ring, 10), (coin, 12), (ace, 17), (king, 18), (queen, 19), (jack, 20), (ten, 20),
        };

        var presets = new[]
        {
            new ReelPreset("Classic3", 3, 22, classic22, Payline.For(3, 5, StripReelSet.DefaultRows)),
            new ReelPreset("Video3", 3, 32, classic32, Payline.For(3, 5, StripReelSet.DefaultRows)),
            new ReelPreset("Line4", 4, 72, video72, Payline.For(4, 9, StripReelSet.DefaultRows)),
            new ReelPreset("Video5x64", 5, 64, video64, Payline.For(5, 9, StripReelSet.DefaultRows)),
            new ReelPreset("Video5x128", 5, 128, video128, Payline.For(5, 9, StripReelSet.DefaultRows)),
        };
        return presets.ToDictionary(p => p.Name);
    }
}
