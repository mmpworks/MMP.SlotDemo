# Episode 3 — Reels Are Strips, Not Dice

**Target:** 24–27 min. **Format:** create the file, paste the finished source, then
walk it. The typing is a jump cut; the walkthrough is the episode.
**Subject:** the engine. The companion site appears three times, for under three
minutes total, and only to make an engine claim visible.
**Companion article:** `docs/articles/03-reels-and-paylines.md`
**Companion site:** MMP.SlotDemo, branch `main`, page `#/ch03`
**Files created on camera:** `CSharp/src/MMP.SlotGame.Core/Reels/Symbol.cs`,
`StripReelSet.cs`, `Payline.cs`. **Shown, not created:**
`CSharp/src/MMP.SlotGame.Core/Paylines/LinePayEvaluator.cs`, `CSharp/games/orca-dive.json`.

> **Discipline note for this recording.** The labs illustrate; they do not carry the
> episode. If a beat can be made on the whiteboard or in Rider, make it there. Cut to
> the browser only where the engine's behavior is easier to see than to describe, and
> cut back inside a minute.

---

## Prep checklist

**Repo — the subject**
- [ ] Rider on `CSharp/MMP.SlotDemo.slnx`, tree expanded to `MMP.SlotGame.Core`
- [ ] `Reels/` folder present, all three target files moved aside so they get created
      on camera
- [ ] `Paylines/LinePayEvaluator.cs` open in a background tab for the flash-on-screen
      beat
- [ ] `CSharp/games/orca-dive.json` open in a background tab, scrolled to `reelStops`
- [ ] Scratch file ready for the wrong-model snippet
- [ ] Test runner loaded: `MultiRowWindowTests`, `PaylineGeometryFuzzTests`
- [ ] Clipboard manager staged with Block A, then Block B, then Block C
- [ ] Whiteboard or Excalidraw with a 10–12 symbol strip pre-drawn (the full 22 is too
      slow to draw on camera)

**Companion site — the illustration**
- [ ] `E:\dev\MMP.SlotDemo`, branch `main`
- [ ] `cd CSharp/web && npm run build`, then `dotnet run --project CSharp/src/SlotDemo.Server`
- [ ] `http://localhost:5090/#/ch03`, each lab run once so nothing pays first-request
      cost
- [ ] `logs/` cleared so the viewer starts empty

**OBS**
- [ ] `MMP.Media/generated/slotdemo-series/episodes/ep03/` staged locally as an OBS
      media source folder, with `strip/` and `symbols/` alongside it
- [ ] Scenes: `RIDER`, `BROWSER`, `WHITEBOARD`
- [ ] Zoom-to-mouse hotkey bound and tested at code-reading zoom
- [ ] Rider font sized for capture

---

## 0:00–1:15 — Cold open

**Scene:** RIDER, the Core project tree, the `Reels` folder absent.

> **Asset (FULL FRAME, open):** `slotdemo-series/episodes/ep03/ep03-title-plate.png` —
> hold three seconds, then cut to Rider on the first line.

- "Episode 2 gave us money that cannot round and randomness that can be replayed.
  Today we model the machine itself, and I want to start with a bug."
- "It is a model that looks right, passes every single-row test you can write, and
  quietly wrecks every statistic that involves two rows at once."
- "Three files today. One of them is a single line. Together they decide whether the
  variance math in episode 4 means anything."
- Set the format: "Same as last time. Each file goes in finished, then we walk it and I
  tell you why every line is the way it is."

## 1:15–5:30 — The wrong model, then the right one

**Scene:** RIDER scratch file, then WHITEBOARD.

Type the naive version live:

```csharp
// The intuitive model: a weighted die per visible cell. Wrong once a window exists.
Symbol Draw() => WeightedChoice(symbolWeights);
for (var cell = 0; cell < 3; cell++) window[cell] = Draw();
```

- "Seven appears on one of twenty-two stops, so the probability is one in twenty-two
  per cell, and each cell rolls independently. It feels obviously right, and it is how
  most people would start."

**Scene:** WHITEBOARD. Draw the strip, then slide a three-cell window along it.

> **Asset (FULL FRAME):** `slotdemo-series/episodes/ep03/ep03-3.5-strip-window-flowchart.png`
> — up as the board turns over, roughly fifteen seconds, then the strip still below.

> **Asset (FULL FRAME):** `slotdemo-series/strip/reel1-strip-4k.png` — reel 1's real
> 26-stop order; pan down it slowly while you describe the strip, and leave it on screen
> for the window slide.

> **Asset (FULL FRAME):** `slotdemo-series/strip/ep03-window-slide-1080p.mp4` — the
> window travelling down the strip and wrapping past the seam. Runs long by design, so
> start it on "its visible column shows that stop and the next two strip positions" and
> stay on it through the wrap; it holds two seconds on the wrap for the neighbor-rule
> line.

- "Each reel has its own ordered cyclic strip. A spin picks one stop on this reel. Its
  visible column shows that stop and the next two strip positions, wrapping at the end.
  One random number fills three visible symbol positions on this reel. A five-reel game repeats that process
  separately for each of its five strips."
- Then the neighbor rule: "If Seven's neighbor on this reel's strip is Bell, then Seven in
  row zero forces Bell in row one. Probability one. The die model says four in
  twenty-two." (Seven and Bell are both real symbols in `classic-three-reel.json`, and
  Bell's count on reel 1 there is 4 of 22, so the whiteboard numbers check against a
  file that is on disk. The strip art on screen is Orca Dive's real reel 1 — bridge the
  two out loud: "The strip you're looking at is the real five-reel game's first reel.
  The numbers I'm working are the small classic strip, so they fit on a whiteboard.
  Same rule on both.")
- Then the part that makes this dangerous: "Single-cell marginals agree between the two
  models. One in twenty-two either way. Every one-row test passes under both. What
  diverges is V-shaped lines, multi-line variance, and the confidence band: the numbers
  you check at the end of the project."

> **Asset (FULL FRAME):** `slotdemo-series/episodes/ep03/ep03-3.2-die-vs-strip-1080p.mp4`
> — cut on "single-cell marginals agree" and play it through: the two models agree on one
> cell, then rows 0 and 1 come up together and separate. Hold the final frame while you
> say the "every one-row test passes under both" line.
- "This was the first thing the design review caught, and the type we are about to write
  is what it turned into."
- Close the segment with the industry tell: "Par sheets publish strip layouts rather
  than symbol frequencies, and this is the reason."

## 5:30–6:15 — Create the first file

**Scene:** RIDER.

- Right-click `MMP.SlotGame.Core` → new directory `Reels` → new file.
- **Path on screen and said out loud:** `CSharp/src/MMP.SlotGame.Core/Reels/Symbol.cs`
- Paste **Block A**. "One line of code and eight times that much comment. Let's read the
  comment."

### Block A — `CSharp/src/MMP.SlotGame.Core/Reels/Symbol.cs`

```csharp
namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// One reel symbol. The stock presets leave both flags clear, which keeps the preset
/// pipeline's analytic math a closed form and its features independent RTP terms: wild
/// substitution makes that closed form non-linear (best-line max), and scatter-count
/// triggering couples features to the window. Loaded game definitions use both — Orca Dive
/// ships a wild and a scatter — and <see cref="MMP.SlotGame.Core.Games.GameAnalyzer"/>
/// prices those by enumeration.
/// </summary>
public readonly record struct Symbol(byte Id, string Name, bool IsWild = false, bool IsScatter = false);
```

## 6:15–8:00 — Walk `Symbol`

### Beat 1 — the smallest type in the repo, and why it is a struct

Two payload fields and two flags. `byte Id` rather than an enum or a string, because
the hot loop compares ids millions of times per second and a byte comparison is one
instruction. `Name` rides along for humans reading output; the engine never branches on
it.

`readonly record struct` again, for the same reasons as `Millicents`: value semantics
where identity would be meaningless, no allocation in a loop that runs constantly, and
generated equality nobody has to maintain.

### Beat 2 — two flags the presets leave alone, and why they ship anyway

`IsWild` and `IsScatter` default to false, and every stock preset leaves them there. On
the preset path they look like dead weight until you read what the comment says they buy.

- Both flags cost one bit of a struct that is already padded, so the price is nothing.
- Adding a field later is a shape change that ripples through every JSON document,
  every builder, and every test fixture. Adding it now costs one line.
- The comment names who does use them: a loaded game definition. Orca Dive ships a wild
  and a scatter, and it arrives in episode 7 through this door.

> **Asset (FULL FRAME):** `slotdemo-series/symbols/_contact-sheet-FINAL.png` — the whole
> Orca Dive symbol set, up while you name the wild and the scatter; point at the orca,
> which is the wild. Twenty seconds, then back to Rider.

**The line to say:** "The presets keep the flags at false so their math stays a closed
form. Episode 7 is the game that uses them."

### Beat 3 — the deferral is a math decision

Read the reason out of the comment.

- A wild substitutes for other symbols, so a line's payout becomes the maximum over
  several interpretations. Maximum is not linear, and the closed-form expected value in
  episode 4 stops being a plain sum.
- A scatter triggers on a count anywhere in the window, which couples a feature to the
  window rather than leaving it an independent term.
- Keeping both off the preset strips is what lets the preset pipeline's analytic math
  stay a closed form. The games that want wilds and scatters get priced a different way:
  `GameAnalyzer` enumerates them, which is episode 5's machinery. "Two pricing paths, and
  the comment says which game takes which."

## 8:00–9:00 — Create the second file

**Scene:** RIDER.

- New file in the same folder. **Path on screen:**
  `CSharp/src/MMP.SlotGame.Core/Reels/StripReelSet.cs`
- Paste **Block B**. Pause on the constructor, then trace one stop through `SymbolAt`.

### Block B — `CSharp/src/MMP.SlotGame.Core/Reels/StripReelSet.cs`

```csharp
using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// Each reel owns one ordered cyclic strip. A spin chooses one stop on each reel. The visible
/// symbol positions for that reel then read neighboring locations from that same strip: s, s+1, and so on,
/// wrapping at the end. Separate reels choose their stops independently.
///
/// Symbol counts can give the chance for one cell, but they do not record which symbols are
/// neighbors. Calculations that inspect two visible positions on the same reel need the ordered strip.
///
/// Reel count, per-reel stop count and window height all arrive as arguments. Strips of
/// differing lengths on the same machine are normal; Orca Dive, the fictional game this
/// project ships, has 26/29/26/29/26 stops, so each reel's length is read separately.
/// </summary>
public sealed class StripReelSet
{
    /// <summary>The window height every stock preset uses. A game definition may declare another.</summary>
    public const int DefaultRows = 3;

    /// <summary>The shortest window this version supports and tests.</summary>
    public const int MinRows = 3;

    /// <summary>
    /// The tallest window this version supports and tests. Raising the limit requires
    /// reviewing generated payline shapes and tests for the new height.
    /// </summary>
    public const int MaxRows = 5;

    private readonly Symbol[][] _strips;

    /// <summary>
    /// Copies one ordered symbol list per reel. Inner lists may have different lengths.
    /// Create a new reel set to change strips for a later run; this snapshot never changes.
    /// </summary>
    public StripReelSet(IReadOnlyList<IReadOnlyList<Symbol>> strips, int rows = DefaultRows)
    {
        ArgumentNullException.ThrowIfNull(strips);
        if (strips.Count < 1)
            throw new ArgumentException("A reel set needs at least one reel.", nameof(strips));
        if (rows < MinRows || rows > MaxRows)
            throw new ArgumentOutOfRangeException(
                nameof(rows), rows, $"A window must have {MinRows}..{MaxRows} rows.");

        for (var reel = 0; reel < strips.Count; reel++)
        {
            if (strips[reel] is null || strips[reel].Count == 0)
                throw new ArgumentException($"Reel {reel + 1} has no stops.", nameof(strips));
        }

        // The reel set is shared by workers and analytic code. Copy the caller's arrays so
        // later mutations cannot change a game that is already running.
        _strips = strips.Select(strip => strip.ToArray()).ToArray();
        Rows = rows;
    }

    public int ReelCount => _strips.Length;

    /// <summary>Number of visible symbol positions in each reel's column. Also the screen-row count. The window is laid out [reel * Rows + row].</summary>
    public int Rows { get; }

    public int WindowSize => ReelCount * Rows;

    public int StopCount(int reel) => _strips[reel].Length;

    public ReadOnlySpan<Symbol> Strip(int reel) => _strips[reel];

    /// <summary>
    /// Marginal probability that a given window row on <paramref name="reel"/> shows
    /// <paramref name="symbolId"/>. By cyclicity every row has the same marginal:
    /// count-on-strip / S. Exact rational, exposed as double for the analytic layer.
    /// </summary>
    public double ProbabilityOf(int reel, byte symbolId)
    {
        var strip = _strips[reel];
        var count = 0;
        foreach (var s in strip)
        {
            if (s.Id == symbolId) count++;
        }
        return (double)count / strip.Length;
    }

    /// <summary>
    /// Joint probability that on <paramref name="reel"/> the window shows
    /// <paramref name="aId"/> at <paramref name="rowA"/> AND <paramref name="bId"/> at
    /// <paramref name="rowB"/>. Enumerates all S stops; the count is exact and the
    /// returned <see cref="double"/> is its floating-point ratio.
    /// </summary>
    public double JointProbabilityOf(int reel, int rowA, byte aId, int rowB, byte bId)
    {
        var strip = _strips[reel];
        var n = strip.Length;
        var count = 0;
        for (var stop = 0; stop < n; stop++)
        {
            if (strip[(stop + rowA) % n].Id == aId && strip[(stop + rowB) % n].Id == bId)
                count++;
        }
        return (double)count / n;
    }

    /// <summary>Draws one stop per reel, then fills that reel's column from neighboring strip positions.</summary>
    public void DrawWindow(ref SpinRng rng, Span<Symbol> window)
    {
        // window layout: [reel * Rows + row]
        for (var reel = 0; reel < _strips.Length; reel++)
        {
            var strip = _strips[reel];
            var stop = rng.NextInt(strip.Length);
            for (var row = 0; row < Rows; row++)
            {
                var pos = (stop + row) % strip.Length;
                window[reel * Rows + row] = strip[pos];
            }
        }
    }

    /// <summary>The symbol shown at (reel, row) for a given stop, wrapping cyclically.</summary>
    public Symbol At(int reel, int stop, int row)
    {
        var strip = _strips[reel];
        return strip[(stop + row) % strip.Length];
    }
}
```

## 9:00–17:00 — Walk `StripReelSet`

**Scene:** RIDER throughout. Zoom on each region as it comes up.

### Beat 4 — geometry is data

The constructor accepts read-only lists and copies them into a jagged `Symbol[][]`.
**Jump to `orca-dive.json`** and point at
`"reelStops": [26, 29, 26, 29, 26]`.

> **Asset (OVERLAY):** `slotdemo-series/episodes/ep03/ep03-3.6-reel1-strip.png` — a
> quarter-frame inset beside the `reelStops` line while you say "each inner list is one
> reel strip", so the 26 on screen has the strip it belongs to next to it. Out before the
> par-sheet line.

- Each inner list is one reel strip, so real machines may carry unequal strips.
- The read-only parameter says the constructor borrows the caller's data. The private
  arrays are the snapshot owned by the reel set.
- "Had we stored a single stops-per-reel field, this file would be unloadable, and we
  would have discovered that the week we tried to validate against a published par
  sheet."
- Reel count, per-reel length, and window height all arrive as arguments. Nothing in
  this type knows the number three. `Rows` is read from the definition and flows
  through every method.

### Beat 4A — replacing geometry between runs

**Scene:** BROWSER, chapter 3 Lab 3.

- Run the 26-stop snapshot twice with one seed. The visible symbols repeat.
- Show the 36-stop snapshot built from a different `Symbol[]`.
- "The next run receives a new reel-set object. We never edit the arrays under workers
  that are already spinning."
- Tie the decision to CUPID: composable inputs, one geometry job, predictable snapshots,
  idiomatic read-only boundaries, and reel-based domain names.

A gaming mathematician would say "each reel has its own strip", and the type says it in
the same shape.

### Beat 5 — the constructor is the boundary

Four guards, each with a message a human can act on.

- Null strips, zero reels, a row count outside `MinRows`..`MaxRows`, and any empty
  reel. The empty-reel message names which reel, one-based, because that is how the
  par sheet numbers them.
- The bounds are named constants rather than literals, and `MaxRows` carries a comment
  saying what a maintainer must review before raising it. "The constant is documentation
  the compiler keeps honest."
- Validate loudly at the boundary; stay silent afterwards. Every method below the
  constructor assumes a valid reel set, and none of them re-check.

### Beat 6 — the defensive copy

Read the comment aloud, then the line: `strips.Select(strip => strip.ToArray()).ToArray()`.

- The reel set is handed to sixteen workers and to the analytic calculator at the same
  time. A caller who kept a reference to the input array could change a running game
  from underneath all of them.
- Copying once at construction costs microseconds and buys an object that is safe to
  share without a lock, for the whole life of the run.
- **The immutability rule at a seam:** the type takes ownership by copying, and
  afterwards there is nothing to synchronize.

### Beat 7 — `ProbabilityOf`, the marginal

Count the symbol on the strip, divide by the strip length. Three lines.

- Cyclicity is what makes this so simple: every window row sees every stop equally
  often, so all rows share one marginal.
- The count is exact; only the division moves to `double`, and only because the analytic
  layer wants a real number.
- "This feeds expected value in episode 4. Every line's contribution to RTP starts
  here."

### Beat 8 — `JointProbabilityOf`, the method the die model cannot have

Slow down here.

- It walks all S stops and counts how many put symbol `a` at `rowA` and symbol `b` at
  `rowB` at the same time. Exhaustive, so the count is exact rather than sampled.
- "The weighted-die model has no way to express this question. In that model rows are
  independent, so the joint probability is forced to be the product of the marginals.
  Here it is whatever the strip layout makes it, and those two numbers differ for every
  real reel."
- The strongest case is on Orca Dive's own reel 1, so say it with the strip on screen:
  "Green7 sits at stops 3, 16 and 24. Its gaps are 13, 8 and 5, so no window ever shows
  two Green7s. The die model prices two Green7s in one window at 1.33 percent, one spin
  in seventy-five. The machine's answer is zero. Same symbol counts, same marginal, and
  one model treats an impossible outcome as a paying one. Every single-row test you can
  write passes under both models."
- Where it gets spent: variance. Correlated rows change how much a run wanders even
  when they leave the mean untouched. "The confidence band in episode 4 is built out of
  this method."

> **Illustration (50 seconds, BROWSER).** Chapter 3 page, reel lab. Load a strip and
> pick two rows. The lab shows the die model's prediction beside the enumerated joint
> probability from the engine's own `JointProbabilityOf`, running server-side. The
> marginals match to the digit; the joint numbers separate. Then it runs both models
> to a variance estimate and the bands come out visibly different widths. "Same mean,
> different spread even though the symbol counts match." Cut back.

### Beat 9 — `DrawWindow`, the hot path

The method that runs five times per spin, ten million spins per run.

- The caller owns the `Span<Symbol>`. The engine allocates one window buffer per worker
  and reuses it forever, so this method allocates nothing at all.
- `ref SpinRng` — rule R3 from episode 2, visible in the signature. Anything that can
  consume randomness says so where you can grep for it.
- One `NextInt` per reel, then that reel's visible symbol positions read neighboring locations from
  its own strip. Five reels use five random numbers and produce fifteen visible cells.
- The layout comment matters more than it looks: `window[reel * Rows + row]` is the
  contract every consumer depends on, and it appears here and in the doc comment on
  `Rows`. One authority, stated twice on the same screen.

### Beat 10 — one type, two audiences

The DRY beat.

- This one class draws windows for the simulator and reports probabilities for the
  calculator. They read the same object.
- "Two implementations proving each other only works if they cannot disagree about the
  board. They cannot drift on geometry here, because there is nothing between them to
  drift."
- Compare against the alternative: a separate `ReelMath` class holding its own
  copy of the strips would be tidier by one measure, and would introduce the failure
  mode episode 8 exists to catch.

## 17:00–17:45 — Create the third file

**Scene:** RIDER.

- New file. **Path on screen:** `CSharp/src/MMP.SlotGame.Core/Reels/Payline.cs`
- Paste **Block C**.

### Block C — `CSharp/src/MMP.SlotGame.Core/Reels/Payline.cs`

```csharp
namespace MMP.SlotGame.Core.Reels;

/// <summary>
/// One payline loaded from a game definition or supplied by a built-in preset.
/// <see cref="Rows"/> contains one visible-position index for each reel.
/// </summary>
public sealed record Payline
{
    public Payline(string name, IReadOnlyList<int> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rows);
        Name = name;
        Rows = Array.AsReadOnly([.. rows]);
    }

    public string Name { get; }

    /// <summary>
    /// A construction-time copy of the visible position selected on each reel.
    /// For example, [0, 1, 2] runs from the top of reel 1 through the middle of
    /// reel 2 to the bottom of reel 3 in a three-position window.
    /// </summary>
    public IReadOnlyList<int> Rows { get; }
}
```

**Open next:** `StandardPaylines.cs`. Explain that it owns Center, Top, Bottom, V,
Hat, and zigzag recipes only for demo defaults. Then open `orca-dive.json` and show
that its `paylines` arrays go straight through `GameDefinitionBuilder` into the same
`Payline` record. Repeat the comparison with `StandardReelPresets.cs` and the JSON
`reels` arrays. A custom PAR path and unequal strip lengths do not require evaluator
changes.

## 17:45–22:30 — Walk `Payline` and the default catalog

### Beat 11 — a payline is data, and it is frozen at construction

Name plus one row index per reel. Center on a five-reel three-row machine is
`1,1,1,1,1`; a V is `0,1,2,1,0`.

- **Whiteboard:** draw a 5×3 grid and trace Center, Top, V, and ZigTop quickly.

> **Asset (FULL FRAME):** `slotdemo-series/episodes/ep03/ep03-3.3-payline-shapes-1080p.mp4`
> — all nine default lines traced one at a time over the 5×3 grid. Run it under the two
> bullets below rather than drawing all nine by hand; it ends on a readable frame with
> every line on the grid.
- The constructor takes `IReadOnlyList<int>` and immediately copies it into a read-only
  wrapper. Same defensive-copy move as the reel set, same reason: this object is read
  concurrently by every worker for the life of the run.
- `IReadOnlyList<int>` as the parameter type says "I will read this", and the copy says
  "and I will not depend on you keeping it still".

### Beat 12 — default shapes are computed, configured shapes are data

Open `StandardPaylines.cs`. `Repeat`, `Bend`, and `Alternate` build the nine demo
lines from three position values. Then open the JSON `paylines` list.

- Say the boundary plainly: "The catalog offers defaults. It does not define what a
  valid game is. A PAR transcription supplies its own path and builds the same
  `Payline` record."

- `topRow`, `bottomRow`, and `middleRow` are derived from the `rows` argument on every
  call, which is why a four-row or five-row preset gets correct shapes without a code
  change.
- `Bend` interpolates between the two rows it is handed, so a four-reel V comes out
  proportioned without anyone drawing it by hand.
- Compare the alternative out loud: a hand-written table of nine shapes for three
  window heights is twenty-seven arrays to keep consistent, and the day someone adds a
  height it becomes thirty-six.

### Beat 13 — the paragraph about even row counts

Read the doc comment's last paragraph aloud, then show the four-row result.

> **Asset (FULL FRAME):** `slotdemo-series/episodes/ep03/ep03-3.4-even-row-asymmetry.png`
> — the same ZigTop line on a 3-row grid and a 4-row grid, side by side. Up on "show the
> four-row result" and held through the three bullets; the swing rows are what you point
> at on the second bullet.

- With an odd row count, `rows / 2` is the true center and everything is symmetric.
- With an even count there is no center, integer division rounds toward the bottom
  half, and the zig-zags come out asymmetric: two rows of swing on one side, one on the
  other.
- The article names what rounding the other way would do: put the shorter swing on the
  top side instead.
- **The point:** "There was no symmetric answer available. The code picked one and wrote
  down what it costs. That paragraph is the difference between a documented convention
  and a bug somebody finds in a year."

### Beat 14 — the catalog throw lists what the catalog offers

`lineCount switch` with 5, 9, and a throw naming both supported values.

- Same failure philosophy as the config boundary in episode 1 and the scaled-multiply
  refusal in episode 2. `StandardPaylines` refuses a default set it does not contain.
  This does not reject a PAR-defined line; that line bypasses the catalog.
- Returning a partial set for an unsupported count would be the quiet failure: the run
  finishes, the RTP is wrong by the missing lines, and nothing says so.

### Beat 15 — the seam, stated and left alone

`StandardPaylines.Center` exists because a classic three-reel demo has exactly one
line, and expressing that through `For` would mean supporting a line count of 1 in a
method built around a nine-line convention.

- Ways-pays games and cascading reels are different implementations of the same
  contract: a window goes in, money comes out.
- The engine takes evaluation as a delegate, so adding one is a new class plus a
  registration, with no edits to anything here.
- "No ways-pays evaluator exists in this repo, because nobody has asked for one. The
  delegate is what makes adding one cheap when somebody does."

> **Illustration (45 seconds, BROWSER).** Chapter 3 page, payline lab. Set the window
> height to 3, 4, and then 5, and the nine generated shapes redraw over the grid from
> the engine's own `StandardPaylines.For` running server-side. Stop on 4 rows and point at the
> asymmetric zig-zags. "The comment said this would happen. Here it is." Cut back.

## 22:30–23:15 — Flash the evaluator

**Scene:** RIDER, `Paylines/LinePayEvaluator.cs` open, no walkthrough.

- Show it on screen for twenty seconds and describe the shape only: take the first
  symbol on a line, extend the run while ids match, stop at the first break, and pay
  when the run reaches three.
- "Lines pay independently and add up. That is the standard industry rule, and it is
  why next episode's expected value is a plain sum over lines. Overlap between lines
  changes the variance; it never changes the mean."
- Name the collaborators so the separation is visible: `StripReelSet` draws, `Payline`
  describes geometry, the evaluator prices, and the engine schedules. "Four types, four
  jobs."

## 23:15–25:30 — The tests are part of the design

**Scene:** RIDER test runner, then TERMINAL.

Two suites hold this episode's claims.

- **`MultiRowWindowTests.ReelSet_CopiesInputStrips_AndWrapsRowsOnShortStrips`** asserts
  both halves of beat 6 and beat 9 in one test: mutate the caller's array after
  construction and the reel set is unchanged, and read a window off a strip shorter than
  the window height to prove the wrap is cyclic rather than clamped. **Why this shape:**
  the copy and the wrap are the two ways this type can silently be wrong, and neither
  one shows up in a normal run.
- **`SyntheticGame_AnalyticNumbers_MatchTheHandDerivedValues`** runs across 3, 4, and 5
  row windows against a fixture built so every probability is a hand-derivable rational
  with a power-of-two denominator. **Why that fixture:** it makes the assertions hard
  equalities rather than tolerance bands. "A test with a tolerance in it is a test that
  can be tuned until it passes. This one cannot."
- **`SyntheticGame_SimulatedRtp_ConvergesOnTheAnalyticValue`** takes the same fixture
  the other way round: run the simulator and check it lands on the number the analytic
  path computed. That is the two-implementation agreement from episode 1, at the scale
  of one test.
- **`WindowRowsOutsideBounds_IsRejectedNamingTheBounds`** checks that the rejection
  message names the bounds. **Why assert on the message:** the error text is part of the
  interface, and an error that says "invalid" sends the next person reading source.
- **`PaylineGeometryFuzzTests.For_EveryGeneratedRow_StaysInsideTheWindow`** fuzzes reel
  counts and window heights and asserts every generated index is in range. **Why fuzz
  here:** `Bend` does floating-point interpolation and rounding, and the stock presets
  only exercise a few of its inputs.
- **`NineLineSet_VAndHatLines_TouchBothWindowEdgesAtTheMiddleReel`** pins the doc
  comment's own claim across random geometry. "The comment claims it; the test checks
  it."
- **`For_UnsupportedLineCount_ThrowsRatherThanReturningAPartialSet`** is the negative
  space again. Beat 14 said a partial set would be the quiet failure; this is what
  keeps it from happening.
- Run both classes. Green.

## 25:30–26:15 — Wrap

- Three files. A symbol that is one line and two deferred features, a reel set that is
  the strip-adjacency model, and paylines that are computed geometry with a written note
  about rounding.
- The three claims to carry forward: reels are ordered cyclic strips, marginals feed
  expected value, and strip enumeration feeds variance.
- Next: "The par sheet as code. Hitting any RTP target with one closed-form scalar, and
  the variance math where the correlations we found today come due."

---

## Recording notes

- Engine-to-browser budget: roughly twenty-three minutes in Rider and on the
  whiteboard, under three in the browser. If a take runs long, browser time goes first.
- Strongest visuals in order: the whiteboard forced-neighbor moment (Seven then Bell,
  probability one), the ragged `reelStops` line in `orca-dive.json`, and the payline lab
  redrawing shapes as the window height changes. Rehearse the first one; it is the hook.
- Zoom hotkey belongs on: the `reelStops` array, the `JointProbabilityOf` loop body, the
  defensive-copy line, and the even-row-count paragraph in the `Payline` doc comment.
- The three paste blocks are the initial-system files verbatim (the state before the
  episode-9 optimization branch; episode 9 shows the optimized versions side by side).
  If a paste lands wrong, cut and re-paste rather than hand-fixing: the file has to
  match that state.
- Running long? Compress beat 12 to one sentence and drop the evaluator flash. Keep
  beat 8 whole, keep beat 13 whole, and keep the test section whole.
- The companion site runs the engine's own reel code server-side, so if a lab ever
  disagrees with the walkthrough, the lab is reporting a real change in the repo.
