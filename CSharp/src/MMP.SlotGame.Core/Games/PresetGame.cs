using MMP.SlotGame.Core.Paylines;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Core.Games;

/// <summary>
/// A validated preset configuration compiled into the reel strips and integer paytable
/// used by both analysis and simulation.
/// </summary>
public sealed class PresetGame
{
    private PresetGame(
        SimulationConfig config,
        StripReelSet reels,
        IReadOnlyList<Payline> lines,
        ScaledPaytable paytable,
        RtpBreakdown analysis)
    {
        Config = config;
        Reels = reels;
        Lines = lines;
        Paytable = paytable;
        Analysis = analysis;
    }

    public SimulationConfig Config { get; }

    public StripReelSet Reels { get; }

    public IReadOnlyList<Payline> Lines { get; }

    public ScaledPaytable Paytable { get; }

    public RtpBreakdown Analysis { get; }

    /// <summary>Compile one validated configuration into the realized preset game.</summary>
    public static PresetGame Build(SimulationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var preset = config.Preset;
        var reels = preset.BuildReels();
        var canonical = MMP.SlotGame.Core.Paytables.Paytable.CanonicalFor(
            preset.ReelCount,
            preset.SymbolWeights.Count);
        var paytable = PaytableSolver.Solve(
            reels,
            preset.Paylines,
            canonical,
            config.BaseRtpBasisPoints / 10_000.0,
            SimulationConfig.Wager);
        var analysis = RtpCalculator.Analyse(
            reels,
            preset.Paylines,
            paytable,
            config.Features,
            SimulationConfig.Wager);

        return new PresetGame(config, reels, preset.Paylines, paytable, analysis);
    }

    public RtpBreakdown Analyse() => Analysis;

    public LinePayEvaluator Evaluator() => new(Lines, Paytable);

    public SimulationEngine Engine() =>
        new(
            Config.Plan,
            Reels,
            Lines,
            Paytable,
            Config.Features,
            SimulationConfig.Wager,
            workerId => SpinRng.ForWorker(Config.MasterSeed, workerId));
}
