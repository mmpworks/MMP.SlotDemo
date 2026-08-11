namespace MMP.SlotGame.Core.Simulation;

/// <summary>
/// The lossless side of the pipeline: integer millicent counters, batched
/// Interlocked adds (architecture §7). Reset (RT-18) is done by swapping in a fresh
/// instance, never by zeroing live fields.
/// </summary>
public sealed class RunTotals
{
    private long _spins;
    private long _wageredMillicents;
    private long _returnedMillicents;
    private long _hits;

    /// <summary>One batched contribution from a worker (four adds per ~4096 spins, not per spin).</summary>
    public void AddBatch(long spins, long wageredMillicents, long returnedMillicents, long hits)
    {
        Interlocked.Add(ref _spins, spins);
        Interlocked.Add(ref _wageredMillicents, wageredMillicents);
        Interlocked.Add(ref _returnedMillicents, returnedMillicents);
        Interlocked.Add(ref _hits, hits);
    }

    /// <summary>
    /// Counter reads are individually atomic, not atomic as a set (RT-20): a mid-run
    /// snapshot can straddle a batch and is display-only. Acceptance assertions read
    /// AFTER the run quiesces (post-WhenAll), where this is exact.
    /// </summary>
    public RunSnapshot Snapshot() => new(
        Interlocked.Read(ref _spins),
        Interlocked.Read(ref _wageredMillicents),
        Interlocked.Read(ref _returnedMillicents),
        Interlocked.Read(ref _hits));
}

public readonly record struct RunSnapshot(long Spins, long WageredMillicents, long ReturnedMillicents, long Hits)
{
    public double MeasuredRtp => WageredMillicents == 0 ? 0 : (double)ReturnedMillicents / WageredMillicents;
    public double HitFrequency => Spins == 0 ? 0 : (double)Hits / Spins;
}

/// <summary>
/// One telemetry message. Carries ABSOLUTE snapshots, never deltas (RT-19): a dropped
/// sample costs one chart point, not accuracy.
/// </summary>
public readonly record struct TelemetrySample(string RunId, RunSnapshot Totals);
