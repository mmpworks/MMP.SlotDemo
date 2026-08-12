namespace SlotDemo.Server.Chapters.Simulation;

/// <summary>
/// Deterministic per-worker RNG stream — xoshiro256** seeded via SplitMix64.
///
/// Randomness in Core enters spin logic through <c>ref SpinRng</c> parameters. Keeping
/// the stream explicit makes a run replayable when the game, seed, worker count, spin
/// target, and code version are unchanged.
///
/// Each worker starts from a distinct value derived from the master seed and worker id;
/// SplitMix64 expands that value into the four xoshiro state words.
///
/// Simulation-grade RNG. Real-money play requires a certified gaming RNG; this is not one.
/// </summary>
public struct SpinRng
{
    private ulong _s0, _s1, _s2, _s3;

    public static SpinRng ForWorker(ulong masterSeed, int workerId)
    {
        // SplitMix64 both mixes the worker id and expands one seed into four
        // well-distributed xoshiro state words (the generator author's own recipe).
        var sm = masterSeed ^ (ulong)workerId;
        SpinRng r;
        r._s0 = SplitMix64(ref sm);
        r._s1 = SplitMix64(ref sm);
        r._s2 = SplitMix64(ref sm);
        r._s3 = SplitMix64(ref sm);
        return r;
    }

    /// <summary>
    /// Demo seam: the naive seeding the episode argues against — the master seed and the
    /// worker id added, with no mixing step. Exists so the demo page can put correlated
    /// streams next to mixed ones on the same screen.
    /// </summary>
    public static SpinRng ForWorkerUnmixed(ulong masterSeed, int workerId)
    {
        SpinRng r;
        r._s0 = masterSeed + (ulong)workerId;
        r._s1 = masterSeed + (ulong)workerId + 1;
        r._s2 = masterSeed + (ulong)workerId + 2;
        r._s3 = masterSeed + (ulong)workerId + 3;
        return r;
    }

    /// <summary>The four state words, for display only.</summary>
    public readonly (ulong S0, ulong S1, ulong S2, ulong S3) State => (_s0, _s1, _s2, _s3);

    public static ulong SplitMix64(ref ulong state)
    {
        var z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>xoshiro256** next value.</summary>
    public ulong NextUInt64()
    {
        var result = ulong.RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = ulong.RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform integer in [0, bound) using Lemire's multiply-shift with rejection.</summary>
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);

        var range = (ulong)bound;
        var threshold = unchecked(0UL - range) % range;
        while (true)
        {
            var product = (UInt128)NextUInt64() * range;
            if ((ulong)product >= threshold)
                return (int)(product >> 64);
        }
    }

    /// <summary>
    /// Demo seam: the modulo reduction the episode rejects, kept as the named counterpart
    /// to NextInt. The bias lab narrows the draw space itself, so this method currently has
    /// no callers.
    /// </summary>
    public int NextIntModulo(int bound) => (int)(NextUInt64() % (ulong)bound);

    /// <summary>Uniform double in [0, 1) with 53 random bits.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
}
