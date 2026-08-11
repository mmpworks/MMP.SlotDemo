using System.Runtime.CompilerServices;

namespace SlotDemo.Server.Tests;

internal static class TestHostConfiguration
{
    /// <summary>
    /// Runs before any test class is constructed, so the setting is in place by the time
    /// <c>WebApplicationFactory</c> executes Program.cs. The in-process host serves requests
    /// without binding a port, so the log-relay sink would post to a closed socket and hold
    /// up the drain the file-sink assertions depend on.
    /// </summary>
    [ModuleInitializer]
    internal static void DisableLogRelay() =>
        Environment.SetEnvironmentVariable("SLOTDEMO_LOG_INGEST_URL", "");
}
