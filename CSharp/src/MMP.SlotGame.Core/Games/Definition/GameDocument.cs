namespace MMP.SlotGame.Core.Games.Definition;

/// <summary>
/// The on-disk JSON shape, and nothing more. Every property is nullable and nothing is
/// checked here on purpose: deserialization should never be the thing that reports a bad
/// game file, because the message it gives is about JSON and the message a user needs is
/// about slots. <see cref="GameDefinitionLoader"/> does the checking and produces
/// <see cref="GameDefinition"/>, which is the only type the rest of the engine ever sees.
///
/// These types are internal for that reason: the JSON shape is not part of the API.
/// </summary>
internal sealed class GameDocument
{
    public string? Name { get; set; }
    public string? Source { get; set; }
    public int? WindowRows { get; set; }
    public List<SymbolDocument>? Symbols { get; set; }
    public Dictionary<string, List<string>>? Groups { get; set; }
    public List<List<string>>? Reels { get; set; }

    /// <summary>Optional declared stops per reel. A PAR sheet states these, so we verify them.</summary>
    public List<int>? ReelStops { get; set; }

    /// <summary>Optional declared per-reel symbol counts, keyed by symbol name. Verified against the strips.</summary>
    public Dictionary<string, List<int>>? SymbolCounts { get; set; }

    public List<PaylineDocument>? Paylines { get; set; }
    public List<PayDocument>? Paytable { get; set; }
    public List<FeatureDocument>? Features { get; set; }

    /// <summary>
    /// How every <see cref="PayDocument.Pays"/> value in this game is interpreted: "units"
    /// (the default, and the only shape before fractional pays existed) for whole multipliers
    /// of the total spin bet, "tenths" for whole tenths of the total spin bet (15 = 1.5X), or
    /// "hundredths" for whole hundredths of the total spin bet (225 = 2.25X) — the finest unit
    /// the engine supports, and the only way to state a multiplier like 2.25X or 1.75X that
    /// tenths cannot express. A PAR sheet's pay table is transcribed as plain integers in every
    /// case, even though traditional multiline paytables often quote those numbers per line — this engine applies
    /// them against the whole spin wager instead (<see cref="Games.WinEvaluator.EvaluateWindow"/>).
    /// </summary>
    public string? PayUnit { get; set; }
}

internal sealed class SymbolDocument
{
    public string? Name { get; set; }
    public bool? Wild { get; set; }
    public bool? Scatter { get; set; }

    /// <summary>Symbols this wild stands in for. A wild that substitutes for nothing pays only itself.</summary>
    public List<string>? SubstitutesFor { get; set; }
}

internal sealed class PaylineDocument
{
    public string? Name { get; set; }

    /// <summary>One window row index per reel, left to right.</summary>
    public List<int>? Rows { get; set; }
}

internal sealed class PayDocument
{
    /// <summary>Exactly one of Symbol or Group.</summary>
    public string? Symbol { get; set; }

    public string? Group { get; set; }

    /// <summary>Category name. Defaults to the symbol or group name.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Run length to pay multiplier, for example { "5": 5000, "4": 100, "3": 40 }. A decimal
    /// type rather than int so a fractional value typed in "units" mode (a PAR sheet with a
    /// 2.25X line pay, mistyped as 2.25 instead of using <see cref="GameDocument.PayUnit"/>
    /// "hundredths") is a validation error with a fix, not a JSON parse failure.
    /// </summary>
    public Dictionary<int, decimal>? Pays { get; set; }
}

internal sealed class FeatureDocument
{
    public string? Kind { get; set; }
    public string? Name { get; set; }
    public string? Scatter { get; set; }

    /// <summary>Reels (zero-based) that must all show the scatter for the feature to trigger.</summary>
    public List<int>? RequiredReels { get; set; }

    public List<PrizeDocument>? Prizes { get; set; }
    public BlankDocument? Blanks { get; set; }
}

internal sealed class PrizeDocument
{
    public int? Value { get; set; }
    public int? Count { get; set; }
}

internal sealed class BlankDocument
{
    public int? Count { get; set; }
    public int? Consolation { get; set; }
}
