namespace SlotDemo.Server.Runs;

/// <summary>
/// What the SPA sends to start a run. Untrusted; every field is validated downstream.
/// A non-empty <c>GameFile</c> runs a shipped game document through
/// GameRunner instead of a solved preset; the RTP fields are ignored then, because a
/// published paytable already decided them.
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
    /// Optional target TOTAL RTP for a shipped game, in basis points. 0 keeps the game's
    /// published paytable, which is the default and what the article describes. Anything
    /// else re-prices the line paytable to hit this total, the way a cabinet's approved
    /// payback versions are produced from one recipe.
    /// </summary>
    int TargetTotalRtpBasisPoints = 0);
