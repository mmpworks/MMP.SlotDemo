using Microsoft.Extensions.FileProviders;
using MMP.Herald.Events;
using MMP.Herald.Quick;
using MMP.Herald.Templating;
using SlotDemo.Server;
using SlotDemo.Server.Chapters;
using SlotDemo.Server.Runs;

// ---- Herald.OSS, native mode, with the harness's custom 10-level set. ------------
// Rendered console sink for the terminal, rolling NDJSON file sink (one JSON object
// per line — extension drives the format) for the structured record. The sys.* levels
// carry framework noise (routed there by SystemAwareHeraldProvider); the plain
// levels carry application signal. See SlotDemoLevels for the ordering rationale.
// The relay sink posts to this server's own ingest route. A host that is not listening
// on it (the in-process test host, for one) leaves the sink posting into nothing, which
// backs up the async drain and keeps request-time lines out of the file. Setting the
// variable to an empty string drops the sink and leaves console + file intact.
var ingestUrl = Environment.GetEnvironmentVariable("SLOTDEMO_LOG_INGEST_URL")
                ?? "http://localhost:5090/api/logs/ingest";

var heraldBuilder = QuickLogBuilder.Create("slotdemo")
    .WithConsoleSink()
    .WithFileSink("logs/slotdemo-.ndjson",
        interval: "daily",
        maxBytes: 10 * 1024 * 1024,
        maxRetainedFiles: 5)
    .WithCustomLevel(SlotDemoLevels.SysVerbose, "SysVerbose")
    .WithCustomLevel(SlotDemoLevels.SysDebug, "SysDebug")
    .WithCustomLevel(SlotDemoLevels.SysInformation, "SysInformation")
    .WithCustomLevel(SlotDemoLevels.SysWarning, "SysWarning")
    .WithAsyncLogging()   // buffered: the HttpJson sink posts off the request thread, and
                          // startup logs emitted before Kestrel is listening never block
    .WithLevelOrder(SlotDemoLevels.Order)
    .WithMinimumLevel(SlotDemoLevels.SysInformation)
    // AtOrAbove: custom-level floor workaround. The ingest-path guard breaks the
    // feedback loop: request logs ABOUT the ingest endpoint would otherwise be
    // posted back to it by the HttpJson sink, forever.
    .WithCustomFilter(logEvent =>
        SlotDemoLevels.AtOrAbove(SlotDemoLevels.SysInformation)(logEvent) &&
        !logEvent.Message.Contains("/api/logs/ingest", StringComparison.Ordinal));

if (!string.IsNullOrWhiteSpace(ingestUrl))
    heraldBuilder = heraldBuilder.WithHttpJsonSink(ingestUrl);   // relayed to the SPA log viewer over SSE

var herald = heraldBuilder.BuildAndCommit();

var log = herald.Logger;
var appCategory = new LogCategory("SlotDemo");

var builder = WebApplication.CreateBuilder(args);
// ASPNETCORE_URLS wins (docker binds 0.0.0.0 there); localhost:5090 is the dev default.
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://localhost:5090");

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:5173")   // Vite dev server
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<LogStreamService>();
builder.Services.AddSingleton<RunStreamService>();
builder.Services.AddSingleton(sp => new RunCoordinator(sp.GetRequiredService<RunStreamService>(), log));

// Framework categories -> sys.* levels, app categories -> plain levels.
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new SystemAwareHeraldProvider(log));
// Result-writer chatter fires on every response but never names the request path,
// so the ingest-path loop guard can't catch it — cut it off at the MEL layer.
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Result", LogLevel.Warning);

var app = builder.Build();
app.UseCors();

app.MapGet("/api/hello", () =>
{
    log.Information(appCategory, "Hello endpoint served for harness {Harness}", new LogProperty("Harness", "CSharp"));
    return Results.Ok(new
    {
        message = "Hello from MMP.SlotDemo",
        harness = "CSharp",
        serverTimeUtc = DateTime.UtcNow,
    });
});

app.MapGet("/api/stats", async (CancellationToken ct) =>
{
    log.Information(appCategory, "STAT probe requested");
    var snapshot = await AiSystemProbe.CaptureAsync(ct);
    log.Information(appCategory,
        "STAT tally: {SystemCount} systems, {RunningCount} running, {ProcessCount} processes",
        new LogProperty("SystemCount", snapshot.Systems.Count),
        new LogProperty("RunningCount", snapshot.Systems.Count(s => s.Running)),
        new LogProperty("ProcessCount", snapshot.Systems.Sum(s => s.ProcessCount)));
    return Results.Ok(snapshot);
});

// ---- Chapter labs: one route group per episode, each running the episode's own code. ----
app.MapChapterTwo(log);
app.MapChapterThree(log);
app.MapChapterFour(log);
app.MapChapterFive(log);
app.MapChapterSix(log);
app.MapChapterSeven(log);
app.MapParSheet(log);

// ---- Simulation runs: the finale page's start/watch/stop surface. ----
app.MapRuns();

// ---- Log relay: Herald's HttpJson sink posts batches here; SSE fans them out. ----
app.MapPost("/api/logs/ingest", async (HttpRequest request, LogStreamService stream) =>
{
    using var doc = await System.Text.Json.JsonDocument.ParseAsync(request.Body);
    if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
    {
        foreach (var logEvent in doc.RootElement.EnumerateArray())
            stream.Publish(logEvent.GetRawText());
    }
    else
    {
        stream.Publish(doc.RootElement.GetRawText());
    }
    return Results.NoContent();
});

app.MapGet("/api/logs/stream", async (HttpContext context, LogStreamService stream) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    var (id, reader) = stream.Subscribe();
    try
    {
        await foreach (var jsonEvent in reader.ReadAllAsync(context.RequestAborted))
        {
            await context.Response.WriteAsync($"data: {jsonEvent}\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
    }
    catch (OperationCanceledException) { /* client disconnected */ }
    finally
    {
        stream.Unsubscribe(id);
    }
});

// ---- SPA hosting: built web/dist in dev checkout, wwwroot inside the container. ----
string[] spaCandidates =
[
    Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "web", "dist")),
    Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
];
var spaRoot = spaCandidates.FirstOrDefault(Directory.Exists);
if (spaRoot is not null)
{
    var files = new PhysicalFileProvider(spaRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files });
}
else
{
    app.MapGet("/", () => Results.Text(
        "MMP.SlotDemo server is up. Build the SPA first: cd CSharp/web && npm install && npm run build",
        "text/plain"));
}

log.Information(appCategory, "SlotDemo server starting; SPA root: {SpaRoot}", new LogProperty("SpaRoot", spaRoot ?? "(not built)"));
try
{
    app.Run();
}
finally
{
    await herald.DisposeAsync();   // drain the pipeline — file lines land before exit
}

// Exposes the entry point to WebApplicationFactory<Program> in the test project.
public partial class Program;
