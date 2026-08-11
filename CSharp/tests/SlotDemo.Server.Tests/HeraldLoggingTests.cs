using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Quick;
using MMP.Herald.Templating;
using SlotDemo.Server;
using Xunit;
using LogLevel = MMP.Herald.Levels.LogLevel;

namespace SlotDemo.Server.Tests;

/// <summary>
/// Herald.OSS native mode with the harness's custom 10-level set, exercised the way
/// Program.cs uses it. Most tests build a rolling-file-sink pipeline (WithCustomLevel +
/// WithLevelOrder) over their own temp directory, so tests stay parallel-safe and file
/// assertions are exact; the console sink has its own test.
/// </summary>
public sealed class HeraldLoggingTests : IDisposable
{
    private static readonly LogCategory Category = new("Test");

    private readonly string _dir = Directory.CreateTempSubdirectory("herald-fuzz-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* Windows file-lock stragglers */ }
    }

    private QuickLogResult CreateFilePipeline(
        long maxBytes = 10 * 1024 * 1024,
        string minimumLevel = "information")
        => QuickLogBuilder.Create()
            .WithFileSink(Path.Combine(_dir, "test-.ndjson"),
                interval: "daily", maxBytes: maxBytes, maxRetainedFiles: 5)
            .WithCustomLevel(SlotDemoLevels.SysVerbose, "SysVerbose")
            .WithCustomLevel(SlotDemoLevels.SysDebug, "SysDebug")
            .WithCustomLevel(SlotDemoLevels.SysInformation, "SysInformation")
            .WithCustomLevel(SlotDemoLevels.SysWarning, "SysWarning")
            .WithLevelOrder(SlotDemoLevels.Order)
            .WithMinimumLevel(minimumLevel)
            // Same workaround Program.cs carries — see SlotDemoLevels.AtOrAbove.
            .WithCustomFilter(SlotDemoLevels.AtOrAbove(minimumLevel))
            .BuildAndCommit();

    private static void Emit(StructuredLogger logger, LogLevel level, string template,
        params LogProperty[] properties)
        => logger.Log(level, Category, template,
            properties.Length > 0 ? properties : null, context: null, eventId: null);

    private string ReadAll()
    {
        var builder = new System.Text.StringBuilder();
        foreach (var file in Directory.GetFiles(_dir))
        {
            using var reader = new StreamReader(new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            builder.Append(reader.ReadToEnd());
        }
        return builder.ToString();
    }

    // ---- The custom level set -----------------------------------------------------

    [Fact]
    public async Task AllTenLevels_EmitAboveTheFloor()
    {
        var result = CreateFilePipeline(minimumLevel: SlotDemoLevels.SysVerbose);
        await using (result)
        {
            Emit(result.Logger, SlotDemoLevels.SysVerboseLevel, "L sys.verbose");
            Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Verbose, "L verbose");
            Emit(result.Logger, SlotDemoLevels.SysDebugLevel, "L sys.debug");
            Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Debug, "L debug");
            Emit(result.Logger, SlotDemoLevels.SysInformationLevel, "L sys.information");
            Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Information, "L information");
            Emit(result.Logger, SlotDemoLevels.SysWarningLevel, "L sys.warning");
            Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Warning, "L warning");
            Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Error, "L error");
            Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Fatal, "L fatal");
        }

        var text = ReadAll();
        foreach (var key in SlotDemoLevels.Order)
            Assert.Contains($"L {key}", text);
        // Display names render into the level column.
        Assert.Contains("SysInformation", text);
        Assert.Contains("SysWarning", text);
    }

    [Fact]
    public async Task InterleavedThreshold_InformationFloor_DropsSysInfoAndAppDebug()
    {
        var result = CreateFilePipeline(minimumLevel: "information");
        await using (result)
        {
            Emit(result.Logger, SlotDemoLevels.SysInformationLevel, "dropped sys-info-marker");
            Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Debug, "dropped app-debug-marker");
            Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Information, "kept info-marker");
            Emit(result.Logger, SlotDemoLevels.SysWarningLevel, "kept sys-warning-marker");
        }

        var text = ReadAll();
        Assert.DoesNotContain("sys-info-marker", text);
        Assert.DoesNotContain("app-debug-marker", text);
        Assert.Contains("kept info-marker", text);
        Assert.Contains("kept sys-warning-marker", text);
    }

    // ---- MEL routing: framework categories land on sys.* --------------------------

    [Fact]
    public async Task SystemAwareProvider_RoutesFrameworkToSysLevels()
    {
        var result = CreateFilePipeline(minimumLevel: SlotDemoLevels.SysVerbose);
        await using (result)
        {
            var provider = new SystemAwareHeraldProvider(result.Logger);
            var frameworkLogger = provider.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");
            var appLogger = provider.CreateLogger("SlotDemo.Probe");

            frameworkLogger.Log(Microsoft.Extensions.Logging.LogLevel.Information,
                new Microsoft.Extensions.Logging.EventId(0), "framework-info-marker",
                null, (s, _) => s);
            appLogger.Log(Microsoft.Extensions.Logging.LogLevel.Information,
                new Microsoft.Extensions.Logging.EventId(0), "app-info-marker",
                null, (s, _) => s);
        }

        var text = ReadAll();
        var frameworkLine = text.Split('\n').Single(l => l.Contains("framework-info-marker"));
        var appLine = text.Split('\n').Single(l => l.Contains("app-info-marker"));
        Assert.Contains("SysInformation", frameworkLine);
        Assert.DoesNotContain("SysInformation", appLine);
    }

    // ---- Structured JSON output ---------------------------------------------------

    [Fact]
    public async Task EveryFileLine_IsValidJson_WithTheStructuredFields()
    {
        var result = CreateFilePipeline(minimumLevel: SlotDemoLevels.SysVerbose);
        await using (result)
        {
            Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Information,
                "probe {Target} answered", new LogProperty("Target", "claude"));
            Emit(result.Logger, SlotDemoLevels.SysInformationLevel, "framework line");
        }

        var lines = ReadAll().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        foreach (var line in lines)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(line); // throws on non-JSON
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("time", out _));
            Assert.True(root.TryGetProperty("level_key", out _));
            Assert.True(root.TryGetProperty("level_rank", out _));
            Assert.True(root.TryGetProperty("category", out _));
            Assert.True(root.TryGetProperty("message", out _));
        }

        var probeLine = System.Text.Json.JsonDocument.Parse(lines[0]).RootElement;
        Assert.Equal("information", probeLine.GetProperty("level_key").GetString());
        Assert.Equal("claude", probeLine.GetProperty("properties")
            .GetProperty("Target").GetProperty("value").GetString());
        Assert.Equal("probe {Target} answered", probeLine.GetProperty("message_template").GetString());
        Assert.Equal("sys.information", System.Text.Json.JsonDocument.Parse(lines[1])
            .RootElement.GetProperty("level_key").GetString());
    }

    // ---- Arities and rendering ----------------------------------------------------

    [Fact]
    public async Task PropertiesRenderIntoTheFile()
    {
        var result = CreateFilePipeline();
        await using (result)
        {
            result.Logger.Information(Category, "zero-arg heartbeat");
            Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Information,
                "three {A} {B} {C}",
                new LogProperty("A", 42), new LogProperty("B", 2.5), new LogProperty("C", "xval"));
        }

        var text = ReadAll();
        Assert.Contains("zero-arg heartbeat", text);
        Assert.Contains("42", text);
        Assert.Contains("2.5", text);
        Assert.Contains("xval", text);
    }

    // ---- Hostile input ------------------------------------------------------------

    [Fact]
    public async Task HostileTemplatesAndArguments_NeverThrow()
    {
        var result = CreateFilePipeline();
        await using (result)
        {
            var logger = result.Logger;
            Emit(logger, MMP.Herald.Levels.KnownLogLevels.Information,
                "crlf payload {P}", new LogProperty("P", "line1\r\n[FAKE] forged-entry"));
            Emit(logger, MMP.Herald.Levels.KnownLogLevels.Information,
                "brace soup {{literal}} {A}", new LogProperty("A", 1));
            Emit(logger, MMP.Herald.Levels.KnownLogLevels.Information,
                "more holes than args {A} {B} {C}", new LogProperty("A", 1));
            Emit(logger, MMP.Herald.Levels.KnownLogLevels.Information,
                "null arg {A}", new LogProperty("A", null));
            Emit(logger, MMP.Herald.Levels.KnownLogLevels.Information,
                "unicode {A}", new LogProperty("A", "中文🚀𝔘𝔫𝔦"));
            Emit(logger, MMP.Herald.Levels.KnownLogLevels.Information,
                "huge {A}", new LogProperty("A", new string('h', 100_000)));
        }

        var text = ReadAll();
        Assert.Contains("forged-entry", text);
        Assert.Contains("中文🚀", text);
        Assert.Contains(new string('h', 1000), text);
    }

    /// <summary>
    /// PINNED ENGINE BEHAVIOR: the NATIVE surface tolerates an unclosed template
    /// brace (no throw). The Serilog COMPAT adapter's parse path throws
    /// InvalidOperationException on the same input — a surface inconsistency
    /// reported upstream to Herald.OSS. If this starts failing, the native
    /// contract changed — revisit this pin and
    /// CustomLevelEvents_CurrentlyBypassMinimumLevel_WithoutWorkaround.
    /// </summary>
    [Fact]
    public async Task UnclosedTemplateBrace_NativeSurfaceTolerates()
    {
        var result = CreateFilePipeline();
        await using (result)
        {
            result.Logger.Information(Category, "unmatched { open BRACEMARK");
        }
        Assert.Contains("BRACEMARK", ReadAll());
    }

    /// <summary>
    /// PINNED ENGINE BUG (reported upstream to Herald.OSS): without the
    /// AtOrAbove workaround filter, events at CUSTOM levels bypass the engine's
    /// minimum-level check — a sys.information event lands even under a warning
    /// floor, despite ranking below warning in the registered order. If this
    /// starts failing, Herald fixed the filter: delete this pin and the
    /// SlotDemoLevels.AtOrAbove workaround.
    /// </summary>
    [Fact]
    public async Task CustomLevelEvents_CurrentlyBypassMinimumLevel_WithoutWorkaround()
    {
        var result = QuickLogBuilder.Create()
            .WithFileSink(Path.Combine(_dir, "test-.ndjson"),
                interval: "daily", maxBytes: 1024 * 1024, maxRetainedFiles: 2)
            .WithCustomLevel(SlotDemoLevels.SysInformation, "SysInformation")
            .WithLevelOrder(SlotDemoLevels.Order)
            .WithMinimumLevel("warning")   // no AtOrAbove filter on purpose
            .BuildAndCommit();
        await using (result)
        {
            Emit(result.Logger, SlotDemoLevels.SysInformationLevel, "bypass-marker");
        }

        Assert.Contains("bypass-marker", ReadAll());
    }

    [Fact]
    public async Task SeededMessageFuzz_TwoThousandEvents_AllLand()
    {
        const int iterations = 2000;
        var rng = new Random(0x5EED);
        // Floor at the bottom — RandomLevel spans sys.* levels that rank below information.
        var result = CreateFilePipeline(maxBytes: 1024 * 1024 * 1024,
            minimumLevel: SlotDemoLevels.SysVerbose);
        await using (result)
        {
            for (var i = 0; i < iterations; i++)
            {
                var template = "FZMARK " + RandomTemplate(rng, out var holeNames);
                var args = holeNames.Select(n => new LogProperty(n, RandomArg(rng))).ToArray();
                Emit(result.Logger, RandomLevel(rng), template, args);
            }
        }

        // One JSON object per line; the marker shows up twice per event (template +
        // rendered message), so count lines, never substring hits.
        var landed = ReadAll().Split('\n').Count(l => l.Contains("FZMARK"));
        Assert.Equal(iterations, landed);
    }

    // ---- Concurrency --------------------------------------------------------------

    [Fact]
    public async Task ParallelWriters_LoseNothing()
    {
        const int writers = 8;
        const int perWriter = 250;
        // Floor at the bottom — RandomLevel spans sys.* levels that rank below information.
        var result = CreateFilePipeline(maxBytes: 1024 * 1024 * 1024,
            minimumLevel: SlotDemoLevels.SysVerbose);
        await using (result)
        {
            await Task.WhenAll(Enumerable.Range(0, writers).Select(w => Task.Run(() =>
            {
                for (var i = 0; i < perWriter; i++)
                    Emit(result.Logger, RandomLevel(new Random(w * 1000 + i)),
                        "CCMARK writer {Writer} message {Index}",
                        new LogProperty("Writer", w), new LogProperty("Index", i));
            })));
        }

        Assert.Equal(writers * perWriter, ReadAll().Split('\n').Count(l => l.Contains("CCMARK")));
    }

    // ---- Rolling ------------------------------------------------------------------

    [Fact]
    public async Task SizeThreshold_RollsAndKeepsTheNewestEvents()
    {
        const int events = 300;
        var payload = new string('r', 200);
        var result = CreateFilePipeline(maxBytes: 8 * 1024);
        await using (result)
        {
            for (var i = 0; i < events; i++)
                Emit(result.Logger, MMP.Herald.Levels.KnownLogLevels.Information,
                    "ROLLMARK {Index} {Payload}",
                    new LogProperty("Index", i), new LogProperty("Payload", payload));
        }

        Assert.True(Directory.GetFiles(_dir).Length >= 2,
            $"expected a size roll; files: {Directory.GetFiles(_dir).Length}");
        // Retention may prune the oldest rolls — the newest events must survive.
        Assert.Contains($"ROLLMARK {events - 1}", ReadAll());
    }

    // ---- Console sink -------------------------------------------------------------

    [Fact]
    public async Task ConsoleSink_WritesToStdout()
    {
        var original = Console.Out;
        var capture = new StringWriter();
        Console.SetOut(capture);
        try
        {
            var result = QuickLogBuilder.Create()
                .WithConsoleSink()
                .WithCustomLevel(SlotDemoLevels.SysInformation, "SysInformation")
                .WithLevelOrder(SlotDemoLevels.Order)
                .WithMinimumLevel(SlotDemoLevels.SysVerbose)
                .BuildAndCommit();
            await using (result)
            {
                Emit(result.Logger, SlotDemoLevels.SysInformationLevel,
                    "console marker {Value}", new LogProperty("Value", 777));
            }
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("console marker", capture.ToString());
        Assert.Contains("777", capture.ToString());
    }

    // ---- Fuzz generators ----------------------------------------------------------

    private static LogLevel RandomLevel(Random rng) => rng.Next(6) switch
    {
        0 => MMP.Herald.Levels.KnownLogLevels.Information,
        1 => SlotDemoLevels.SysInformationLevel,
        2 => SlotDemoLevels.SysWarningLevel,
        3 => MMP.Herald.Levels.KnownLogLevels.Warning,
        4 => MMP.Herald.Levels.KnownLogLevels.Fatal,
        _ => MMP.Herald.Levels.KnownLogLevels.Error,
    };

    private static string RandomTemplate(Random rng, out List<string> holeNames)
    {
        holeNames = [];
        var parts = new List<string>();
        for (var i = 0; i < rng.Next(1, 6); i++)
        {
            switch (rng.Next(6))
            {
                case 0:
                    var name = "H" + holeNames.Count;
                    holeNames.Add(name);
                    parts.Add("{" + name + "}");
                    break;
                case 1: parts.Add("{{escaped}}"); break;
                case 2: parts.Add("plain words"); break;
                default:
                    // Random printable chars, braces stripped — the unclosed-brace
                    // tokenizer contract is covered by its own pinned test.
                    parts.Add(new string(Enumerable.Range(0, rng.Next(0, 40))
                            .Select(_ => (char)rng.Next(0x20, 0x7f))
                            .Select(c => c is '{' or '}' ? '(' : c)
                            .ToArray())
                        .Replace("FZMARK", "******"));
                    break;
            }
        }
        return string.Join(' ', parts);
    }

    private static object? RandomArg(Random rng) => rng.Next(8) switch
    {
        0 => null,
        1 => rng.Next(),
        2 => rng.NextDouble() * 1e9,
        3 => double.NaN,
        4 => new string((char)rng.Next(0x20, 0x3000), rng.Next(0, 300)),
        5 => DateTime.UtcNow,
        6 => new { Nested = rng.Next(), Name = "anon" },
        _ => rng.Next(2) == 0,
    };
}
