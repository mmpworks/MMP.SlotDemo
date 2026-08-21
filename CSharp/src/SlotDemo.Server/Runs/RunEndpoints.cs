using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// HTTP endpoints for reading run options, starting a run, streaming its progress, and
/// stopping it. The coordinator is the only component that changes run state.
/// </summary>
public static class RunEndpoints
{
    public static void MapRuns(this WebApplication app)
    {
        // Return server-owned limits and defaults so the SPA does not duplicate them.
        app.MapGet("/api/run/limits", () => Results.Ok(new
        {
            maxAggregateBasisPoints = SimulationConfig.MaxAggregateBasisPoints,
            minAggregateBasisPoints = SimulationConfig.MinAggregateBasisPoints,
            defaults = new
            {
                presetName = SimulationConfig.DefaultPresetName,
                baseRtpBasisPoints = SimulationConfig.DefaultBaseRtpBasisPoints,
                freeSpinsRtpBasisPoints = SimulationConfig.DefaultFreeSpinsRtpBasisPoints,
                pickBonusRtpBasisPoints = SimulationConfig.DefaultPickBonusRtpBasisPoints,
                stride = ConvergenceRecorder.DefaultStride,
            },
            workerCeiling = 64,
            games = SlotDemo.Server.Chapters.ReelSources.GameFiles(),
            presets = StandardReelPresets.All.Values.Select(p => new
            {
                name = p.Name,
                reels = p.ReelCount,
                rows = StripReelSet.DefaultRows,
                stopsPerReel = p.StopCounts,
                paylines = p.Paylines.Count,
            }),
        }));

        // The SPA keeps Start disabled until warm-up reaches its threshold or pass limit.
        app.MapGet("/api/run/readiness", (EngineWarmupService warmup) =>
        {
            var state = warmup.CurrentState;
            return Results.Ok(new
            {
                ready = state.Ready,
                settled = state.Settled,
                bestSpinsPerSecond = state.BestSpinsPerSecond,
                passesRun = state.PassesRun,
                thresholdSpinsPerSecond = EngineWarmupService.SettledSpinsPerSecond,
            });
        });

        app.MapPost("/api/run", (RunRequest request, RunCoordinator runs) =>
        {
            var (status, body) = runs.Start(request);
            return Results.Json(body, statusCode: status);
        });

        app.MapGet("/api/run/current", (RunCoordinator runs) =>
        {
            var status = runs.GetCurrentStatus();
            return status is null ? Results.NoContent() : Results.Ok(status);
        });

        app.MapPost("/api/run/cancel", (RunCoordinator runs) =>
            runs.Cancel() ? Results.Accepted() : Results.Conflict(new { title = "No run is active" }));

        app.MapGet("/api/run/stream", StreamRunEventsAsync);
    }

    /// <summary>Sends run events until the browser disconnects.</summary>
    private static async Task StreamRunEventsAsync(
        HttpContext context,
        RunStreamService stream)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        var (subscriptionId, events) = stream.SubscribeToRunEvents();
        try
        {
            await foreach (var serializedEvent in events.ReadAllAsync(context.RequestAborted))
            {
                await context.Response.WriteAsync(
                    $"data: {serializedEvent}\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // Request cancellation is the normal end of an SSE subscription.
        }
        finally
        {
            stream.UnsubscribeFromRunEvents(subscriptionId);
        }
    }
}
