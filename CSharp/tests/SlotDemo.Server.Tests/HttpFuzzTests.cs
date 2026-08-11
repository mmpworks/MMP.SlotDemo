using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// Seeded HTTP fuzz against the in-process server: random paths (traversal attempts,
/// percent-encoding, unicode, long segments) and random methods on the API surface.
/// Invariants: no request produces a 5xx, and known traversal shapes never return
/// server-source or package-manifest content.
/// </summary>
public sealed class HttpFuzzTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const int Iterations = 300;
    private const int Seed = 0xBEEF;

    private readonly WebApplicationFactory<Program> _factory;

    public HttpFuzzTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task RandomPaths_NeverProduce5xx()
    {
        using var client = _factory.CreateClient();
        var rng = new Random(Seed);
        var sent = 0;

        for (var i = 0; i < Iterations; i++)
        {
            var path = RandomPath(rng);
            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(RandomMethod(rng), path);
                response = await client.SendAsync(request);
            }
            catch (Exception e) when (e is InvalidOperationException or UriFormatException or HttpRequestException)
            {
                continue; // HttpClient refused to send this shape — nothing reached the server
            }

            sent++;
            Assert.True((int)response.StatusCode < 500,
                $"iteration {i}: {path} -> {(int)response.StatusCode}");
            response.Dispose();
        }

        Assert.True(sent > Iterations / 2, $"only {sent}/{Iterations} requests were sendable");
    }

    [Fact]
    public async Task TraversalAttempts_DoNotEscapeSpaRoot()
    {
        using var client = _factory.CreateClient();
        string[] attacks =
        [
            "/../../src/SlotDemo.Server/Program.cs",
            "/..%2f..%2fsrc%2fSlotDemo.Server%2fProgram.cs",
            "/%2e%2e/%2e%2e/docs/PRD.md",
            "/assets/../../package.json",
            "/..\\..\\CSharp\\web\\package.json",
        ];
        foreach (var attack in attacks)
        {
            using var response = await client.GetAsync(attack);
            var body = await response.Content.ReadAsStringAsync();
            // SPA fallback may answer 200 with index.html — the invariant is that no
            // source file content leaks, and no 5xx surfaces.
            Assert.True((int)response.StatusCode < 500, $"{attack} -> {(int)response.StatusCode}");
            Assert.DoesNotContain("WebApplication.CreateBuilder", body);
            Assert.DoesNotContain("\"scripts\"", body);
        }
    }

    [Fact]
    public async Task ApiEndpoints_SurviveMethodAndQueryFuzz()
    {
        using var client = _factory.CreateClient();
        var rng = new Random(Seed + 1);
        string[] bases = ["/api/hello", "/api/stats"];

        for (var i = 0; i < 60; i++)
        {
            var url = bases[rng.Next(bases.Length)] + RandomQuery(rng);
            using var request = new HttpRequestMessage(RandomMethod(rng), url);
            if (rng.Next(3) == 0)
                request.Content = new StringContent(RandomToken(rng, 200));
            using var response = await client.SendAsync(request);
            Assert.True((int)response.StatusCode < 500, $"{request.Method} {url} -> {(int)response.StatusCode}");
        }
    }

    private static HttpMethod RandomMethod(Random rng) => rng.Next(6) switch
    {
        0 => HttpMethod.Get,
        1 => HttpMethod.Post,
        2 => HttpMethod.Put,
        3 => HttpMethod.Delete,
        4 => HttpMethod.Options,
        _ => HttpMethod.Head,
    };

    private static string RandomPath(Random rng)
    {
        var segments = rng.Next(1, 6);
        var parts = Enumerable.Range(0, segments).Select(_ => rng.Next(8) switch
        {
            0 => "..",
            1 => "%2e%2e",
            2 => "api",
            3 => new string('a', rng.Next(1, 2000)),
            4 => Uri.EscapeDataString(RandomToken(rng, 12)),
            _ => RandomToken(rng, rng.Next(1, 20)),
        });
        return "/" + string.Join('/', parts);
    }

    private static string RandomQuery(Random rng) =>
        rng.Next(2) == 0 ? "" : $"?{RandomToken(rng, 8)}={Uri.EscapeDataString(RandomToken(rng, 30))}";

    private static string RandomToken(Random rng, int maxLength)
    {
        const string alphabet = "abcXYZ019._-~!$&'()*+,;=:@ %é中";
        var length = rng.Next(1, maxLength + 1);
        return new string(Enumerable.Range(0, length).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());
    }
}
