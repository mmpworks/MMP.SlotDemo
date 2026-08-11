using MMP.SlotGame.Core.Features;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Simulation;

/// <summary>Raw, untrusted input from the API/SPA. RTP terms are integer basis points (RT-12).</summary>
public sealed record ConfigDraft(
    string PresetName,
    int BaseRtpBasisPoints,
    int FreeSpinsRtpBasisPoints,
    int PickBonusRtpBasisPoints,
    ulong MasterSeed,
    int WorkerCount,
    long TargetSpins);

/// <summary>
/// A validated simulation configuration. THE single validation boundary (architecture
/// §8): the only constructor path is <see cref="TryCreate"/>, so a SimulationConfig
/// that EXISTS is one satisfying the aggregate cap — the invariant rides on the type.
/// Immutable per run (RT-17): changing anything means a new config, a new run, fresh
/// counters.
/// </summary>
public sealed record SimulationConfig
{
    /// <summary>The aggregate RTP cap in basis points. Integer comparison — the 99.00% boundary is exact (RT-12).</summary>
    public const int MaxAggregateBasisPoints = 9_900;

    /// <summary>
    /// The PRD's default RTP split for a new config: 75% base + 13% free spins + 10%
    /// pick bonus = 98% total, comfortably under <see cref="MaxAggregateBasisPoints"/>.
    /// The Server's /api/config/limits suggests these to a new SPA session, and the
    /// test harness (TestGame) derives its own defaults from these same three values,
    /// so a "default config" test exercises the same numbers a real session starts
    /// with. Changing any one of the three changes the suggested total; nothing
    /// enforces the sum stays under the cap, so re-check that by hand.
    /// </summary>
    public const int DefaultBaseRtpBasisPoints = 7500;

    public const int DefaultFreeSpinsRtpBasisPoints = 1300;
    public const int DefaultPickBonusRtpBasisPoints = 1000;

    /// <summary>The preset name suggested alongside the default RTP split above.</summary>
    public const string DefaultPresetName = "Video5x64";

    /// <summary>
    /// The TOTAL amount staked per spin — every payline and every feature scales against
    /// this SAME value; the engine has no concept of a per-line share of it. See
    /// <see cref="Games.WinEvaluator.EvaluateWindow"/> and <see cref="Paytables.PaytableSolver.Solve"/>
    /// for where that basis is load-bearing.
    /// </summary>
    public static readonly Millicents Wager = Millicents.FromCredits(1);

    public required string RunId { get; init; }
    public required ReelPreset Preset { get; init; }
    public required int BaseRtpBasisPoints { get; init; }
    public required IReadOnlyList<FeatureSchedule> Features { get; init; }
    public required ulong MasterSeed { get; init; }
    public required int WorkerCount { get; init; }
    public required long TargetSpins { get; init; }

    public int AggregateBasisPoints => BaseRtpBasisPoints + Features.Sum(f => f.ContributionBasisPoints);
    public double TargetTotalRtp => AggregateBasisPoints / 10_000.0;

    /// <summary>The scheduling half of this config, which is all the engine needs to run it.</summary>
    public RunPlan Plan => new(RunId, MasterSeed, WorkerCount, TargetSpins);

    private SimulationConfig() { }

    public static bool TryCreate(ConfigDraft draft, out SimulationConfig? config, out IReadOnlyList<string> errors)
    {
        var errs = new List<string>();
        config = null;

        if (!ReelPreset.All.TryGetValue(draft.PresetName ?? "", out var preset))
            errs.Add($"Unknown preset '{draft.PresetName}'. Valid: {string.Join(", ", ReelPreset.All.Keys)}.");
        if (draft.BaseRtpBasisPoints is < 1 or > MaxAggregateBasisPoints)
            errs.Add($"Base RTP must be 1..{MaxAggregateBasisPoints} basis points; got {draft.BaseRtpBasisPoints}.");
        if (draft.FreeSpinsRtpBasisPoints < 0)
            errs.Add($"FreeSpins RTP cannot be negative; got {draft.FreeSpinsRtpBasisPoints}.");
        if (draft.PickBonusRtpBasisPoints < 0)
            errs.Add($"PickBonus RTP cannot be negative; got {draft.PickBonusRtpBasisPoints}.");

        // The cap. Integer arithmetic; no floating-point boundary ambiguity.
        var aggregate = (long)draft.BaseRtpBasisPoints + draft.FreeSpinsRtpBasisPoints + draft.PickBonusRtpBasisPoints;
        if (aggregate > MaxAggregateBasisPoints)
            errs.Add($"Aggregate RTP {aggregate} bp exceeds the {MaxAggregateBasisPoints} bp (99.00%) cap. Rejected, never clamped.");

        if (draft.WorkerCount is < 1 or > 64)
            errs.Add($"WorkerCount must be 1..64; got {draft.WorkerCount}.");
        if (draft.TargetSpins < 1)
            errs.Add($"TargetSpins must be ≥ 1; got {draft.TargetSpins}.");

        if (errs.Count > 0)
        {
            errors = errs;
            return false;
        }

        var features = new List<FeatureSchedule>();
        if (draft.FreeSpinsRtpBasisPoints > 0)
            features.Add(FeatureSchedule.Create(FeatureKind.FreeSpins, draft.FreeSpinsRtpBasisPoints, Wager));
        if (draft.PickBonusRtpBasisPoints > 0)
            features.Add(FeatureSchedule.Create(FeatureKind.PickBonus, draft.PickBonusRtpBasisPoints, Wager));

        config = new SimulationConfig
        {
            RunId = Guid.CreateVersion7().ToString("n"),
            Preset = preset!,
            BaseRtpBasisPoints = draft.BaseRtpBasisPoints,
            Features = features.AsReadOnly(),
            MasterSeed = draft.MasterSeed,
            WorkerCount = draft.WorkerCount,
            TargetSpins = draft.TargetSpins,
        };
        errors = [];
        return true;
    }
}
