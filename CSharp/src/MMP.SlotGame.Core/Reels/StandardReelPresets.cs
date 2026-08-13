namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// Built-in reel strips used when no game-definition file is selected. This catalog keeps
/// the demo's historical defaults separate from <see cref="ReelPreset"/>, which is only the
/// data container consumed by a run.
/// </summary>
public static class StandardReelPresets
{
    public static IReadOnlyDictionary<string, ReelPreset> All { get; } = BuildAll();

    private static Dictionary<string, ReelPreset> BuildAll()
    {
        var seven = new Symbol(0, "Seven");
        var bar3 = new Symbol(1, "TripleBar");
        var bar2 = new Symbol(2, "DoubleBar");
        var bar1 = new Symbol(3, "Bar");
        var bell = new Symbol(4, "Bell");
        var cherry = new Symbol(5, "Cherry");
        var lemon = new Symbol(6, "Lemon");
        var blank = new Symbol(7, "Blank");

        var gem = new Symbol(0, "Gem");
        var crown = new Symbol(1, "Crown");
        var ring = new Symbol(2, "Ring");
        var coin = new Symbol(3, "Coin");
        var ace = new Symbol(4, "A");
        var king = new Symbol(5, "K");
        var queen = new Symbol(6, "Q");
        var jack = new Symbol(7, "J");
        var ten = new Symbol(8, "T");

        var classic22 = Spec((seven, 1), (bar3, 2), (bar2, 2), (bar1, 3),
            (bell, 3), (cherry, 3), (lemon, 3), (blank, 5));
        var classic32 = Spec((seven, 1), (bar3, 2), (bar2, 3), (bar1, 5),
            (bell, 5), (cherry, 5), (lemon, 5), (blank, 6));
        var video64 = Spec((gem, 3), (crown, 4), (ring, 5), (coin, 6),
            (ace, 8), (king, 9), (queen, 9), (jack, 10), (ten, 10));
        var video72 = Spec((gem, 3), (crown, 4), (ring, 6), (coin, 7),
            (ace, 9), (king, 10), (queen, 10), (jack, 11), (ten, 12));
        var video128 = Spec((gem, 5), (crown, 7), (ring, 10), (coin, 12),
            (ace, 17), (king, 18), (queen, 19), (jack, 20), (ten, 20));

        ReelPreset[] presets =
        [
            Preset("Classic3", classic22, 3, 5),
            Preset("Video3", classic32, 3, 5),
            Preset("Line4", video72, 4, 9),
            Preset("Video5x64", video64, 5, 9),
            Preset("Video5x128", video128, 5, 9),
        ];
        return presets.ToDictionary(preset => preset.Name);
    }

    // These historical presets know symbol counts but not a published PAR stop order.
    // Generate one stable, evenly spaced order for their repeatable teaching runs.
    private static Symbol[] Spec(params (Symbol Symbol, int Count)[] counts) =>
        EvenlySpacedStripBuilder.Build(counts);

    private static ReelPreset Preset(string name, Symbol[] strip, int reelCount, int lineCount) =>
        new(name, Enumerable.Repeat((IReadOnlyList<Symbol>)strip, reelCount).ToArray(),
            StandardPaylines.For(reelCount, lineCount, StripReelSet.DefaultRows));
}
