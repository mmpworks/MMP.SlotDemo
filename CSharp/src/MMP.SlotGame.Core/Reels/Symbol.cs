namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// One reel symbol. v1 strips carry neither wilds nor scatters (both are AIF seams —
/// the flags exist so the shape is extensible, but no preset sets them; see
/// red-team-resolutions RT-5/RT-25 and the wild note in architecture §10):
/// wild substitution makes the analytic closed form non-linear (best-line max), and
/// scatter-count triggering couples features to the window. v1 keeps both out so the
/// analytic math is exact and features stay independent RTP terms.
/// </summary>
public readonly record struct Symbol(byte Id, string Name, bool IsWild = false, bool IsScatter = false);
