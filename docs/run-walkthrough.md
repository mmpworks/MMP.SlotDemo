# How the SPA Runs a Simulation, Step by Step

This document walks one run from the button click to the final verdict. It also
shows how the server sets up the harness before the first request arrives.
Every step names the code that does it.

## Part 1 — The harness starts

You type one command:

```
cd CSharp/src/SlotDemo.Server
dotnet run
```

The server then does five things, in order (`Program.cs`):

1. **Build the Herald logger.** `QuickLogBuilder.Create("slotdemo")` adds three
   sinks: a console sink (your terminal), a rolling NDJSON file sink
   (`logs/slotdemo-.ndjson`), and an HttpJson sink. The HttpJson sink posts log
   batches back to this same server at `/api/logs/ingest`. That loop is what
   feeds the on-page log viewer. Ten custom levels split framework noise
   (`sys.*` levels) from application signal (plain levels).
2. **Register the services.** Three singletons: `LogStreamService` (fans log
   events out to browsers), `RunStreamService` (fans run events out to
   browsers), and `RunCoordinator` (owns the one active run).
3. **Route framework logs into Herald.** `SystemAwareHeraldProvider` replaces
   the default .NET loggers. ASP.NET's own messages go to the `sys.*` levels.
4. **Map the routes.** One route group per chapter lab (`MapChapterTwo` …
   `MapChapterNine`, `MapParSheet`), the run surface (`MapRuns`), and the two
   log routes (`/api/logs/ingest`, `/api/logs/stream`).
5. **Serve the SPA.** The server looks for `web/dist` (a dev checkout) or
   `wwwroot` (the container). If neither exists, it tells you to build the SPA
   first: `cd CSharp/web && npm install && npm run build`.

Open <http://localhost:5090>. The Vue SPA loads. Hash routes (`#/ch02` …
`#/ch09`, `#/par`, `#/library`, `#/finale`) pick the page.

## Part 2 — The page gets ready

You open `#/finale`. The page is `Finale.vue`. On mount it does three things:

1. `GET /api/run/limits` — asks the server what it may request: the 9,900 bp
   RTP cap, the default RTP split, the preset list, the shipped game list, and
   the 64-worker ceiling. The client never hardcodes a rule; it previews the
   server's rule.
2. `GET /api/run/current` — asks if a run is already live or finished. If yes,
   the page adopts it: config, analytic prediction, and the whole curve arrive
   in one read. A reload never loses the chart.
3. Opens an `EventSource` on `GET /api/run/stream` — the SSE pipe that will
   carry `started`, `point`, `progress`, `completed`, and `cancelled` events.

The log viewer at the bottom of the page does the same with
`useLogStream.ts`: it subscribes to `GET /api/logs/stream`, batches incoming
events per animation frame, and keeps at most 25,000 rows in a ring buffer.

## Part 3 — You press "Run the proof"

The page posts one JSON body to `POST /api/run` (`RunRequest`): subject, RTP
split in basis points, seed, worker count, target spins, and curve stride.

`RunEndpoints` hands the request to `RunCoordinator.Start`. The coordinator
prepares the subject. There are two kinds:

**A solved preset** (you picked the RTP):

1. `SimulationConfig.TryCreate` validates the draft. An aggregate RTP over the
   9,900 bp cap is rejected with every error named. Nothing is clamped.
2. `PresetGame.Build` builds the reels, asks `PaytableSolver` to scale the
   paytable toward your target, and prices the result: expected RTP and the
   per-spin standard deviation σ, via `AnalyticMath`.
3. A second cap check runs on the *realized* RTP, after rounding, against the
   same `MaxAggregateBasisPoints` constant.

**A shipped game** (Orca Dive; the paytable is already published):

1. `GameDefinitionLoader` loads and validates the JSON game document. All
   errors come back at once.
2. `GameAnalyzer` enumerates every stop combination. That gives the exact
   RTP and σ from the document alone, before a single spin.
3. `GameRunner` will play the spins.

Both paths produce the same three things: the run facts (what the page shows),
the analytic view (RTP, feature split, σ), and a runner function.

The coordinator then takes a lock, refuses a second run with 409 if one is
live, creates a `ConvergenceRecorder` (analytic RTP, σ, stride), and starts the
run task. The HTTP response is `201` with the run description — the page has
the analytic target before the first chart point exists.

## Part 4 — The engine plays the spins

`SimulationEngine.RunAsync` (in `MMP.SlotGame.Core`, which has no I/O and no
logging) splits the work:

1. **Fixed quotas.** `TargetSpins / WorkerCount` spins per worker; worker 0
   absorbs the remainder. The split is deterministic.
2. **One RNG stream per worker.** The `SpinRngFactory` seeds each worker's
   `SpinRng` from the master seed. Same seed + same worker count = same totals,
   bit for bit.
3. **One `SpinPlay` per worker.** Each worker owns its scratch byte window.
   The shared `StripReelSet` is read-only, so all workers share one copy.
4. **The spin.** Draw the window (`DrawWindowIds`), evaluate the paylines
   (`LinePayEvaluator.EvaluateIds`), play each `FeatureSchedule`, return a
   `SpinOutcome` (wagered, base payout, feature payout) in integer millicents.
5. **Batches of 4,096.** Each worker sums a batch into local `long`s, then
   publishes with four `Interlocked.Add` calls into `RunTotals`. Exact, atomic,
   low contention.
6. **Telemetry.** After each batch the worker `TryWrite`s an absolute snapshot
   into a bounded channel (capacity 1,024, drop-oldest). The write never
   blocks. A dropped sample costs one chart point and zero counted spins.

## Part 5 — The server turns spins into a chart

`RunCoordinator` runs a pump loop next to the engine:

1. Every ~100 ms it drains the telemetry channel.
2. Each sample goes to the `ConvergenceRecorder`. When the run crosses a
   stride boundary (default 50,000 spins), the recorder appends one curve
   point: spins, measured RTP, hit frequency, and the band half-width
   `z·σ/√N`. Ten million spins become about two hundred points.
3. Each new point publishes a `point` event; every other drain publishes a
   `progress` event (live counters at a human rate).
4. `RunStreamService` fans every event out to all SSE subscribers. A slow
   browser drops its own oldest events; the workers never wait.

Meanwhile Herald logs the run start, and later the verdict, through all three
sinks — so the same lines land in your terminal, the NDJSON file, and the
on-page log viewer.

## Part 6 — The run ends

1. `RunAsync` waits for every worker, then takes the final quiesced snapshot
   straight from `RunTotals` — outside the channel. The lossy path cannot
   touch this number.
2. The recorder gets that final snapshot and computes the last point.
3. The status becomes `completed` (or `cancelled` — Stop asks the token, and
   workers notice at the next batch boundary).
4. A `completed` event goes out with the full description. The page shows two
   verdict banners. The first is the statistical band: WITHIN BAND or OUTSIDE
   BAND, with the measured RTP and the band half-width at the final spin count.
   The second is the industry check: the measured RTP must sit within ±0.5
   percentage points of the analytic RTP over at least ten million spins, the
   fixed tolerance test labs quote. A run under ten million spins shows
   NOT QUALIFIED instead of a pass or fail.

## The two paths, one sentence each

- **Exact path:** worker → local batch sums → `Interlocked.Add` →
  `RunTotals` → final snapshot. Never dropped, always integer.
- **Lossy path:** worker → `TryWrite` → bounded channel → pump → recorder →
  SSE → chart. May drop points, can never change the totals.

## Where each part lives

| Part | File |
|---|---|
| Harness setup, Herald, routes, SPA hosting | `CSharp/src/SlotDemo.Server/Program.cs` |
| Run HTTP surface | `CSharp/src/SlotDemo.Server/Runs/RunEndpoints.cs` |
| Run lifecycle, prep, pump | `CSharp/src/SlotDemo.Server/Runs/RunCoordinator.cs` |
| Curve points and band | `CSharp/src/SlotDemo.Server/Runs/ConvergenceRecorder.cs` |
| SSE fan-out (runs) | `CSharp/src/SlotDemo.Server/Runs/RunStreamService.cs` |
| SSE fan-out (logs) | `CSharp/src/SlotDemo.Server/LogStreamService.cs` |
| Worker scheduling, batches, totals | `CSharp/src/MMP.SlotGame.Core/Simulation/SimulationEngine.cs` |
| Exact counters | `CSharp/src/MMP.SlotGame.Core/Simulation/RunTotals.cs` |
| The finale page | `CSharp/web/src/chapters/Finale.vue` |
| Log stream composable | `CSharp/web/src/composables/useLogStream.ts` |
