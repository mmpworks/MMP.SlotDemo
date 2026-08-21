# How a Simulation Run Moves Through the System

The [system diagram](system-design-simplified.svg) shows the components involved
in a simulation run. This guide follows the same path and points to the code at
each handoff. All paths are relative to the repository root.

The main path below loads a game named in the request from `CSharp/games/*.json`.
A preset run starts through the same HTTP and coordination code, then uses the
solver path described under Run Coordinator. Both paths join again at
`GameRunner` and `SimulationEngine`.

---

## 1. Client (Vue SPA)

The SPA builds the request, starts the run, and updates the charts from streamed
samples.

| Work | Where |
|---|---|
| Run controls and live charts for the finale run | `CSharp/web/src/chapters/Finale.vue` (posts to `/api/run`, opens the SSE stream) |
| Shared fetch client and payload types | `CSharp/web/src/api/client.ts`, `CSharp/web/src/api/types.ts` |
| Warmup gate: holds the run button until the engine reports ready | `CSharp/web/src/run/warmup.ts` |
| Log streaming composable (same SSE pattern as the run stream) | `CSharp/web/src/composables/useLogStream.ts` |

`RunRequest` carries the game or preset, seed, worker count, target spin count,
and sample stride. The client posts it to `/api/run`, opens an `EventSource` on
`/api/run/stream`, and keeps that connection open until the run finishes or the
user cancels it.

## 2. HTTP Endpoints

The endpoints translate HTTP requests into coordinator and stream operations.
Request validation belongs to `RunCoordinator`.

| Work | Where |
|---|---|
| `MapRunEndpoints`: `GET /api/run/limits`, `GET /api/run/readiness`, `POST /api/run`, `GET /api/run/current`, `POST /api/run/cancel`, `GET /api/run/stream` (SSE loop: subscribe, write `data:` frames, unsubscribe) | `CSharp/src/SlotDemo.Server/Runs/RunEndpoints.cs` |
| Readiness numbers served to the gate | `CSharp/src/SlotDemo.Server/Runs/EngineWarmupService.cs` (`BackgroundService` that measures warm spin rate at startup) |

## 3. Run Coordinator

`RunCoordinator` allows one active run. It validates the request, prepares the
game, starts the background task, and publishes run events.

| Work | Where |
|---|---|
| `Start(RunRequest)` rejects a second concurrent run, chooses `PrepareGame` or `PreparePreset`, and starts the run task | `CSharp/src/SlotDemo.Server/Runs/RunCoordinator.cs` |
| `PrepareGame` loads and validates the JSON, runs `GameAnalyzer.Analyze`, optionally scales line pays to a target RTP with `Reprice`, warms the outcome tables, and creates the `RunPlan` and `GameRunner` | same file |
| `PreparePreset` asks `PaytableSolver` to price a stock `ReelPreset` for the requested base and feature RTP contributions | same file, with `CSharp/src/MMP.SlotGame.Core/Paytables/PaytableSolver.cs` and `CSharp/src/MMP.SlotGame.Core/Features/FeatureSchedule.cs` |
| `Cancel()` stops the active run; `Describe()` supplies its current state to a page that joins late | same file |
| `ConvergenceRecorder` compares measured RTP with the analytic band at each sample stride | `CSharp/src/SlotDemo.Server/Runs/ConvergenceRecorder.cs` |

## 4. Loading a Game Definition

For a loaded-game request, the loader deserializes the JSON and returns either a
`GameDefinition` or the full set of validation errors.

| Work | Where |
|---|---|
| `LoadFile`, `Load`, and `TryLoad` deserialize the document, pass it to the builder, and warm the outcome tables after a successful build | `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinitionLoader.cs` |
| `GameDefinitionBuilder.TryBuild` validates in dependency order: symbols, substitutions, groups, reels, declared counts, outcome-table geometry, paylines, paytable, then features | `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinitionBuilder.cs` |
| `GameDocument` and its related types hold the nullable data produced by deserialization | `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDocument.cs` |
| The validated result: symbols, `StripReelSet`, paylines, compiled `PayCategory` list, optional `ScatterPickBonus` | `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinition.cs`, `CSharp/src/MMP.SlotGame.Core/Reels/StripReelSet.cs` |

## 5. PAR Data

The game files contain the strips, paylines, pays, and feature configuration
consumed by the loader.

| Work | Where |
|---|---|
| Shipped game definitions: strips, paylines, paytable, feature block, declared counts the loader verifies | `CSharp/games/orca-dive.json`, `CSharp/games/classic-three-reel.json`, `CSharp/games/two-line-tide.json` |

## 6. Calculating the Analytic Result

Before sampling begins, `GameAnalyzer` calculates the reference RTP, hit
frequency, and sigma from the game definition. This path enumerates outcomes; it
does not draw random stops.

| Work | Where |
|---|---|
| `Analyze(definition)` uses weighted symbol enumeration for a single payline. `Enumeration.Descend` walks the symbol classes and stop-count weights, `Accumulate` tallies pay and trigger weight, and `Summarize` produces RTP, hit frequency, and sigma. Multi-payline games use `AnalyzePhysicalOutcomes` instead | `CSharp/src/MMP.SlotGame.Core/Games/GameAnalyzer.cs` |
| The result object: `LineRtp`, `BonusRtp`, `TotalRtp`, `HitFrequency`, `SigmaPerUnitWagered`, per-category combination counts | `CSharp/src/MMP.SlotGame.Core/Games/GameAnalysis.cs` |
| Pick-bonus mean and second moment in closed form (no enumeration needed) | `CSharp/src/MMP.SlotGame.Core/Games/Definition/PickBonus.cs` (`Mean`, `MeanSquared`) |

## 7. Evaluating Wins

`WinEvaluator` owns the rules for line wins and scatter triggers. The analyzer
uses it while calculating the reference result, and the outcome-table builder
uses it when compiling the spin-time lookup.

| Work | Where |
|---|---|
| `Evaluate(cells)` finds the best win on one payline, including left-aligned runs, the wild continues/requires split, best-paying prefixes, and longer-run tie breaking | `CSharp/src/MMP.SlotGame.Core/Games/WinEvaluator.cs` |
| `EvaluateWindow` and `EvaluateWindowIds` sum the paylines; `IsTriggered` checks for a scatter-anywhere feature | same file |
| The compiled per-category lookups it reads (`Continues`, `IsRequired`, `PayFor`) | `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinition.cs` (`PayCategory`) |

## 8. Precomputing Outcome Tables

The builder evaluates the full reel cycle before the run. During a spin, the
engine can look up the packed stops instead of evaluating paylines again.

| Work | Where |
|---|---|
| `WinningOutcomeTable.Build` enumerates every stop combination, evaluates the paylines and feature trigger, and stores winning results under packed stop keys. `TryGetValue` reads those results | `CSharp/src/MMP.SlotGame.Core/Games/WinningOutcomeTable.cs` |
| `ProgressiveOutcomeTable.Build` rearranges the results into reel-by-reel narrowing tables. The spin path calls `TryGetValue(stops)` | `CSharp/src/MMP.SlotGame.Core/Games/ProgressiveOutcomeTable.cs` |

## 9. Adapting the Game to the Spin Loop

`GameRunner` connects a loaded game to the general-purpose simulation loop. It
creates one play delegate for each worker and combines the worker totals when
the run ends.

| Work | Where |
|---|---|
| `RunAsync` warms the tables, builds a `SimulationEngine` with the play factory and per-worker RNG streams, combines the worker tallies, and pairs the totals with the analytic result | `CSharp/src/MMP.SlotGame.Core/Games/GameRunner.cs` |
| `CreatePlay` returns the per-spin delegate. It calls `DrawStops`, looks up the line pay in `ProgressiveOutcomes`, runs `PickBonus.Play` when triggered, tallies line and bonus millicents, and returns a `SpinOutcome` | same file |
| The wager and money math | `CSharp/src/MMP.SlotGame.Core/Money/Millicents.cs` (`ScaledMultiply`), `CSharp/src/MMP.SlotGame.Core/Simulation/SimulationConfig.cs` (`Wager`) |

## 10. Running the Workers

`SimulationEngine` schedules the workers and collects their counters. Slot rules
stay inside the delegates supplied by `GameRunner`, so the engine works only
with quotas, RNG streams, `SpinOutcome` values, and telemetry.

| Work | Where |
|---|---|
| `RunAsync(telemetry, observer, ct)` assigns fixed worker quotas, invokes the `SpinPlay` delegates, batches counters, emits samples at the requested stride, and observes cancellation | `CSharp/src/MMP.SlotGame.Core/Simulation/SimulationEngine.cs` |
| `SpinRng.ForWorker(masterSeed, workerId)` creates a `xoshiro256**` stream seeded through SplitMix64; `NextInt` uses Lemire rejection | `CSharp/src/MMP.SlotGame.Core/Simulation/SpinRng.cs` |
| Run counters and the immutable `RunSnapshot` handed back | `CSharp/src/MMP.SlotGame.Core/Simulation/RunTotals.cs` |
| Reel draws on the hot path (`DrawStops`, `DrawWindowIds`, precomputed Lemire thresholds per reel) | `CSharp/src/MMP.SlotGame.Core/Reels/StripReelSet.cs` |

## 11. Streaming Progress to the Browser

`RunStreamService` sends each event to every subscribed browser tab. A slow
subscriber loses its oldest queued event instead of delaying the run.

| Work | Where |
|---|---|
| `Publish(jsonEvent)` writes to every subscriber and drops the oldest queued event for a slow client. Each SSE connection calls `Subscribe` and `Unsubscribe` | `CSharp/src/SlotDemo.Server/Runs/RunStreamService.cs` |
| Event producers: the coordinator publishes run-started (with the analytic RTP band), per-stride telemetry, convergence notes, and run-completed | `CSharp/src/SlotDemo.Server/Runs/RunCoordinator.cs` (`Publish`) |

## Free spins in this repository

The solver/preset path represents free spins as a scheduled RTP term in
`CSharp/src/MMP.SlotGame.Core/Features/FeatureSchedule.cs`. Loaded games use the
precomputed outcome tables and play the pick bonus inline. They do not start a
separate free-game session during a spin.
