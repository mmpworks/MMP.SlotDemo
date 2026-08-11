using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SlotDemo.Server;

/// <summary>One process belonging to a detected AI system.</summary>
public sealed record AiProcessInfo(int Pid, string Name, double MemoryMb, DateTime? StartedUtc);

/// <summary>Snapshot of one AI system: CLI installation + live processes.</summary>
public sealed record AiSystemInfo(
    string Id,
    string Name,
    bool Installed,
    string? CliVersion,
    bool Running,
    int ProcessCount,
    double TotalMemoryMb,
    IReadOnlyList<AiProcessInfo> Processes);

public sealed record MachineInfo(
    string Host, string Os, int ProcessorCount, double TotalMemoryMb, double UptimeHours);

public sealed record StatsSnapshot(
    DateTime GeneratedAtUtc, MachineInfo Machine, IReadOnlyList<AiSystemInfo> Systems);

/// <summary>
/// Probes the host for known AI systems (Claude Code, Copilot, Codex, Cursor, Gemini, Ollama):
/// CLI presence/version plus a live process scan. Extend by adding a row to the catalog array
/// in this file.
/// </summary>
public static class AiSystemProbe
{
    private sealed record CatalogEntry(string Id, string Name, string[] VersionCommands, string[] ProcessKeys);

    // A flat row per system is the whole extension mechanism: adding a system is one
    // line here, with no registry or plugin interface to wire up.
    private static readonly CatalogEntry[] Catalog =
    [
        new("claude",  "Claude Code",     ["claude --version"],                          ["claude"]),
        new("copilot", "GitHub Copilot",  ["copilot --version", "gh copilot --version"], ["copilot"]),
        new("codex",   "OpenAI Codex",    ["codex --version"],                           ["codex"]),
        new("cursor",  "Cursor",          ["cursor --version"],                          ["cursor"]),
        new("gemini",  "Gemini CLI",      ["gemini --version"],                          ["gemini"]),
        new("ollama",  "Ollama",          ["ollama --version"],                          ["ollama"]),
    ];

    public static async Task<StatsSnapshot> CaptureAsync(CancellationToken ct = default)
    {
        var processesBySystem = ScanProcesses();
        var versionTasks = Catalog.Select(e => ProbeVersionAsync(e.VersionCommands, ct)).ToArray();
        var versions = await Task.WhenAll(versionTasks);

        var systems = Catalog.Select((entry, i) =>
        {
            var procs = processesBySystem.GetValueOrDefault(entry.Id, []);
            var version = versions[i];
            return new AiSystemInfo(
                entry.Id, entry.Name,
                Installed: version is not null,
                CliVersion: version,
                Running: procs.Count > 0,
                ProcessCount: procs.Count,
                TotalMemoryMb: Math.Round(procs.Sum(p => p.MemoryMb), 1),
                Processes: procs);
        }).ToArray();

        return new StatsSnapshot(DateTime.UtcNow, CaptureMachine(), systems);
    }

    private static MachineInfo CaptureMachine()
    {
        var gc = GC.GetGCMemoryInfo();
        return new MachineInfo(
            Host: Environment.MachineName,
            Os: RuntimeInformation.OSDescription,
            ProcessorCount: Environment.ProcessorCount,
            TotalMemoryMb: Math.Round(gc.TotalAvailableMemoryBytes / 1024.0 / 1024.0, 0),
            UptimeHours: Math.Round(Environment.TickCount64 / 3_600_000.0, 1));
    }

    private static Dictionary<string, List<AiProcessInfo>> ScanProcesses()
    {
        var result = new Dictionary<string, List<AiProcessInfo>>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = process.ProcessName;
                var entry = Catalog.FirstOrDefault(e =>
                    e.ProcessKeys.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)));
                if (entry is null) continue;

                DateTime? started = null;
                try { started = process.StartTime.ToUniversalTime(); }
                catch { /* access denied on foreign-user processes; started stays null */ }

                var info = new AiProcessInfo(
                    process.Id, name, Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1), started);
                (result.TryGetValue(entry.Id, out var list) ? list : result[entry.Id] = []).Add(info);
            }
            catch { /* process exited mid-scan; skip */ }
            finally { process.Dispose(); }
        }
        return result;
    }

    /// <summary>First command that exits 0 with usable output wins; null means the CLI is absent.</summary>
    private static async Task<string?> ProbeVersionAsync(string[] commands, CancellationToken ct)
    {
        foreach (var command in commands)
        {
            var line = NormalizeVersionOutput(await RunShellAsync(command, ct));
            if (line is not null) return line;
        }
        return null;
    }

    /// <summary>
    /// First non-empty line only, prefix chatter stripped ("ollama version is 0.32.1" -> "0.32.1").
    /// Null when the output holds no usable line. Internal for fuzz tests.
    /// </summary>
    internal static string? NormalizeVersionOutput(string? raw)
    {
        // Split on both CR and LF — a lone '\r' (progress spinner, old-Mac ending) must
        // never survive into the "single line" result.
        var line = raw?.Split(['\r', '\n']).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        if (line is null) return null;
        return line.StartsWith("ollama version is ", StringComparison.OrdinalIgnoreCase)
            ? line["ollama version is ".Length..] : line;
    }

    private static async Task<string?> RunShellAsync(string command, CancellationToken ct)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/sh",
            Arguments = isWindows ? $"/c {command}" : $"-c \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return null;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null; // missing shell, timeout, or kill race — treat as not installed
        }
    }
}
