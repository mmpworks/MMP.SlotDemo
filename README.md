# MMP.SlotDemo

The companion site for the *Building a Slot Machine RTP Simulator* series. Every
episode has a page: a short written brief plus labs that run the engine's own code
on the server and narrate each step through Herald, so the log stream at the
bottom of the page shows the same computation from the inside.

The full engine lives in this repo (`CSharp/src/MMP.SlotGame.Core/`) with its
complete test suite, so the labs and the episodes walk the same source. It began
as a copy of the engine the series was written against and has since taken fixes
of its own, so this repository is now the authority for what the site runs.

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

Or in Docker, which builds the SPA and the server together:

```bash
docker compose -f docker/docker-compose.yml up --build
```

Everything the site needs is in this repository. The only external dependencies are
public NuGet and npm packages, so a fresh clone builds and runs with no other checkout.

## What else is here

| Directory | What it is |
|---|---|
| `CSharp/src` | The engine (`MMP.SlotGame.Core`) and the server that hosts the labs |
| `CSharp/web` | The Vue SPA: chapter labs, the PAR sheet, and the TEACH ME reading section |
| `CSharp/games` | The shipped game definitions the labs and the PAR sheet load |
| `docs/articles` | The nine articles, served to the site's TEACH ME section |
| `docs/scripts` | Recording scripts for the video series |
| `python/verification` | An independent re-derivation of the math, sharing no code with the engine |
| `video` | The Remotion project for the series' video assets |

## The pages

| Page | What it shows | Lab endpoints |
|---|---|---|
| Start | Episode cards + the STAT machine probe (pipeline smoke test) | `/api/stats` |
| 01 System Design | The map of the machine. Written brief; no lab yet | — |
| 02 Money You Can Trust | Integer money vs a double twin, bit view, seeded streams, die-first rejection, modulo bias | `/api/ch2/money` `/rng` `/bias` |
| 03 Reels and Paylines | Strip geometry, deterministic spin walk, payline reads, centre-row census | `/api/ch3/sources` `/spin` `/census` `/reel-snapshots` |
| 04 Paytable Math | Paytable solve path with drift, band table at a spin ladder | `/api/ch4/solve` `/band` `/published` |
| 05 Weighted Enumeration | Grouping repeated symbols without losing outcomes. Runs in the page | — |
| 06 The Simulation Engine | Same-seed triple runs (bit-identical), telemetry starvation | `/api/ch5/determinism` `/telemetry` |
| 07 Games as Data | Shipped game documents via the real loader, paste-anything validator | `/api/ch6/games` `/validate` |
| 08 Proving the Machine | Exhaustive enumeration census, simulation vs referee verdict | `/api/ch7/enumerate` `/referee` |
| 09 Optimize the Machine | The original window draw against the byte-id version, same seeds and work | `/api/ch9/draw-window` |
| PAR | The full Probability & Accounting Report for Orca Dive, computed live from every stop combination | `/api/par/sheet` `/api/par/summary` |
| Books | The slot-math, PAR-sheet, PRNG, and regulation reading behind the series | — |
| Teach me! | The nine articles, read on the site, cross-linked with the labs both ways | `/api/articles` |
| Run | **The proving ground** — a live run converging inside the band | `/api/run/*` |

The episodes were renumbered to nine partway through; the API routes were not, so
`/api/ch5/*` serves episode 6 and so on down the list. The page routes (`#/ch06`)
follow the episode numbers.

## The proving ground

`RunCoordinator` owns one run at a time. Workers publish absolute snapshots into a
bounded drop-oldest channel; a `ConvergenceRecorder` consolidates them into one
curve point per stride (default 50,000 spins → ~200 points on a 10M run), each
carrying its own z·σ/√N half-width. Points stream to the page over SSE
(`/api/run/stream`); a page that connects mid-run reads `/api/run/current` once
and catches up. The SVG chart draws the analytic band as a narrowing funnel and
the measured RTP walking inside it.

The page reports two different numbers and keeps them apart. Engine spins/second
is the workers' own spin-loop time, measured inside the engine; it excludes the
telemetry hand-off and whatever a watching browser costs. Warm, that is 105-140M
spins/second across 8 workers on a developer machine, so a 10M-spin run finishes
in well under a second.

The server warms the engine at startup and the run button stays disabled until
`/api/run/readiness` reports ready, because .NET compiles and re-optimizes the
spin loop on first use: an unwarmed first run reads about a tenth of the real
rate while producing identical spins, RTP and verdict. A completed run under the
threshold is still flagged on the chart rather than quoted as engine speed.

Recording tip: for a convergence walk the camera can follow, use 1 worker and
100M+ spins, or a bigger preset (`Video5x128`).

## Running a game at a different RTP

A shipped game brings its own paytable, and the proving ground can re-price it.
Line RTP is the sum of probability times pay, so one scalar re-prices the whole
table: tick **Re-price paytable**, give a target total RTP in basis points, and the
run uses a re-priced copy.

The strips are untouched, so the game hits exactly as often and only the amounts
change; volatility and the top award scale with them. The feature keeps its own
prize table and is not scaled, which puts a floor under the total — a target at or
below the feature's own contribution is refused rather than clamped. Each re-priced
pay rounds to a whole hundredth of the wager, so the enumerated RTP lands near the
request rather than exactly on it, and it is that enumerated figure the band and
the verdict are measured against.

## Geometry flexibility

The five presets cover 3, 4, and 5 reels (`Classic3`, `Video3`, `Line4`,
`Video5x64`, `Video5x128`), all selectable in the ch3 lab and the proving ground.
JSON game definitions support arbitrary reel counts, ragged strip lengths, and
3–5 window rows; the shipped `classic-three-reel.json` and `orca-dive.json` are
the worked examples.

## Chapter → source files (what each episode creates on camera)

| Episode | Files pasted and walked | What the episode covers |
|---|---|---|
| 01 | — | The map of the machine, and the estimate that says one process is enough |
| 02 | `Money/Millicents.cs`, `Simulation/SpinRng.cs` | M1/M2/R3, the invariants the rest of the engine relies on |
| 03 | `Reels/StripReelSet.cs`, `Reels/Payline.cs`, `Reels/ReelPreset.cs` | A reel is a strip; the strip is the distribution |
| 04 | `Paytables/Paytable.cs`, `Paytables/PaytableSolver.cs`, `Rtp/AnalyticMath.cs` | One scale factor; sigma priced in closed form |
| 05 | `Rtp/AnalyticMath.cs` (weighted paths) | Grouping repeated symbols without losing any physical outcome |
| 06 | `Simulation/SimulationEngine.cs`, `Simulation/RunTotals.cs` | Fixed quotas, batched atomics, the two-lane split |
| 07 | `Games/Definition/*` + `games/*.json` | Games as data; validation that reports everything at once |
| 08 | `Games/GameAnalyzer.cs` + the ground-truth tests | Enumeration referees simulation |
| 09 | `Reels/StripReelSet.cs` (byte-id draw), `Games/WinningOutcomeTable.cs` | Optimising only what was already proven, and measuring it |

Recording scripts live in this repo under `docs/scripts/`, one per episode.

## Tests

```bash
cd CSharp && dotnet test MMP.SlotDemo.slnx     # engine + server suites
cd web && npm test                              # SPA suite, incl. chart geometry
```

Server tests include seeded fuzz across every lab route (no request may 5xx),
hostile numeric extremes on run start, recorder boundary cases, and the full run
lifecycle through the public API — including same-seed reproducibility.

Engine tests pin the properties the series teaches rather than only exercising the
code: money stays exact and order-independent, a seed reproduces a run bit for bit,
worker count does not move the totals, a simulated run lands inside z·σ/√N of the
enumerated RTP, and the optimised window draw matches a modulo baseline. The long
convergence and stress runs are a separate tier, skipped unless
`SLOTGAME_SLOW_TESTS=1` is set; the fast versions above always run.

### The check that shares no code

`python/verification/` re-derives Orca Dive from the game JSON in Python: it
recompiles the pay categories, enumerates all 14,781,416 stop combinations, and
prices the pick bonus by exact subset-sum instead of the engine's closed form. It
agrees with the engine on every published figure, and the sigma values it produces
are asserted in the engine suite.

```bash
pip install numpy
cd python/verification && python orca_check.py
```

`coverage.py` goes further and measures the confidence band's real coverage by
sampling 20,000 replicate runs from the exact outcome law: 98.86, 98.96 and 98.94
percent against a nominal 99 at 1e6, 1e7 and 1e8 spins. `skew.py` shows why that
was worth measuring, since about 26% of the line variance sits in one outcome that
occurs 4 times in 14.8 million spins.

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
