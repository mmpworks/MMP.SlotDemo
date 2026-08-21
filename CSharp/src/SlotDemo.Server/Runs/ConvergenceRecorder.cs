using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Selects convergence-chart points from cumulative telemetry snapshots.
///
/// The recorder keeps the latest totals and adds one point when a snapshot crosses the
/// next spin-count stride. At the default 50,000-spin stride, a 10M-spin run produces
/// about 200 points. Each point includes its confidence half-width.
/// </summary>
public sealed class ConvergenceRecorder(double analyticRtp, double sigmaPerUnitWagered, long stride)
{
    /// <summary>Default consolidation stride. One point per 50,000 spins.</summary>
    public const long DefaultStride = 50_000;

    /// <summary>
    /// Fixed comparison limit used after 10M spins. This ±0.5 percentage-point rule is a
    /// lab convention, not a requirement quoted from GLI-11. The confidence band remains
    /// a separate, sigma-based calculation.
    /// </summary>
    public const double FixedTolerance = 0.005;

    /// <summary>Minimum sample size for the fixed-tolerance check.</summary>
    public const long MinimumSpinsForFixedTolerance = 10_000_000;

    private readonly Lock _gate = new();
    private readonly List<ConvergencePoint> _curve = [];
    private long _nextBoundary = stride;
    private RunSnapshot _latest;

    public long Stride { get; } = stride > 0 ? stride : DefaultStride;

    public RunSnapshot Latest
    {
        get { lock (_gate) return _latest; }
    }

    public IReadOnlyList<ConvergencePoint> Curve
    {
        get { lock (_gate) return _curve.ToArray(); }
    }

    /// <summary>
    /// Records the newest cumulative snapshot. Returns a point when the spin count crosses
    /// the next stride boundary; otherwise returns null.
    /// </summary>
    public ConvergencePoint? RecordSnapshot(RunSnapshot snapshot)
    {
        lock (_gate)
        {
            // Snapshots arrive from several workers and can straddle a batch, so spins can
            // appear to move backwards between two reads. Keeping the highest reading stops
            // the curve from stuttering.
            if (snapshot.Spins < _latest.Spins) return null;
            _latest = snapshot;

            if (snapshot.Spins < _nextBoundary) return null;

            // A burst can jump several strides at once; the next boundary follows the run
            // rather than the stride count, so the curve never tries to backfill.
            _nextBoundary = (snapshot.Spins / Stride + 1) * Stride;
            var point = Measure(snapshot);
            _curve.Add(point);
            return point;
        }
    }

    /// <summary>
    /// Records and returns the terminal point, including runs that end between stride
    /// boundaries.
    /// </summary>
    public ConvergencePoint RecordFinalSnapshot(RunSnapshot finalSnapshot)
    {
        lock (_gate)
        {
            _latest = finalSnapshot;
            var point = Measure(finalSnapshot);
            if (_curve.Count == 0 || _curve[^1].Spins != point.Spins)
                _curve.Add(point);
            return point;
        }
    }

    /// <summary>
    /// Applies the fixed RTP tolerance after <see cref="MinimumSpinsForFixedTolerance"/>. Returns
    /// null below that sample size.
    /// </summary>
    public FixedToleranceResult? CheckFixedTolerance()
    {
        RunSnapshot latest;
        lock (_gate) latest = _latest;
        if (latest.Spins < MinimumSpinsForFixedTolerance) return null;
        var deviation = Math.Abs(latest.MeasuredRtp - analyticRtp);
        return new FixedToleranceResult(latest.Spins, deviation, deviation <= FixedTolerance);
    }

    private ConvergencePoint Measure(RunSnapshot snapshot)
    {
        // The band narrows with sqrt(N): 100 times as many spins makes it 10 times narrower.
        var halfWidth = snapshot.Spins > 0
            ? NormalQuantile.TwoSided99 * sigmaPerUnitWagered / Math.Sqrt(snapshot.Spins)
            : 0;

        return new ConvergencePoint(
            snapshot.Spins,
            snapshot.MeasuredRtp,
            snapshot.HitFrequency,
            halfWidth,
            Math.Abs(snapshot.MeasuredRtp - analyticRtp) <= halfWidth);
    }
}

/// <summary>
/// One consolidated point on the convergence chart: where the measured RTP sat after
/// this many spins, and how wide the band was at that N.
/// </summary>
public readonly record struct ConvergencePoint(
    long Spins,
    double MeasuredRtp,
    double HitFrequency,
    double BandHalfWidth,
    bool WithinBand);

/// <summary>
/// The fixed-tolerance check: how far measured RTP sits from analytic RTP after at least
/// <see cref="ConvergenceRecorder.MinimumSpinsForFixedTolerance"/> spins, and whether that
/// deviation fits inside <see cref="ConvergenceRecorder.FixedTolerance"/>.
/// </summary>
public readonly record struct FixedToleranceResult(long Spins, double Deviation, bool Passed);
