using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The four Orca Dive line rules, one case at a time, through the generic evaluator
/// reading the loaded definition.
///
/// The combination-table test proves the rules are right in aggregate; these prove WHICH rule
/// each case exercises. Without them a future change could trade two compensating errors and
/// still total 14,781,416.
///
/// Note what is being tested: none of these rules are written in C#. Substitution comes from
/// the wild entry in the JSON, the mixed-seven category from a symbol group, and the tie-break
/// from the one engine-wide rule in <see cref="WinEvaluator"/>.
/// </summary>
[Trait("Category", "Fast")]
public sealed class OrcaDiveLineRulesTests
{
    private static readonly Core.Games.Definition.GameDefinition Game = GameFiles.Load(GameFiles.OrcaDive);
    private static readonly WinEvaluator Evaluator = new(Game);

    private static readonly byte J = Id("Red7");
    private static readonly byte G = Id("Green7");
    private static readonly byte B = Id("Blue7");
    private static readonly byte L = Id("Seal");
    private static readonly byte W = Id("WildOrca");
    private static readonly byte M = Id("Salmon");
    private static readonly byte O = Id("Squid");
    private static readonly byte C = Id("Mackerel");
    private static readonly byte Y = Id("Penguin");

    private static byte Id(string name) => (byte)Game.SymbolId(name);

    private static LineWin Evaluate(params byte[] cells) => Evaluator.Evaluate(cells);

    /// <summary><paramref name="multiplier"/> is the real pay multiplier (5 = 5X); <see cref="LineWin.Multiplier"/> is scaled by <see cref="Millicents.ScaleFactor"/>, so it is compared against <paramref name="multiplier"/> * ScaleFactor.</summary>
    private static void AssertWin(LineWin win, string category, int count, int multiplier)
    {
        Assert.True(win.IsWin, $"Expected {category} x{count}, got no win.");
        Assert.Equal(category, Game.Categories[win.CategoryIndex].Name);
        Assert.Equal(count, win.Count);
        Assert.Equal(multiplier * Millicents.ScaleFactor, win.Multiplier);
    }

    private static void AssertNoWin(LineWin win) =>
        Assert.False(win.IsWin, $"Expected no win, got category {win.CategoryIndex} x{win.Count} paying {win.Multiplier}.");

    /// <summary>Rule 1: the wild is a wild ORCA. A seal run it happens to lead is not a seal run.</summary>
    [Fact]
    public void Wild_DoesNotSubstituteForBellsOrSevens()
    {
        AssertWin(Evaluate(W, L, L, L, L), "WildOrca", 1, 2);
        AssertWin(Evaluate(W, J, J, J, J), "WildOrca", 1, 2);
        AssertNoWin(Evaluate(L, W, L, L, L));
    }

    /// <summary>Rule 1, the other half: it does substitute for all four fruits.</summary>
    [Fact]
    public void Wild_SubstitutesForFruit()
    {
        AssertWin(Evaluate(W, C, C, L, L), "Mackerel", 3, 5);
        AssertWin(Evaluate(M, W, M, W, M), "Salmon", 5, 150);
    }

    /// <summary>Rule 2: an all-wild run is the wild paying on its own account, not a fruit win.</summary>
    [Fact]
    public void AllWildRun_PaysAsWildNotAsFruit()
    {
        AssertWin(Evaluate(W, W, W, L, L), "WildOrca", 3, 10);
        AssertWin(Evaluate(W, W, W, W, W), "WildOrca", 5, 2000);
        AssertWin(Evaluate(W, W, W, C, L), "Mackerel", 4, 10);
    }

    /// <summary>Rule 3: mixed 7s pay as their own category, and a single colour always beats it.</summary>
    [Fact]
    public void MixedSevens_PayAsACategory_ButPureColourWins()
    {
        AssertWin(Evaluate(G, B, J, C, C), "MixedSeven", 3, 5);
        AssertWin(Evaluate(G, G, G, C, C), "Green7", 3, 25);
        AssertWin(Evaluate(W, J, J, J, J), "WildOrca", 1, 2);
    }

    /// <summary>
    /// Rule 4: equal pays go to the LONGER run. Both pairs below pay the same either way, so
    /// only the category assignment differs, and the published table books both to Mixed 7.
    /// </summary>
    [Fact]
    public void EqualPays_GoToTheLongerRun()
    {
        AssertWin(Evaluate(J, J, J, J, G), "MixedSeven", 5, 100);
        AssertWin(Evaluate(J, J, J, G, C), "MixedSeven", 4, 40);
        AssertWin(Evaluate(W, W, W, W, O), "Squid", 5, 50);
    }

    /// <summary>The Penguin symbol is a scatter: it never takes part in a line win.</summary>
    [Fact]
    public void PenguinSymbol_NeverPaysOnTheLine()
    {
        AssertNoWin(Evaluate(Y, Y, Y, Y, Y));
        AssertWin(Evaluate(C, C, C, Y, Y), "Mackerel", 3, 5);
    }
}
