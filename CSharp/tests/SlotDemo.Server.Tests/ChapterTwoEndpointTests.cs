using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// The chapter-2 lab routes. What matters here is that the page cannot show a result the
/// engine would not produce: exact totals stay exact, the refusal path reaches the
/// browser as text rather than as a 500, and 64-bit draws survive the wire.
/// </summary>
public sealed class ChapterTwoEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly WebApplicationFactory<Program> _factory;

    public ChapterTwoEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Money_returns_an_exact_total_and_flags_the_double_that_drifts()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ch2/money",
            new { wagerCredits = 1, scaledMultiplier = 110, repeats = 1_000_000 });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(110_000_000_000L, body.GetProperty("exactTotal").GetProperty("millicents").GetInt64());
        Assert.False(body.GetProperty("floatAgrees").GetBoolean());
        Assert.NotEqual(0, body.GetProperty("driftCredits").GetDouble());
        Assert.Null(body.GetProperty("refusal").GetString());
    }

    [Fact]
    public async Task Money_reports_no_drift_for_a_multiplier_binary_can_hold()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ch2/money",
            new { wagerCredits = 1, scaledMultiplier = 225, repeats = 1_000_000 });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        // 2.25 is a sum of powers of two, so the double column keeps up. The contrast with
        // the test above is the lesson, and it only works if both stay true.
        Assert.True(body.GetProperty("floatAgrees").GetBoolean());
    }

    [Fact]
    public async Task Money_surfaces_the_refusal_instead_of_failing_the_request()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ch2/money",
            new { wagerCredits = 0, wagerMillicents = 12_345, scaledMultiplier = 110, repeats = 10 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var refusal = body.GetProperty("refusal").GetString();
        Assert.NotNull(refusal);
        Assert.Contains("12345", refusal);
    }

    [Theory]
    [InlineData(0, 0, 100, 10)]        // no wager at all
    [InlineData(1, 0, 100, 0)]         // no repeats
    [InlineData(1, 0, -5, 10)]         // negative multiplier
    public async Task Money_rejects_a_request_it_cannot_answer(
        long credits, long millicents, int multiplier, long repeats)
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ch2/money", new
        {
            wagerCredits = credits,
            wagerMillicents = millicents,
            scaledMultiplier = multiplier,
            repeats,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rng_separates_workers_under_mixed_seeding_and_correlates_without_it()
    {
        using var client = _factory.CreateClient();

        var mixed = await PostRng(client, mixed: true);
        var naive = await PostRng(client, mixed: false);

        var mixedShared = mixed.GetProperty("sharedPrefixBits").GetInt32();
        var naiveShared = naive.GetProperty("sharedPrefixBits").GetInt32();

        Assert.True(mixedShared < 16, $"mixed seeding shared {mixedShared} leading bits");
        Assert.True(naiveShared > mixedShared);
        Assert.True(mixed.GetProperty("reproduced").GetBoolean());
    }

    [Fact]
    public async Task Rng_sends_raw_draws_as_hex_so_64_bits_survive_the_wire()
    {
        using var client = _factory.CreateClient();

        var body = await PostRng(client, mixed: true);
        var raw = body.GetProperty("streams")[0].GetProperty("raw");

        foreach (var value in raw.EnumerateArray())
        {
            var hex = value.GetString();
            Assert.NotNull(hex);
            Assert.Equal(16, hex.Length);
            Assert.True(ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out _));
        }
    }

    [Fact]
    public async Task Rng_keeps_reduced_stops_inside_the_strip()
    {
        using var client = _factory.CreateClient();

        var body = await PostRng(client, mixed: true);

        foreach (var stream in body.GetProperty("streams").EnumerateArray())
        foreach (var stop in stream.GetProperty("reduced").EnumerateArray())
            Assert.InRange(stop.GetInt32(), 0, 31);
    }

    [Fact]
    public async Task Bias_shows_modulo_skewing_further_than_the_multiply_shift()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ch2/bias",
            new { seed = 7, bound = 37, bits = 8, samples = 200_000 });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        var modulo = body.GetProperty("moduloWorstErrorPercent").GetDouble();
        var lemire = body.GetProperty("lemireWorstErrorPercent").GetDouble();

        Assert.True(modulo > lemire * 2,
            $"modulo worst {modulo:F2}% should dwarf multiply-shift worst {lemire:F2}%");
        Assert.True(body.GetProperty("rejections").GetInt32() > 0);

        // Both histograms must count the same number of samples, or the comparison on the
        // page is between two differently sized populations and proves nothing.
        Assert.Equal(200_000, Sum(body.GetProperty("moduloCounts")));
        Assert.Equal(200_000, Sum(body.GetProperty("lemireCounts")));
    }

    [Fact]
    public async Task Bias_rejects_a_bound_wider_than_the_draw_space()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ch2/bias",
            new { seed = 7, bound = 500, bits = 8, samples = 1_000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<JsonElement> PostRng(HttpClient client, bool mixed)
    {
        var response = await client.PostAsJsonAsync("/api/ch2/rng",
            new { seed = 20260810, workerCount = 4, draws = 5, bound = 32, mixed });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private static int Sum(JsonElement counts) =>
        counts.EnumerateArray().Sum(c => c.GetInt32());
}
