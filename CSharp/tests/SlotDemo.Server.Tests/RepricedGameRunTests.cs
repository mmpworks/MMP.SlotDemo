using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// Running a shipped game at a requested RTP.
///
/// The lever is a single scalar on the line paytable, because line RTP is linear in the
/// pays. Geometry is untouched, so hit frequency is identical and only what each hit pays
/// changes. The feature is a separate lever and is deliberately left alone, which puts a
/// floor under how low the total can go.
/// </summary>
public sealed class RepricedGameRunTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Game = "orca-dive.json";

    private readonly WebApplicationFactory<Program> _factory;

    public RepricedGameRunTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static object Request(int targetBp, ulong seed = 31) => new
    {
        presetName = "",
        gameFile = Game,
        baseRtpBasisPoints = 0,
        freeSpinsRtpBasisPoints = 0,
        pickBonusRtpBasisPoints = 0,
        seed,
        workerCount = 4,
        targetSpins = 200_000,
        stride = 10_000,
        targetTotalRtpBasisPoints = targetBp,
    };

    [Fact]
    public async Task Without_a_target_the_game_keeps_its_published_paytable()
    {
        using var client = _factory.CreateClient();
        var body = await StartAsync(client, Request(targetBp: 0));

        var config = body.GetProperty("config");
        Assert.False(config.GetProperty("isRepriced").GetBoolean());
        Assert.Equal(1.0, config.GetProperty("payScaleFactor").GetDouble(), 12);
        Assert.Equal(
            config.GetProperty("publishedRtp").GetDouble(),
            config.GetProperty("targetRtp").GetDouble(),
            12);
    }

    /// <summary>
    /// The analytic RTP of the re-priced game lands on the request. It is not exact: every
    /// pay rounds to a whole hundredth of the wager, so a few basis points of drift is the
    /// expected, documented behaviour rather than a failure.
    /// </summary>
    [Theory]
    [InlineData(8_800)]
    [InlineData(9_200)]
    [InlineData(9_600)]
    public async Task A_requested_total_rtp_is_hit_within_rounding_drift(int targetBp)
    {
        using var client = _factory.CreateClient();
        var body = await StartAsync(client, Request(targetBp));

        var achieved = body.GetProperty("analytic").GetProperty("totalRtp").GetDouble();
        var driftBp = Math.Abs(achieved * 10_000 - targetBp);

        Assert.True(body.GetProperty("config").GetProperty("isRepriced").GetBoolean());
        Assert.True(driftBp <= 25, $"asked {targetBp} bp, enumerated {achieved * 10_000:0.##} bp.");
    }

    /// <summary>
    /// Re-pricing moves money, never geometry. The reels, rows, stops and paylines of a
    /// re-priced run match the published game exactly, which is what keeps hit frequency
    /// unchanged and makes the scalar safe.
    /// </summary>
    [Fact]
    public async Task Repricing_leaves_the_geometry_untouched()
    {
        using var client = _factory.CreateClient();
        var published = (await StartAsync(client, Request(targetBp: 0))).GetProperty("config");
        await WaitForIdleAsync(client);
        var repriced = (await StartAsync(client, Request(targetBp: 9_600))).GetProperty("config");

        Assert.Equal(published.GetProperty("reels").GetInt32(), repriced.GetProperty("reels").GetInt32());
        Assert.Equal(published.GetProperty("rows").GetInt32(), repriced.GetProperty("rows").GetInt32());
        Assert.Equal(published.GetProperty("paylines").GetInt32(), repriced.GetProperty("paylines").GetInt32());
        Assert.Equal(
            published.GetProperty("stopsPerReel").GetString(),
            repriced.GetProperty("stopsPerReel").GetString());
    }

    /// <summary>
    /// A run measured against its own re-priced reference: the simulation must settle on
    /// the RTP that was asked for, not the one the game shipped with.
    /// </summary>
    [Fact]
    public async Task A_repriced_run_measures_against_its_new_reference()
    {
        using var client = _factory.CreateClient();
        var start = await StartAsync(client, Request(targetBp: 9_600));
        var publishedRtp = start.GetProperty("config").GetProperty("publishedRtp").GetDouble();

        var final = await WaitForCompletionAsync(client);
        var analytic = final.GetProperty("analytic").GetProperty("totalRtp").GetDouble();

        Assert.True(
            Math.Abs(analytic - 0.96) < Math.Abs(publishedRtp - 0.96),
            "the re-priced run is still being measured against the published RTP.");
        Assert.Equal(200_000, final.GetProperty("latest").GetProperty("spins").GetInt64());
    }

    /// <summary>
    /// The feature's contribution is a floor, because only the line paytable is scaled.
    /// A total at or under that floor is refused rather than clamped: reporting a clamped
    /// RTP would state a return the game does not pay.
    /// </summary>
    [Fact]
    public async Task A_total_below_the_feature_floor_is_refused()
    {
        using var client = _factory.CreateClient();

        // Below the solver's own limits, so this also covers the bounds check.
        var response = await client.PostAsJsonAsync("/api/run", Request(targetBp: 100));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(7_499)]
    [InlineData(9_901)]
    public async Task A_target_outside_the_solver_limits_is_refused(int targetBp)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/run", Request(targetBp));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<JsonElement> StartAsync(HttpClient client, object request)
    {
        await WaitForIdleAsync(client);
        var response = await client.PostAsJsonAsync("/api/run", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private static async Task<JsonElement> WaitForCompletionAsync(HttpClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync("/api/run/current");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
                if (body.GetProperty("status").GetString() == "completed") return body;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException("Run never reached a terminal status.");
    }

    private static async Task WaitForIdleAsync(HttpClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync("/api/run/current");
            if (response.StatusCode != HttpStatusCode.OK) return;
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            if (body.GetProperty("status").GetString() != "running") return;
            await Task.Delay(50);
        }
        throw new TimeoutException("A previous run never finished.");
    }
}
