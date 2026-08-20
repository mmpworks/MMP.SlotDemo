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
        // What the SPA is allowed to ask for. The cap and the defaults live on the server,
        // so the client previews the rule rather than owning a second copy of it.
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

        // Whether the engine has been warmed enough to be worth timing. The page holds its
        // run button until this reports ready, so a visitor's first run is a real
        // measurement rather than a compilation. The threshold lives here, with the rest of
        // the run limits, so the page reads it instead of keeping a second copy.
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

        // Stream run events with SSE. Each slow client drops its oldest queued events.
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
