using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// The readiness gate the page holds its run button behind.
///
/// The engine reports a fraction of its real speed until .NET has compiled and re-optimized
/// the spin loop, so a visitor's first run used to time a compilation. The server now warms
/// the engine at startup and reports when a run is worth timing.
/// </summary>
public sealed class EngineReadinessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly WebApplicationFactory<Program> _factory;

    public EngineReadinessTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Readiness_reports_the_threshold_it_is_judging_against()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/run/readiness");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        // The page reads the threshold from here rather than keeping its own copy, so a
        // missing or zero value would leave it judging warm-up runs against nothing.
        Assert.True(body.GetProperty("thresholdSpinsPerSecond").GetDouble() > 0);
        Assert.True(body.GetProperty("passesRun").GetInt32() >= 0);
    }

    /// <summary>
    /// Warm-up always finishes. A machine that never reaches the threshold still reports
    /// ready, because a run button that never opens is worse than an honest slower number;
    /// "settled" is what says whether the target was actually reached.
    /// </summary>
    [Fact]
    public async Task Warmup_finishes_and_opens_the_run_button()
    {
        using var client = _factory.CreateClient();

        var deadline = DateTime.UtcNow.AddSeconds(90);
        JsonElement body;
        while (true)
        {
            body = await client.GetFromJsonAsync<JsonElement>("/api/run/readiness", Json);
            if (body.GetProperty("ready").GetBoolean()) break;
            if (DateTime.UtcNow > deadline) Assert.Fail("Warm-up never reported ready.");
            await Task.Delay(200);
        }

        Assert.True(body.GetProperty("passesRun").GetInt32() >= 1);
        Assert.True(body.GetProperty("bestSpinsPerSecond").GetDouble() > 0);

        // Settled is a claim about speed, so it may only be true when the measured rate
        // actually reached the threshold.
        if (body.GetProperty("settled").GetBoolean())
        {
            Assert.True(
                body.GetProperty("bestSpinsPerSecond").GetDouble()
                    >= body.GetProperty("thresholdSpinsPerSecond").GetDouble(),
                "reported settled without reaching the threshold.");
        }
    }
}
