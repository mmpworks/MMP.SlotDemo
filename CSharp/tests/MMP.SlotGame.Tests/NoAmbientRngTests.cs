using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Tests;

/// <summary>
/// INVARIANT R3 (RT-15) — randomness in Core exists ONLY as <c>ref SpinRng</c>.
///
/// This is a source scan, not a behavioural test, and that is the point: AC-6 says the
/// same seed must give the same result, and no amount of seeded-run testing can prove
/// that when a single <c>Random.Shared.Next()</c> or <c>DateTime.UtcNow</c> hiding in a
/// rarely-taken branch would break it. The structural check is the one that holds.
///
/// SpinRng.cs is the sanctioned home of the generator itself and is excluded.
/// </summary>
[Trait("Category", "Fast")]
public sealed class NoAmbientRngTests
{
    private static readonly string[] BannedTokens =
    [
        "Random.Shared",
        "new Random(",
        "Guid.NewGuid",
        "DateTime.Now",
        "DateTime.UtcNow",
    ];

    private const string SanctionedFile = "SpinRng.cs";

    [Fact]
    public void CoreSourceContainsNoAmbientRandomnessOrClock()
    {
        var coreRoot = LocateCoreSource();
        var files = Directory
            .EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsHandWrittenSource)
            .ToArray();

        Assert.True(files.Length >= 10, $"Only {files.Length} Core source files found under '{coreRoot}' — the scan is not seeing the assembly.");

        var violations = new List<string>();
        foreach (var file in files)
        {
            if (Path.GetFileName(file).Equals(SanctionedFile, StringComparison.OrdinalIgnoreCase)) continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var token in BannedTokens)
                {
                    if (!lines[i].Contains(token, StringComparison.Ordinal)) continue;
                    violations.Add($"{Path.GetFileName(file)}:{i + 1}  [{token}]  {lines[i].Trim()}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Invariant R3 broken — ambient randomness or clock access in Core:\n  " +
            string.Join("\n  ", violations));
    }

    /// <summary>
    /// The complement of the scan: the generator is a mutable struct passed by ref, so a
    /// copy cannot silently fork a stream. If SpinRng ever becomes a class or a readonly
    /// struct, the ref-parameter discipline stops meaning anything.
    /// </summary>
    [Fact]
    public void SpinRng_IsAMutableStruct_SoRefPassingIsLoadBearing()
    {
        var type = typeof(SpinRng);

        Assert.True(type.IsValueType, "SpinRng must be a struct; a class would share state across workers.");
        Assert.False(
            type.IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), inherit: false),
            "SpinRng must not be readonly — advancing the stream mutates it in place.");
    }

    [Fact]
    public void SpinRng_ForWorker_GivesEachWorkerADistinctStream()
    {
        const ulong master = 0xDEAD_BEEF_0000_0001UL;
        var firstDraws = new Dictionary<int, ulong[]>();

        for (var workerId = 0; workerId < 64; workerId++)
        {
            var rng = SpinRng.ForWorker(master, workerId);
            firstDraws[workerId] = [rng.NextUInt64(), rng.NextUInt64(), rng.NextUInt64(), rng.NextUInt64()];
        }

        // 64 workers, 4 words each: every prefix must be unique. Adjacent-seed
        // correlation (masterSeed + workerId) is precisely what RT-14 rejected.
        var prefixes = firstDraws.Values.Select(d => string.Join(",", d)).ToHashSet();
        Assert.Equal(64, prefixes.Count);
    }

    [Fact]
    public void SpinRng_ForWorker_IsReproducible()
    {
        var a = SpinRng.ForWorker(12345UL, 7);
        var b = SpinRng.ForWorker(12345UL, 7);

        for (var i = 0; i < 1_000; i++)
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
    }

    [Fact]
    public void SpinRng_NextInt_StaysInBounds()
    {
        var rng = SpinRng.ForWorker(99UL, 1);
        foreach (var bound in new[] { 1, 2, 3, 22, 32, 64, 72, 128 })
        {
            for (var i = 0; i < 20_000; i++)
            {
                var value = rng.NextInt(bound);
                Assert.InRange(value, 0, bound - 1);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SpinRng_NextInt_RejectsNonPositiveBounds(int bound)
    {
        var rng = SpinRng.ForWorker(99UL, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(bound));
    }

    [Fact]
    public void SpinRng_NextDouble_StaysInTheUnitInterval()
    {
        var rng = SpinRng.ForWorker(0UL, 0);
        for (var i = 0; i < 200_000; i++)
        {
            var value = rng.NextDouble();
            Assert.True(value >= 0.0 && value < 1.0, $"NextDouble returned {value:R}");
        }
    }

    private static bool IsHandWrittenSource(string path)
    {
        var normalized = path.Replace('\\', '/');
        return !normalized.Contains("/obj/", StringComparison.Ordinal)
            && !normalized.Contains("/bin/", StringComparison.Ordinal);
    }

    private static string LocateCoreSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "MMP.SlotGame.Core");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate src/MMP.SlotGame.Core walking up from '{AppContext.BaseDirectory}'.");
    }
}
