using MMP.SlotGame.Core.Games.Definition;

namespace MMP.SlotGame.Core.Games;

/// <summary>
/// Analytic results for a loaded game. Combination totals are exact integers; RTP and
/// sigma are computed without simulation and stored as floating-point values.
/// </summary>
public sealed record GameAnalysis
{
    public required GameDefinition Definition { get; init; }

    /// <summary>
    /// Combinations per (pay category, run length), out of <see cref="StopCombinations"/>.
    /// Empty for a multi-payline analysis because one window may pay several categories.
    /// </summary>
    public required IReadOnlyDictionary<(int CategoryIndex, int Count), long> CombinationCounts { get; init; }

    public required long StopCombinations { get; init; }

    /// <summary>Combinations that pay on the line. Divided by the total this is the hit frequency.</summary>
    public required long HitCombinations { get; init; }

    /// <summary>Combinations that trigger the feature. Zero when the game has none.</summary>
    public required long TriggerCombinations { get; init; }

    public required double LineRtp { get; init; }
    public required double BonusRtp { get; init; }
    public required double LineSigma { get; init; }
    public required double BonusSigma { get; init; }

    /// <summary>Sigma of the FULL per-spin return per unit wagered, line and feature together.</summary>
    public required double SigmaPerUnitWagered { get; init; }

    public double TotalRtp => LineRtp + BonusRtp;

    public double HitFrequency => (double)HitCombinations / StopCombinations;

    public double TriggerProbability => (double)TriggerCombinations / StopCombinations;

    /// <summary>Combination count for a named category at a run length, for reporting and tests.</summary>
    public long CountFor(string categoryName, int count) =>
        CombinationCounts.GetValueOrDefault((Definition.Category(categoryName).Index, count));
}
