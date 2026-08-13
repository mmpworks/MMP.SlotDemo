using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// Seeded fuzz across every lab and run route: random field values (extremes, negatives,
/// overflow-shaped numbers), random junk bodies, and malformed JSON. The invariant is
/// blunt — no request produces a 5xx. Bad input earns a 400 with a reason; the labs are
/// a public-facing teaching surface and a stack trace teaches the wrong lesson.
/// </summary>
public sealed class LabEndpointFuzzTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const int IterationsPerRoute = 60;
    private const int Seed = 0x51071;

    private static readonly string[] PostRoutes =
    [
        "/api/ch2/money", "/api/ch2/rng", "/api/ch2/bias",
        "/api/ch3/spin", "/api/ch3/census", "/api/ch3/reel-snapshots",
        "/api/ch4/solve", "/api/ch4/band",
        "/api/ch6/validate",
        "/api/ch7/enumerate",
    ];

    private readonly WebApplicationFactory<Program> _factory;

    public LabEndpointFuzzTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Random_field_values_never_produce_a_5xx()
    {
        using var client = _factory.CreateClient();
        var rng = new Random(Seed);

        foreach (var route in PostRoutes)
        {
            for (var i = 0; i < IterationsPerRoute; i++)
            {
                var body = RandomBody(rng);
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(route, content);

                Assert.True((int)response.StatusCode < 500,
                    $"{route} iteration {i}: {(int)response.StatusCode} for body {Truncate(body)}");
            }
        }
    }

    [Fact]
    public async Task Malformed_json_is_a_client_error_on_every_route()
    {
        using var client = _factory.CreateClient();
        string[] garbage =
        [
            "", "{", "null", "[]", "\"a string\"", "{\"unterminated\": ",
            "{\"presetName\": {\"nested\": true}}",
            new string('x', 10_000),
        ];

        foreach (var route in PostRoutes)
        foreach (var body in garbage)
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(route, content);
            Assert.True((int)response.StatusCode is >= 400 and < 500,
                $"{route}: {(int)response.StatusCode} for {Truncate(body)}");
        }
    }

    [Fact]
    public async Task Run_start_survives_hostile_numeric_extremes()
    {
        using var client = _factory.CreateClient();
        var rng = new Random(Seed + 1);

        for (var i = 0; i < 40; i++)
        {
            var body = $$"""
                {
                  "presetName": {{JsonString(RandomPresetName(rng))}},
                  "baseRtpBasisPoints": {{RandomExtreme(rng)}},
                  "freeSpinsRtpBasisPoints": {{RandomExtreme(rng)}},
                  "pickBonusRtpBasisPoints": {{RandomExtreme(rng)}},
                  "seed": {{(ulong)rng.NextInt64()}},
                  "workerCount": {{RandomExtreme(rng)}},
                  "targetSpins": {{RandomExtreme(rng)}},
                  "stride": {{RandomExtreme(rng)}}
                }
                """;
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/run", content);

            Assert.True((int)response.StatusCode < 500,
                $"iteration {i}: {(int)response.StatusCode} for {Truncate(body)}");

            // The rare valid combination starts a real run; stop it so the fixture's
            // single-run gate stays clear for the next iteration.
            if (response.StatusCode == HttpStatusCode.Created)
            {
                await client.PostAsync("/api/run/cancel", null);
                await WaitForTerminalAsync(client);
            }
        }
    }

    private static string RandomBody(Random rng)
    {
        // Field-name soup drawn from every route's real fields plus junk; values from a
        // hostile pool. Shapes that miss required fields, mistype them, or overflow them
        // all land here.
        string[] fields =
        [
            "wagerCredits", "scaledMultiplier", "repeats", "wagerMillicents",
            "seed", "workerCount", "draws", "bound", "mixed", "bits", "samples",
            "presetName", "spinIndex", "spins", "symbolId",
            "targetBaseRtpBasisPoints", "baseRtpBasisPoints", "freeSpinsRtpBasisPoints",
            "pickBonusRtpBasisPoints", "json", "gameFile", "stride", "channelCapacity",
            "unknownField", "constructor", "__proto__",
        ];

        var count = rng.Next(0, 8);
        var parts = new List<string>(count);
        for (var i = 0; i < count; i++)
            parts.Add($"{JsonString(fields[rng.Next(fields.Length)])}: {RandomValue(rng)}");
        return "{" + string.Join(", ", parts) + "}";
    }

    private static string RandomValue(Random rng) => rng.Next(10) switch
    {
        0 => RandomExtreme(rng).ToString(),
        1 => "null",
        2 => "true",
        3 => "-0.0000001",
        4 => "1e308",
        5 => JsonString(new string('z', rng.Next(1, 200))),
        6 => "[1,2,3]",
        7 => "{\"a\": 1}",
        8 => "\"Classic3\"",
        _ => rng.Next(-100, 10_000).ToString(),
    };

    private static long RandomExtreme(Random rng) => rng.Next(8) switch
    {
        0 => long.MinValue,
        1 => long.MaxValue,
        2 => int.MinValue,
        3 => int.MaxValue,
        4 => -1,
        5 => 0,
        6 => rng.Next(1, 100),
        _ => rng.NextInt64(),
    };

    private static string RandomPresetName(Random rng) => rng.Next(4) switch
    {
        0 => "Classic3",
        1 => "",
        2 => new string('A', 500),
        _ => "nope'; DROP TABLE presets; --",
    };

    private static string JsonString(string value) =>
        System.Text.Json.JsonSerializer.Serialize(value);

    private static string Truncate(string s) => s.Length <= 160 ? s : s[..160] + "…";

    private static async Task WaitForTerminalAsync(HttpClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync("/api/run/current");
            if (response.StatusCode == HttpStatusCode.NoContent) return;
            var body = await response.Content.ReadAsStringAsync();
            if (body.Contains("\"completed\"") || body.Contains("\"cancelled\"")) return;
            await Task.Delay(50);
        }
    }
}
