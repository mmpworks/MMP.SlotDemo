using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Simulation;
using MMP.SlotGame.Tests.Support;

namespace MMP.SlotGame.Tests;

[Trait("Category", "Fast")]
public sealed class GameRunnerTests
{
    [Fact]
    public async Task ReusingRunner_DoesNotCarryComponentTalliesIntoTheNextRun()
    {
        var definition = GameFiles.Load(GameFiles.ClassicThreeReel);
        var plan = new RunPlan("runner-reuse", 0x1234_5678UL, WorkerCount: 4, TargetSpins: 50_000);
        var runner = new GameRunner(definition, plan);

        var first = await runner.RunAsync();
        var second = await runner.RunAsync();

        Assert.Equal(first.Totals, second.Totals);
        Assert.Equal(first.LineMillicents, second.LineMillicents);
        Assert.Equal(first.BonusMillicents, second.BonusMillicents);
        Assert.Equal(first.LineHits, second.LineHits);
        Assert.Equal(first.BonusTriggers, second.BonusTriggers);
    }
}
