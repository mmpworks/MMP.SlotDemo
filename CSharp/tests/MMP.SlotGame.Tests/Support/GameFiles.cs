using MMP.SlotGame.Core.Games.Definition;

namespace MMP.SlotGame.Tests.Support;

/// <summary>
/// Loads the shipped game definitions the same way a deployment would: from JSON on disk,
/// through the public loader, with no test-only shortcut. If a definition stops being valid
/// the suite finds out at the same moment a user would.
/// </summary>
public static class GameFiles
{
    public const string OrcaDive = "orca-dive";
    public const string ClassicThreeReel = "classic-three-reel";
    public const string TwoLineTide = "two-line-tide";

    private static readonly Dictionary<string, GameDefinition> Cache = new(StringComparer.Ordinal);
    private static readonly Lock Gate = new();

    public static string PathFor(string name) =>
        Path.Combine(AppContext.BaseDirectory, "games", $"{name}.json");

    /// <summary>Cached because loading is pure and several suites want the same definition.</summary>
    public static GameDefinition Load(string name)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var definition = GameDefinitionLoader.LoadFile(PathFor(name));
            Cache[name] = definition;
            return definition;
        }
    }

    public static string ReadJson(string name) => File.ReadAllText(PathFor(name));
}
