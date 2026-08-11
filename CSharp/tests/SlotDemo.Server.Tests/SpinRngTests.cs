using System.Numerics;
using SlotDemo.Server.Chapters.Simulation;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// Replayability and stream separation — the two properties episode 2 asks of the
/// generator. The naive-seeding case is tested too: the trap has to keep behaving like a
/// trap, or the demo that contrasts them stops teaching anything.
/// </summary>
public sealed class SpinRngTests
{
    private const ulong Seed = 20260810;

    [Fact]
    public void R3_the_same_seed_and_worker_replay_the_same_stream()
    {
        var first = SpinRng.ForWorker(Seed, 3);
        var second = SpinRng.ForWorker(Seed, 3);

        for (var i = 0; i < 64; i++)
            Assert.Equal(first.NextUInt64(), second.NextUInt64());
    }

    [Fact]
    public void A_different_seed_produces_a_different_stream()
    {
        var a = SpinRng.ForWorker(Seed, 0);
        var b = SpinRng.ForWorker(Seed + 1, 0);

        // Guards against determinism achieved by ignoring the seed entirely.
        var differs = Enumerable.Range(0, 16).Any(_ => a.NextUInt64() != b.NextUInt64());
        Assert.True(differs);
    }

    [Fact]
    public void SplitMix64_seeding_leaves_neighbouring_workers_uncorrelated()
    {
        for (var worker = 0; worker < 7; worker++)
        {
            var left = SpinRng.ForWorker(Seed, worker);
            var right = SpinRng.ForWorker(Seed, worker + 1);
            var shared = SharedLeadingBits(left.NextUInt64(), right.NextUInt64());

            Assert.True(shared < 16,
                $"workers {worker}/{worker + 1} share {shared} leading bits");
        }
    }

    [Fact]
    public void Naive_seeding_correlates_neighbouring_workers()
    {
        // Documents the failure the demo contrasts against. If this ever stops correlating,
        // the demo's whole point has quietly expired and the episode needs a new example.
        var left = SpinRng.ForWorkerUnmixed(Seed, 0);
        var right = SpinRng.ForWorkerUnmixed(Seed, 1);

        Assert.True(SharedLeadingBits(left.NextUInt64(), right.NextUInt64()) >= 16);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(32)]
    [InlineData(37)]
    [InlineData(101)]
    public void NextInt_stays_inside_the_strip(int bound)
    {
        var rng = SpinRng.ForWorker(Seed, 1);
        for (var i = 0; i < 20_000; i++)
        {
            var value = rng.NextInt(bound);
            Assert.InRange(value, 0, bound - 1);
        }
    }

    [Fact]
    public void NextInt_rejects_a_bound_that_cannot_name_a_stop()
    {
        var rng = SpinRng.ForWorker(Seed, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(-5));
    }

    [Fact]
    public void NextInt_spreads_evenly_enough_for_a_strip_of_37()
    {
        // One seed is a single sample of a random variable, and a healthy generator lands
        // in the tail roughly one run in a hundred by definition. Twenty fixed seeds, each
        // measured against the 99% critical value, keeps the test deterministic while
        // asking the question the episode actually cares about: is the reduction even.
        const int bound = 37;
        const int samples = 100_000;
        const double criticalValue99 = 58.62;   // chi-square, 36 degrees of freedom
        const int seedCount = 20;

        var overCritical = new List<string>();
        for (var seedIndex = 0; seedIndex < seedCount; seedIndex++)
        {
            var counts = new int[bound];
            var rng = SpinRng.ForWorker(Seed + (ulong)seedIndex * 7919, seedIndex);
            for (var i = 0; i < samples; i++) counts[rng.NextInt(bound)]++;

            var expected = (double)samples / bound;
            var chiSquare = counts.Sum(c => (c - expected) * (c - expected) / expected);
            if (chiSquare >= criticalValue99)
                overCritical.Add($"seed {seedIndex}: {chiSquare:F2}");
        }

        // At the 99% level, two or more excursions out of twenty already argues the
        // reduction is skewed rather than unlucky.
        Assert.True(overCritical.Count <= 1,
            $"{overCritical.Count}/{seedCount} runs exceeded the critical value: "
            + string.Join(", ", overCritical));
    }

    [Fact]
    public void NextDouble_lands_in_the_unit_interval()
    {
        var rng = SpinRng.ForWorker(Seed, 4);
        for (var i = 0; i < 10_000; i++)
        {
            var value = rng.NextDouble();
            Assert.InRange(value, 0.0, double.BitDecrement(1.0));
        }
    }

    [Fact]
    public void The_stream_advances_through_the_reference_the_caller_holds()
    {
        // The ref discipline in one assertion: a method handed the stream by reference
        // moves the caller's stream, so no worker silently replays its own values.
        var rng = SpinRng.ForWorker(Seed, 5);
        var first = rng.NextUInt64();
        var afterCall = Draw(ref rng);

        Assert.NotEqual(first, afterCall);
        Assert.NotEqual(afterCall, rng.NextUInt64());
    }

    private static ulong Draw(ref SpinRng rng) => rng.NextUInt64();

    private static int SharedLeadingBits(ulong a, ulong b)
    {
        var diff = a ^ b;
        return diff == 0 ? 64 : BitOperations.LeadingZeroCount(diff);
    }
}
