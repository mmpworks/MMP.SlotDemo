using System.Diagnostics;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Tests.Support;
using Xunit.Abstractions;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Manual throughput measurements. These are reports rather than pass/fail performance
/// requirements because results depend on the machine, power plan, and concurrent work.
/// </summary>
[Trait("Category", "Slow")]
public sealed class PerformanceBaselineTests(ITestOutputHelper output)
{
    [SlowFact]
    public async Task Preset_ReportsReleaseThroughputAcrossFiveRuns()
    {
        const long spins = 30_000_000;
        var rates = new double[5];

        for (var sample = 0; sample < rates.Length; sample++)
        {
            var game = TestGame.Build(
                TestGame.DefaultPreset,
                masterSeed: 0xBADC0FFEEUL + (ulong)sample,
                workerCount: 8,
                targetSpins: spins);

            var clock = Stopwatch.StartNew();
            var result = await game.Engine().RunAsync(telemetry: null);
            clock.Stop();

            Assert.Equal(spins, result.Spins);
            rates[sample] = spins / clock.Elapsed.TotalSeconds;
            output.WriteLine($"sample {sample + 1}: {rates[sample]:N0} spins/second ({clock.Elapsed.TotalMilliseconds:N1} ms)");
        }

        Array.Sort(rates);
        output.WriteLine($"median: {rates[rates.Length / 2]:N0} spins/second");
    }

    [SlowFact]
    public async Task LoadedGame_ReportsReleaseThroughputAcrossFiveRuns()
    {
        const long spins = 10_000_000;
        var definition = GameFiles.Load(GameFiles.OrcaDive);
        var analysis = GameAnalyzer.Analyze(definition);
        var rates = new double[5];

        for (var sample = 0; sample < rates.Length; sample++)
        {
            var plan = new RunPlan($"perf-{sample}", 0xC0FFEEUL + (ulong)sample, 8, spins);
            var clock = Stopwatch.StartNew();
            var result = await new GameRunner(definition, plan, analysis).RunAsync();
            clock.Stop();

            Assert.Equal(spins, result.Totals.Spins);
            rates[sample] = spins / clock.Elapsed.TotalSeconds;
            output.WriteLine($"sample {sample + 1}: {rates[sample]:N0} spins/second ({clock.Elapsed.TotalMilliseconds:N1} ms)");
        }

        Array.Sort(rates);
        output.WriteLine($"median: {rates[rates.Length / 2]:N0} spins/second");
    }
}
