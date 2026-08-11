using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Tests.Support;

/// <summary>
/// The one place the harness wires a game together, so every suite runs the SAME
/// composition the server runs: validated config -> preset strips -> canonical
/// paytable -> solved integer paytable -> engine.
///
/// This helper deliberately contains NO game math. The exhaustive ground truth
/// (ExhaustiveGroundTruthTests) reimplements evaluation from scratch and shares
/// nothing with this file beyond the data types.
/// </summary>
public static class TestGame
{
    /// <summary>
    /// The PRD's default RTP split (75% base + 13% free spins + 10% pick bonus = 98%),
    /// same source as the Server's /api/config/limits defaults: see
    /// <see cref="SimulationConfig.DefaultBaseRtpBasisPoints"/>.
    /// </summary>
    public const int DefaultBaseBp = SimulationConfig.DefaultBaseRtpBasisPoints;

    public const int DefaultFreeSpinsBp = SimulationConfig.DefaultFreeSpinsRtpBasisPoints;
    public const int DefaultPickBonusBp = SimulationConfig.DefaultPickBonusRtpBasisPoints;

    public const string DefaultPreset = SimulationConfig.DefaultPresetName;

    public static SimulationConfig Config(
        string presetName,
        int baseBp = DefaultBaseBp,
        int freeSpinsBp = DefaultFreeSpinsBp,
        int pickBonusBp = DefaultPickBonusBp,
        ulong masterSeed = 0xC0FFEE_1234_5678UL,
        int workerCount = 1,
        long targetSpins = 1_000)
    {
        var draft = new ConfigDraft(
            presetName, baseBp, freeSpinsBp, pickBonusBp, masterSeed, workerCount, targetSpins);

        if (!SimulationConfig.TryCreate(draft, out var config, out var errors))
            throw new InvalidOperationException(
                $"Test config was rejected: {string.Join(" | ", errors)}");

        return config!;
    }

    public static PresetGame Solve(SimulationConfig config) => PresetGame.Build(config);

    public static PresetGame Build(
        string presetName,
        int baseBp = DefaultBaseBp,
        int freeSpinsBp = DefaultFreeSpinsBp,
        int pickBonusBp = DefaultPickBonusBp,
        ulong masterSeed = 0xC0FFEE_1234_5678UL,
        int workerCount = 1,
        long targetSpins = 1_000) =>
        Solve(Config(presetName, baseBp, freeSpinsBp, pickBonusBp, masterSeed, workerCount, targetSpins));

    public static IEnumerable<object[]> AllPresetNames() =>
        ReelPreset.All.Keys.Order().Select(name => new object[] { name });
}
