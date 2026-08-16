using MMP.SlotGame.Core.Simulation;
using SlotDemo.Server.Runs;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// The consolidation rules the finale chart depends on. The recorder is the reason ten
/// million spins reach the browser as a couple hundred points, so its boundary
/// behavior is the contract under test, boundary by boundary.
/// </summary>
public sealed class ConvergenceRecorderTests
{
    private const double AnalyticRtp = 0.98;
    private const double Sigma = 8.6;

    private static RunSnapshot At(long spins) =>
        new(spins, spins * 100_000, (long)(spins * 100_000 * 0.978), spins / 3);

    [Fact]
    public void A_snapshot_below_the_first_boundary_records_no_point()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 50_000);
        Assert.Null(recorder.Observe(At(49_999)));
        Assert.Empty(recorder.Curve);
    }

    [Fact]
    public void Crossing_a_boundary_records_one_point()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 50_000);
        var point = recorder.Observe(At(50_000));
        Assert.NotNull(point);
        Assert.Equal(50_000, point!.Value.Spins);
        Assert.Single(recorder.Curve);
    }

    [Fact]
    public void A_burst_that_jumps_several_strides_records_one_point_and_moves_on()
    {
        // Workers can advance a million spins between two drains. The curve keeps one
        // point for the burst instead of backfilling boundaries nothing observed.
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 50_000);
        Assert.NotNull(recorder.Observe(At(1_000_000)));
        Assert.Null(recorder.Observe(At(1_040_000)));    // inside the next stride
        Assert.NotNull(recorder.Observe(At(1_050_000))); // crosses it
        Assert.Equal(2, recorder.Curve.Count);
    }

    [Fact]
    public void Snapshots_arriving_out_of_order_cannot_walk_the_curve_backwards()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 50_000);
        recorder.Observe(At(100_000));
        Assert.Null(recorder.Observe(At(60_000)));   // stale straddling read
        Assert.Equal(100_000, recorder.Latest.Spins);
    }

    [Fact]
    public void The_band_narrows_as_the_square_root_of_spins()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 10_000);
        var early = recorder.Observe(At(10_000))!.Value;
        RunSnapshot later = At(1_000_000);
        recorder.Observe(later);
        var late = recorder.Curve[^1];

        // 100x the spins, 10x narrower — the funnel shape as arithmetic.
        Assert.Equal(early.BandHalfWidth / 10, late.BandHalfWidth, precision: 10);
    }

    [Fact]
    public void Complete_always_lands_a_final_point_even_off_boundary()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 50_000);
        recorder.Observe(At(50_000));
        var final = recorder.Complete(At(73_412));
        Assert.Equal(73_412, final.Spins);
        Assert.Equal(73_412, recorder.Curve[^1].Spins);
    }

    [Fact]
    public void Complete_on_a_boundary_point_does_not_duplicate_it()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 50_000);
        recorder.Observe(At(100_000));
        recorder.Complete(At(100_000));
        Assert.Single(recorder.Curve);
    }

    [Fact]
    public void Within_band_reflects_the_distance_from_the_analytic_line()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 1_000);

        // 0.978 measured against 0.98 analytic: inside at small N (wide band), outside
        // at huge N (narrow band). Same measurement, different certainty.
        var early = recorder.Observe(At(1_000))!.Value;
        Assert.True(early.WithinBand);

        var recorder2 = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 1_000);
        var wagered = 40_000_000_000L * 100_000;
        var far = new RunSnapshot(40_000_000_000, wagered, (long)(wagered * 0.978), 1);
        var latePoint = recorder2.Complete(far);
        Assert.False(latePoint.WithinBand);
    }

    [Fact]
    public void Concurrent_observers_never_corrupt_the_curve()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 10_000);
        Parallel.For(0, 8, worker =>
        {
            for (var i = 1; i <= 200; i++)
                recorder.Observe(At(i * 5_000 + worker));
        });

        // Strictly increasing spins is the invariant a chart needs; the exact count
        // depends on interleaving and is not the contract.
        var curve = recorder.Curve;
        for (var i = 1; i < curve.Count; i++)
            Assert.True(curve[i].Spins > curve[i - 1].Spins,
                $"curve went backwards at index {i}: {curve[i - 1].Spins} -> {curve[i].Spins}");
    }

    // ---- the industry acceptance (±0.5pp over at least 10M spins) ----

    private static RunSnapshot AtRtp(long spins, double rtp) =>
        new(spins, spins * 100_000, (long)(spins * 100_000 * rtp), spins / 3);

    [Fact]
    public void Industry_check_is_null_below_ten_million_spins()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 50_000);
        recorder.Observe(AtRtp(9_999_999, 0.978));
        Assert.Null(recorder.IndustryCheck());
    }

    [Fact]
    public void Industry_check_passes_inside_half_a_percentage_point()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 50_000);
        recorder.Observe(AtRtp(10_000_000, 0.978));   // deviation 0.002
        var check = recorder.IndustryCheck();
        Assert.NotNull(check);
        Assert.True(check!.Value.Passed);
        Assert.Equal(0.002, check.Value.Deviation, precision: 6);
    }

    [Fact]
    public void Industry_check_fails_outside_half_a_percentage_point()
    {
        var recorder = new ConvergenceRecorder(AnalyticRtp, Sigma, stride: 50_000);
        recorder.Observe(AtRtp(10_000_000, 0.970));   // deviation 0.010
        var check = recorder.IndustryCheck();
        Assert.NotNull(check);
        Assert.False(check!.Value.Passed);
    }
}
