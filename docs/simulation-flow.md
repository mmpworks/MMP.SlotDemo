# Simulation Flow, Node by Node

This document walks one simulation run through every node of
`docs/system-design-simplified.png`. For each node: what work happens there,
and the classes that do it, with paths relative to the repository root.

The flow below follows a **loaded game** run (a `games/*.json` file named in
the request). The preset/solver path shares nodes 1-3 and 9-11; its
difference is noted in node 4.

---

## 1. Client (Vue SPA)

Sends the run request, then draws what comes back.

| Work | Where |
|---|---|
| Run controls and live charts for the finale run | `CSharp/web/src/chapters/Finale.vue` (posts to `/api/run`, opens the SSE stream) |
| Shared fetch client and payload types | `CSharp/web/src/api/client.ts`, `CSharp/web/src/api/types.ts` |
| Warmup gate: holds the run button until the engine reports ready | `CSharp/web/src/run/warmup.ts` |
| Log streaming composable (same SSE pattern as the run stream) | `CSharp/web/src/composables/useLogStream.ts` |

Functions performed: build a `RunRequest` JSON (preset or game file, seed,
workers, target spins, stride), `POST /api/run`, open `EventSource` on
`/api/run/stream`, update charts per sample, offer cancel.

## 2. Run Endpoints

The HTTP door. Thin: validates nothing itself beyond routing.

| Work | Where |
|---|---|
| `MapRunEndpoints`: `GET /api/run/limits`, `GET /api/run/readiness`, `POST /api/run`, `GET /api/run/current`, `POST /api/run/cancel`, `GET /api/run/stream` (SSE loop: subscribe, write `data:` frames, unsubscribe) | `CSharp/src/SlotDemo.Server/Runs/RunEndpoints.cs` |
| Readiness numbers served to the gate | `CSharp/src/SlotDemo.Server/Runs/EngineWarmupService.cs` (`BackgroundService` that measures warm spin rate at startup) |

## 3. Run Coordinator

One run at a time. Validates the request, prepares the subject, owns the
run's lifecycle, and publishes every event.

| Work | Where |
|---|---|
| `Start(RunRequest)` — rejects a second concurrent run, branches to `PrepareGame` (game file set) or `PreparePreset` (solver path), spawns the run task | `CSharp/src/SlotDemo.Server/Runs/RunCoordinator.cs` |
| `PrepareGame` — load + validate the JSON, run `GameAnalyzer.Analyze`, optionally `Reprice` (scale line pays to a target RTP), warm the outcome tables, build the `RunPlan` and `GameRunner` | same file |
| `PreparePreset` — solver path: `PaytableSolver` prices a stock `ReelPreset` to the requested RTP split (base + scheduled free-spins/pick-bonus terms) | same file, with `CSharp/src/MMP.SlotGame.Core/Paytables/PaytableSolver.cs` and `CSharp/src/MMP.SlotGame.Core/Features/FeatureSchedule.cs` |
| `Cancel()`, `Describe()` — stop the active run; report the current one to a late-joining page | same file |
| `ConvergenceRecorder` — per-stride check of measured RTP against the analytic band | `CSharp/src/SlotDemo.Server/Runs/ConvergenceRecorder.cs` |

## 4. Game Definition Loader

Turns a JSON document into a validated game, or a complete error list.

| Work | Where |
|---|---|
| `LoadFile` / `Load` / `TryLoad` — deserialize, hand to the builder, warm the outcome tables on success | `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinitionLoader.cs` |
| `GameDefinitionBuilder.TryBuild` — phase-ordered validation: symbols, substitutions, groups, reels, declared stop/symbol counts, outcome-table geometry, paylines, paytable (pay-unit compilation), features | `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinitionBuilder.cs` |
| `GameDocument` and friends — the nullable deserialization DTOs | `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDocument.cs` |
| The validated result: symbols, `StripReelSet`, paylines, compiled `PayCategory` list, optional `ScatterPickBonus` | `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinition.cs`, `CSharp/src/MMP.SlotGame.Core/Reels/StripReelSet.cs` |

## 5. PAR Data (config)

Pure data. No code runs here.

| Work | Where |
|---|---|
| Shipped game definitions: strips, paylines, paytable, feature block, declared counts the loader verifies | `CSharp/games/orca-dive.json`, `CSharp/games/classic-three-reel.json`, `CSharp/games/two-line-tide.json` |

## 6. Game Analyzer (enumerated RTP, no RNG)

Computes the truth the simulation must converge to.

| Work | Where |
|---|---|
| `Analyze(definition)` — dispatch: single payline uses weighted symbol enumeration (`Enumeration.Descend` walks per-reel symbol classes with stop-count weights; `Accumulate` tallies pay, pay-squared, and trigger weight; `Summarize` produces RTP, hit frequency, sigma). Multi-payline games price from the compiled physical outcomes instead (`AnalyzePhysicalOutcomes`) | `CSharp/src/MMP.SlotGame.Core/Games/GameAnalyzer.cs` |
| The result object: `LineRtp`, `BonusRtp`, `TotalRtp`, `HitFrequency`, `SigmaPerUnitWagered`, per-category combination counts | `CSharp/src/MMP.SlotGame.Core/Games/GameAnalysis.cs` |
| Pick-bonus mean and second moment in closed form (no enumeration needed) | `CSharp/src/MMP.SlotGame.Core/Games/Definition/PickBonus.cs` (`Mean`, `MeanSquared`) |

## 7. Win Evaluator

The one place that knows what a win is. Both the analyzer and the
table builder call it; nothing else re-implements the rules.

| Work | Where |
|---|---|
| `Evaluate(cells)` — best win on one payline: left-aligned runs, wild continues/requires split, best-paying prefix, tie to the longer run | `CSharp/src/MMP.SlotGame.Core/Games/WinEvaluator.cs` |
| `EvaluateWindow` / `EvaluateWindowIds` — sum over paylines; `IsTriggered` — scatter-anywhere feature check | same file |
| The compiled per-category lookups it reads (`Continues`, `IsRequired`, `PayFor`) | `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinition.cs` (`PayCategory`) |

## 8. Outcome Tables (precomputed)

The full cycle, evaluated once, so the hot loop is a dictionary probe.

| Work | Where |
|---|---|
| `WinningOutcomeTable.Build` — enumerate every stop combination, evaluate all paylines and the feature trigger, store only winners keyed by packed stop bytes (`PackKey`); `TryGetValue` at spin time | `CSharp/src/MMP.SlotGame.Core/Games/WinningOutcomeTable.cs` |
| `ProgressiveOutcomeTable.Build` — the same outcomes rearranged as reel-by-reel narrowing tables; `TryGetValue(stops)` is the spin-path lookup | `CSharp/src/MMP.SlotGame.Core/Games/ProgressiveOutcomeTable.cs` |

## 9. GameRunner (per-spin play)

Adapts a loaded game to the shared engine: one delegate per worker.

| Work | Where |
|---|---|
| `RunAsync` — force tables warm, build the `SimulationEngine` with per-worker RNG streams and the play factory, sum per-worker tallies afterward, pair the totals with the analytic reference | `CSharp/src/MMP.SlotGame.Core/Games/GameRunner.cs` |
| `CreatePlay` — the per-spin delegate: `DrawStops`, probe `ProgressiveOutcomes` for the line pay, play `PickBonus.Play` inline on a trigger, tally line/bonus millicents, return a `SpinOutcome` | same file |
| The wager and money math | `CSharp/src/MMP.SlotGame.Core/Money/Millicents.cs` (`ScaledMultiply`), `CSharp/src/MMP.SlotGame.Core/Simulation/SimulationConfig.cs` (`Wager`) |

## 10. Simulation Engine

The shared spin loop. Knows nothing about slots; it schedules workers,
streams counters, and keeps determinism.

| Work | Where |
|---|---|
| `RunAsync(telemetry, observer, ct)` — fixed worker quotas, per-worker `SpinPlay` delegates, batched counters, telemetry samples per stride, cancellation | `CSharp/src/MMP.SlotGame.Core/Simulation/SimulationEngine.cs` |
| `SpinRng.ForWorker(masterSeed, workerId)` — xoshiro256** seeded via SplitMix64; `NextInt` with Lemire rejection | `CSharp/src/MMP.SlotGame.Core/Simulation/SpinRng.cs` |
| Run counters and the immutable `RunSnapshot` handed back | `CSharp/src/MMP.SlotGame.Core/Simulation/RunTotals.cs` |
| Reel draws on the hot path (`DrawStops`, `DrawWindowIds`, precomputed Lemire thresholds per reel) | `CSharp/src/MMP.SlotGame.Core/Reels/StripReelSet.cs` |

## 11. Run Stream (SSE)

Fan-out of run events to every open browser tab.

| Work | Where |
|---|---|
| `Publish(jsonEvent)` — push to every subscriber, dropping the oldest queued event for a slow client; `Subscribe`/`Unsubscribe` per SSE connection | `CSharp/src/SlotDemo.Server/Runs/RunStreamService.cs` |
| Event producers: the coordinator publishes run-started (with the analytic RTP band), per-stride telemetry, convergence notes, and run-completed | `CSharp/src/SlotDemo.Server/Runs/RunCoordinator.cs` (`Publish`) |

---

## The sequence in one list

1. Client POSTs `RunRequest` to `/api/run` (node 1 → 2).
2. `RunCoordinator.Start` validates and branches (node 3).
3. `GameDefinitionLoader` reads the game JSON and the builder validates it
   (nodes 4, 5).
4. `GameAnalyzer.Analyze` enumerates the RTP and sigma with no sampling error; the
   coordinator publishes the analytic band (nodes 6, 7, 11).
5. Outcome tables are (re)built warm so workers never pay construction
   time (nodes 7, 8).
6. `GameRunner.RunAsync` hands per-worker play delegates to
   `SimulationEngine.RunAsync` (nodes 9, 10).
7. Workers draw stops, probe the tables, play features inline, and batch
   counters; every stride a telemetry sample flows to the stream (nodes
   10 → 11).
8. The client's `EventSource` redraws the charts per sample until the run
   completes or is cancelled (node 1).

## Free spins in this repository

Free spins appear only on the solver/preset path, modeled analytically as a
scheduled RTP term (`CSharp/src/MMP.SlotGame.Core/Features/FeatureSchedule.cs`).
Loaded games play every spin through the precomputed tables plus the inline
pick bonus; there is no per-spin free-game session here.
