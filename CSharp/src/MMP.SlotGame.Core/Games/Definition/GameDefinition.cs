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
    /// Pay multiplier for a run of <paramref name="count"/>, in hundredths of the total
    /// spin wager. For example, 225 pays 2.25 times the whole wager. Zero means no award at
    /// that run length. The loader converts every declared pay unit to this representation.
    /// </summary>
    public int PayFor(int count) => count >= 0 && count < _paysByCount.Length ? _paysByCount[count] : 0;

    /// <summary>
    /// A copy of this category with every award multiplied by <paramref name="factor"/>.
    ///
    /// Line RTP is linear in the pays, so one scalar re-prices a whole table. Rounding is
    /// half-even to the same whole hundredth of the wager the loader produces, which keeps
    /// the copy in the representation the evaluator already reads and avoids the low bias
    /// truncation would introduce. A zero stays zero: zero means "no award at this run
    /// length", which is a rule about the game rather than a price to scale.
    /// </summary>
    public PayCategory WithScaledPays(double factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(factor);
        if (!double.IsFinite(factor))
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Scale factor must be finite.");

        var scaled = new int[_paysByCount.Length];
        for (var count = 0; count < _paysByCount.Length; count++)
        {
            if (_paysByCount[count] == 0) continue;
            scaled[count] = (int)Math.Round(_paysByCount[count] * factor, MidpointRounding.ToEven);
        }

        return new PayCategory(Index, Name, Kind, _continuesRun, _requires, scaled);
    }

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

    /// <summary>The first reel count with a non-zero pay for this category.</summary>
    public int MinPayingCount
    {
        get
        {
            for (var count = 0; count < _paysByCount.Length; count++)
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
/// A game whose symbols, reels, paylines, paytable, and feature rules passed validation.
/// Reel counts, strip lengths, and symbol counts come from the loaded definition rather than
/// constants in this class.
///
/// Only <see cref="GameDefinitionLoader"/> creates instances, after all validation succeeds.
/// Downstream code can therefore use a GameDefinition without repeating validation or
/// depending on the JSON document types.
/// </summary>
public sealed class GameDefinition
{
    private readonly Lazy<WinningOutcomeTable> _winningOutcomes;
    private readonly Lazy<ProgressiveOutcomeTable> _progressiveOutcomes;

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
        _winningOutcomes = new Lazy<WinningOutcomeTable>(
            () => WinningOutcomeTable.Build(this),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _progressiveOutcomes = new Lazy<ProgressiveOutcomeTable>(
            () => ProgressiveOutcomeTable.Build(WinningOutcomes, Reels),
            LazyThreadSafetyMode.ExecutionAndPublication);
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

    /// <summary>
    /// Every paying or feature-triggering combination implied by the PAR strips, paylines,
    /// paytable, and feature rules. The table is compiled once with the rest of the game;
    /// combinations that do nothing are absent.
    /// </summary>
    public WinningOutcomeTable WinningOutcomes => _winningOutcomes.Value;

    /// <summary>The same calculated outcomes arranged as reel-by-reel narrowing tables.</summary>
    public ProgressiveOutcomeTable ProgressiveOutcomes => _progressiveOutcomes.Value;

    /// <summary>
    /// A copy of this game with every line award multiplied by <paramref name="factor"/>.
    ///
    /// This is the one lever that moves RTP without touching geometry: strips, paylines and
    /// symbols are carried over unchanged, so the probability of every combination, and
    /// therefore the hit frequency, is identical. Only what each hit pays changes, which
    /// also scales volatility and the top award.
    ///
    /// The feature is NOT scaled. A bonus contributes trigger probability times its mean
    /// award, which is a separate lever with its own prize table; folding it into this
    /// factor would silently re-price a part of the game the caller did not ask about.
    ///
    /// Each pay rounds independently, so the realized RTP lands a little off a naive
    /// target times factor. Re-analyze the returned game to read what it actually pays.
    /// </summary>
    public GameDefinition WithScaledPays(double factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(factor);
        if (!double.IsFinite(factor))
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Scale factor must be finite.");

        var scaled = Categories.Select(category => category.WithScaledPays(factor)).ToArray();
        return new GameDefinition(Name, Source, Symbols, Reels, Paylines, scaled, Bonus);
    }

    public int ReelCount => Reels.ReelCount;

    /// <summary>The fewest leftmost payline symbols that can award money in this game.</summary>
    public int MinimumPayingReels => Categories.Min(category => category.MinPayingCount);

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
