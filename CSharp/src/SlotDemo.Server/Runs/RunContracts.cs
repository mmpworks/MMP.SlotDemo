using System.Threading.Channels;
using MMP.Herald.Events;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>The one log category every run component writes under.</summary>
internal static class RunLogging
{
    internal static readonly LogCategory Category = new("SimulationRun");
}

/// <summary>What the page shows about the subject, independent of which kind it is.</summary>
internal sealed record RunFacts(
    string Subject,
    bool IsGame,
    int Reels,
    int Rows,
    string StopsByReel,
    int Paylines,
    double TargetRtp,
    int Workers,
    long TargetSpins,
    double PublishedRtp,
    double PayScaleFactor,
    ulong Seed);

/// <summary>The analytic reference the live chart converges toward.</summary>
internal sealed record AnalyticView(
    double BaseRtp,
    IReadOnlyList<(string Name, double Rtp)> Features,
    double TotalRtp,
    double Sigma);

/// <summary>Runs the subject's spins; both kinds return the quiesced final snapshot.</summary>
internal delegate Task<(RunSnapshot Totals, EngineTimings Timings)> SubjectRunner(
    ChannelWriter<TelemetrySample> telemetry, CancellationToken ct);

/// <summary>Everything a validated request produced: what to run and how to describe it.</summary>
internal sealed record PreparedRun(
    RunFacts Facts,
    AnalyticView Analytic,
    SubjectRunner Runner,
    string RunId);

/// <summary>
/// The outcome of a preparation path: a subject ready to run, or the HTTP status and body
/// explaining why not. Exactly one side is set.
/// </summary>
internal sealed record PrepareResult(PreparedRun? Prepared, (int Status, object Body)? Error)
{
    public static PrepareResult Ok(PreparedRun prepared) => new(prepared, null);

    public static PrepareResult Fail(int status, object body) => new(null, (status, body));
}
