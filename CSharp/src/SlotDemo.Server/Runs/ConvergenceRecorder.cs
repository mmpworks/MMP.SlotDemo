using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;

namespace SlotDemo.Server.Runs;

/// <summary>
/// Reduces worker snapshots to the points shown on the convergence chart.
///
/// <see cref="Latest"/> keeps the newest totals for the live readout. <see cref="Curve"/>
/// adds a point at each stride boundary and retains the full run for browsers that connect
/// late. With the default 50,000-spin stride, a ten-million-spin run keeps about 200 points.
/// Each point includes the confidence half-width calculated for its spin count.
/// </summary>
public sealed class ConvergenceRecorder(double analyticRtp, double sigmaPerUnitWagered, long stride)
{
    /// <summary>Default consolidation stride. One point per 50,000 spins.</summary>
    public const long DefaultStride = 50_000;

    /// <summary>
    /// Fixed half-percentage-point tolerance used by this lab after ten million spins.
    /// This is a lab convention, not a value quoted from GLI-11. The statistical band is a
    /// separate check and may be wider or narrower depending on the game's sigma.
    /// </summary>
    public const double IndustryTolerance = 0.005;

    /// <summary>Minimum spins before the industry check applies.</summary>
    public const long IndustryMinimumSpins = 10_000_000;

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
    /// Records the latest totals and returns a chart point after a stride boundary is
    /// crossed. Returns <see langword="null"/> between boundaries.
    /// </summary>
    public CurvePoint? Observe(RunSnapshot snapshot)
    {
        lock (_gate)
        {
            // Worker snapshots can arrive out of order at a batch boundary. Ignore an older
            // total so the live count and curve remain monotonic.
            if (snapshot.Spins < _latest.Spins) return null;
            _latest = snapshot;

            if (snapshot.Spins < _nextBoundary) return null;

            // One snapshot may cross several boundaries. Resume after its actual spin count
            // instead of inventing points for totals the recorder never received.
            _nextBoundary = (snapshot.Spins / Stride + 1) * Stride;
            var point = Measure(snapshot);
            _curve.Add(point);
            return point;
        }
    }

    /// <summary>
    /// Adds the final totals to the curve unless that spin count is already present.
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

    /// <summary>
    /// Compares the latest RTP with the fixed lab tolerance after
    /// <see cref="IndustryMinimumSpins"/>. Returns <see langword="null"/> before then.
    /// </summary>
    public IndustryVerdict? IndustryCheck()
    {
        RunSnapshot latest;
        lock (_gate) latest = _latest;
        if (latest.Spins < IndustryMinimumSpins) return null;
        var deviation = Math.Abs(latest.MeasuredRtp - analyticRtp);
        return new IndustryVerdict(latest.Spins, deviation, deviation <= IndustryTolerance);
    }

    private CurvePoint Measure(RunSnapshot snapshot)
    {
        // 99% confidence half-width: z * sigma / sqrt(N).
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
/// Measured RTP and its confidence band at one spin count.
/// </summary>
public readonly record struct CurvePoint(
    long Spins,
    double MeasuredRtp,
    double HitFrequency,
    double BandHalfWidth,
    bool WithinBand);

/// <summary>
/// Result of the fixed-tolerance lab check after the minimum spin count.
/// </summary>
public readonly record struct IndustryVerdict(long Spins, double Deviation, bool Passed);
