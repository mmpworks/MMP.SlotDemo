namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// One payline loaded from a game definition or supplied by a built-in preset.
/// <see cref="Rows"/> contains one visible-position index for each reel.
/// </summary>
public sealed record Payline
{
    public Payline(string name, IReadOnlyList<int> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rows);
        Name = name;
        Rows = Array.AsReadOnly([.. rows]);
    }

    public string Name { get; }

    /// <summary>
    /// A construction-time copy of the visible position selected on each reel.
    /// For example, [0, 1, 2] runs from the top of reel 1 through the middle of
    /// reel 2 to the bottom of reel 3 in a three-position window.
    /// </summary>
    public IReadOnlyList<int> Rows { get; }
}
