using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Orca Dive checked against the hand method in Muir's "Elements of Slot Design"
/// (3rd ed., ch. 2 and 8): hits-over-cycle factor products, prioritisation discounts,
/// and separated-scatter window counting. Each fact here was derived by the book's
/// formulas independently of the enumerator, so a strip or paytable edit that breaks
/// one of the book's identities fails a named test instead of drifting the RTP quietly.
/// See MMP.SlotGame docs/learnings/muir-elements-of-slot-design.md for the derivations.
/// </summary>
public sealed class MuirCrossCheckTests
{
    private static GameAnalysis Analyse()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "games", "orca-dive.json");
        var definition = GameDefinitionLoader.LoadFile(path);
        return GameAnalyzer.Analyse(definition);
    }

    [Fact]
    public void Five_red_sevens_match_the_plain_factor_product()
    {
        // Wilds substitute for fish only, so the count is the bare per-reel product:
        // 1 * 2 * 1 * 2 * 1.
        Assert.Equal(4, Analyse().CountFor("Red7", 5));
    }

    [Fact]
    public void Exactly_four_red_sevens_carry_the_mixed_seven_discount()
    {
        // Naive book count: 1*2*1*2*(26-1) = 100. Twenty of those put another seven on
        // reel 5, and Red7-4 (100x) ties MixedSeven-5 (100x) — ties go to the longer
        // run, so they leave this bucket: 100 - 4*(2+3) = 80.
        Assert.Equal(80, Analyse().CountFor("Red7", 4));
    }

    [Fact]
    public void Exactly_three_salmon_carry_the_all_wild_discount()
    {
        // Naive wild-inclusive book count: (2+2)(3+1)(4+1)(29-4-1)(26) = 49,920. The
        // 1,248 all-wild-led lines (2*1*1 * 24 * 26) classify as WildOrca instead:
        // 49,920 - 1,248 = 48,672.
        Assert.Equal(48_672, Analyse().CountFor("Salmon", 3));
    }

    [Fact]
    public void The_scatter_trigger_equals_the_separated_window_form()
    {
        // Penguins sit at stops 0 and 13 on each 26-stop scatter reel — separation is
        // at least the window height, so the exact enumeration must equal the clean
        // window-area count (6/26)^3. If someone reorders a strip and two Penguins
        // share a window, this identity breaks and the shipped RTP shifts silently —
        // the exact failure Muir warns about.
        var analysis = Analyse();
        var windowArea = Math.Pow(6.0 / 26.0, 3);
        Assert.Equal(windowArea, analysis.TriggerProbability, precision: 12);
        Assert.Equal(181_656, analysis.TriggerCombinations);
    }
}
