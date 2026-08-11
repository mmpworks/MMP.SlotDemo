using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Randomized checks on <see cref="SpinRng"/>'s three load-bearing promises: a (seed, workerId)
/// pair reproduces its stream exactly, distinct worker ids diverge, and draws are not visibly
/// biased. This is not a cryptographic or gaming-grade RNG certification (see the type's own
/// doc comment) — the chi-square check below is a loose sanity bound, not a rigor statistical
/// test.
/// </summary>
[Trait("Category", "Fast")]
public sealed class SpinRngFuzzTests
{
    private const int Iterations = 500;
    private const int DrawsPerStream = 200;

    [Fact]
    public void ForWorker_SameSeedAndWorkerId_ProducesIdenticalStreams()
    {
        var rng = new Random(5001);
        for (var i = 0; i < Iterations; i++)
        {
            var seed = (ulong)rng.NextInt64();
            var workerId = rng.Next(0, 64);

            var a = SpinRng.ForWorker(seed, workerId);
            var b = SpinRng.ForWorker(seed, workerId);

            for (var draw = 0; draw < DrawsPerStream; draw++)
                Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }
    }

    [Fact]
    public void ForWorker_DifferentWorkerIds_DivergeWithinAFewDraws()
    {
        var rng = new Random(5002);
        for (var i = 0; i < Iterations; i++)
        {
            var seed = (ulong)rng.NextInt64();
            var workerA = rng.Next(0, 1000);
            int workerB;
            do { workerB = rng.Next(0, 1000); } while (workerB == workerA);

            var a = SpinRng.ForWorker(seed, workerA);
            var b = SpinRng.ForWorker(seed, workerB);

            var diverged = false;
            for (var draw = 0; draw < DrawsPerStream && !diverged; draw++)
            {
                if (a.NextUInt64() != b.NextUInt64()) diverged = true;
            }

            Assert.True(
                diverged,
                $"Iteration {i}: workers {workerA} and {workerB} under seed {seed} produced "
                + $"{DrawsPerStream} identical draws.");
        }
    }

    /// <summary>
    /// Chi-square-lite sanity: <see cref="SpinRng.NextInt"/> draws should land in every bucket
    /// roughly evenly. The threshold is deliberately loose — this catches a broken modulus or a
    /// badly biased rejection loop, not minor sampling noise.
    /// </summary>
    [Fact]
    public void NextInt_DistributesRoughlyUniformlyAcrossTheBound()
    {
        var rng = new Random(5003);
        for (var i = 0; i < 50; i++)
        {
            var seed = (ulong)rng.NextInt64();
            var bound = rng.Next(2, 37);
            const int draws = 20_000;

            var spinRng = SpinRng.ForWorker(seed, workerId: rng.Next(0, 8));
            var buckets = new int[bound];
            for (var d = 0; d < draws; d++) buckets[spinRng.NextInt(bound)]++;

            var expected = (double)draws / bound;
            var chiSquare = buckets.Sum(count => Math.Pow(count - expected, 2) / expected);
            var threshold = 5.0 * bound + 50.0;

            Assert.True(
                chiSquare <= threshold,
                $"Iteration {i}: bound {bound}, seed {seed}, chi-square {chiSquare:F1} exceeded loose threshold {threshold:F1}.");
        }
    }

    [Fact]
    public void NextDouble_AlwaysStaysInTheHalfOpenUnitInterval()
    {
        var seed = (ulong)new Random(5004).NextInt64();
        var spinRng = SpinRng.ForWorker(seed, workerId: 0);

        for (var i = 0; i < 50_000; i++)
        {
            var value = spinRng.NextDouble();
            Assert.True(value is >= 0.0 and < 1.0, $"NextDouble returned {value:R}, outside [0, 1).");
        }
    }
}
