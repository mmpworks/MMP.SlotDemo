using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// Covers the PAR endpoints and, more broadly, the class of bug found on 2026-08-19:
/// endpoints that read <c>GameAnalysis.CombinationCounts</c> without allowing for it being
/// empty.
///
/// That dictionary is empty BY DESIGN for any game with more than one payline
/// (<c>GameAnalyzer.Analyze</c> routes those to <c>AnalyzePhysicalOutcomes</c>, which cannot
/// attribute one window to a single category). Two-Line Tide is the first shipped game with
/// two paylines, so it is the first game to exercise that path end to end.
///
/// The PAR endpoints had no server tests before this file, which is why a live 500 on
/// <c>/api/par/summary</c> sat behind a green suite.
/// </summary>
public sealed class ParSheetEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Every game shipped in <c>CSharp/games</c>.</summary>
    private static readonly string[] Shipped =
    [
        "classic-three-reel.json",
        "orca-dive.json",
        "two-line-tide.json",
    ];

    public static TheoryData<string> ShippedGames => [.. Shipped];

    private const string MultiPaylineGame = "two-line-tide.json";
    private const string SinglePaylineGame = "orca-dive.json";

    private readonly WebApplicationFactory<Program> _factory;

    public ParSheetEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ---- the reported bug -------------------------------------------------------------

    /// <summary>
    /// Regression pin. The summary picked the highest-paying rule with First(), so a game
    /// with an empty category breakdown threw InvalidOperationException and returned 500 for
    /// EVERY game on the page, not only that one. Fails on the pre-fix code.
    /// </summary>
    [Fact]
    public async Task Summary_returns_a_row_for_every_shipped_game()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/par/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rows = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var files = rows.EnumerateArray().Select(row => row.GetProperty("file").GetString()).ToArray();

        foreach (var game in Shipped)
        {
            Assert.Contains(game, files);
        }
    }

    /// <summary>
    /// A multi-payline game has no single "jackpot rule" to report, and it still belongs in
    /// the summary. Dropping it would keep the endpoint green while quietly losing a shipped
    /// game from the page, which is the failure the crash was masking.
    ///
    /// The empty columns report null rather than 0. This assertion asked for 0 until the
    /// 2026-08-19 math review pointed out that a 0 in a "plays per jackpot" column reads as
    /// a measurement of an impossibly common event rather than an absent one. playsPerBonus
    /// already used null for the same reason, so the row is now internally consistent.
    /// </summary>
    [Fact]
    public async Task Summary_keeps_a_multi_payline_game_and_reports_no_jackpot_columns()
    {
        using var client = _factory.CreateClient();
        var rows = await client.GetFromJsonAsync<JsonElement>("/api/par/summary", Json);

        var tide = rows.EnumerateArray()
            .Single(row => row.GetProperty("file").GetString() == MultiPaylineGame);

        Assert.Equal(2, tide.GetProperty("lines").GetInt32());
        Assert.True(tide.GetProperty("paybackPercent").GetDouble() > 0);
        Assert.Equal(JsonValueKind.Null, tide.GetProperty("jackpotCredits").ValueKind);
        Assert.Equal(JsonValueKind.Null, tide.GetProperty("playsPerJackpot").ValueKind);

        // A single-payline game still reports both, so the null is about this game's shape
        // rather than the columns having been dropped from the payload.
        var orca = rows.EnumerateArray()
            .Single(row => row.GetProperty("file").GetString() == SinglePaylineGame);
        Assert.Equal(JsonValueKind.Number, orca.GetProperty("jackpotCredits").ValueKind);
        Assert.Equal(JsonValueKind.Number, orca.GetProperty("playsPerJackpot").ValueKind);
    }

    // ---- the class: every consumer of CombinationCounts --------------------------------

    [Theory]
    [MemberData(nameof(ShippedGames))]
    public async Task Sheet_renders_for_every_shipped_game(string game)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/par/sheet", new { gameFile = game });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(body.GetProperty("supported").GetBoolean());
        Assert.True(body.GetProperty("totals").GetProperty("totalRtp").GetDouble() > 0);
    }

    [Theory]
    [MemberData(nameof(ShippedGames))]
    public async Task Enumerate_renders_for_every_shipped_game(string game)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/ch7/enumerate", new { gameFile = game });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ShippedGames))]
    public async Task Published_paytable_renders_for_every_shipped_game(string game)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/ch4/published", new { gameFile = game });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- the documented design this all rests on ---------------------------------------

    /// <summary>
    /// Pins the analyzer contract the endpoints above depend on. A single-payline game
    /// reports a per-category breakdown; a multi-payline game reports none, because one
    /// stopped window can pay several categories at once. If this ever flips, the endpoint
    /// tests alone would not say why, so state it here.
    /// </summary>
    [Fact]
    public async Task Single_payline_game_reports_a_category_breakdown()
    {
        using var client = _factory.CreateClient();
        var body = await PostSheet(client, SinglePaylineGame);

        Assert.Equal(1, body.GetProperty("game").GetProperty("paylines").GetArrayLength());
        Assert.NotEmpty(body.GetProperty("paytable").EnumerateArray());
    }

    [Fact]
    public async Task Multi_payline_game_reports_no_category_breakdown()
    {
        using var client = _factory.CreateClient();
        var body = await PostSheet(client, MultiPaylineGame);

        Assert.True(body.GetProperty("game").GetProperty("paylines").GetArrayLength() > 1);
        Assert.Empty(body.GetProperty("paytable").EnumerateArray());

        // The aggregates are still real: the empty breakdown is an attribution limit, never
        // a claim that the game pays nothing.
        Assert.True(body.GetProperty("totals").GetProperty("hitCombinations").GetInt64() > 0);
        Assert.True(body.GetProperty("totals").GetProperty("lineRtp").GetDouble() > 0);
    }

    // ---- boundary neighbors -------------------------------------------------------------

    [Fact]
    public async Task Sheet_rejects_a_game_that_is_not_shipped()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/par/sheet", new { gameFile = "no-such-game.json" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sheet_rejects_a_path_escape()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/par/sheet",
            new { gameFile = "../../appsettings.json" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<JsonElement> PostSheet(HttpClient client, string game)
    {
        var response = await client.PostAsJsonAsync("/api/par/sheet", new { gameFile = game });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }
}
