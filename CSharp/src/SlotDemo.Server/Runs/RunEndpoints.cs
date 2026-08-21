using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Maps the HTTP API used by the finale page. Requests that change run state are delegated
/// to <see cref="RunCoordinator"/>.
/// </summary>
public static class RunEndpoints
{
    public static void MapRuns(this WebApplication app)
    {
        // Return server-owned limits and defaults so the form displays the same rules that
        // RunCoordinator validates.
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

        // The SPA keeps the run button disabled until warm-up either reaches the threshold
        // or exhausts its pass limit.
        app.MapGet("/api/run/readiness", (EngineWarmupService warmup) =>
        {
            var state = warmup.Snapshot;
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
            var described = runs.Describe();
            return described is null ? Results.NoContent() : Results.Ok(described);
        });

        app.MapPost("/api/run/cancel", (RunCoordinator runs) =>
            runs.Cancel() ? Results.Accepted() : Results.Conflict(new { title = "No run is active" }));

        // Subscribe creates this browser's queue. Each payload becomes one SSE message;
        // the blank line terminates the frame and FlushAsync sends it without buffering.
        app.MapGet("/api/run/stream", async (HttpContext context, RunStreamService stream) =>
        {
            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            var (id, reader) = stream.Subscribe();
            try
            {
                await foreach (var payload in reader.ReadAllAsync(context.RequestAborted))
                {
                    await context.Response.WriteAsync($"data: {payload}\n\n", context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
            }
            catch (OperationCanceledException) { /* client disconnected */ }
            finally
            {
                stream.Unsubscribe(id);
            }
        });
    }
}
