namespace SlotDemo.Server.Runs;

/// <summary>
/// Request body for <c>POST /api/run</c>. <see cref="RunPreparer"/> validates every field.
/// When <c>GameFile</c> is set, the request selects a shipped game instead of a preset;
/// the preset RTP fields do not apply.
/// </summary>
public sealed record RunRequest(
    string PresetName,
    int BaseRtpBasisPoints,
    int FreeSpinsRtpBasisPoints,
    int PickBonusRtpBasisPoints,
    ulong Seed,
    int WorkerCount,
    long TargetSpins,
    long Stride,
    string GameFile = "",
    /// <summary>
    /// Optional total-RTP target for a shipped game, in basis points. Zero keeps the
    /// published paytable. A nonzero value rescales line pays; feature pays stay fixed.
    /// </summary>
    int TargetTotalRtpBasisPoints = 0);
