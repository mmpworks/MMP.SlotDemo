using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

public sealed class ChapterThreeEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly WebApplicationFactory<Program> _factory;

    public ChapterThreeEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Reel_snapshots_accept_different_lengths_and_repeat_the_same_configuration()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/ch3/reel-snapshots", new { seed = 42 });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(26, body.GetProperty("shortSnapshot").GetProperty("stops").GetInt32());
        Assert.Equal(36, body.GetProperty("longSnapshot").GetProperty("stops").GetInt32());
        Assert.True(body.GetProperty("shortSnapshotRepeatedExactly").GetBoolean());
        Assert.True(body.GetProperty("shortSnapshotKeptOriginalFirstSymbol").GetBoolean());
        Assert.Equal(
            body.GetProperty("shortSnapshot").GetProperty("window").GetRawText(),
            body.GetProperty("repeatedShortSnapshot").GetProperty("window").GetRawText());
    }

    [Fact]
    public async Task Sources_include_the_loaded_games_par_payouts()
    {
        using var client = _factory.CreateClient();
        var sources = await client.GetFromJsonAsync<JsonElement>("/api/ch3/sources", Json);
        var orca = sources.EnumerateArray()
            .Single(source => source.GetProperty("id").GetString() == "game:orca-dive.json");
        var seal = orca.GetProperty("paytable").EnumerateArray()
            .Single(category => category.GetProperty("name").GetString() == "Seal");
        var threeReelPay = seal.GetProperty("pays").EnumerateArray()
            .Single(pay => pay.GetProperty("count").GetInt32() == 3);

        Assert.Equal(2_000, threeReelPay.GetProperty("payHundredths").GetInt32());
    }
}
