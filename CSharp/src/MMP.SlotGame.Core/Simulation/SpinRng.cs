namespace MMP.SlotGame.Core.Simulation;

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

    private static ulong SplitMix64(ref ulong state)
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

    /// <summary>
    /// Uniform integer in [0, <paramref name="bound"/>) using Lemire's rejection method
    /// (Lemire 2019, "Fast Random Integer Generation in an Interval").
    ///
    /// Why rejection at all: 2⁶⁴ raw values folded into a bound that does not divide 2⁶⁴
    /// leaves a remainder — 2⁶⁴ mod bound values that cannot complete a full lap — so a
    /// plain remainder mapping makes that many outcomes more likely than the rest. The
    /// fix discards exactly that leftover. The rejected set is a pure function of the
    /// bound: fixed in advance, identical on every machine, and decided before the raw
    /// bits mean anything — it can never see stops, symbols, or payouts.
    ///
    /// The threshold computed here IS that leftover count: (2⁶⁴ − range) % range ==
    /// 2⁶⁴ mod range (the 0UL wraparound stands in for 2⁶⁴, which no ulong can hold).
    /// This is the scheme's only division; per-draw work is a multiply and a compare.
    /// </summary>
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);

        var range = (ulong)bound;
        var threshold = unchecked(0UL - range) % range;
        return NextInt(range, threshold);
    }

    /// <summary>
    /// Hot-path form for a range and Lemire rejection threshold calculated when the reel
    /// set was built (see StripReelSet: one threshold per reel, so mixed strip lengths
    /// such as 26 and 29 each trim their own leftover). Both values remain constant for
    /// the life of that reel set.
    ///
    /// Fixed-point view of the multiply: raw/2⁶⁴ is a uniform fraction in [0,1), so
    /// floor(fraction × range) — the bin — is the HIGH 64 bits of the 128-bit product,
    /// with the shift standing in for the division. The LOW 64 bits are the landing
    /// position inside the bin's slice; positions 0..threshold−1 are the per-slice
    /// leftover, so those draws redraw. With threshold &lt; range ≤ a strip length, the
    /// reject zone is ~range/2⁶⁴ of all draws (a 26-stop reel: 16 of 2⁶⁴ ≈ 9e-19), so
    /// the loop body runs once essentially always.
    /// </summary>
    internal int NextInt(ulong range, ulong threshold)
    {
        while (true)
        {
            var product = (UInt128)NextUInt64() * range;
            if ((ulong)product >= threshold)
                return (int)(product >> 64);
        }
    }

    /// <summary>Uniform double in [0, 1) with 53 random bits.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
}
