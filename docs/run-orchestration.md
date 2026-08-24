# Run Orchestration

This page traces one simulation request through the types in
`CSharp/src/SlotDemo.Server/Runs/`. The request enters through `RunEndpoints`,
is prepared and executed by the coordinator, and leaves the server as SSE
events for each connected browser tab.

The counts for a 10-worker run:

**10 workers → 1 channel → 1 pump → 1 recorder → 1 stream service → N browser tabs**

Workers write cumulative snapshots to a shared channel; they do not publish
SSE events. One pump reads that channel, records chart points, and passes
display events to the stream service. Each worker keeps its own tally and
buffers, so producing telemetry does not require workers to update shared run
totals.

```mermaid
flowchart TD
    subgraph Browser["Browser — N tabs"]
        SPA["Vue SPA<br/>builds the RunRequest,<br/>draws the charts"]
    end

    subgraph Server["SlotDemo.Server — one per host"]
        EP["RunEndpoints<br/>POST /api/run"]
        CO["RunCoordinator<br/>Start / Cancel / GetCurrentStatus"]
        PREP["RunPreparer<br/>PreparePreset | PrepareShippedGame"]
        AR["ActiveRun — 1 per run<br/>facts, analytic, clocks, status"]
        CH["telemetry Channel — 1<br/>bounded 1024, drop-oldest"]
        PUMP["PumpTelemetryAsync — 1 reader"]
        REC["ConvergenceRecorder — 1<br/>curve points + band"]
        SSE["RunStreamService — 1<br/>SSE fan-out"]
    end

    subgraph Core["MMP.SlotGame.Core"]
        SR["SimulationExecutor — 1 delegate"]
        WK["SimulationEngine workers — 10<br/>one SpinPlay + one RNG stream each"]
    end

    SPA -- "RunRequest (JSON)" --> EP
    EP -- "Start(RunRequest)" --> CO
    CO -- "validate + analyze" --> PREP
    PREP -- "RunPreparationResult → PreparedRun<br/>(RunConfiguration, AnalyticReference, SimulationExecutor, RunId)" --> CO
    CO -- "installs" --> AR
    CO -- "ExecuteAsync" --> SR
    SR --> WK
    WK -- "TelemetrySample × many" --> CH
    CH --> PUMP
    PUMP -- "Observe(totals)" --> REC
    PUMP -- "point / progress events" --> SSE
    SSE -- "SSE — N tabs" --> SPA
```

## Type responsibilities, in request order

| Step | Type | Kind | Role |
|---|---|---|---|
| 1 | `RunRequest` | public record | This is the order form from the browser. It says which game to run, how many spins to play, how many workers to use, which random seed to start with, and how often to add a chart point. It contains requests, not trusted settings; the server still has to check every value. |
| 2 | `RunCoordinator` | class | This is the run manager. It accepts the order form, asks `RunPreparer` to get everything ready, starts the work, handles cancellation, and announces progress. It also refuses a new run while another run is active because the page displays one run at a time. |
| 3 | `RunPreparer` | class | This gets the game ready before the stopwatch starts. For a preset, it builds the game from the requested settings. For a shipped game, it loads the game file. It rejects bad input and calculates the exact RTP and volatility numbers that the simulation will be compared with. |
| 4 | `RunPreparationResult` | record | This is `RunPreparer`'s answer. Success carries a `PreparedRun` to the coordinator. Failure carries the HTTP status and message that explain why the server cannot start the run. |
| 5 | `PreparedRun` | record | This is the ready-to-run package. `RunConfiguration` describes the game and requested work. `AnalyticReference` holds the calculated RTP and volatility used for comparison. `SimulationExecutor` runs the selected game, and `RunId` identifies this attempt. Presets and shipped games both produce this package, so the coordinator starts them the same way. |
| 6 | `ActiveRun` | mutable class | This holds the state that changes while one simulation runs: status, clocks, cancellation, recorded chart points, and the execution task. `CreateStatusSnapshot()` copies that state into the response returned to the page. |
| 7 | `SimulationExecutor` | delegate | This is the common execution contract for presets and shipped games. The coordinator gives it a telemetry writer and cancellation token. It runs the spins, publishes cumulative telemetry samples, and returns the authoritative final totals and worker timings. |
| 8 | `TelemetrySample` | Core record | Each worker processes at most 4,096 spins in a batch. After finishing a batch, the worker adds its batch totals to the shared `RunTotals` counters, takes a read-only snapshot of those shared totals, and puts that snapshot on the telemetry channel. This message is for observing progress; it does not drive the simulation or determine the final result. If the channel drops a message, the next message contains newer cumulative totals. |
| 9 | `ConvergenceRecorder` | class | This turns many progress snapshots into a chart small enough for the browser to keep. It remembers the newest totals and saves a point whenever the spin count crosses the requested stride. Each saved point also shows how close the measured RTP is to the exact RTP and whether it falls inside the expected confidence band. |
| 10 | `RunStreamService` | class | This is the delivery service for live run events. Each browser tab gets its own small, bounded queue. The service copies events into those queues without waiting, so a slow or disconnected browser cannot slow the simulation; that browser may miss older progress events and can recover from the current snapshot. |

## Testing the flow without Core

The internal `RunCoordinator.Start(PreparedRun, stride)` overload lets tests
bypass `RunPreparer`. A test supplies a `PreparedRun` with a fake
`SimulationExecutor` that writes synthetic `TelemetrySample` values and returns
fixed totals. `tests/SlotDemo.Server.Tests/RunCoordinatorFlowTests.cs` uses
that path to cover startup, recording, streaming, overlapping-run rejection,
and both cancellation paths without loading a game or running a spin.

A simulation executor must complete its telemetry writer before returning.
`PumpTelemetryAsync`
reads until that completion signal, then the coordinator records and publishes
the terminal state.
