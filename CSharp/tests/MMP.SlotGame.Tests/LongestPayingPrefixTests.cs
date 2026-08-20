using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using Xunit;

namespace MMP.SlotGame.Tests;

/// <summary>
/// A line pays its longest PAYING prefix.
///
/// Real PAR sheets are transcribed into the JSON format this engine loads, and a real sheet
/// can list a symbol that pays at three of a kind and nothing above it. Landing four or five
/// of that symbol is a strictly better outcome, so it must not pay strictly less. Awarding
/// zero there would be invisible: the analytic enumeration uses this same evaluator, so the
/// analysis and the simulation would agree with each other and both be wrong.
///
/// Every game shipped today declares its pays contiguously to the top, so these cases change
/// no published number. They guard the first reader who transcribes a sparser sheet.
/// </summary>
public sealed class LongestPayingPrefixTests
{
    /// <summary>Five reels; Ace pays only at three of a kind. Wild substitutes for Ace.</summary>
    private const string SparsePayJson = """
    {
      "name": "Sparse Pay",
      "windowRows": 3,
      "symbols": [
        { "name": "Ace" },
        { "name": "King" },
        { "name": "Wild", "wild": true, "substitutesFor": ["Ace", "King"] },
        { "name": "Blank" }
      ],
      "reels": [
        ["Ace", "King", "Wild", "Blank"],
        ["Ace", "King", "Wild", "Blank"],
        ["Ace", "King", "Wild", "Blank"],
        ["Ace", "King", "Wild", "Blank"],
        ["Ace", "King", "Wild", "Blank"]
      ],
      "paylines": [{ "name": "Center", "rows": [1, 1, 1, 1, 1] }],
      "paytable": [
        { "symbol": "Ace", "pays": { "3": 10 } },
        { "symbol": "King", "pays": { "3": 5, "4": 20, "5": 100 } }
      ]
    }
    """;

    private static (GameDefinition Game, WinEvaluator Evaluator) Load()
    {
        Assert.True(GameDefinitionLoader.TryLoad(SparsePayJson, out var game, out var errors),
            $"definition failed to load: {string.Join("; ", errors)}");
        return (game!, new WinEvaluator(game!));
    }

    private static byte Id(GameDefinition game, string name) => (byte)game.SymbolId(name);

    [Fact]
    public void A_run_longer_than_the_top_pay_entry_still_pays_that_entry()
    {
        var (game, evaluator) = Load();
        byte ace = Id(game, "Ace"), blank = Id(game, "Blank");

        // Compare against the compiled table rather than the declared number: the loader
        // stores pays in hundredths of the wager, and the point here is the run length
        // chosen, not the unit.
        var acePaysThree = game.Category("Ace").PayFor(3);

        var three = evaluator.Evaluate([ace, ace, ace, blank, blank]);
        var five = evaluator.Evaluate([ace, ace, ace, ace, ace]);

        Assert.Equal(acePaysThree, three.Multiplier);
        Assert.True(five.IsWin, "five of a kind paid nothing while three of a kind paid.");
        Assert.Equal(acePaysThree, five.Multiplier);
    }

    [Fact]
    public void Four_of_a_kind_pays_the_three_entry_when_no_four_entry_exists()
    {
        var (game, evaluator) = Load();
        byte ace = Id(game, "Ace"), blank = Id(game, "Blank");

        var four = evaluator.Evaluate([ace, ace, ace, ace, blank]);

        Assert.Equal(game.Category("Ace").PayFor(3), four.Multiplier);
    }

    /// <summary>
    /// Shortening the run must not smuggle in a win the category never satisfied. Three
    /// wilds followed by an Ace satisfies Ace only at the fourth reel, so the paying
    /// prefixes are length four and up. Ace has no four entry, so this line pays nothing
    /// on the Ace category.
    /// </summary>
    [Fact]
    public void A_prefix_that_never_satisfies_the_category_does_not_pay_it()
    {
        var (game, evaluator) = Load();
        byte ace = Id(game, "Ace"), wild = Id(game, "Wild"), blank = Id(game, "Blank");

        var win = evaluator.Evaluate([wild, wild, wild, ace, blank]);

        Assert.NotEqual(game.Category("Ace").Index, win.CategoryIndex);
    }

    /// <summary>
    /// The ordinary case is unchanged: a table that pays at every length still pays the
    /// entry for the run actually achieved, which is the highest of them.
    /// </summary>
    [Fact]
    public void A_contiguous_table_still_pays_the_entry_for_the_run_it_reached()
    {
        var (game, evaluator) = Load();
        byte king = Id(game, "King"), blank = Id(game, "Blank");

        var king3 = game.Category("King");
        Assert.Equal(king3.PayFor(3), evaluator.Evaluate([king, king, king, blank, blank]).Multiplier);
        Assert.Equal(king3.PayFor(4), evaluator.Evaluate([king, king, king, king, blank]).Multiplier);
        Assert.Equal(king3.PayFor(5), evaluator.Evaluate([king, king, king, king, king]).Multiplier);
    }
}
