using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// The finale page's surface: describe what can be configured, start a run, watch it,
/// stop it. Reads are safe at any time; there is one writer and it is the coordinator.
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
            defaults = new
            {
                presetName = SimulationConfig.DefaultPresetName,
                baseRtpBasisPoints = SimulationConfig.DefaultBaseRtpBasisPoints,
                freeSpinsRtpBasisPoints = SimulationConfig.DefaultFreeSpinsRtpBasisPoints,
                pickBonusRtpBasisPoints = SimulationConfig.DefaultPickBonusRtpBasisPoints,
                stride = ConvergenceRecorder.DefaultStride,
            },
            workerCeiling = 64,
            presets = ReelPreset.All.Values.Select(p => new
            {
                name = p.Name,
                reels = p.ReelCount,
                rows = StripReelSet.DefaultRows,
                stopsPerReel = p.StopsPerReel,
                paylines = p.Paylines.Count,
            }),
        }));

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

        // Same SSE shape as the log relay: the browser subscribes, the server pushes, a
        // client that falls behind drops its own oldest events.
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
