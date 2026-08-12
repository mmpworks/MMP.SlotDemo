using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Turns a flood of run snapshots into a curve a browser can hold.
///
/// A ten-million-spin run produces roughly 2,400 batch snapshots per worker. Sending
/// them all would spend the network on points that land on top of each other, and the
/// SPA would hold an array it never draws. So the recorder consolidates: it keeps the
/// newest snapshot for the live readout, and appends one curve point each time the run
/// crosses a stride boundary — 50,000 spins by default, which puts 200 points on a 10M
/// run and none of them redundant.
///
/// Every point carries its own confidence half-width, because the band narrows as the
/// square root of N. Computing it server-side leaves the SPA one statistic to draw.
///
/// The full curve is kept in memory (a 10M run costs about 200 records), so a browser that
/// connects late still receives every point from the start of the run.
/// </summary>
public sealed class ConvergenceRecorder(double analyticRtp, double sigmaPerUnitWagered, long stride)
{
    /// <summary>Default consolidation stride. One point per 50,000 spins.</summary>
    public const long DefaultStride = 50_000;

    private readonly Lock _gate = new();
    private readonly List<CurvePoint> _curve = [];
    private long _nextBoundary = stride;
    private RunSnapshot _latest;

    public long Stride { get; } = stride > 0 ? stride : DefaultStride;

    public RunSnapshot Latest
    {
        get { lock (_gate) return _latest; }
    }

    public IReadOnlyList<CurvePoint> Curve
    {
        get { lock (_gate) return _curve.ToArray(); }
    }

    /// <summary>
    /// Records a snapshot. Returns the curve point when this snapshot crossed a stride
    /// boundary, and null otherwise — callers push the returned point and skip the rest.
    /// </summary>
    public CurvePoint? Observe(RunSnapshot snapshot)
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
    /// The end of the run always earns a point, whether or not it landed on a boundary,
    /// because the page reads its final verdict off the last point.
    /// </summary>
    public CurvePoint Complete(RunSnapshot finalSnapshot)
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

    private CurvePoint Measure(RunSnapshot snapshot)
    {
        // Band = z * sigma / sqrt(N), the same closed form the analytic twin supplies.
        var halfWidth = snapshot.Spins > 0
            ? NormalQuantile.TwoSided99 * sigmaPerUnitWagered / Math.Sqrt(snapshot.Spins)
            : 0;

        return new CurvePoint(
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
public readonly record struct CurvePoint(
    long Spins,
    double MeasuredRtp,
    double HitFrequency,
    double BandHalfWidth,
    bool WithinBand);
