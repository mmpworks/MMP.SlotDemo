namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// Holds one named set of completed reel strips and paylines. It does not generate or
/// rearrange symbols. Every inner list is already in final stop order.
/// </summary>
public sealed class ReelPreset
{
    private readonly Symbol[][] _strips;
    private readonly Payline[] _paylines;
    private readonly IReadOnlyList<int> _stopCounts;

    public ReelPreset(
        string name,
        IReadOnlyList<IReadOnlyList<Symbol>> strips,
        IReadOnlyList<Payline> paylines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(strips);
        ArgumentNullException.ThrowIfNull(paylines);
        if (strips.Count == 0) throw new ArgumentException("A preset needs at least one reel.", nameof(strips));
        if (strips.Any(strip => strip is null || strip.Count == 0))
            throw new ArgumentException("Every preset reel needs at least one stop.", nameof(strips));

        Name = name;
        // Copy the outer list and every inner strip. A preset keeps the exact order it
        // receives, and later changes to the caller's arrays cannot alter that order.
        _strips = strips.Select(strip => strip.ToArray()).ToArray();
        _paylines = [.. paylines];
        _stopCounts = Array.AsReadOnly(_strips.Select(strip => strip.Length).ToArray());
        Symbols = Array.AsReadOnly(_strips
            .SelectMany(strip => strip)
            .DistinctBy(symbol => symbol.Id)
            .OrderBy(symbol => symbol.Id)
            .ToArray());
    }

    public string Name { get; }
    public IReadOnlyList<Payline> Paylines => _paylines;
    public IReadOnlyList<Symbol> Symbols { get; }
    public int ReelCount => _strips.Length;
    /// <summary>One cached strip length per reel. Reading this property does not allocate.</summary>
    public IReadOnlyList<int> StopCounts => _stopCounts;

    public StripReelSet BuildReels() => new(_strips);
}
