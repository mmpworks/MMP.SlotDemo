using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Reels;

namespace SlotDemo.Server.Chapters;

/// <summary>
/// One resolver for "which reels am I looking at" across the labs. Two kinds of source
/// share it: the stock presets, and the shipped game definitions — Orca Dive above all,
/// because a viewer relates to the game they have watched being built, and its ragged
/// strips (26/29/26/29/26) and wild/scatter symbols show shapes the presets cannot.
/// </summary>
public static class ReelSources
{
    /// <summary>Game sources are addressed as "game:&lt;file&gt;"; anything else is a preset name.</summary>
    public const string GamePrefix = "game:";

    public sealed record ReelSource(
        string Id,
        string DisplayName,
        StripReelSet Reels,
        IReadOnlyList<Payline> Paylines,
        IReadOnlyList<SymbolInfo> Symbols,
        IReadOnlyList<PaytableCategory> Paytable,
        bool IsGame);

    public sealed record SymbolInfo(byte Id, string Name, bool IsWild, bool IsScatter);
    public sealed record PaytableCategory(string Name, string Kind, IReadOnlyList<PaytablePay> Pays);
    public sealed record PaytablePay(int Count, int PayHundredths);

    private static string GamesDirectory => Path.Combine(AppContext.BaseDirectory, "games");

    public static IEnumerable<string> GameFiles() =>
        Directory.Exists(GamesDirectory)
            ? Directory.GetFiles(GamesDirectory, "*.json").Select(Path.GetFileName).OrderBy(f => f)!
            : [];

    public static bool TryResolve(string? id, out ReelSource? source, out string error)
    {
        source = null;
        error = "";
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "A source id is required.";
            return false;
        }

        if (id.StartsWith(GamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var file = Path.GetFileName(id[GamePrefix.Length..]);
            var path = Path.Combine(GamesDirectory, file);
            if (!File.Exists(path))
            {
                error = $"No shipped game named '{file}'.";
                return false;
            }
            if (!GameDefinitionLoader.TryLoad(File.ReadAllText(path), out var definition, out var errors))
            {
                error = $"Game '{file}' failed to load: {string.Join(" ", errors)}";
                return false;
            }

            var game = definition!;
            source = new ReelSource(
                id,
                game.Name,
                game.Reels,
                game.Paylines,
                game.Symbols.Select(s => new SymbolInfo(s.Id, s.Name, s.IsWild, s.IsScatter)).ToArray(),
                game.Categories.Select(category => new PaytableCategory(
                    category.Name,
                    category.Kind.ToString(),
                    Enumerable.Range(1, game.ReelCount)
                        .Where(count => category.PayFor(count) > 0)
                        .Select(count => new PaytablePay(count, category.PayFor(count)))
                        .ToArray())).ToArray(),
                IsGame: true);
            return true;
        }

        if (!StandardReelPresets.All.TryGetValue(id, out var preset))
        {
            error = $"Unknown source '{id}'. Presets: {string.Join(", ", StandardReelPresets.All.Keys)}; games use the '{GamePrefix}' prefix.";
            return false;
        }

        source = new ReelSource(
            id,
            preset.Name,
            preset.BuildReels(),
            preset.Paylines,
            preset.Symbols.Select(symbol => new SymbolInfo(symbol.Id, symbol.Name, false, false)).ToArray(),
            [],
            IsGame: false);
        return true;
    }
}
