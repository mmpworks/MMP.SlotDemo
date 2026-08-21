using System.Threading.Channels;
using MMP.Herald.Events;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>Shared log category for simulation-run components.</summary>
internal static class RunLogging
{
    internal static readonly LogCategory Category = new("SimulationRun");
}

/// <summary>Game identity and immutable settings for an accepted run.</summary>
internal sealed record RunConfiguration(
    string GameName,
    bool IsShippedGame,
    int Reels,
    int Rows,
    string StopCounts,
    int Paylines,
    double TargetRtp,
    int Workers,
    long TargetSpins,
    double PublishedRtp,
    double PayScaleFactor,
    ulong Seed);

/// <summary>Calculated RTP and volatility values used to evaluate simulation results.</summary>
internal sealed record AnalyticReference(
    double BaseRtp,
    IReadOnlyList<(string Name, double Rtp)> FeatureContributions,
    double TotalRtp,
    double Sigma);

/// <summary>
/// Executes a prepared game, publishes cumulative telemetry, and returns final totals and timings.
/// </summary>
internal delegate Task<(RunSnapshot Totals, EngineTimings Timings)> SimulationExecutor(
    ChannelWriter<TelemetrySample> telemetry, CancellationToken ct);

/// <summary>Everything the coordinator needs to execute and report an accepted run.</summary>
internal sealed record PreparedRun(
    RunConfiguration Configuration,
    AnalyticReference Reference,
    SimulationExecutor Execute,
    string RunId);

/// <summary>
/// Returns either a prepared subject or the HTTP error used to reject the request.
/// </summary>
internal sealed record RunPreparationResult(PreparedRun? Prepared, (int Status, object Body)? Error)
{
    public static RunPreparationResult Success(PreparedRun prepared) => new(prepared, null);

    public static RunPreparationResult Failure(int status, object body) => new(null, (status, body));

    public static RunPreparationResult Failure((int Status, object Body) error) => new(null, error);
}
