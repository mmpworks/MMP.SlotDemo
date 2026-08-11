using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// The finale's start/watch/stop surface, end to end against the in-process server:
/// a real run with real workers, watched to completion through the public API only.
/// </summary>
public sealed class RunEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly WebApplicationFactory<Program> _factory;

    public RunEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static object Request(long spins = 200_000, int stride = 10_000, ulong seed = 99) => new
    {
        presetName = "Classic3",
        baseRtpBasisPoints = 7500,
        freeSpinsRtpBasisPoints = 1300,
        pickBonusRtpBasisPoints = 1000,
        seed,
        workerCount = 4,
        targetSpins = spins,
        stride,
    };

    [Fact]
    public async Task Limits_expose_the_cap_defaults_and_presets()
    {
        using var client = _factory.CreateClient();
        var body = await client.GetFromJsonAsync<JsonElement>("/api/run/limits", Json);

        Assert.Equal(9_900, body.GetProperty("maxAggregateBasisPoints").GetInt32());
        Assert.Equal("Video5x64", body.GetProperty("defaults").GetProperty("presetName").GetString());
        Assert.Equal(5, body.GetProperty("presets").GetArrayLength());
    }

    [Fact]
    public async Task A_run_completes_with_a_curve_and_a_verdict_inside_the_band()
    {
        using var client = _factory.CreateClient();

        var started = await client.PostAsJsonAsync("/api/run", Request());
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var startBody = await started.Content.ReadFromJsonAsync<JsonElement>(Json);
        var analyticRtp = startBody.GetProperty("analytic").GetProperty("totalRtp").GetDouble();

        var final = await WaitForCompletionAsync(client);

        Assert.Equal(200_000, final.GetProperty("latest").GetProperty("spins").GetInt64());
        var curve = final.GetProperty("curve");
        Assert.True(curve.GetArrayLength() >= 10, $"only {curve.GetArrayLength()} curve points");

        // Spins strictly increase along the curve, and the last point carries the verdict.
        long previous = 0;
        foreach (var point in curve.EnumerateArray())
        {
            var spins = point.GetProperty("spins").GetInt64();
            Assert.True(spins > previous);
            previous = spins;
        }

        var last = curve[curve.GetArrayLength() - 1];
        var measured = last.GetProperty("measuredRtp").GetDouble();
        var band = last.GetProperty("bandHalfWidth").GetDouble();
        Assert.Equal(Math.Abs(measured - analyticRtp) <= band, last.GetProperty("withinBand").GetBoolean());
    }

    [Fact]
    public async Task The_same_seed_reproduces_the_same_final_totals_through_the_api()
    {
        using var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/run", Request(spins: 100_000, seed: 777))).EnsureSuccessStatusCode();
        var first = await WaitForCompletionAsync(client);

        (await client.PostAsJsonAsync("/api/run", Request(spins: 100_000, seed: 777))).EnsureSuccessStatusCode();
        var second = await WaitForCompletionAsync(client);

        Assert.Equal(
            first.GetProperty("latest").GetProperty("returnedMillicents").GetInt64(),
            second.GetProperty("latest").GetProperty("returnedMillicents").GetInt64());
    }

    [Fact]
    public async Task A_second_start_while_running_is_refused_not_queued()
    {
        using var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/run", Request(spins: 3_000_000))).EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/api/run", Request());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        (await client.PostAsync("/api/run/cancel", null)).EnsureSuccessStatusCode();
        await WaitForCompletionAsync(client, acceptCancelled: true);
    }

    [Fact]
    public async Task An_aggregate_over_the_cap_is_rejected_with_the_reason()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/run", new
        {
            presetName = "Classic3",
            baseRtpBasisPoints = 9_000,
            freeSpinsRtpBasisPoints = 900,
            pickBonusRtpBasisPoints = 100,
            seed = 1,
            workerCount = 1,
            targetSpins = 1_000,
            stride = 100,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = body.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains(errors, e => e!.Contains("Rejected, never clamped"));
    }

    [Fact]
    public async Task Cancel_with_no_active_run_is_a_conflict()
    {
        using var client = _factory.CreateClient();
        // Drain any run left by another test in this fixture before asserting.
        await WaitForIdleAsync(client);

        var response = await client.PostAsync("/api/run/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static async Task<JsonElement> WaitForCompletionAsync(
        HttpClient client, bool acceptCancelled = false)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync("/api/run/current");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
                var status = body.GetProperty("status").GetString();
                if (status == "completed" || (acceptCancelled && status == "cancelled"))
                    return body;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("Run never reached a terminal status.");
    }

    private static async Task WaitForIdleAsync(HttpClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync("/api/run/current");
            if (response.StatusCode == HttpStatusCode.NoContent) return;
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            var status = body.GetProperty("status").GetString();
            if (status is "completed" or "cancelled") return;
            await Task.Delay(100);
        }
        throw new TimeoutException("Run never went idle.");
    }
}
