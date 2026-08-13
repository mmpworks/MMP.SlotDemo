using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Games.Definition;

/// <summary>How a pay category decides what counts as a run.</summary>
public enum PayCategoryKind
{
    /// <summary>One symbol, extended by any wild that substitutes for it.</summary>
    Symbol,

    /// <summary>A named set of symbols, any of which continues the run. Wilds do not extend a group.</summary>
    Group,
}

/// <summary>
/// One row of the pay table, compiled into the two lookups the evaluator actually needs.
///
/// <see cref="Continues"/> answers "does this symbol keep the run going" and
/// <see cref="IsRequired"/> answers "does this symbol make the run count". The second one is
/// what stops an all-wild line from being read as a fruit win: the wild continues a fruit
/// run but does not satisfy it, so a line of nothing but wilds falls through to the wild
/// category, which requires the wild. The lookups are indexed by symbol id.
/// </summary>
public sealed record PayCategory
{
    private readonly bool[] _continuesRun;
    private readonly bool[] _requires;
    private readonly int[] _paysByCount;

    public PayCategory(
        int index,
        string name,
        PayCategoryKind kind,
        bool[] continuesRun,
        bool[] requires,
        int[] paysByCount)
    {
        Index = index;
        Name = name;
        Kind = kind;
        _continuesRun = [.. continuesRun];
        _requires = [.. requires];
        _paysByCount = [.. paysByCount];
    }

    public int Index { get; }
    public string Name { get; }
    public PayCategoryKind Kind { get; }

    public bool Continues(byte symbolId) => _continuesRun[symbolId];

    public bool IsRequired(byte symbolId) => _requires[symbolId];

    /// <summary>
    /// Pay multiplier for a run of <paramref name="count"/>, in hundredths of the TOTAL
    /// SPIN BET (225 = 2.25X of the whole wager — see <see cref="Games.WinEvaluator.EvaluateWindow"/>
    /// for why it is the total, not a single line's share), 0 for no pay. Always hundredths
    /// regardless of the game's declared payUnit: the loader compiles "units", "tenths" and
    /// "hundredths" pays to this one
    /// representation.
    /// </summary>
    public int PayFor(int count) => count >= 0 && count < _paysByCount.Length ? _paysByCount[count] : 0;

    /// <summary>The longest run this category can ever pay on. Useful for reporting, not for evaluation.</summary>
    public int MaxPayingCount
    {
        get
        {
            for (var count = _paysByCount.Length - 1; count >= 0; count--)
            {
                if (_paysByCount[count] != 0) return count;
            }
            return 0;
        }
    }
}

/// <summary>
/// A scatter-triggered pick bonus. Triggers when the scatter symbol is visible anywhere in
/// the window on EVERY reel in <see cref="RequiredReels"/>, then plays
/// <see cref="Bonus"/> once.
/// </summary>
public sealed record ScatterPickBonus
{
    public ScatterPickBonus(string name, byte scatterSymbolId, int[] requiredReels, PickBonus bonus)
    {
        Name = name;
        ScatterSymbolId = scatterSymbolId;
        RequiredReels = Array.AsReadOnly([.. requiredReels]);
        Bonus = bonus;
    }

    public string Name { get; }
    public byte ScatterSymbolId { get; }
    public IReadOnlyList<int> RequiredReels { get; }
    public PickBonus Bonus { get; }
}

/// <summary>
/// A complete, VALIDATED game. Reels, symbols, paylines, pay table and features all arrived
/// as data; nothing in this type or anything downstream of it knows how many reels, symbols
/// or stops a real game has.
///
/// Only <see cref="GameDefinitionLoader"/> creates instances, after all validation succeeds.
/// Downstream code can therefore use a GameDefinition without repeating validation or
/// depending on the JSON document types.
/// </summary>
public sealed class GameDefinition
{
    internal GameDefinition(
        string name,
        string? source,
        IReadOnlyList<Symbol> symbols,
        StripReelSet reels,
        IReadOnlyList<Payline> paylines,
        IReadOnlyList<PayCategory> categories,
        ScatterPickBonus? bonus)
    {
        Name = name;
        Source = source;
        Symbols = Array.AsReadOnly([.. symbols]);
        Reels = reels;
        Paylines = Array.AsReadOnly([.. paylines]);
        Categories = Array.AsReadOnly([.. categories]);
        Bonus = bonus;
    }

    public string Name { get; }

    /// <summary>Where the numbers came from, for example a PAR sheet URL. Carried for provenance.</summary>
    public string? Source { get; }

    /// <summary>Symbols indexed by id. Ids are assigned by position in the definition.</summary>
    public IReadOnlyList<Symbol> Symbols { get; }

    public StripReelSet Reels { get; }

    public IReadOnlyList<Payline> Paylines { get; }

    public IReadOnlyList<PayCategory> Categories { get; }

    public ScatterPickBonus? Bonus { get; }

    public int ReelCount => Reels.ReelCount;

    /// <summary>Product of the per-reel stop counts: the size of the exhaustive outcome space.</summary>
    public long StopCombinations
    {
        get
        {
            long total = 1;
            for (var reel = 0; reel < Reels.ReelCount; reel++) total *= Reels.StopCount(reel);
            return total;
        }
    }

    public int SymbolId(string name)
    {
        for (var id = 0; id < Symbols.Count; id++)
        {
            if (string.Equals(Symbols[id].Name, name, StringComparison.Ordinal)) return id;
        }
        throw new ArgumentException($"Game '{Name}' has no symbol named '{name}'.", nameof(name));
    }

    public PayCategory Category(string name) =>
        Categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal))
        ?? throw new ArgumentException($"Game '{Name}' has no pay category named '{name}'.", nameof(name));
}
