using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Games.Definition;

/// <summary>
/// Validates a parsed <see cref="GameDocument"/> and compiles it. Phased on purpose: reels,
/// paylines, pay table and features all reference symbols, so if the symbol table is broken
/// every later phase would report the same problem again in a different voice. Each phase
/// runs only when the phases it depends on were clean, which keeps the error list short and
/// about the actual mistake.
/// </summary>
internal sealed class GameDefinitionBuilder(GameDocument document)
{
    private readonly List<string> _errors = [];
    private readonly Dictionary<string, int> _symbolIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<int>> _groups = new(StringComparer.Ordinal);

    private Symbol[] _symbols = [];
    private bool[][] _substitutedBy = [];
    private int _rows = StripReelSet.DefaultRows;
    private PayUnit _payUnit = PayUnit.Units;

    /// <summary>
    /// How the JSON paytable's numbers are interpreted. Compiled pays are always hundredths of
    /// the TOTAL SPIN BET regardless of which unit the document declared, so the evaluator and
    /// the analyser never carry more than one representation. "Total spin bet," not "line
    /// bet": see <see cref="Games.WinEvaluator.EvaluateWindow"/> for why — a PAR sheet quotes
    /// pays per line, but this engine applies every declared multiplier against the whole wager.
    /// </summary>
    private enum PayUnit
    {
        /// <summary>Pays are whole multipliers of the total spin bet — the default.</summary>
        Units,

        /// <summary>Pays are whole tenths of the total spin bet (15 = 1.5X).</summary>
        Tenths,

        /// <summary>Pays are whole hundredths of the total spin bet (225 = 2.25X); the finest unit the engine supports.</summary>
        Hundredths,
    }

    public IReadOnlyList<string> Errors => _errors;

    private void Fail(string message) => _errors.Add(message);

    private bool Clean => _errors.Count == 0;

    /// <summary>Defaults to <see cref="PayUnit.Units"/> on a bad value, so the rest of the paytable phase still reports its own problems instead of going silent behind this one.</summary>
    private PayUnit ResolvePayUnit(string? declared)
    {
        if (string.IsNullOrWhiteSpace(declared)) return PayUnit.Units;

        var trimmed = declared.Trim();
        if (string.Equals(trimmed, "units", StringComparison.OrdinalIgnoreCase)) return PayUnit.Units;
        if (string.Equals(trimmed, "tenths", StringComparison.OrdinalIgnoreCase)) return PayUnit.Tenths;
        if (string.Equals(trimmed, "hundredths", StringComparison.OrdinalIgnoreCase)) return PayUnit.Hundredths;

        Fail($"payUnit '{declared}' is not recognised; supported values are \"units\", \"tenths\" and \"hundredths\".");
        return PayUnit.Units;
    }

    public bool TryBuild(out GameDefinition? definition)
    {
        definition = null;

        var name = string.IsNullOrWhiteSpace(document.Name) ? null : document.Name!.Trim();
        if (name is null) Fail("name is required and cannot be blank.");

        _rows = document.WindowRows ?? StripReelSet.DefaultRows;
        if (_rows < StripReelSet.MinRows || _rows > StripReelSet.MaxRows)
            Fail($"windowRows must be {StripReelSet.MinRows}..{StripReelSet.MaxRows}; got {_rows}.");

        _payUnit = ResolvePayUnit(document.PayUnit);

        BuildSymbols();
        if (!Clean) return false;

        BuildGroups();
        var strips = BuildReels();
        if (strips is null) return false;

        // From here every phase is independent, so they all run and all report. Only a
        // structurally unusable reel set forces an early exit, because paylines and the
        // feature both need a reel count to check anything against.
        VerifyDeclaredStops(strips);
        VerifyDeclaredCounts(strips);
        var paylines = BuildPaylines(strips.Length);
        var categories = BuildPaytable(strips.Length);
        var bonus = BuildFeatures(strips);
        if (!Clean) return false;

        definition = new GameDefinition(
            name!, document.Source, _symbols, new StripReelSet(strips, _rows), paylines, categories, bonus);
        return true;
    }

    // ---------------------------------------------------------------------------
    // Symbols. Ids are positions in the list, which is what lets everything below
    // index by byte and never look a name up again.
    // ---------------------------------------------------------------------------

    private void BuildSymbols()
    {
        var declared = document.Symbols;
        if (declared is null || declared.Count == 0)
        {
            Fail("symbols is required and must list at least one symbol.");
            return;
        }
        if (declared.Count > byte.MaxValue + 1)
        {
            Fail($"symbols lists {declared.Count} symbols; the engine addresses symbols by byte id, so the limit is {byte.MaxValue + 1}.");
            return;
        }

        var symbols = new Symbol[declared.Count];
        for (var id = 0; id < declared.Count; id++)
        {
            var entry = declared[id];
            var symbolName = entry.Name?.Trim();
            if (string.IsNullOrEmpty(symbolName))
            {
                Fail($"symbols[{id}] has no name.");
                continue;
            }
            if (!_symbolIds.TryAdd(symbolName, id))
            {
                Fail($"symbols[{id}] repeats the name '{symbolName}'; symbol names must be unique.");
                continue;
            }
            symbols[id] = new Symbol((byte)id, symbolName, entry.Wild == true, entry.Scatter == true);
        }

        if (!Clean) return;
        _symbols = symbols;
        BuildSubstitutions(declared);
    }

    /// <summary>
    /// Substitution is stored the way the evaluator reads it: for each TARGET symbol, which
    /// symbols may stand in for it. The config states it the way a human thinks about it
    /// (this wild covers those symbols), and this inverts it once, here.
    /// </summary>
    private void BuildSubstitutions(List<SymbolDocument> declared)
    {
        _substitutedBy = new bool[_symbols.Length][];
        for (var id = 0; id < _symbols.Length; id++) _substitutedBy[id] = new bool[_symbols.Length];

        for (var id = 0; id < declared.Count; id++)
        {
            var covers = declared[id].SubstitutesFor;
            if (covers is null || covers.Count == 0) continue;

            if (declared[id].Wild != true)
            {
                Fail($"symbol '{_symbols[id].Name}' declares substitutesFor but is not marked wild; substitution would be silently ignored.");
                continue;
            }

            foreach (var target in covers)
            {
                if (target is null)
                {
                    Fail($"symbol '{_symbols[id].Name}' declares a null entry in substitutesFor; expected a symbol name.");
                    continue;
                }
                if (!_symbolIds.TryGetValue(target, out var targetId))
                {
                    Fail($"symbol '{_symbols[id].Name}' substitutes for '{target}', which is not a declared symbol.");
                    continue;
                }
                if (targetId == id)
                {
                    Fail($"symbol '{_symbols[id].Name}' substitutes for itself.");
                    continue;
                }
                _substitutedBy[targetId][id] = true;
            }
        }
    }

    private void BuildGroups()
    {
        if (document.Groups is null) return;

        foreach (var (groupName, members) in document.Groups)
        {
            if (members is null || members.Count == 0)
            {
                Fail($"group '{groupName}' is empty.");
                continue;
            }

            var ids = new List<int>(members.Count);
            foreach (var member in members)
            {
                if (member is null) Fail($"group '{groupName}' has a null member; expected a symbol name.");
                else if (_symbolIds.TryGetValue(member, out var id)) ids.Add(id);
                else Fail($"group '{groupName}' names '{member}', which is not a declared symbol.");
            }
            if (!_groups.TryAdd(groupName, ids))
                Fail($"group '{groupName}' is declared more than once.");
        }
    }

    // ---------------------------------------------------------------------------
    // Reels. Strips are ordered stop lists of any length, and reels need not agree.
    // ---------------------------------------------------------------------------

    /// <summary>Returns null when the reel set is structurally unusable and later phases cannot run.</summary>
    private Symbol[][]? BuildReels()
    {
        var declared = document.Reels;
        if (declared is null || declared.Count == 0)
        {
            Fail("reels is required and must list at least one reel.");
            return null;
        }

        var usable = true;

        var strips = new Symbol[declared.Count][];
        for (var reel = 0; reel < declared.Count; reel++)
        {
            var stops = declared[reel];
            if (stops is null || stops.Count == 0)
            {
                Fail($"reel {reel + 1} has no stops.");
                strips[reel] = [];
                usable = false;
                continue;
            }

            var strip = new Symbol[stops.Count];
            for (var stop = 0; stop < stops.Count; stop++)
            {
                var stopValue = stops[stop];
                if (stopValue is null) Fail($"reel {reel + 1} stop {stop} is missing (null); expected a symbol name.");
                else if (_symbolIds.TryGetValue(stopValue, out var id)) strip[stop] = _symbols[id];
                else Fail($"reel {reel + 1} stop {stop} is '{stopValue}', which is not a declared symbol.");
            }
            strips[reel] = strip;
        }

        return usable ? strips : null;
    }

    /// <summary>A PAR sheet states its strip lengths, so a transcription that drops a stop is caught here.</summary>
    private void VerifyDeclaredStops(Symbol[][] strips)
    {
        var declared = document.ReelStops;
        if (declared is null) return;

        if (declared.Count != strips.Length)
        {
            Fail($"reelStops lists {declared.Count} reels but reels has {strips.Length}.");
            return;
        }

        for (var reel = 0; reel < strips.Length; reel++)
        {
            if (declared[reel] != strips[reel].Length)
                Fail($"reel {reel + 1} declares {declared[reel]} stops but the strip has {strips[reel].Length}.");
        }
    }

    /// <summary>
    /// The PAR sheet symbol-count table, verified against the strips we actually wrote. This
    /// is the check that matters most when transcribing by hand: the strips are long, the
    /// table is short, and the table is the thing the published maths was derived from.
    /// </summary>
    private void VerifyDeclaredCounts(Symbol[][] strips)
    {
        var declared = document.SymbolCounts;
        if (declared is null) return;

        foreach (var (symbolName, perReel) in declared)
        {
            if (!_symbolIds.TryGetValue(symbolName, out var id))
            {
                Fail($"symbolCounts names '{symbolName}', which is not a declared symbol.");
                continue;
            }
            if (perReel is null || perReel.Count != strips.Length)
            {
                Fail($"symbolCounts['{symbolName}'] must list one count per reel ({strips.Length}).");
                continue;
            }

            for (var reel = 0; reel < strips.Length; reel++)
            {
                var actual = 0;
                foreach (var symbol in strips[reel])
                {
                    if (symbol.Id == id) actual++;
                }
                if (actual != perReel[reel])
                    Fail($"reel {reel + 1} carries {actual} '{symbolName}' but symbolCounts declares {perReel[reel]}.");
            }
        }
    }

    private Payline[] BuildPaylines(int reelCount)
    {
        var declared = document.Paylines;
        if (declared is null || declared.Count == 0)
        {
            Fail("paylines is required and must list at least one line.");
            return [];
        }

        var lines = new List<Payline>(declared.Count);
        for (var index = 0; index < declared.Count; index++)
        {
            var rows = declared[index].Rows;
            var lineName = declared[index].Name?.Trim() is { Length: > 0 } n ? n : $"Line{index + 1}";

            if (rows is null || rows.Count != reelCount)
            {
                Fail($"payline '{lineName}' must list one row per reel ({reelCount}); got {rows?.Count ?? 0}.");
                continue;
            }

            var bad = rows.FindIndex(row => row < 0 || row >= _rows);
            if (bad >= 0)
            {
                Fail($"payline '{lineName}' reel {bad + 1} uses row {rows[bad]}, outside the {_rows}-row window.");
                continue;
            }

            lines.Add(new Payline(lineName, [.. rows]));
        }

        return [.. lines];
    }

    // ---------------------------------------------------------------------------
    // Pay table. Each row compiles to the two symbol lookups the evaluator walks.
    // ---------------------------------------------------------------------------

    private PayCategory[] BuildPaytable(int reelCount)
    {
        var declared = document.Paytable;
        if (declared is null || declared.Count == 0)
        {
            Fail("paytable is required and must list at least one category.");
            return [];
        }

        var categories = new List<PayCategory>(declared.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in declared)
        {
            var category = BuildCategory(entry, categories.Count, reelCount);
            if (category is null) continue;
            if (!seen.Add(category.Name))
            {
                Fail($"paytable declares the category '{category.Name}' more than once.");
                continue;
            }
            categories.Add(category);
        }

        return [.. categories];
    }

    private PayCategory? BuildCategory(PayDocument entry, int index, int reelCount)
    {
        var hasSymbol = !string.IsNullOrWhiteSpace(entry.Symbol);
        var hasGroup = !string.IsNullOrWhiteSpace(entry.Group);
        if (hasSymbol == hasGroup)
        {
            Fail($"paytable[{index}] must name exactly one of symbol or group.");
            return null;
        }

        var label = entry.Name?.Trim() is { Length: > 0 } n ? n : (entry.Symbol ?? entry.Group)!.Trim();
        var continuesRun = new bool[_symbols.Length];
        var requires = new bool[_symbols.Length];
        PayCategoryKind kind;

        if (hasSymbol)
        {
            if (!_symbolIds.TryGetValue(entry.Symbol!.Trim(), out var id))
            {
                Fail($"paytable category '{label}' pays symbol '{entry.Symbol}', which is not a declared symbol.");
                return null;
            }

            // The run continues on the symbol itself and on anything that substitutes for it,
            // but only the symbol itself makes the run count. That single asymmetry is what
            // keeps an all-wild line with the wild.
            kind = PayCategoryKind.Symbol;
            continuesRun[id] = true;
            requires[id] = true;
            for (var other = 0; other < _symbols.Length; other++)
            {
                if (_substitutedBy[id][other]) continuesRun[other] = true;
            }
        }
        else
        {
            if (!_groups.TryGetValue(entry.Group!.Trim(), out var members))
            {
                Fail($"paytable category '{label}' pays group '{entry.Group}', which is not a declared group.");
                return null;
            }

            // Any member continues the run and any member satisfies it. Substitutes do not
            // extend a group; a game that wants that declares the substitute as a member.
            kind = PayCategoryKind.Group;
            foreach (var id in members)
            {
                continuesRun[id] = true;
                requires[id] = true;
            }
        }

        var pays = BuildPays(entry.Pays, label, reelCount);
        return pays is null ? null : new PayCategory(index, label, kind, continuesRun, requires, pays);
    }

    /// <summary>
    /// Compiles declared pays to the category's internal pay lookup, always as the real
    /// multiplier × <see cref="Millicents.ScaleFactor"/> regardless of <see cref="_payUnit"/>: a
    /// "units" pay of 5, a "tenths" pay of 50 and a "hundredths" pay of 500 all compile to the
    /// same value at the current ScaleFactor of 100, so the evaluator and the analyser read one
    /// representation. ScaleFactor is the engine's one named scale constant; "tenths" and
    /// "hundredths" convert to a real multiplier by their own definition (/10, /100).
    ///
    /// Hundredths is the finest unit the engine supports. One cent, the smallest real-money
    /// denomination, is 1,000 millicents — three orders finer — so a hundredth of the total
    /// spin bet always lands on a whole millicent. A pay needing finer precision than
    /// hundredths is rejected here, in every mode.
    /// </summary>
    private int[]? BuildPays(Dictionary<int, decimal>? declared, string label, int reelCount)
    {
        if (declared is null || declared.Count == 0)
        {
            Fail($"paytable category '{label}' has no pays.");
            return null;
        }

        var pays = new int[reelCount + 1];
        var any = false;
        foreach (var (count, rawPay) in declared)
        {
            if (count < 1 || count > reelCount)
            {
                Fail($"paytable category '{label}' pays at a run of {count}, outside 1..{reelCount}.");
                continue;
            }
            if (rawPay < 0)
            {
                Fail($"paytable category '{label}' pays {rawPay} at a run of {count}; pays cannot be negative.");
                continue;
            }
            if (rawPay != decimal.Truncate(rawPay))
            {
                Fail(_payUnit switch
                {
                    PayUnit.Tenths => $"paytable category '{label}' pays {rawPay} at a run of {count}; "
                        + "\"tenths\" mode expects a whole number of tenths (15 for 1.5X).",
                    PayUnit.Hundredths => $"paytable category '{label}' pays {rawPay} at a run of {count}; "
                        + "\"hundredths\" mode expects a whole number of hundredths (225 for 2.25X) — hundredths "
                        + "is the finest unit the engine supports, and this value needs more precision than that.",
                    _ => $"paytable category '{label}' pays {rawPay} at a run of {count}; \"units\" mode allows "
                        + $"whole-number multipliers only. Declare \"payUnit\": \"tenths\" or \"hundredths\" to "
                        + $"express {rawPay}X.",
                });
                continue;
            }

            var realMultiplier = _payUnit switch
            {
                PayUnit.Tenths => rawPay / 10m,
                PayUnit.Hundredths => rawPay / 100m,
                _ => rawPay,
            };

            int compiledPay;
            try
            {
                compiledPay = checked((int)(realMultiplier * Millicents.ScaleFactor));
            }
            catch (OverflowException)
            {
                Fail($"paytable category '{label}' pays {rawPay} at a run of {count}; that multiplier is too large to compile.");
                continue;
            }

            pays[count] = compiledPay;
            any |= compiledPay > 0;
        }

        if (!any) Fail($"paytable category '{label}' never pays anything.");
        return pays;
    }

    // ---------------------------------------------------------------------------
    // Features. One kind today; the discriminator is the seam for the next one.
    // ---------------------------------------------------------------------------

    private const string ScatterPickBonusKind = "scatterPickBonus";

    /// <summary>
    /// A prize's declared count expands to that many entries in the pick list
    /// (<see cref="BuildPickBonus"/>). No real pick-bonus wheel needs more than a few dozen
    /// slots; this bound exists so a mistyped or adversarial count (JSON has no int ceiling of
    /// its own) fails as a validation error instead of an allocation the process cannot serve.
    /// </summary>
    private const int MaxPrizeCount = 100_000;

    private ScatterPickBonus? BuildFeatures(Symbol[][] strips)
    {
        var declared = document.Features;
        if (declared is null || declared.Count == 0) return null;

        if (declared.Count > 1)
        {
            Fail($"features lists {declared.Count} entries; one feature per game is supported today.");
            return null;
        }

        var entry = declared[0];
        if (!string.Equals(entry.Kind, ScatterPickBonusKind, StringComparison.Ordinal))
        {
            Fail($"feature kind '{entry.Kind}' is not recognised; supported kinds: {ScatterPickBonusKind}.");
            return null;
        }

        var featureName = entry.Name?.Trim() is { Length: > 0 } n ? n : ScatterPickBonusKind;
        var scatterId = ValidateScatter(entry, featureName, strips);
        var reels = ValidateRequiredReels(entry, featureName, strips.Length);
        var bonus = BuildPickBonus(entry, featureName);

        return scatterId is null || reels is null || bonus is null
            ? null
            : new ScatterPickBonus(featureName, scatterId.Value, reels, bonus);
    }

    private byte? ValidateScatter(FeatureDocument entry, string featureName, Symbol[][] strips)
    {
        if (string.IsNullOrWhiteSpace(entry.Scatter) || !_symbolIds.TryGetValue(entry.Scatter.Trim(), out var id))
        {
            Fail($"feature '{featureName}' names scatter '{entry.Scatter}', which is not a declared symbol.");
            return null;
        }
        if (!_symbols[id].IsScatter)
        {
            Fail($"feature '{featureName}' uses '{_symbols[id].Name}' as a scatter, but that symbol is not marked scatter.");
            return null;
        }

        var onSomeReel = strips.Any(strip => strip.Any(symbol => symbol.Id == id));
        if (!onSomeReel)
        {
            Fail($"feature '{featureName}' uses scatter '{_symbols[id].Name}', which appears on no reel, so it can never trigger.");
            return null;
        }

        return (byte)id;
    }

    private int[]? ValidateRequiredReels(FeatureDocument entry, string featureName, int reelCount)
    {
        var reels = entry.RequiredReels;
        if (reels is null || reels.Count == 0)
        {
            Fail($"feature '{featureName}' must list at least one required reel.");
            return null;
        }

        var before = _errors.Count;
        var seen = new HashSet<int>();
        foreach (var reel in reels)
        {
            if (reel < 0 || reel >= reelCount)
                Fail($"feature '{featureName}' requires reel index {reel}, outside 0..{reelCount - 1}.");
            else if (!seen.Add(reel))
                Fail($"feature '{featureName}' lists reel index {reel} twice.");
        }

        return _errors.Count == before ? [.. reels] : null;
    }

    private PickBonus? BuildPickBonus(FeatureDocument entry, string featureName)
    {
        var declared = entry.Prizes;
        if (declared is null || declared.Count == 0)
        {
            Fail($"feature '{featureName}' has no prizes.");
            return null;
        }

        var before = _errors.Count;
        var prizes = new List<int>();
        foreach (var prize in declared)
        {
            var value = prize.Value ?? 0;
            var count = prize.Count ?? 0;
            if (value <= 0) Fail($"feature '{featureName}' declares a prize worth {value}; prize values must be positive.");
            else if (count <= 0) Fail($"feature '{featureName}' declares {count} of the {value} prize; counts must be positive.");
            else if (count > MaxPrizeCount) Fail($"feature '{featureName}' declares {count} of the {value} prize; counts above {MaxPrizeCount} are not supported.");
            else prizes.AddRange(Enumerable.Repeat(value, count));
        }

        var blankCount = entry.Blanks?.Count ?? 0;
        var consolation = entry.Blanks?.Consolation ?? 0;
        if (blankCount < 1)
            Fail($"feature '{featureName}' needs at least one blank; without one the round never ends.");
        if (consolation < 0)
            Fail($"feature '{featureName}' pays a consolation of {consolation}; it cannot be negative.");

        return _errors.Count == before ? new PickBonus([.. prizes], blankCount, consolation) : null;
    }
}
