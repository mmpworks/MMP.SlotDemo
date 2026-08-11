using System.Text.Json.Nodes;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The loader is the one validation boundary for imported games, so these are the tests that
/// decide whether a bad PAR-sheet transcription fails loudly or quietly produces wrong odds.
///
/// Each case takes the REAL Orca Dive file and breaks one thing in it. Mutating the real
/// file rather than hand-rolling a minimal broken one matters: a minimal fixture drifts away
/// from the shipped schema and stops proving anything about the file that actually loads.
/// </summary>
[Trait("Category", "Fast")]
public sealed class GameDefinitionLoaderTests
{
    private static IReadOnlyList<string> ErrorsAfter(Action<JsonObject> break_)
    {
        var document = JsonNode.Parse(GameFiles.ReadJson(GameFiles.OrcaDive))!.AsObject();
        break_(document);
        GameDefinitionLoader.TryLoad(document.ToJsonString(), out _, out var errors);
        return errors;
    }

    private static void AssertReports(string fragment, Action<JsonObject> break_)
    {
        var errors = ErrorsAfter(break_);
        Assert.True(
            errors.Any(e => e.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
            $"Expected an error mentioning '{fragment}'. Got:\n  " + string.Join("\n  ", errors));
    }

    [Theory]
    [InlineData(GameFiles.OrcaDive)]
    [InlineData(GameFiles.ClassicThreeReel)]
    public void ShippedDefinitions_LoadWithoutErrors(string name)
    {
        var loaded = GameDefinitionLoader.TryLoad(GameFiles.ReadJson(name), out var definition, out var errors);
        Assert.True(loaded, "Shipped definition failed to load:\n  " + string.Join("\n  ", errors));
        Assert.NotNull(definition);
        Assert.Empty(errors);
    }

    [Fact]
    public void MissingRequiredSections_AreReported()
    {
        AssertReports("name is required", d => d.Remove("name"));
        AssertReports("symbols is required", d => d.Remove("symbols"));
        AssertReports("reels is required", d => d.Remove("reels"));
        AssertReports("paylines is required", d => d.Remove("paylines"));
        AssertReports("paytable is required", d => d.Remove("paytable"));
    }

    [Fact]
    public void StripsReferencingUnknownSymbols_AreReported() =>
        AssertReports("not a declared symbol", d => d["reels"]![0]![0] = "Nope");

    [Fact]
    public void DuplicateSymbolNames_AreReported() =>
        AssertReports("repeats the name", d => d["symbols"]![1]!["name"] = "Red7");

    /// <summary>
    /// The two PAR-sheet cross-checks. A published sheet states its strip lengths and its
    /// per-reel symbol counts, and a hand transcription gets exactly these wrong, so the
    /// loader verifies the strips against both rather than trusting the typing.
    /// </summary>
    [Fact]
    public void DeclaredGeometryThatDisagreesWithTheStrips_IsReported()
    {
        AssertReports("declares 25 stops", d => d["reelStops"]![0] = 25);
        AssertReports("symbolCounts declares 9", d => d["symbolCounts"]!["Seal"]![0] = 9);
    }

    [Fact]
    public void BadPaylines_AreReported()
    {
        AssertReports("outside the 3-row window", d => d["paylines"]![0]!["rows"]![0] = 3);
        AssertReports("one row per reel", d => d["paylines"]![0]!["rows"] = new JsonArray(1, 1, 1));
    }

    [Fact]
    public void BadPaytableEntries_AreReported()
    {
        AssertReports("not a declared symbol", d => d["paytable"]![0]!["symbol"] = "Nope");
        AssertReports("not a declared group", d => d["paytable"]![9]!["group"] = "Nope");
        AssertReports("exactly one of symbol or group", d => d["paytable"]![0]!["group"] = "AnySeven");
        AssertReports("outside 1..5", d => d["paytable"]![0]!["pays"]!["6"] = 1);
    }

    [Fact]
    public void BadSubstitutions_AreReported()
    {
        AssertReports("not marked wild", d => d["symbols"]![3]!["substitutesFor"] = new JsonArray("Mackerel"));
        AssertReports("not a declared symbol", d => d["symbols"]![4]!["substitutesFor"] = new JsonArray("Nope"));
    }

    [Fact]
    public void BadFeatures_AreReported()
    {
        AssertReports("not marked scatter", d => d["features"]![0]!["scatter"] = "Seal");
        AssertReports("outside 0..4", d => d["features"]![0]!["requiredReels"] = new JsonArray(9));
        AssertReports("at least one blank", d => d["features"]![0]!["blanks"]!["count"] = 0);
        AssertReports("not recognised", d => d["features"]![0]!["kind"] = "someFutureThing");
    }

    /// <summary>
    /// Errors are COLLECTED, not thrown one at a time. Someone transcribing a PAR sheet wants
    /// the whole list, not a dozen edit-and-rerun cycles.
    /// </summary>
    [Fact]
    public void EveryProblemIsReportedTogether()
    {
        var errors = ErrorsAfter(d =>
        {
            d["reelStops"]![0] = 25;
            d["reelStops"]![1] = 30;
            d["paylines"]![0]!["rows"]![0] = 7;
        });

        Assert.True(errors.Count >= 3, "Expected all three problems at once, got:\n  " + string.Join("\n  ", errors));
    }

    [Fact]
    public void MalformedJson_FailsWithASlotMessageNotAParserStackTrace()
    {
        Assert.False(GameDefinitionLoader.TryLoad("{ not json", out _, out var errors));
        Assert.Contains(errors, e => e.Contains("not valid JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadFile_ThrowsWithEveryProblemListed()
    {
        var broken = Path.Combine(Path.GetTempPath(), $"slotgame-broken-{Guid.NewGuid():n}.json");
        try
        {
            var document = JsonNode.Parse(GameFiles.ReadJson(GameFiles.OrcaDive))!.AsObject();
            document["reelStops"]![0] = 25;
            File.WriteAllText(broken, document.ToJsonString());

            var ex = Assert.Throws<GameDefinitionException>(() => GameDefinitionLoader.LoadFile(broken));
            Assert.NotEmpty(ex.Errors);
            Assert.Contains("declares 25 stops", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(broken);
        }
    }
}
