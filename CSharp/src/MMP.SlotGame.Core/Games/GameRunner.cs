using System.Collections.Concurrent;
using System.Threading.Channels;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Core.Games;

/// <summary>
/// Where the line money and the feature money came from, plus how often each fired. The
/// engine counters stay the authority on the totals; this only answers which half paid.
///
/// One instance per WORKER, so nothing on the spin path is shared and nothing is interlocked.
/// The run sums them after the workers join, where the engine has already quiesced.
/// </summary>
internal sealed class ComponentTally
{
    public long LineMillicents;
    public long BonusMillicents;
    public long LineHits;
    public long BonusTriggers;
}

/// <summary>The result of a run: engine totals, component split, and the analytic reference.</summary>
public sealed record GameRunResult(
    RunSnapshot Totals,
    long LineMillicents,
    long BonusMillicents,
    long LineHits,
    long BonusTriggers,
    GameAnalysis Analytic,
    EngineTimings Timings)
{
    public double LineRtp => Totals.WageredMillicents == 0
        ? 0
        : (double)LineMillicents / Totals.WageredMillicents;

    public double BonusRtp => Totals.WageredMillicents == 0
        ? 0
        : (double)BonusMillicents / Totals.WageredMillicents;

    public double LineHitFrequency => Totals.Spins == 0 ? 0 : (double)LineHits / Totals.Spins;

    public double BonusTriggerFrequency => Totals.Spins == 0 ? 0 : (double)BonusTriggers / Totals.Spins;
}

/// <summary>
/// Runs any loaded <see cref="GameDefinition"/> on the shared <see cref="SimulationEngine"/>.
/// All this class supplies is the per-spin play; the fixed worker quotas, the per-worker
/// SplitMix64 streams, the batched counters and the telemetry are the engine, unchanged.
/// Keeping that machinery in one place prevents loaded games from developing a different
/// scheduling or determinism policy.
/// </summary>
public sealed class GameRunner(
    GameDefinition definition,
    RunPlan plan,
    GameAnalysis? preparedAnalysis = null)
{
    public async Task<GameRunResult> RunAsync(
        ChannelWriter<TelemetrySample>? telemetry = null,
        CancellationToken ct = default)
    {
        ConcurrentBag<ComponentTally> tallies = [];

        // Build the outcome tables BEFORE any worker starts.
        //
        // CreatePlay reads definition.ProgressiveOutcomes, and the engine calls CreatePlay
        // from inside each worker. Left cold, the first worker to arrive builds an
        // exhaustive enumeration of the whole game while every other worker blocks on the
        // same Lazy. That put a full enumeration inside the worker phase, where it was
        // charged to the run as though it were spinning: a measured spin loop of 148M
        // spins/s reported as 7.7M once the wait was included.
        _ = definition.ProgressiveOutcomes;

        var engine = new SimulationEngine(
            plan,
            workerId => SpinRng.ForWorker(plan.MasterSeed, workerId),
            () => CreatePlay(tallies));

        var totals = await engine.RunAsync(telemetry, observer: null, ct).ConfigureAwait(false);

        long lineMillicents = 0, bonusMillicents = 0, lineHits = 0, bonusTriggers = 0;
        foreach (var tally in tallies)
        {
            lineMillicents += tally.LineMillicents;
            bonusMillicents += tally.BonusMillicents;
            lineHits += tally.LineHits;
            bonusTriggers += tally.BonusTriggers;
        }

        // A server request may calculate the exact result before starting the run so it
        // can draw the analytic band immediately. Reuse that result instead of enumerating
        // the same game a second time after the simulation finishes.
        var analytic = preparedAnalysis ?? GameAnalyzer.Analyze(definition);
        return new GameRunResult(
            totals, lineMillicents, bonusMillicents, lineHits, bonusTriggers, analytic, engine.Timings);
    }

    /// <summary>
    /// One spin: draw the window, pay every payline, and if the feature triggers, play it
    /// for real. Pays are the real multiplier × <see cref="Millicents.ScaleFactor"/>, and
    /// <see cref="Millicents.ScaledMultiply"/> divides that out with no remainder.
    ///
    /// The feature draws from the same worker stream as the reels: one stream per worker,
    /// advanced in a fixed order. That is why the bonus is played inline instead of batched
    /// for later.
    /// </summary>
    private SpinPlay CreatePlay(ConcurrentBag<ComponentTally> tallies)
    {
        var tally = new ComponentTally();
        tallies.Add(tally);

        var reels = definition.Reels;
        var progressiveOutcomes = definition.ProgressiveOutcomes;
        var bonus = definition.Bonus;
        var wager = SimulationConfig.Wager;
        var stops = new byte[reels.ReelCount];
        var scratch = new int[bonus?.Bonus.GiftCount ?? 0];

        return (ref SpinRng rng) =>
        {
            reels.DrawStops(ref rng, stops);

            var multiplier = 0;
            if (progressiveOutcomes.TryGetValue(stops, out var outcome) && outcome is not null)
                multiplier = outcome.TotalMultiplier;
            var linePay = wager.ScaledMultiply(multiplier);

            var bonusPay = Millicents.Zero;
            if (bonus is not null && outcome?.TriggeredFeatures.Count > 0)
            {
                bonusPay = wager * bonus.Bonus.Play(ref rng, scratch);
                tally.BonusTriggers++;
            }

            if (multiplier > 0) tally.LineHits++;
            tally.LineMillicents += linePay.Value;
            tally.BonusMillicents += bonusPay.Value;

            return new SpinOutcome(wager, linePay, bonusPay);
        };
    }
}
