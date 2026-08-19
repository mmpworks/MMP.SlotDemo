using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// Guards the property the convergence chart is built on: the curve is sampled BY SPINS,
/// at the recorder's stride, not by how often a background loop happens to wake up.
///
/// The bug these pin, found 2026-08-19: the telemetry pump slept 100ms between drains
/// while workers published into a bounded drop-oldest channel, so most samples were
/// overwritten before the recorder saw them. The curve came back as dense clusters joined
/// by long straight jumps. Once the engine got fast enough to finish a 10M-spin run inside
/// a single sleep, the first drain landed most of the way through the run and the chart
/// lost its entire early history, which is the part the convergence lesson teaches.
///
/// These assertions are about COVERAGE, not timing, so they hold on a fast machine and a
/// slow one. Bounds are deliberately loose: the drop-oldest channel is still allowed to
/// drop, it just may not swallow whole regions of the run.
/// </summary>
public sealed class RunCurveCoverageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Sized so the workers publish far more samples than the telemetry channel holds
    // (capacity 1024, one sample per 4,096 spins per worker). Below that the channel
    // buffers the whole run and the defect cannot show itself.
    private const long Spins = 20_000_000;
    private const long Stride = 50_000;
    private const long ExpectedPoints = Spins / Stride;   // 400

    private readonly WebApplicationFactory<Program> _factory;

    public RunCurveCoverageTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static object Request(ulong seed) => new
    {
        presetName = "Classic3",
        baseRtpBasisPoints = 7500,
        freeSpinsRtpBasisPoints = 1300,
        pickBonusRtpBasisPoints = 1000,
        seed,
        workerCount = 8,
        targetSpins = Spins,
        stride = Stride,
    };

    /// <summary>
    /// The direct pin for the reported chart. A run that draws its curve starting at 60% of
    /// the way through has lost the convergence story; the first point must land near the
    /// beginning. Ten strides of slack absorbs normal channel drops.
    /// </summary>
    [Fact]
    public async Task The_curve_starts_near_the_beginning_of_the_run()
    {
        var curve = await RunAndReadCurveAsync(seed: 4242);

        var firstSpins = curve[0].GetProperty("spins").GetInt64();

        Assert.True(
            firstSpins <= Stride * 10,
            $"first curve point sits at {firstSpins:N0} spins; the run is only {Spins:N0} long, "
            + "so the chart would open most of the way through the story.");
    }

    /// <summary>
    /// Sampling by spins means the point count tracks spins/stride. Wall-clock sampling
    /// makes this collapse to a handful of points on a fast machine.
    /// </summary>
    [Fact]
    public async Task The_curve_carries_roughly_one_point_per_stride()
    {
        var curve = await RunAndReadCurveAsync(seed: 4243);

        Assert.True(
            curve.Count >= ExpectedPoints / 2,
            $"{curve.Count} curve points for {ExpectedPoints} strides; sampling is not following the stride.");
    }

    /// <summary>
    /// No region of the run may go unrepresented. A long gap between two points is what the
    /// chart draws as a straight line through territory it never measured.
    /// </summary>
    [Fact]
    public async Task No_gap_between_curve_points_swallows_a_region_of_the_run()
    {
        var curve = await RunAndReadCurveAsync(seed: 4244);

        long widest = 0;
        for (var i = 1; i < curve.Count; i++)
        {
            var gap = curve[i].GetProperty("spins").GetInt64() - curve[i - 1].GetProperty("spins").GetInt64();
            widest = Math.Max(widest, gap);
        }

        Assert.True(
            widest <= Stride * 20,
            $"widest gap is {widest:N0} spins across a {Spins:N0} spin run.");
    }

    /// <summary>
    /// Spins rise along the curve and the last point is the finished total, so the page can
    /// read its verdict off the end of the array.
    /// </summary>
    [Fact]
    public async Task The_curve_rises_and_ends_on_the_target()
    {
        var curve = await RunAndReadCurveAsync(seed: 4245);

        for (var i = 1; i < curve.Count; i++)
        {
            Assert.True(
                curve[i].GetProperty("spins").GetInt64() > curve[i - 1].GetProperty("spins").GetInt64(),
                $"curve went backwards at index {i}.");
        }

        Assert.Equal(Spins, curve[^1].GetProperty("spins").GetInt64());
    }

    /// <summary>
    /// The band is z*sigma/sqrt(N), so it narrows as the run grows. A chart drawn from a
    /// curve that starts late showed a band that looked wrong; this states the real shape.
    /// </summary>
    [Fact]
    public async Task The_band_narrows_as_the_run_grows()
    {
        var curve = await RunAndReadCurveAsync(seed: 4246);

        var first = curve[0].GetProperty("bandHalfWidth").GetDouble();
        var last = curve[^1].GetProperty("bandHalfWidth").GetDouble();

        Assert.True(last < first, $"band went from {first} to {last}; it must narrow.");
    }

    /// <summary>Throughput is published and is a real, positive rate once a run finishes.</summary>
    [Fact]
    public async Task A_finished_run_publishes_its_spins_per_second()
    {
        using var client = _factory.CreateClient();
        var final = await RunToCompletionAsync(client, seed: 4247);
        var latest = final.GetProperty("latest");

        Assert.True(latest.GetProperty("elapsedSeconds").GetDouble() > 0);

        var rate = latest.GetProperty("spinsPerSecond").GetDouble();
        Assert.True(rate > 0, "spinsPerSecond was not published for a finished run.");

        // The published rate is the run's own totals over its own clock, so it must agree
        // with them rather than being an independently drifting number.
        var expected = Spins / latest.GetProperty("elapsedSeconds").GetDouble();
        Assert.Equal(expected, rate, 3);
    }

    private async Task<List<JsonElement>> RunAndReadCurveAsync(ulong seed)
    {
        using var client = _factory.CreateClient();
        var final = await RunToCompletionAsync(client, seed);
        var curve = final.GetProperty("curve").EnumerateArray().ToList();

        Assert.NotEmpty(curve);
        return curve;
    }

    private async Task<JsonElement> RunToCompletionAsync(HttpClient client, ulong seed)
    {
        var started = await client.PostAsJsonAsync("/api/run", Request(seed));
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);

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
}
