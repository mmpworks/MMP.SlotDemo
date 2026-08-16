using MMP.SlotGame.Core.Features;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Simulation;

/// <summary>Raw, untrusted input from the API/SPA. RTP terms are integer basis points.</summary>
public sealed record ConfigDraft(
    string PresetName,
    int BaseRtpBasisPoints,
    int FreeSpinsRtpBasisPoints,
    int PickBonusRtpBasisPoints,
    ulong MasterSeed,
    int WorkerCount,
    long TargetSpins);

/// <summary>
/// A validated simulation configuration. <see cref="TryCreate"/> checks the aggregate cap
/// before constructing an instance, and it is the only constructor path. The values stay
/// fixed for the life of a run: changing one means a new config, a new run, fresh counters.
/// </summary>
public sealed record SimulationConfig
{
    /// <summary>
    /// The solver's RTP limits: the range of aggregate RTP targets a request may hand the
    /// solver, in integer basis points so both boundaries are exact. They bound INPUT only —
    /// no check during or after a run reads them. The pair pretends this simulator is a
    /// casino floor: jurisdictions set legal floors (Nevada: 75% theoretical payback) and
    /// operators set commercial ceilings, both enforced on paper before deployment.
    /// </summary>
    public const int MaxAggregateBasisPoints = 9_900;

    /// <summary>The floor of the solver's RTP limits. See <see cref="MaxAggregateBasisPoints"/>.</summary>
    public const int MinAggregateBasisPoints = 7_500;

    /// <summary>
    /// The default RTP split for a new config: 75% base + 13% free spins + 10%
    /// pick bonus = 98% total, comfortably under <see cref="MaxAggregateBasisPoints"/>.
    /// The Server's /api/config/limits suggests these to a new SPA session, and the
    /// test harness (TestGame) derives its own defaults from these same three values,
    /// so a "default config" test exercises the same numbers a real session starts
    /// with. Keep these three below <see cref="MaxAggregateBasisPoints"/> when changing
    /// them. TryCreate catches an invalid sum, but the SPA would still open with defaults
    /// it cannot run.
    /// </summary>
    public const int DefaultBaseRtpBasisPoints = 7500;

    public const int DefaultFreeSpinsRtpBasisPoints = 1300;
    public const int DefaultPickBonusRtpBasisPoints = 1000;

    /// <summary>The preset name suggested alongside the default RTP split above.</summary>
    public const string DefaultPresetName = "Video5x64";

    /// <summary>
    /// The total amount staked per spin. Every payline and every feature scales against this
    /// value; the engine has no concept of a per-line share of it.
    /// <see cref="Games.WinEvaluator.EvaluateWindow"/> and
    /// <see cref="Paytables.PaytableSolver.Solve"/> both depend on that basis.
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

        if (!StandardReelPresets.All.TryGetValue(draft.PresetName ?? "", out var preset))
            errs.Add($"Unknown preset '{draft.PresetName}'. Valid: {string.Join(", ", StandardReelPresets.All.Keys)}.");
        if (draft.BaseRtpBasisPoints is < 1 or > MaxAggregateBasisPoints)
            errs.Add($"Base RTP must be 1..{MaxAggregateBasisPoints} basis points; got {draft.BaseRtpBasisPoints}.");
        if (draft.FreeSpinsRtpBasisPoints < 0)
            errs.Add($"FreeSpins RTP cannot be negative; got {draft.FreeSpinsRtpBasisPoints}.");
        if (draft.PickBonusRtpBasisPoints < 0)
            errs.Add($"PickBonus RTP cannot be negative; got {draft.PickBonusRtpBasisPoints}.");

        // The solver's RTP limits. Integer arithmetic; no floating-point boundary ambiguity.
        var aggregate = (long)draft.BaseRtpBasisPoints + draft.FreeSpinsRtpBasisPoints + draft.PickBonusRtpBasisPoints;
        if (aggregate > MaxAggregateBasisPoints)
            errs.Add($"Aggregate RTP {aggregate} bp exceeds the solver's {MaxAggregateBasisPoints} bp (99.00%) ceiling. Rejected, never clamped.");
        if (aggregate < MinAggregateBasisPoints)
            errs.Add($"Aggregate RTP {aggregate} bp is below the solver's {MinAggregateBasisPoints} bp (75.00%) floor. Rejected, never clamped.");

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
