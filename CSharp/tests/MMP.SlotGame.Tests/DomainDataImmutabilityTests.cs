using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Tests;

[Trait("Category", "Fast")]
public sealed class DomainDataImmutabilityTests
{
    [Fact]
    public void Payline_CopiesCallerRows()
    {
        var rows = new[] { 0, 1, 2 };
        var line = new Payline("V", rows);

        rows[0] = 2;

        Assert.Equal(0, line.Rows[0]);
    }

    [Fact]
    public void Paytables_CopyCallerDictionaries()
    {
        var canonicalSource = new Dictionary<(byte, int), double> { [(0, 3)] = 5.0 };
        var scaledSource = new Dictionary<(byte, int), Millicents> { [(0, 3)] = new(500) };
        var canonical = new Paytable(canonicalSource);
        var scaled = new ScaledPaytable(scaledSource);

        canonicalSource[(0, 3)] = 99.0;
        scaledSource[(0, 3)] = new Millicents(9_900);

        Assert.Equal(5.0, canonical.PayFor(0, 3));
        Assert.Equal(new Millicents(500), scaled.PayFor(0, 3));
    }

    [Fact]
    public void PayCategory_CopiesCallerRuleArrays()
    {
        var continues = new[] { true, false };
        var requires = new[] { true, false };
        var pays = new[] { 0, 100 };
        var category = new PayCategory(
            0, "A", PayCategoryKind.Symbol, continues, requires, pays);

        continues[0] = false;
        requires[0] = false;
        pays[1] = 0;

        Assert.True(category.Continues(0));
        Assert.True(category.IsRequired(0));
        Assert.Equal(100, category.PayFor(1));
    }

    [Fact]
    public void StripReelSet_AcceptsDifferentLengthsAndCopiesEveryStrip()
    {
        var a = new Symbol(0, "A");
        var b = new Symbol(1, "B");
        var reel26 = Enumerable.Repeat(a, 26).ToArray();
        var reel36 = Enumerable.Repeat(b, 36).ToArray();
        IReadOnlyList<IReadOnlyList<Symbol>> source = [reel26, reel36];

        var reels = new StripReelSet(source);
        reel26[0] = b;
        reel36[0] = a;

        Assert.Equal(26, reels.StopCount(0));
        Assert.Equal(36, reels.StopCount(1));
        Assert.Equal(a, reels.Strip(0)[0]);
        Assert.Equal(b, reels.Strip(1)[0]);
    }

    [Fact]
    public void ReplacingReels_CreatesANewSnapshotWithoutChangingTheOldOne()
    {
        var a = new Symbol(0, "A");
        var b = new Symbol(1, "B");
        var original = new StripReelSet([Enumerable.Repeat(a, 26).ToArray()]);
        var replacement = new StripReelSet([Enumerable.Repeat(b, 36).ToArray()]);

        Assert.Equal(26, original.StopCount(0));
        Assert.Equal(a, original.Strip(0)[0]);
        Assert.Equal(36, replacement.StopCount(0));
        Assert.Equal(b, replacement.Strip(0)[0]);
    }

    [Fact]
    public void ReelPreset_CanBuildDifferentLengthReels()
    {
        var a = new Symbol(0, "A");
        var b = new Symbol(1, "B");
        var preset = new ReelPreset(
            "Mixed",
            [
                EvenlySpacedStripBuilder.Build(new[] { (a, 13), (b, 13) }),
                EvenlySpacedStripBuilder.Build(new[] { (a, 18), (b, 18) }),
            ],
            [new Payline("Center", [1, 1])]);

        var reels = preset.BuildReels();

        Assert.Equal([26, 36], preset.StopCounts);
        Assert.Equal(26, reels.StopCount(0));
        Assert.Equal(36, reels.StopCount(1));
    }

    [Fact]
    public void EvenlySpacedBuilder_OrdersCopiesByTheirTemporaryPositions()
    {
        var pearl = new Symbol(0, "Pearl");
        var shell = new Symbol(1, "Shell");

        var strip = EvenlySpacedStripBuilder.Build([(pearl, 2), (shell, 1)]);

        // Pearl marks are 0.25 and 0.75; the Shell mark is 0.50.
        Assert.Equal([pearl, shell, pearl], strip);
    }

    [Fact]
    public void ReelPreset_PreservesCompletedStripOrder()
    {
        var pearl = new Symbol(0, "Pearl");
        var shell = new Symbol(1, "Shell");
        Symbol[] publishedParOrder = [shell, pearl, pearl, shell];
        var preset = new ReelPreset(
            "Published order",
            [publishedParOrder],
            [new Payline("Center", [1])]);

        var reels = preset.BuildReels();

        Assert.Equal(publishedParOrder, reels.Strip(0).ToArray());
    }

    [Fact]
    public void SymbolAndIdWindowPaths_DrawTheSameStops()
    {
        var pearl = new Symbol(0, "Pearl");
        var shell = new Symbol(1, "Shell");
        var reels = new StripReelSet([[pearl, shell, shell, pearl]]);
        var symbolWindow = new Symbol[reels.WindowSize];
        var idWindow = new byte[reels.WindowSize];
        var symbolRng = SpinRng.ForWorker(1234, 0);
        var idRng = SpinRng.ForWorker(1234, 0);

        reels.DrawWindow(ref symbolRng, symbolWindow);
        reels.DrawWindowIds(ref idRng, idWindow);

        Assert.Equal(symbolWindow.Select(symbol => symbol.Id), idWindow);
    }
}
