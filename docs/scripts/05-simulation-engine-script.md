# Episode 5 — The Engine: Ten Million Spins, Same Answer Every Time

**Target:** 24–26 min. **Format:** create the file, paste the finished source, then
walk it. The typing is a jump cut; the walkthrough is the episode.
**Subject:** the engine. The companion site appears three times, for under three
minutes total, and only to make an engine claim visible.
**Companion article:** `docs/articles/05-simulation-engine.md`
**Companion site:** MMP.SlotDemo, branch `main`, page `#/ch05`

> **Discipline note for this recording.** The labs illustrate; they do not carry the
> episode. If a beat can be made in Rider, make it in Rider. Cut to the browser only
> where the engine's behaviour is easier to see than to describe, and cut back inside
> a minute.

---

## Prep checklist

**Repo — the subject**
- [ ] Rider on `MMP.SlotGame.slnx`, tree expanded to `MMP.SlotGame.Core`
- [ ] `Simulation/` folder present with `SpinRng.cs` and `SimulationConfig.cs`; the two
      target files moved aside so they get created on camera
- [ ] Test runner loaded: `ConcurrencyTests`, `DeterminismTests`, `StressTests`
- [ ] A terminal ready to run a ten-million-spin run twice and diff the totals
- [ ] Clipboard manager staged with Block A, then Block B
- [ ] Task Manager or a CPU meter on a second monitor for the all-cores-busy shot

**Companion site — the illustration**
- [ ] `E:\dev\MMP.SlotDemo`, branch `main`
- [ ] `cd CSharp/web && npm run build`, then `dotnet run --project CSharp/src/SlotDemo.Server`
- [ ] `http://localhost:5090/#/ch05`, the engine lab and the telemetry lab each run once
- [ ] `logs/` cleared so the viewer starts empty

**OBS**
- [ ] Scenes: `RIDER`, `BROWSER`, `TERMINAL`
- [ ] Zoom-to-mouse hotkey bound and tested at code-reading zoom
- [ ] Rider font sized for capture

---

## 0:00–1:15 — Cold open

**Scene:** TERMINAL, then RIDER with the `Simulation` folder half empty.

- "Ten million spins across every core, and the total comes out the same every single
  time, down to the last millicent."
- "That sentence is the episode. It sounds like it should require locks and careful
  ordering, and it turns out to require the opposite: no coordination at all, because
  episodes 2 and 4 already removed the reasons to coordinate."
- "Two files. One holds the counters, one holds the schedule. About 240 lines together,
  and half of that is delegates and doc comments."
- Set the format: "Each file goes in finished, then we walk it and I tell you why every
  line is the way it is."

## 1:15–3:30 — The three decisions that make this hard, and how they were already made

**Scene:** RIDER, a comment block or the whiteboard from episode 1.

Write three problems and their answers before any code appears.

1. **Who adds up the money?** Sixteen workers finishing in scheduler order. If the total
   were a `double`, the answer would depend on finish order and no lock would fix it.
   Integer millicents from episode 2 make addition order-independent, so the counters
   can be lock-free.
2. **Who gets which spins?** Dynamic scheduling steals work, so the same spin lands on a
   different worker between runs, and each worker has its own random stream. Fixed
   quotas, assigned up front, keep the partition stable.
3. **Who waits for the browser?** Nobody. The telemetry lane is bounded and lossy, and a
   sample is written with a call that can decline rather than one that blocks.

"Every one of those is a decision made in an earlier episode. Today they get spent."

## 3:30–4:15 — Create the first file

**Scene:** RIDER.

- New file in `Simulation`. **Path on screen and said out loud:**
  `src/MMP.SlotGame.Core/Simulation/RunTotals.cs`
- Paste **Block A**. "Forty-six lines, and it is the entire lossless side of the
  pipeline."

### Block A — `src/MMP.SlotGame.Core/Simulation/RunTotals.cs`

```csharp
namespace MMP.SlotGame.Core.Simulation;

/// <summary>
/// The lossless side of the pipeline: integer millicent counters, batched
/// Interlocked adds (architecture §7). Reset (RT-18) is done by swapping in a fresh
/// instance, never by zeroing live fields.
/// </summary>
public sealed class RunTotals
{
    private long _spins;
    private long _wageredMillicents;
    private long _returnedMillicents;
    private long _hits;

    /// <summary>One batched contribution from a worker (four adds per ~4096 spins, not per spin).</summary>
    public void AddBatch(long spins, long wageredMillicents, long returnedMillicents, long hits)
    {
        Interlocked.Add(ref _spins, spins);
        Interlocked.Add(ref _wageredMillicents, wageredMillicents);
        Interlocked.Add(ref _returnedMillicents, returnedMillicents);
        Interlocked.Add(ref _hits, hits);
    }

    /// <summary>
    /// Counter reads are individually atomic, not atomic as a set (RT-20): a mid-run
    /// snapshot can straddle a batch and is display-only. Acceptance assertions read
    /// AFTER the run quiesces (post-WhenAll), where this is exact.
    /// </summary>
    public RunSnapshot Snapshot() => new(
        Interlocked.Read(ref _spins),
        Interlocked.Read(ref _wageredMillicents),
        Interlocked.Read(ref _returnedMillicents),
        Interlocked.Read(ref _hits));
}

public readonly record struct RunSnapshot(long Spins, long WageredMillicents, long ReturnedMillicents, long Hits)
{
    public double MeasuredRtp => WageredMillicents == 0 ? 0 : (double)ReturnedMillicents / WageredMillicents;
    public double HitFrequency => Spins == 0 ? 0 : (double)Hits / Spins;
}

/// <summary>
/// One telemetry message. Carries ABSOLUTE snapshots, never deltas (RT-19): a dropped
/// sample costs one chart point, not accuracy.
/// </summary>
public readonly record struct TelemetrySample(string RunId, RunSnapshot Totals);
```

## 4:15–9:30 — Walk `RunTotals`

**Scene:** RIDER throughout. Zoom on each region as it comes up.

### Beat 1 — four `long` fields and no lock

The whole shared mutable state of a ten-million-spin run is four 64-bit integers.

- `Interlocked.Add` is a single atomic instruction on each. No `lock`, no monitor, no
  contention on an object header.
- **Why this is available at all:** integer addition is order-independent, so sixteen
  workers arriving in any order produce the same total. "A lock would have made the
  order deterministic. Not needing the order to be deterministic is better than making
  it so."
- Say the counterfactual out loud: with a `double` total, `Interlocked` has no
  `Add(ref double)` that helps, and even a lock would only serialize the additions
  without making them associative. "The concurrency problem was solved in episode 2 by a
  decision about arithmetic."

### Beat 2 — batching, and the number 4096

The method is `AddBatch`, and the doc comment says four adds per roughly four thousand
spins rather than four adds per spin.

- Ten million spins with per-spin atomics is forty million atomic operations, every one
  of them a cache line bouncing between cores.
- Batched, it is about ten thousand atomic operations for the whole run. The per-spin
  accumulation happens in worker-local `long` variables that live in registers.
- **Why 4096 and not larger:** the batch size also sets the telemetry granularity and
  the cancellation granularity. Bigger batches make the chart coarser and cancellation
  slower to respond. "The number is a three-way trade, and it is a named constant so
  the trade has one place to be revisited."
- **The general lesson:** "The fix for atomic contention is usually fewer atomics rather
  than faster ones."

### Beat 3 — the honest comment on `Snapshot`

Read it aloud. The four reads are individually atomic and they are not atomic as a set.

- A mid-run snapshot can straddle a batch, so spins and money can be a few thousand
  spins out of step with each other.
- The comment says what that means: mid-run snapshots are display-only, and the
  assertions read after every worker has joined, where the numbers are exact.
- **Why this beats making it atomic as a set:** a consistent set would need a lock
  around all four counters, taken on every batch, to make a chart frame slightly
  prettier. "The cost lands on the hot path and the benefit lands on a picture nobody is
  measuring."
- "This is the kind of comment that separates a design from an accident. Somebody knew
  the guarantee was partial, wrote down where it holds, and pointed at the place the
  strong guarantee is actually needed."

### Beat 4 — reset by replacement

The class comment says reset is done by swapping in a fresh instance rather than zeroing
live fields.

- Zeroing four fields while workers might still be adding to them is a race with no
  correct outcome.
- A new `RunTotals` has no relationship to the old one, so there is nothing to
  coordinate. The old instance keeps its numbers, and anyone still holding it sees a
  consistent past.
- **The immutability rule, applied to a mutable object:** "The object is mutable by
  necessity. Its lifetime is not. Replacing beats resetting whenever the reset would
  need a lock the mutation does not."

### Beat 5 — snapshots are absolute, and that is what makes dropping safe

`TelemetrySample` carries a whole `RunSnapshot` rather than a difference.

- A dropped delta corrupts every number after it, forever. A dropped snapshot costs one
  chart point, and the next sample is already correct.
- **The line:** "The lossy lane is only safe because of what it carries. Change the
  payload to deltas and every other telemetry decision in this system becomes wrong at
  once."
- `RunSnapshot` is a `readonly record struct` with two computed properties. Measured RTP
  and hit frequency are derived on read rather than stored, so a snapshot cannot carry a
  total and a ratio that disagree.

> **Illustration (40 seconds, BROWSER).** Chapter 5 page, telemetry lab. Run with the
> consumer throttled hard. The drop counter climbs into the thousands while the spin
> total stays on pace, and the chart keeps its shape. Then point at the final row: after
> the run quiesces, the last sample matches the counters. "Thousands of dropped frames,
> and the number at the end is still the number." Cut back.

## 9:30–10:15 — Create the second file

**Scene:** RIDER.

- New file. **Path on screen:** `src/MMP.SlotGame.Core/Simulation/SimulationEngine.cs`
- Paste **Block B**. "Five delegates, three constructors, and two methods that do the
  work."

### Block B — `src/MMP.SlotGame.Core/Simulation/SimulationEngine.cs`

```csharp
using System.Threading.Channels;
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paylines;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Simulation;

/// <summary>Per-spin diagnostic hook (architecture §4). Null by default and therefore free; NOT for 10M-spin runs.</summary>
public delegate void SpinObserver(in SpinOutcome outcome);

/// <summary>The seeding policy as a one-behaviour seam: tests inject scripted streams with one lambda.</summary>
public delegate SpinRng SpinRngFactory(int workerId);

/// <summary>
/// Plays ONE spin: draw, evaluate, award. RNG arrives by ref (invariant R3), so the
/// stream advances in the caller's worker.
/// </summary>
public delegate SpinOutcome SpinPlay(ref SpinRng rng);

/// <summary>
/// Builds one <see cref="SpinPlay"/> per worker. This is the seam that lets a game with
/// its own evaluation rules — wilds, scatter-triggered bonuses, honest pick simulation —
/// reuse the determinism, quota partitioning, batching and telemetry below instead of
/// re-implementing them (OrcaDive is the first such game). A worker's play owns its
/// own scratch buffers, which is why this is a factory and not one shared instance.
/// </summary>
public delegate SpinPlay SpinPlayFactory();

public readonly record struct SpinOutcome(Millicents Wagered, Millicents BasePayout, Millicents FeaturePayout)
{
    public Millicents Total => BasePayout + FeaturePayout;
    public bool IsHit => Total.Value > 0;
}

/// <summary>
/// What the engine needs in order to schedule a run: identity, seed, worker count, quota.
/// <see cref="SimulationConfig"/> exposes one of these; a game whose geometry does not fit
/// the preset shape (ragged strip lengths, a fixed published paytable) supplies its own.
/// </summary>
public sealed record RunPlan(string RunId, ulong MasterSeed, int WorkerCount, long TargetSpins);

/// <summary>
/// Runs spins on logical workers with fixed, pre-assigned quotas. For a fixed game
/// definition, code version, target spin count, master seed, and worker count, the result
/// is reproducible. Changing the worker count changes the RNG partition.
/// </summary>
public sealed class SimulationEngine
{
    private const int BatchSize = 4096;

    private readonly RunPlan _plan;
    private readonly SpinRngFactory _streamFactory;
    private readonly SpinPlayFactory _playFactory;

    /// <summary>The stock composition: preset strips + line-pay evaluator + independent feature schedules.</summary>
    public SimulationEngine(SimulationConfig config, ScaledPaytable paytable, SpinRngFactory streamFactory)
        : this(
            config.Plan,
            config.Preset.BuildReels(),
            config.Preset.Paylines,
            paytable,
            config.Features,
            SimulationConfig.Wager,
            streamFactory)
    {
    }

    /// <summary>Run a compiled stock game without rebuilding its shared game data.</summary>
    public SimulationEngine(
        RunPlan plan,
        StripReelSet reels,
        IReadOnlyList<Payline> lines,
        ScaledPaytable paytable,
        IReadOnlyList<Features.FeatureSchedule> features,
        Millicents wager,
        SpinRngFactory streamFactory)
        : this(plan, streamFactory, StockPlayFactory(reels, lines, paytable, features, wager))
    {
    }

    /// <summary>A game that brings its own spin rules supplies the play; everything else is shared.</summary>
    public SimulationEngine(RunPlan plan, SpinRngFactory streamFactory, SpinPlayFactory playFactory)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _plan = plan;
        _streamFactory = streamFactory;
        _playFactory = playFactory;
    }

    public RunTotals Totals { get; } = new();

    /// <summary>
    /// Telemetry goes to a caller-owned writer (lossy, bounded, drop-oldest at the
    /// caller's choice) — the exact math never depends on it. Returns the quiesced
    /// final snapshot after all workers join.
    /// </summary>
    public async Task<RunSnapshot> RunAsync(
        ChannelWriter<TelemetrySample>? telemetry,
        SpinObserver? observer = null,
        CancellationToken ct = default)
    {
        var workers = new Task[_plan.WorkerCount];
        var spinsPerWorker = _plan.TargetSpins / _plan.WorkerCount;
        var remainder = _plan.TargetSpins % _plan.WorkerCount;

        for (var w = 0; w < _plan.WorkerCount; w++)
        {
            var workerId = w;
            // Deterministic quota: worker 0 absorbs the remainder.
            var quota = spinsPerWorker + (workerId == 0 ? remainder : 0);
            workers[workerId] = Task.Run(
                () => WorkerLoop(workerId, quota, telemetry, observer, ct), ct);
        }

        await Task.WhenAll(workers).ConfigureAwait(false);
        var final = Totals.Snapshot();
        telemetry?.TryWrite(new TelemetrySample(_plan.RunId, final));
        telemetry?.TryComplete();
        return final;
    }

    private void WorkerLoop(
        int workerId,
        long quota,
        ChannelWriter<TelemetrySample>? telemetry,
        SpinObserver? observer,
        CancellationToken ct)
    {
        var rng = _streamFactory(workerId);
        var play = _playFactory();

        long done = 0;
        while (done < quota)
        {
            if (ct.IsCancellationRequested) return; // per batch, not per spin

            var batch = (int)Math.Min(BatchSize, quota - done);
            long batchWagered = 0, batchReturned = 0, batchHits = 0;

            for (var i = 0; i < batch; i++)
            {
                var outcome = play(ref rng);

                batchWagered += outcome.Wagered.Value;
                var total = outcome.Total;
                batchReturned += total.Value;
                if (total.Value > 0) batchHits++;

                if (observer is not null) observer(in outcome);
            }

            Totals.AddBatch(batch, batchWagered, batchReturned, batchHits);
            done += batch;

            // Absolute snapshot per batch; TryWrite -> dropped under load, by design.
            telemetry?.TryWrite(new TelemetrySample(_plan.RunId, Totals.Snapshot()));
        }
    }

    /// <summary>
    /// The strips are built ONCE and shared by every worker: <see cref="StripReelSet"/> is
    /// read-only after construction, so sharing costs nothing and keeps every worker on
    /// byte-identical geometry. The scratch window is per-worker.
    /// </summary>
    private static SpinPlayFactory StockPlayFactory(
        StripReelSet reels,
        IReadOnlyList<Payline> lines,
        ScaledPaytable paytable,
        IReadOnlyList<Features.FeatureSchedule> features,
        Millicents wager)
    {
        return () =>
        {
            var evaluator = new LinePayEvaluator(lines, paytable);
            // Symbol carries a string (managed) — no stackalloc; one array per worker, reused for every spin.
            var window = new Symbol[reels.WindowSize];

            return (ref SpinRng rng) =>
            {
                reels.DrawWindow(ref rng, window);
                var basePay = evaluator.Evaluate(window, reels.ReelCount, reels.Rows);
                var featurePay = Millicents.Zero;
                for (var f = 0; f < features.Count; f++)
                    featurePay += features[f].Play(ref rng);
                return new SpinOutcome(wager, basePay, featurePay);
            };
        };
    }
}
```

## 10:15–20:30 — Walk `SimulationEngine`

### Beat 6 — four delegates at the top of the file

Before the class, four one-line contracts. Read each and say what it buys.

- `SpinObserver` — a per-spin diagnostic hook, taking the outcome `in` so no copy
  happens. Null by default.
- `SpinRngFactory` — the seeding policy as a seam. A test injects a scripted stream with
  one lambda and no test double.
- `SpinPlay` — plays one spin, with `ref SpinRng` in the signature. Rule R3 from episode
  2, still visible in the type system.
- `SpinPlayFactory` — builds one play per worker.

**The CUPID reading:** composable. Each of these is one behaviour with no identity and
no lifetime, so each is a delegate rather than an interface. "Four interfaces, four
files, four implementations, and a registration would express the same four ideas and
give the reader four more things to open."

### Beat 7 — the factory, and the reason it is a factory

This is the seam worth the most in the whole file, so slow down.

- A worker's play owns scratch buffers — a window array, an evaluator. Sharing one play
  across sixteen workers would mean sixteen threads writing the same window.
- So the engine takes a factory and calls it once per worker inside the worker loop.
  Each worker gets its own play with its own buffers, and nothing is shared that is
  written.
- **What the seam buys:** a game with its own rules — wilds, scatter-triggered bonuses,
  an honest pick simulation — supplies a play and inherits determinism, quota
  partitioning, batching, and telemetry unchanged. "Episode 6's real game walks through
  this door. The engine does not learn a single thing about scatters."
- **AIF reading:** the factory is a one-line shape choice made before there was a second
  game. It cost nothing and it saved the retrofit.

### Beat 8 — three constructors, chaining inward

Point at the chain: config-shaped, game-data-shaped, and play-shaped.

- The outermost is convenience for the stock preset path. The innermost is the real
  contract: a plan, a stream factory, and a play factory.
- Each outer constructor supplies defaults and delegates to the next, so there is one
  place where fields are assigned.
- "The convenience overloads make the common case one line. The inner constructor makes
  the uncommon case possible. Neither one duplicates the other's logic."

### Beat 9 — fixed quotas, and the remainder rule

**Scene:** RIDER, zoomed on the loop in `RunAsync`.

- Divide the target by the worker count, and worker 0 absorbs the remainder. Every
  worker knows its quota before any of them start.
- **Say what this replaces:** `Parallel.For` steals work, so which worker plays which
  spin changes between runs. Each worker has its own random stream, so a different
  partition means a different set of spins and a different total.
- "Work stealing would make the run faster on paper and non-reproducible in practice.
  Reproducibility is worth more here than the last few percent of load balance, and the
  quotas are equal to within one spin anyway."
- The remainder going to worker 0 is a rule rather than a convenience. It is what makes
  a target of ten million and one spins run exactly ten million and one.

### Beat 10 — the worker loop, from the outside in

Walk it as four layers.

1. **Per worker:** a stream from the factory and a play from the factory. Both local.
2. **Per batch:** the cancellation check, and the comment saying why it is here rather
   than per spin. A branch per spin on a token that is almost never set is real cost in
   a loop this hot, and a batch of four thousand spins is sub-millisecond
   responsiveness.
3. **Per spin:** three local `long` accumulators and an optional observer call. Nothing
   atomic, nothing shared, nothing allocated.
4. **After the batch:** four atomic adds and one `TryWrite`.

**The shape to name out loud:** "Everything expensive is outside the inner loop and
everything shared is outside it too. The inner loop touches worker-local memory only."

### Beat 11 — `observer is not null`, and a hook that is free when unused

The null check costs a predicted branch per spin, and the delegate is never allocated
when nobody passes one.

- "The alternative shapes are a no-op observer instance, which pays an indirect call ten
  million times, or an event, which pays an invocation-list walk. Nullable costs a
  branch the predictor gets right every time."
- The doc comment says it plainly: null by default and therefore free, and not for
  ten-million-spin runs when it is set. "The hook exists for a diagnostic run of a few
  thousand spins. Saying so in the comment is how the next person avoids using it wrong."

### Beat 12 — `TryWrite`, and the direction the pressure flows

The one line that keeps a laptop from slowing down a simulation.

- `TryWrite` returns false when the channel is full and the sample is gone. The worker
  never awaits.
- The channel is caller-owned and bounded with drop-oldest, so the newest picture wins
  and the buffer never grows.
- "Await here and the browser gets a lever on the simulation. That is backpressure
  flowing the wrong direction: the lossy lane would be throttling the exact lane."
- The final write after `WhenAll` is the one that matters, and it goes out after every
  worker has joined, so it is the quiesced total.

### Beat 13 — `StockPlayFactory`, and what is shared versus what is copied

Read the doc comment, then the closure.

- The strips are built once and shared by every worker, because `StripReelSet` is
  read-only after construction. Sharing read-only data across threads costs nothing and
  guarantees byte-identical geometry.
- The window array is per worker, created inside the factory lambda and reused for every
  spin. One allocation per worker for the whole run.
- The comment explains why there is no `stackalloc`: `Symbol` carries a string, so the
  array is managed. "The comment answers the question a reader would otherwise spend ten
  minutes on."
- Then the play itself, four statements: draw the window, evaluate the lines, play each
  feature in schedule order, return the outcome. **The order is part of the contract** —
  swap the draw and the features and every stream desynchronizes while nothing looks
  wrong.

> **Illustration (45 seconds, BROWSER).** Chapter 5 page, engine lab. Run the same seed
> at 1, 4, and 16 workers and put the three final snapshots side by side. The one-worker
> and sixteen-worker totals differ, because the partition differs and each worker draws
> its own stream. Then run each configuration twice: every configuration reproduces
> itself exactly. "The contract is the pair — seed and worker count — and the lab is
> showing both halves of it." Cut back.

## 20:30–21:15 — Prove it in the terminal

**Scene:** TERMINAL, CPU meter visible.

- Run ten million spins. Every core pegged, about a second.
- Run it again with the same seed and worker count, and diff the two totals. Identical,
  field for field.
- Change the seed by one and run again. Different totals. "Determinism that ignores the
  seed is not determinism. The next segment has a test for that."

## 21:15–24:30 — The tests are part of the design

**Scene:** RIDER test runner, then TERMINAL.

This section is the payoff for every decision above, so give it real time.

- **`ConcurrencyTests.ParallelRun_EqualsSequentialReplication_BitForBit`** is invariant
  M2 collecting. The test replicates the engine's contract by hand — the same quota
  rule, the same per-worker seeding, and the same RNG consumption order — then asserts
  exact equality against a real parallel run. **Why exact rather than approximate:** the
  class comment says it. If this ever passes only within a tolerance, floating point has
  leaked into the accumulation path and invariant M1 is broken. "The assertion operator
  is the alarm."
- **`ParallelRun_IsRaceFreeUnderRepetition`** runs the eight-worker equivalence several
  times. **Why repetition:** a torn accumulator is intermittent by nature, and one green
  run is not evidence that the atomics did their job under contention.
- **`ParallelRun_EqualsSequentialReplication_AtProcessorCount`** does the same at one
  worker per logical core, which is maximum contention on the four counters.
- **`SpinObserver_FiresOncePerSpin_AndReconcilesWithTheCounters`** guards beat 11. **Why
  it matters:** the observer is what a diagnostic run trusts, so a hook that double-fires
  or drops makes the diagnostic lie about the game.
- **`DeterminismTests.SameSeedAndWorkerCount_ProducesIdenticalSnapshots`** states the
  contract, and **`DifferentSeed_ProducesDifferentTotals`** tests the negative space.
  "Test only the first one and a generator stuck on a constant passes for a reliable
  one."
- **`DifferentWorkerCounts_AllConvergeOnTheSameAnalyticRtp`** is the subtle one. The suite
  deliberately declines to assert that different worker counts give identical totals,
  because they should not — repartitioning changes which spins exist. What must hold is
  that every partition converges on the same game, and that is what this asserts, with a
  band wide on purpose. **Why the wide band:** this test is asking "same game", rather
  than "converged". Episode 7 asks the second question.
- **`WorkerQuotasCoverTheTargetExactly_IncludingTheRemainder`** pins beat 9. An
  off-by-remainder here would quietly shrink every run whose spin count is not a multiple
  of the worker count, and nothing would report it.
- **`StressTests`** covers the boundary between the exact lane and the lossy one:
  **`CancellationMidRun_LeavesConsistentPartialTotals`** asks for self-consistency rather
  than merely not crashing, **`FloodedDropOldestTelemetry_DoesNotChangeTheFinalSnapshot`**
  and **`SlowConsumer_NeverStallsTheSimulation`** are beat 12 from both sides, and
  **`ResetByNewInstance_IsolatesRuns`** is beat 4.
- Run all three classes. Green.

## 24:30–25:30 — Wrap

- Two files. Counters that need no lock because the arithmetic is associative, and a
  scheduler that needs no coordination because the quotas are fixed.
- The three claims: batched atomics keep the hot path worker-local, fixed quotas keep the
  partition reproducible, and `TryWrite` keeps the pressure flowing away from the
  simulation.
- "None of this required clever concurrency. It required earlier episodes making
  decisions that removed the need for it."
- Next: "Games as data. A real published machine loaded from a JSON file, with ragged
  strips and a scatter bonus that breaks one of the assumptions we have been leaning on."

---

## Recording notes

- Engine-to-browser budget: roughly twenty-two minutes in Rider, the terminal, and the
  test runner; under three in the browser. If a take runs long, browser time goes first.
- Strongest visuals in order: the CPU meter pegged during a ten-million-spin run, the
  two identical totals side by side in the terminal, and the drop counter climbing while
  the final number stays correct. Give each a beat of silence.
- Zoom hotkey belongs on: the four `Interlocked.Add` lines, the quota and remainder
  calculation, the `TryWrite` line with its comment, and the four statements inside the
  play closure.
- The two paste blocks are the finished files verbatim. If a paste lands wrong, cut and
  re-paste rather than hand-fixing — the file has to match the repo.
- Running long? Compress beat 8 (the constructor chain) to one sentence and drop the
  terminal proof, since the tests cover it. Keep beat 7 (the play factory), beat 9
  (fixed quotas), beat 12 (`TryWrite`), and the test section whole.
- The companion site runs the engine's own code server-side, so if a lab ever disagrees
  with the walkthrough, the lab is reporting a real change in the repo.
