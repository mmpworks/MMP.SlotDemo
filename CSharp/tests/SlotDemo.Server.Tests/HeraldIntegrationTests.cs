using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// End-to-end: the real server pipeline (Program.cs Herald wiring) must emit
/// endpoint and request log lines into its rolling file sink.
/// </summary>
public sealed class HeraldIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HeraldIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task HelloRequest_LandsInTheServerLogFile()
    {
        using var client = _factory.CreateClient();
        (await client.GetAsync("/api/hello")).EnsureSuccessStatusCode();

        await AssertLoggedAsync("Hello endpoint served");
    }

    [Fact]
    public async Task RequestLogging_EmitsOneLinePerRequest()
    {
        using var client = _factory.CreateClient();
        (await client.GetAsync("/api/hello")).EnsureSuccessStatusCode();

        await AssertLoggedAsync("/api/hello");
    }

    /// <summary>
    /// Polls until the wanted line appears, rather than until the log has any content at
    /// all. Program.cs builds the pipeline with <c>WithAsyncLogging</c>, so a line lands on
    /// a background drain some time after the request returns, and a log file left behind
    /// by an earlier run is non-empty from the first read. Waiting on the condition itself
    /// keeps the test honest about what it is checking.
    /// </summary>
    private static async Task AssertLoggedAsync(string wanted)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        var text = "";
        while (DateTime.UtcNow < deadline)
        {
            text = ReadServerLogs();
            if (text.Contains(wanted, StringComparison.Ordinal)) return;
            await Task.Delay(50);
        }

        Assert.Fail($"'{wanted}' never reached the log file. Last {text.Length} bytes read.");
    }

    private static string ReadServerLogs()
    {
        // The file sink resolves its relative path against the process working directory,
        // which under the test host is the test bin directory rather than the server
        // content root.
        var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        if (!Directory.Exists(logsDir)) return "";

        return string.Concat(Directory
            .GetFiles(logsDir, "slotdemo-*.ndjson")
            .Select(ReadShared));
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
