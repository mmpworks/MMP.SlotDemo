using System.Diagnostics;

namespace MMP.SlotGame.Core.Simulation;

/// <summary>
/// What the workers themselves spent their time on, collected by the engine and read after
/// every worker is done.
///
/// Wall-clock around a run answers "how long did this take to watch", which includes the
/// telemetry drain, serialization, and anything a connected browser costs. It cannot answer
/// "how fast is the engine", because those costs steal CPU from the same cores the workers
/// are running on. These counters separate the two: <see cref="SpinTicks"/> is time inside
/// the spin loop, <see cref="PublishTicks"/> is time handing a snapshot to the telemetry
/// channel, and each is tracked both as a total across workers and as the worst single
/// worker, which is the span that actually gates completion.
/// </summary>
public sealed class EngineTimings
{
    private long _spinTicks;
    private long _publishTicks;
    private long _maxWorkerSpinTicks;
    private long _maxWorkerPublishTicks;
    private long _workers;

    /// <summary>Summed across every worker, so it exceeds wall time on a parallel run.</summary>
    public TimeSpan SpinTime => TimeSpan.FromTicks(Interlocked.Read(ref _spinTicks));

    /// <summary>Summed across every worker.</summary>
    public TimeSpan PublishTime => TimeSpan.FromTicks(Interlocked.Read(ref _publishTicks));

    /// <summary>
    /// The slowest single worker's spin time. Workers run at once, so this is the parallel
    /// span the run waits on, and the right denominator for a spins-per-second figure.
    /// </summary>
    public TimeSpan SlowestWorkerSpinTime => TimeSpan.FromTicks(Interlocked.Read(ref _maxWorkerSpinTicks));

    public TimeSpan SlowestWorkerPublishTime => TimeSpan.FromTicks(Interlocked.Read(ref _maxWorkerPublishTicks));

    public int Workers => (int)Interlocked.Read(ref _workers);

    /// <summary>
    /// Spins per second the engine sustained, measured against the slowest worker's own
    /// spin time. Publishing telemetry is excluded; CPU stolen from a worker still lands
    /// inside its spin time, so this stays an honest measurement rather than a best case.
    /// </summary>
    public double SpinsPerSecond(long spins)
    {
        var seconds = SlowestWorkerSpinTime.TotalSeconds;
        return spins <= 0 || seconds <= 0 ? 0 : spins / seconds;
    }

    /// <summary>Share of worker time spent publishing telemetry rather than spinning.</summary>
    public double PublishShare
    {
        get
        {
            var total = SpinTime.Ticks + PublishTime.Ticks;
            return total == 0 ? 0 : (double)PublishTime.Ticks / total;
        }
    }

    internal void AddWorker(long spinTicks, long publishTicks)
    {
        Interlocked.Add(ref _spinTicks, spinTicks);
        Interlocked.Add(ref _publishTicks, publishTicks);
        Interlocked.Increment(ref _workers);
        Max(ref _maxWorkerSpinTicks, spinTicks);
        Max(ref _maxWorkerPublishTicks, publishTicks);
    }

    private static void Max(ref long target, long candidate)
    {
        long seen;
        do
        {
            seen = Interlocked.Read(ref target);
            if (candidate <= seen) return;
        }
        while (Interlocked.CompareExchange(ref target, candidate, seen) != seen);
    }

    /// <summary>A stopwatch that measures without allocating one per batch.</summary>
    internal static long Now => Stopwatch.GetTimestamp();

    internal static long ToTicks(long start, long end) =>
        (long)((end - start) * (10_000_000.0 / Stopwatch.Frequency));
}
