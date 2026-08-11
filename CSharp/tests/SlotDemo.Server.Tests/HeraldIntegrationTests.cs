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

        // Program.cs builds the pipeline with WithAsyncLogging, so lines land on a
        // background drain — poll rather than reading once.
        var text = await ReadServerLogsAsync();
        Assert.Contains("Hello endpoint served", text);
    }

    [Fact]
    public async Task RequestLogging_EmitsOneLinePerRequest()
    {
        using var client = _factory.CreateClient();
        (await client.GetAsync("/api/hello")).EnsureSuccessStatusCode();

        var text = await ReadServerLogsAsync();
        Assert.Contains("/api/hello", text);
    }

    private static async Task<string> ReadServerLogsAsync()
    {
        // The file sink resolves its relative path against the process working
        // directory — under the test host that resolves to the test bin directory
        // rather than the server content root.
        var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (Directory.Exists(logsDir))
            {
                var text = string.Concat(Directory
                    .GetFiles(logsDir, "slotdemo-*.ndjson")
                    .Select(f => { using var s = new StreamReader(new FileStream(
                        f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)); return s.ReadToEnd(); }));
                if (text.Length > 0) return text;
            }
            await Task.Delay(100);
        }
        return "";
    }
}
