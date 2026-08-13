# MMP.SlotDemo

The companion site for the *Building a Slot Machine RTP Simulator* series. Every
episode has a page: a short written brief plus labs that run the engine's own code
on the server and narrate each step through Herald, so the log stream at the
bottom of the page shows the same computation from the inside.

The full engine lives in this repo (`CSharp/src/MMP.SlotGame.Core/`, imported
verbatim from MMP.SlotGame with its complete test suite), so the labs and the
episodes walk the same source.

## Run it

```bash
# 1. Build the SPA
cd CSharp/web && npm install && npm run build && cd ../..

# 2. Start the server
dotnet run -c Release --project CSharp/src/SlotDemo.Server

# 3. Open http://localhost:5090
```

Dev loop for the SPA: `npm run dev` in `CSharp/web` (Vite on `:5173`, proxies
`/api` to `:5090`).

## The pages

| Page | What it shows | Lab endpoints |
|---|---|---|
| Start | Episode cards + the STAT machine probe (pipeline smoke test) | `/api/stats` |
| 02 Money | Integer money vs a double twin, bit view, seeded streams, modulo bias | `/api/ch2/money` `/rng` `/bias` |
| 03 Reels | Strip geometry, deterministic spin walk, payline reads, center-row census | `/api/ch3/presets` `/spin` `/census` |
| 04 Math | Paytable solve path with drift, band table at a spin ladder | `/api/ch4/solve` `/band` |
| 05 Engine | Same-seed triple runs (bit-identical), telemetry starvation | `/api/ch5/determinism` `/telemetry` |
| 06 Data | Shipped game documents via the real loader, paste-anything validator | `/api/ch6/games` `/validate` |
| 07 Proof | Exhaustive enumeration census, simulation vs referee verdict | `/api/ch7/enumerate` `/referee` |
| PAR | The full Probability & Accounting Report for Orca Dive, computed live from every stop combination | `/api/par/sheet` `/api/par/summary` |
| Books | The slot-math, PAR-sheet, PRNG, and regulation reading behind the series | — |
| Run | **The proving ground** — a live 10M-spin run converging inside the band | `/api/run/*` |

## The proving ground

`RunCoordinator` owns one run at a time. Workers publish absolute snapshots into a
bounded drop-oldest channel; a `ConvergenceRecorder` consolidates them into one
curve point per stride (default 50,000 spins → ~200 points on a 10M run), each
carrying its own z·σ/√N half-width. Points stream to the page over SSE
(`/api/run/stream`); a page that connects mid-run reads `/api/run/current` once
and catches up. The SVG chart draws the analytic band as a narrowing funnel and
the measured RTP walking inside it.

Recording tip: at Release speed the engine clears ~50M spins/sec across 8 workers,
so a 10M-spin run finishes in under a second. For a convergence walk the camera
can follow, use 1 worker and 100M+ spins, or a bigger preset (`Video5x128`).

## Geometry flexibility

The five presets cover 3, 4, and 5 reels (`Classic3`, `Video3`, `Line4`,
`Video5x64`, `Video5x128`), all selectable in the ch3 lab and the proving ground.
JSON game definitions support arbitrary reel counts, ragged strip lengths, and
3–5 window rows; the shipped `classic-three-reel.json` and `orca-dive.json` are
the worked examples.

## Chapter → source files (what each episode creates on camera)

| Episode | Files pasted and walked | What the episode covers |
|---|---|---|
| 02 | `Money/Millicents.cs`, `Simulation/SpinRng.cs` | M1/M2/R3, the invariants the rest of the engine relies on |
| 03 | `Reels/StripReelSet.cs`, `Reels/Payline.cs`, `Reels/ReelPreset.cs` | A reel is a strip; the strip is the distribution |
| 04 | `Paytables/Paytable.cs`, `Paytables/PaytableSolver.cs`, `Rtp/AnalyticMath.cs` | One scale factor; sigma priced in closed form |
| 05 | `Simulation/SimulationEngine.cs`, `Simulation/RunTotals.cs` | Fixed quotas, batched atomics, the two-lane split |
| 06 | `Games/Definition/*` + `games/*.json` | Games as data; validation that reports everything at once |
| 07 | `Games/GameAnalyzer.cs` + the ground-truth tests | Enumeration referees simulation |

Recording scripts live in this repo under `docs/scripts/`, one per episode.

## Tests

```bash
cd CSharp && dotnet test MMP.SlotDemo.slnx     # engine + server suites
cd web && npm test                              # SPA suite, incl. chart geometry
```

Server tests include seeded fuzz across every lab route (no request may 5xx),
hostile numeric extremes on run start, recorder boundary cases, and the full run
lifecycle through the public API — including same-seed reproducibility.

## Logging

Herald.OSS in native mode with a custom 10-level set; `sys.*` levels carry
framework noise, plain levels carry application signal. The HttpJson sink posts to
`/api/logs/ingest` and fans out over SSE to the always-mounted viewer. Set
`SLOTDEMO_LOG_INGEST_URL=` (empty) to drop the relay sink; the test host does this
so the file-sink drain never waits on a port nothing bound.

## Copyright and license

Copyright (c) 2026 Steven Muchow.

MMP.SlotDemo is open-source software licensed under the
[Apache License 2.0](LICENSE). Attribution information is also recorded in
[NOTICE](NOTICE).
