# Episode 1 — System Design: A Slot Machine That Proves Itself

**Target:** 22–24 min. **Format:** whiteboard first, source last. This is the only
episode with no paste blocks: the artifact today is a diagram, and every later episode
builds one box of it.
**Subject:** the engine. The companion site appears three times, for under three
minutes total, and only to make a design claim visible.
**Companion article:** `docs/articles/01-system-design.md`
**Companion site:** MMP.SlotDemo, branch `main`. Episode 1 has no lab page of its
own; the illustrations borrow `#/finale` and `#/ch06`.

> **Discipline note for this recording.** The labs illustrate; they do not carry the
> episode. The whiteboard is the subject. Cut to the browser where a number is easier
> to see than to describe, and cut back inside a minute.

---

## Prep checklist

**Repo — the subject**
- [ ] Excalidraw (or draw.io) open on a blank canvas, pen color set, grid off
- [ ] Rider on `CSharp/MMP.SlotDemo.slnx`, tree collapsed to project level
- [ ] `docs/architecture.md` open in a Rider tab, scrolled to the ADR table in §6
- [ ] `CSharp/src/MMP.SlotGame.Core/Simulation/SimulationConfig.cs` open in a second tab
- [ ] Test runner loaded with `ConfigValidationTests`

**Companion site — the illustration**
- [ ] `E:\dev\MMP.SlotDemo`, branch `main`
- [ ] `cd CSharp/web && npm run build`, then `dotnet run --project CSharp/src/SlotDemo.Server`
- [ ] `http://localhost:5090/#/ch06` and `#/finale` each loaded once so nothing pays
      first-request cost
- [ ] A finished 10M-spin run left on screen in a second tab for the cold open

**OBS**
- [ ] Scenes: `WHITEBOARD`, `RIDER`, `BROWSER`
- [ ] Zoom-to-mouse hotkey bound and tested at diagram-reading zoom
- [ ] Pen pressure and color tested on camera; thin lines vanish at 1080p

---

## 0:00–1:30 — Cold open: the result, then the map

**Scene:** BROWSER, a completed run on the finale page.

- "This chart is a 98% slot machine proving itself. The line is ten million simulated
  spins. The shaded band is what probability theory says the wander should be. It
  walks in and it stays in."
- "Over eight episodes we build everything behind that chart. A ninth follow-up measures
  what is worth optimizing. Today is the map: the
  way I would whiteboard this in a design interview, requirements through deep dives.
  By the end of this one you know where the whole series goes and what gets built
  when."
- Name the route, one question per episode, so viewers know where they are. Episode 2,
  how do you hold money that never drifts and randomness you can replay. Episode 3,
  what a reel actually is, and why a weighted die gets the odds wrong. Episode 4, how
  to compute the return and the confidence band without playing a spin. Episode 5, how
  weighted counting replaces millions of repeated outcomes. Episode 6, how to play
  millions of spins in parallel and still reproduce them bit for bit. Episode 7, how
  the game itself becomes a data file. Episode 8, why you should believe the numbers. Then
  episode 9, what a correctness-first build costs in speed and how much of it comes back.
  Any one of episodes 2 through 9 works on its own if that is the subject you came for.
  Watch them in order and each one hands the next its foundation.
- Set the format for the series: "Every later episode creates a file on camera, pastes
  the finished source, and then I tell you why every line is the way it is. Today is the
  design those files come out of."

## 1:30–4:30 — Requirements

**Scene:** WHITEBOARD. Two columns, written as you talk.

**Functional** — write these abbreviated, say them fully.

1. Configure a game: a reel preset, a base RTP target, and two feature RTP targets.
2. Run N spins, where N reaches the millions, across every core on the box.
3. Stream live progress to a browser: a convergence chart and a confidence band.
4. Deliver a verdict at the end: the run landed inside the band, or it did not.

**Non-functional** — spend the time here, and say why each one is hard.

1. **Exact.** Totals are integers an auditor can add up by hand. Say the word: exact,
   rather than accurate to a few decimal places. That difference is why episode 2
   exists.
2. **Deterministic.** The same seed and the same worker count produce the same result,
   bit for bit. Identical, rather than statistically similar.
3. **Fast.** Millions of spins per second, because the quality of the proof improves
   with N and a slow simulator caps how much proof you can afford.
4. **Loud on failure.** A configuration that breaks the RTP cap gets rejected with a
   message. It never gets clamped to something legal and run anyway.

**Non-goals**, said out loud, because naming what you refuse to build is part of the
design.

- Simulation-grade randomness, and no claim of a certified gaming RNG.
- No real money anywhere in the system.
- Features pay out and end; they never re-enter the base game.

### Beat — what each non-goal removes

Each non-goal removes a whole subsystem. Certified randomness would pull in hardware
entropy and an audit trail. Real money would pull in accounts, ledgers, and
regulation. Features re-entering the base game would make the closed-form math
recursive. Three sentences of restraint buy back months of work.

## 4:30–6:30 — Back of the envelope

**Scene:** WHITEBOARD, arithmetic in the corner.

- Cost of one spin: five random draws, one window read, nine line walks. Call it a few
  hundred nanoseconds.
- Sixteen cores at roughly a million spins per second each puts ten million spins near
  one second. **Draw a box around the conclusion:** one process, one machine. No queue
  and no cluster.
- Then the number that shapes everything: workers can generate ten million events per
  second, and a browser chart wants about ten updates per second. Write both numbers
  and the gap between them. "Seven orders of magnitude. That gap is the design
  problem, and every architecture decision today is an answer to it."

### Beat — estimates as a design tool

The estimate exists to eliminate options rather than to be right to two significant
figures. One second on one machine means a distributed run is pure cost. Had
the number come back at four hours, the whole shape of the system would change. Doing
the arithmetic before drawing boxes is what keeps the boxes honest.

## 6:30–8:30 — The core idea: two lanes, opposite guarantees

**Scene:** WHITEBOARD. Draw two horizontal lanes across the board.

- **Lane 1, the math path.** Exact, lossless, integer counters. Nothing is ever
  dropped, because a dropped spin is a wrong answer.
- **Lane 2, the telemetry path.** Lossy, bounded, drop-oldest, paced by the browser.
  Dropping here costs a chart frame nobody was looking at.
- "Every box we draw next lives in one lane. Mix them and you get one of two failures:
  audit numbers corrupted by a dropped event, or a simulation stalled because a laptop
  fell behind on a chart."

### Beat — the same word meaning two things

Both lanes carry "the result of a spin", and that shared phrase is the trap. In lane 1
the result is a counter increment that must land. In lane 2 the result is a picture of
progress that may be skipped. Once the two are named separately, the guarantees stop
fighting, and each lane gets the machinery it needs instead of the strongest machinery
either one needs.

## 8:30–12:30 — High-level design

**Scene:** WHITEBOARD. Build the diagram incrementally, narrating each addition. Match
the flowchart in the companion article so the two line up.

Draw order:

1. The Core box. N workers inside it, each with its own RNG stream.
2. `RunTotals`: exact counters, updated by batched atomic adds. Lane 1.
3. A bounded channel out of the workers, into a single pump, into the run stream,
   into the SPA over server-sent events. Lane 2.
4. The Server box drawn around the pump, the stream, and the REST endpoints.
5. The SPA box with the chart in it.
6. REST arrows: `GET /api/run/limits` and `POST /api/run`.

Key lines to land while drawing:

- "Core is a class library with zero I/O. No ASP.NET, no logging, no file system. It
  returns values and writes to a channel the caller handed it. That is why the test
  suite can run ten million spins with no web server anywhere."
- "The SPA reads the RTP cap from the API rather than hardcoding it. The rule lives on
  the server and the client previews it, so there is one authority for it."
- "Nothing here is a message broker, a database, or a cache. Each one would be a box
  that has to be run, monitored, and explained. They are absent because the arithmetic
  in the last segment said they buy nothing."

> **Illustration (45 seconds, BROWSER).** The simulation lab page, `#/ch06`, Lab 2. Run it
> once at the default capacity. The spin total climbs by every spin while the delivered
> and dropped sample counts sit next to each other. Point at the two rates. "Same run,
> two lanes. The counter took every spin, and the drop count is how many chart samples
> lane 2 threw away." Cut back to the whiteboard.

## 12:30–15:30 — Deep dive 1: one validation boundary, and exact money

**Scene:** WHITEBOARD first, then RIDER on `SimulationConfig.TryCreate`.

- One construction path. `TryCreate(draft)` returns a config or a list of errors, never
  both and never neither. "If a `SimulationConfig` exists in this program, it is
  valid. The invariant rides on the type rather than on everyone remembering to call a
  validator."
- The cap is an integer comparison on basis points: 9900 passes, 9901 fails. There is
  no floating-point boundary to argue about at 99.00%.
- Money is a `long` count of millicents, and no conversion to `double` exists in the
  type. "The compiler enforces the money rule on every build."
- State the associativity point once, because episode 8 collects on it: integer
  addition gives the same total in any order, so an N-worker total equals a
  single-worker total bit for bit. Determinism becomes a property that can be asserted
  with `==`.
- Tease it: "Episode 2 is two files and about a hundred lines, and it is about making
  these two guarantees hold."

### Beat — why validation lives at one boundary

Scatter the same checks across constructors, endpoints, and the SPA and they drift.
One of them gets a new rule, another keeps the old one, and the disagreement surfaces
as a bug report six months later. A single boundary has a different failure mode: when
the rule is wrong it is wrong in one place, and fixing it is one edit. The SPA still
previews the cap for a good user experience, and it fetches the number rather than
owning it, so the preview cannot disagree with the enforcement.

## 15:30–18:00 — Deep dive 2: telemetry, and the decision record

**Scene:** WHITEBOARD, then RIDER on the ADR table in `docs/architecture.md` §6.

Two rules make a lossy lane safe:

1. **Absolute snapshots, never deltas.** A dropped delta corrupts every number after
   it. A dropped snapshot costs one frame, and the next snapshot is already correct.
   This single choice is what makes dropping acceptable at all.
2. **`TryWrite`, never `await`.** A worker that awaits a slow consumer has just given
   the browser a lever on simulation throughput. `TryWrite` returns false, the sample
   is skipped, and the worker keeps going.

Then the two transport questions that always come up: why no message broker, and why
plain server-sent events instead of a socket?

- Every event here is ephemeral, every consumer is in-process, and the real result of
  the run is a counter rather than a stream of messages.
- The browser-facing leg is one-way. The SPA starts and cancels a run over REST and
  reads everything else, so SSE is a `MapGet` writing `data:` lines, with reconnect
  the browser handles itself. A duplex transport would carry a hub abstraction and a
  connection lifecycle for a return channel nothing sends on.
- A broker adds serialization at megahertz rates to buy durability that nothing in
  this system asks for.
- Show the alternatives table in the ADR and read one row. "The decision is written
  down with what it costs, so the next person can reopen it with evidence."
- **The seam:** if cross-process ever matters, a broker slots in behind
  `ChannelWriter<TelemetrySample>` and the engine keeps its current shape.

> **Illustration (40 seconds, BROWSER).** Same page, Lab 2, second run with the channel
> capacity shrunk. The drop count climbs, and the exact final total counts every spin
> either way. "The chart lost frames and it is still correct, because the next
> message carries the current totals rather than a difference." Cut back.

## 18:00–20:00 — Deep dive 3: the analytic twin

**Scene:** WHITEBOARD. Two boxes, one data source, no shared code between them.

- Draw the simulator and the calculator side by side. Both read the same paytable and
  the same strips. Neither calls into the other.
- "The band on the chart comes from closed-form math. Exact expected RTP and exact
  standard deviation from the strips alone. The band is z times sigma over the square
  root of N, and it narrows as the run proceeds."
- "Two independent implementations of the same game. One counts probabilities, one
  plays spins. They share the paytable and the strips, and no payout code. Their
  agreement is the check."
- Then plant episode 8: "A third implementation joins in the last episode. Brute-force
  enumeration walks every possible outcome and referees the other two. Three
  implementations agreeing is a much stronger claim than one implementation passing
  its own tests."

### Beat — why independence is the point

Two implementations that share a helper share that helper's bugs. The value here comes
from the calculator and the simulator having no common code path at all: they agree
because the game is what it says it is, rather than because they inherited the same
mistake. Keeping them independent costs some duplicated understanding of the domain.

## 20:00–22:00 — The tests are part of the design

**Scene:** RIDER test runner.

The validation boundary from deep dive 1 is a claim, and `ConfigValidationTests` is
what turns it into a fact.

- **`Aggregate_9900_IsAccepted_BoundaryIsInclusive`** and
  **`Aggregate_9901_IsRejected_AndNeverClamped`** sit next to each other on purpose.
  One test pins the last legal value and the next pins the first illegal one. **Why
  this shape:** a boundary needs both sides asserted, because a test that only checks
  the accepted side passes just as happily against a rule that accepts everything.
- The rejection test also asserts the caller's draft is untouched. **Why that matters:**
  "loud failure" means the system refuses; it does not quietly adjust the request into
  something legal. A clamp would still produce a run, and the operator would never
  learn their configuration was wrong.
- The cap is asserted on integers, so the boundary case is decidable. There is no
  argument about whether 0.99 is representable, because the number under test is 9900.
- Run the class. Green. "One boundary, tested on both sides, in a file named for the
  boundary. That is the pattern every later episode reuses."

## 22:00–23:00 — Wrap and repo tour

**Scene:** RIDER, expanding the Core project tree slowly.

- Point at the namespaces: `Money`, `Reels`, `Paytables`, `Rtp`, `Simulation`, `Games`.
  "Domain names. No Services folder, no Helpers, no Managers. If a folder name would
  not appear in a conversation between two gaming mathematicians, it is the wrong
  name."
- Recap the requirements as a chain, one breath each: exact leads to millicents,
  deterministic leads to seeded per-worker streams and fixed quotas, fast leads to a
  lock-free hot path, loud leads to a single validation boundary.
- Point back at the route from the cold open, now that the boxes exist: each episode
  builds one of them, and the companion article carries the same map in writing along
  with what you can run at the end of each chapter.
- Next: "The two smallest files in the repo. About a hundred lines between them, one
  for money and one for randomness, and they are the reason everything after them gets
  to be ordinary."

---

## Recording notes

- Budget: roughly twenty minutes on the whiteboard and in Rider, under three in the
  browser. If a take runs long, browser time goes first.
- Strongest visuals in order: the two-lane split as it gets drawn, the seven-orders-of-
  magnitude gap written out as two numbers, and the drop counter climbing while the
  chart stays correct. Hold on each for a beat.
- Zoom hotkey belongs on: the `TryCreate` signature, the ADR alternatives table, and
  the two boundary test names. The whiteboard reads at normal size if the pen is thick
  enough.
- The high-level diagram from 8:30 gets reused in every later episode's cold open.
  Screenshot the finished board before wiping it.
- Running long? Compress deep dive 2 to a thirty-second point-and-read on the ADR
  table. Keep both deep dive 1 and deep dive 3 whole; they set up episodes 2 and 7.
- Do not open the browser cold on camera. A dead demo in episode 1 costs the series
  more than it costs this recording.
