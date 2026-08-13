using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

public sealed class ChapterNineEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Benchmark_compares_equal_output_and_reports_both_rates()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/ch9/draw-window", new
        {
            sourceId = "Video5x64",
            seed = 42,
            spins = 100_000,
            trials = 3,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(500_000, body.GetProperty("randomSelections").GetInt64());
        Assert.Equal(1_500_000, body.GetProperty("visibleCellWrites").GetInt64());
        Assert.True(body.GetProperty("baseline").GetProperty("medianSpinsPerSecond").GetDouble() > 0);
        Assert.True(body.GetProperty("optimized").GetProperty("medianSpinsPerSecond").GetDouble() > 0);

        // A body arrives only when both implementations drew the same stream, so the
        // shared checksum is the correctness gate the page reports.
        Assert.StartsWith("0x", body.GetProperty("checksum").GetString());
    }

    [Theory]
    [InlineData("Video5x64", 99_999, 3)]      // below the spin floor
    [InlineData("Video5x64", 10_000_001, 3)]  // above the spin ceiling
    [InlineData("Video5x64", 100_000, 2)]     // trials below the floor
    [InlineData("Video5x64", 100_000, 11)]    // trials above the ceiling
    [InlineData("Video5x64", 100_000, 4)]     // an even trial count cannot have a median pairing
    [InlineData("NoSuchReelSource", 100_000, 3)]
    public async Task Benchmark_rejects_a_request_it_cannot_answer(string sourceId, int spins, int trials)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ch9/draw-window", new
        {
            sourceId,
            seed = 42,
            spins,
            trials,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("error").GetString()));
    }
}
