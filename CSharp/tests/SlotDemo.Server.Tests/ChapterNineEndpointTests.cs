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

        Assert.True(body.GetProperty("outputsMatch").GetBoolean());
        Assert.Equal(500_000, body.GetProperty("randomSelections").GetInt64());
        Assert.Equal(1_500_000, body.GetProperty("visibleCellWrites").GetInt64());
        Assert.True(body.GetProperty("baseline").GetProperty("medianSpinsPerSecond").GetDouble() > 0);
        Assert.True(body.GetProperty("optimized").GetProperty("medianSpinsPerSecond").GetDouble() > 0);
    }
}
