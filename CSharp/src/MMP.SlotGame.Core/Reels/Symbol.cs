namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// One reel symbol. The stock presets leave both flags clear, which keeps the preset
/// pipeline's analytic math a closed form and its features independent RTP terms: wild
/// substitution makes that closed form non-linear (best-line max), and scatter-count
/// triggering couples features to the window. Loaded game definitions use both — Orca Dive
/// ships a wild and a scatter — and <see cref="MMP.SlotGame.Core.Games.GameAnalyzer"/>
/// prices those by enumeration.
/// </summary>
public readonly record struct Symbol(byte Id, string Name, bool IsWild = false, bool IsScatter = false);
