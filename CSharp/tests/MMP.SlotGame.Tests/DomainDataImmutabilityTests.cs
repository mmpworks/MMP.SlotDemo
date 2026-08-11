using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

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
}
