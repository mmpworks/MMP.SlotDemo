using System.Text.Json;
using System.Threading.Channels;
using MMP.Herald.Quick;
using MMP.SlotGame.Core.Simulation;
using SlotDemo.Server;
using SlotDemo.Server.Runs;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// The run orchestration driven end to end with a FAKE SubjectRunner: no engine, no game
/// files, no Core spinning. These tests pin the coordinator's flow — start, telemetry
/// funnel, recorder, stream events, cancel, single-run gate — so the server side stays
/// testable even when Core is unavailable or mid-rework.
///
/// The internal Start(PreparedRun, stride) overload is the seam: preparation (which needs
/// Core) is bypassed, and the lifecycle runs against a hand-built subject. The fake runner
/// must complete the telemetry writer before returning, exactly as the real engine does;
/// the pump drains until the writer completes.
/// </summary>
public sealed class RunCoordinatorFlowTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("run-flow-").FullName;
    private readonly QuickLogResult _pipeline;
    private readonly RunStreamService _stream = new();
    private readonly RunCoordinator _coordinator;

    public RunCoordinatorFlowTests()
    {
        _pipeline = QuickLogBuilder.Create()
            .WithFileSink(Path.Combine(_dir, "flow-.ndjson"), interval: "daily",
                maxBytes: 10 * 1024 * 1024, maxRetainedFiles: 2)
            .WithMinimumLevel("warning")
            .BuildAndCommit();
        _coordinator = new RunCoordinator(_stream, _pipeline.Logger);
    }

    public void Dispose()
    {
        _pipeline.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { Directory.Delete(_dir, recursive: true); } catch { /* file-lock stragglers */ }
    }

    private static PreparedRun Prepared(SubjectRunner runner, string runId = "flow-test-run") =>
        new(
            new RunFacts(
                Subject: "FakeGame", IsGame: true, Reels: 5, Rows: 3,
                StopsByReel: "10/10/10/10/10", Paylines: 1,
                TargetRtp: 0.95, Workers: 10, TargetSpins: 30,
                PublishedRtp: 0.95, PayScaleFactor: 1.0, Seed: 42),
            new AnalyticView(0.75, [("FreeSpins", 0.20)], 0.95, Sigma: 10.0),
            runner,
            runId);

    /// <summary>A runner that emits cumulative snapshots like the engine's workers, then quiesces.</summary>
    private static SubjectRunner EmittingRunner(params RunSnapshot[] samples) =>
        (telemetry, ct) =>
        {
            foreach (var snapshot in samples)
                telemetry.TryWrite(new TelemetrySample("flow-test-run", snapshot));
            telemetry.TryComplete();
            return Task.FromResult((samples[^1], new EngineTimings()));
        };

    private async Task<string> WaitForStatusAsync(string expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var status = StatusOf(_coordinator.Describe());
            if (status == expected) return status;
            await Task.Delay(20);
        }
        return StatusOf(_coordinator.Describe()) ?? "(no run)";
    }

    private static string? StatusOf(object? described) =>
        described is null ? null
        : JsonDocument.Parse(JsonSerializer.Serialize(described)).RootElement
            .GetProperty("status").GetString();

    [Fact]
    public void Describe_is_null_before_any_run()
    {
        Assert.Null(_coordinator.Describe());
        Assert.False(_coordinator.IsRunning);
    }

    [Fact]
    public async Task Fake_run_flows_from_start_to_completed_with_final_totals()
    {
        var (_, reader) = _stream.Subscribe();

        // Three cumulative snapshots: 10, 20, 30 spins at RTP 0.9 with stride 10, so the
        // recorder crosses a boundary on each one.
        var (status, _) = _coordinator.Start(Prepared(EmittingRunner(
            new RunSnapshot(10, 1_000_000, 900_000, 3),
            new RunSnapshot(20, 2_000_000, 1_800_000, 6),
            new RunSnapshot(30, 3_000_000, 2_700_000, 9))), stride: 10);

        Assert.Equal(201, status);
        Assert.Equal("completed", await WaitForStatusAsync("completed"));
        Assert.False(_coordinator.IsRunning);

        var final = JsonDocument.Parse(JsonSerializer.Serialize(_coordinator.Describe())).RootElement;
        Assert.Equal(30, final.GetProperty("latest").GetProperty("spins").GetInt64());
        Assert.Equal(0.9, final.GetProperty("latest").GetProperty("measuredRtp").GetDouble(), 10);
        Assert.Equal("FakeGame", final.GetProperty("config").GetProperty("preset").GetString());

        // The stream saw the run begin and end; a late browser could replay the same story.
        var types = new List<string>();
        while (reader.TryRead(out var json))
            types.Add(JsonDocument.Parse(json).RootElement.GetProperty("type").GetString()!);
        Assert.Contains("started", types);
        Assert.Contains("completed", types);
    }

    [Fact]
    public async Task Second_start_is_refused_while_a_run_is_active()
    {
        var release = new TaskCompletionSource();
        SubjectRunner blocked = async (telemetry, ct) =>
        {
            await release.Task.ConfigureAwait(false);
            telemetry.TryComplete();
            return (new RunSnapshot(1, 100_000, 95_000, 1), new EngineTimings());
        };

        var (first, _) = _coordinator.Start(Prepared(blocked, "first-run"), stride: 1);
        Assert.Equal(201, first);
        Assert.True(_coordinator.IsRunning);

        var (second, _) = _coordinator.Start(
            Prepared(EmittingRunner(new RunSnapshot(1, 100_000, 95_000, 1)), "second-run"), stride: 1);
        Assert.Equal(409, second);

        release.SetResult();
        Assert.Equal("completed", await WaitForStatusAsync("completed"));
    }

    [Fact]
    public async Task Cancel_ends_a_run_that_honors_the_token()
    {
        SubjectRunner waiting = async (telemetry, ct) =>
        {
            // The throwing cancellation path: cancelled before any totals return.
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            telemetry.TryComplete();
            return (new RunSnapshot(0, 0, 0, 0), new EngineTimings());
        };

        _coordinator.Start(Prepared(waiting, "cancel-run"), stride: 1);
        Assert.True(_coordinator.Cancel());
        Assert.Equal("cancelled", await WaitForStatusAsync("cancelled"));
        Assert.False(_coordinator.Cancel());   // nothing left to cancel
    }
}
