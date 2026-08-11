# Chapter 1 as an interactive page — "The Blueprint"

Chapter 1 is the system-design episode, so its page should be the thing every other
chapter page points back to: a live map of the machine where each part can be
opened, poked, and followed forward into the episode that builds it.

The organizing idea: **the blueprint is the table of contents.** A viewer who lands
on chapter 5 should be able to click one chip and see where the simulation engine
sits relative to money, reels, and telemetry. A viewer who starts at chapter 1
should be able to click any box and land in the chapter that builds it.

## The page, top to bottom

### 1. The map

A hand-authored SVG of the system, matching the whiteboard diagram from the
episode. Nodes, each clickable:

| Node | Lane | Opens into |
|---|---|---|
| Game definition | data | Chapter 6 |
| Money (`Millicents`) | exact | Chapter 2 |
| RNG (`SpinRng`) | exact | Chapter 2 |
| Reel strips + paylines | exact | Chapter 3 |
| Paytable + solver | exact | Chapter 4 |
| Simulation engine (N workers) | exact | Chapter 5 |
| Run totals | exact | Chapter 5 |
| Telemetry channel → pump → hub | lossy | Chapter 5 |
| Analytic twin (RTP + sigma) | exact | Chapter 4 |
| Verdict: inside the band | proof | Chapter 7 |

Two lane colors, one toggle: **Exact path** and **Telemetry path**. Flipping it
dims everything in the other lane. That single control carries the episode's core
argument — one path never drops a value, the other is allowed to drop everything
but the latest — without a paragraph of explanation.

### 2. The spin walk

A **Trace one spin** button animates a single token through the map: definition →
RNG draw → stop indices → window → payline walk → pay in millicents → totals, with
a branch peeling off into the telemetry lane and arriving at the chart as a
sampled snapshot. Step controls (next / back), so it can be paused on any hop
during recording.

Each hop writes a Herald line, so the log viewer below fills in as the token
moves. The animation and the log are the same event stream, which is the point:
the map is not a drawing of the system, it is a view of it.

### 3. The parts drawer

Clicking a node opens a panel, and every panel has the same four slots:

- **What it is** — two sentences, high-school reading level.
- **The invariant it carries** — M1, M2, R3, and the rest, one line each.
- **A live probe** — the smallest possible interaction that proves the node is
  real. The money node converts credits to millicents. The reel node spins one
  strip and shows the stop. The telemetry node drops samples on purpose to show
  what "lossy" buys. Each probe calls the same chapter endpoint that chapter's own
  page uses, so there is one implementation, not two.
- **Go deeper →** — the link into that chapter's page, and back.

### 4. The symbol tray

Slot icons are the part everybody recognizes, so give them a home on the design
page rather than saving them for chapter 3: the game's symbol set rendered as
tiles with their strip counts. Hovering a symbol highlights every strip position
it occupies. It costs little and it turns an abstract map into something that
looks like a slot machine.

### 5. Back-of-envelope, live

The episode does arithmetic on camera: spin cost, cores, spins per second, the
seven-orders-of-magnitude gap between what workers emit and what a browser can
draw. Put those numbers in sliders. Move cores from 1 to 32 and the "10M spins in
_" readout moves with it; move the emit rate up and the telemetry lane visibly
saturates and starts dropping. The conclusion box — *one process, one machine* —
recomputes rather than being asserted.

## Tying chapters back to the blueprint

Two mechanics, both cheap:

1. **Deep links into the map.** `#/ch01?focus=money` opens the page with the money
   node selected and its panel open. Every chapter page carries a "where this sits"
   chip in its header that links to its own focus URL.
2. **A shared registry entry.** The node table above lives in one file next to the
   chapter registry, so a node knows its chapter and a chapter knows its node.
   Adding chapter 5's lab wires both directions at once.

The result is a site with a spine: the blueprint names the parts, each chapter
builds one, and every chapter can point at where it fits without duplicating the
diagram.

## Build order

1. Static SVG map with clickable nodes and the lane toggle (no animation yet).
2. The parts drawer with prose and deep links; probes stubbed.
3. Probes wired to chapter endpoints as those chapters land — chapter 2's are
   already live, so the money and RNG nodes can be real from day one.
4. The spin walk, once chapters 3 and 4 exist to supply real strips and a paytable.
5. Back-of-envelope sliders.

Steps 1 and 2 make the page useful on their own, which means chapter 1 does not
have to wait on the chapters it points at.
