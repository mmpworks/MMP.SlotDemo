# Reels Are Strips, Not Dice: Modeling Slot Geometry

*Part 3 builds the reels, the visible window, and the paylines. The main problem is
easy to miss: a model can get the chance of one symbol right while getting the
chance of two neighboring symbols wrong.*

Suppose Seven appears once on a 22-stop reel. Its chance of landing in one chosen
position is `1/22`. We could reproduce that number by making a separate weighted
choice for every visible position, but then we would lose the order of the reel.
On this machine, the symbols visible above and below Seven are its neighbors on the
same strip.

Slot games can use many layouts, including virtual reels, independently generated
positions, expanding reels, and cascading grids. This engine models independently
stopped reels with ordered cyclic strips. Each reel stops once, and the window shows
neighboring symbols from that strip.

These terms keep the geometry precise:

| Term | Meaning |
|---|---|
| **Reel strip** | The ordered loop of symbols belonging to one reel |
| **Stop** | The numbered position where that reel lands |
| **Window** | The grid of symbols visible after all reels stop. Each column belongs to one reel. |
| **Visible symbol position** | One top, middle, or bottom location in a reel's visible column |
| **Screen row** | A horizontal band across the full window, such as the top row across five reels |
| **Payline** | A path across the window. It chooses one visible symbol position from each reel. |

### Check your understanding

A cherry appears twice on a 20-stop reel. What is the chance that a chosen row shows a
cherry after the reel stops?

<details><summary>Answer</summary>

`2 ÷ 20 = 0.10`, or 10%. The positions of those two cherries also affect which symbols can
appear directly above and below them.

</details>

## What a strip-based reel is

An **ordered cyclic strip** is a fixed list of symbols joined end to end. If a reel
has 22 stops, its positions are numbered 0 through 21. A spin chooses one of those
numbers with equal probability. For a 3-position window, a stop at `s` displays
`s`, `s+1`, and `s+2`. The count wraps to position 0 when it reaches the end.

> Picture 22 charms fixed to a bracelet. A small frame reveals three charms at a
> time. Moving the bracelet changes all three visible charms together because their
> order on the bracelet never changes.

<!-- EXPORT: render this Mermaid block to PNG before publishing -->
```mermaid
flowchart TB
    subgraph strip["One reel: 22-stop cyclic strip"]
        direction LR
        p0["…"] --- p1["Bell"] --- p2["Seven"] --- p3["Blank"] --- p4["Cherry"] --- p5["Bar"] --- p6["…"]
    end
    strip -->|"draw one stop s per reel"| win
    subgraph win["3-row window on this reel"]
        r0["row 0: strip[s]"]
        r1["row 1: strip[s+1 mod 22]"]
        r2["row 2: strip[s+2 mod 22]"]
    end
```

One random number selects the stop. That stop fills the reel's entire visible column:
position 0 reads `strip[s]`, position 1 reads the next symbol, and position 2 reads
the symbol after that.

The neighbor relationship stays within one reel. A five-reel game has five strips
and draws five stop numbers, one per reel. A screen row crosses those five separate
reels.

Suppose `Seven` is followed by `Blank` on one strip. Whenever Seven appears in
visible position 0, Blank appears in position 1. That pair is fixed by the strip.
Separate random choices for the two positions could produce combinations that the
strip can never show.

The error hides because both models still give Seven a `1/22` chance in one chosen
position. They disagree about which symbols can appear with Seven elsewhere in the
same reel column. One strip stop fixes the whole column.

A payline reads one visible position from each reel. A V-shaped line changes position
as it crosses the window, but it still reads exactly one symbol from each reel.

Strip order matters when two paylines use different positions on the same reel, or
when a scatter rule checks the reel's full visible column. Symbol counts alone cannot
answer either question.

`StripReelSet` preserves that order.

## StripReelSet

```csharp
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

    /// <summary>The tallest window this engine currently supports and tests.</summary>
    public const int MaxRows = 5;

    private readonly Symbol[][] _strips;
    private readonly Symbol[][] _drawStrips;
    private readonly ulong[] _rngRanges;
    private readonly ulong[] _rngThresholds;

    public StripReelSet(IReadOnlyList<IReadOnlyList<Symbol>> strips, int rows = DefaultRows)
    {
        if (rows < MinRows || rows > MaxRows)
            throw new ArgumentOutOfRangeException(
                nameof(rows), rows, $"A window must have {MinRows}..{MaxRows} rows.");
        // … reel and strip validation …
        // Copy the caller's arrays so later mutations cannot change an active game.
        _strips = strips.Select(strip => strip.ToArray()).ToArray();
        Rows = rows;

        // Append the first Rows - 1 symbols once. DrawWindow can then read across
        // the end of a reel without calculating a remainder for every visible cell.
        _drawStrips = new Symbol[_strips.Length][];
        _rngRanges = new ulong[_strips.Length];
        _rngThresholds = new ulong[_strips.Length];
        for (var reel = 0; reel < _strips.Length; reel++)
        {
            var strip = _strips[reel];
            var range = (ulong)strip.Length;
            _rngRanges[reel] = range;
            _rngThresholds[reel] = unchecked(0UL - range) % range;

            var drawStrip = new Symbol[strip.Length + Rows - 1];
            strip.CopyTo(drawStrip, 0);
            for (var extra = 0; extra < Rows - 1; extra++)
                drawStrip[strip.Length + extra] = strip[extra % strip.Length];
            _drawStrips[reel] = drawStrip;
        }
    }

    public int ReelCount => _strips.Length;
    public int Rows { get; }
    public int WindowSize => ReelCount * Rows;
    public int StopCount(int reel) => _strips[reel].Length;
    public ReadOnlySpan<Symbol> Strip(int reel) => _strips[reel];

    /// <summary>Marginal P(symbol at any row of this reel) = count-on-strip / S.</summary>
    public double ProbabilityOf(int reel, byte symbolId)
    {
        var strip = _strips[reel];
        var count = 0;
        foreach (var s in strip)
            if (s.Id == symbolId) count++;
        return (double)count / strip.Length;
    }

    /// <summary>Joint P(rowA shows a AND rowB shows b) on one reel, found by
    /// enumerating all S stops. This is the method the weighted-die model lacks.</summary>
    public double JointProbabilityOf(int reel, int rowA, byte aId, int rowB, byte bId)
    {
        var strip = _strips[reel];
        var n = strip.Length;
        var count = 0;
        for (var stop = 0; stop < n; stop++)
            if (strip[(stop + rowA) % n].Id == aId && strip[(stop + rowB) % n].Id == bId)
                count++;
        return (double)count / n;
    }

    /// <summary>Draws one stop per reel, then fills its column from neighboring strip positions.</summary>
    public void DrawWindow(ref SpinRng rng, Span<Symbol> window)
    {
        for (var reel = 0; reel < _strips.Length; reel++)
        {
            var stop = rng.NextInt(_rngRanges[reel], _rngThresholds[reel]);
            var drawStrip = _drawStrips[reel];
            var windowOffset = reel * Rows;
            for (var row = 0; row < Rows; row++)
                window[windowOffset + row] = drawStrip[stop + row];
        }
    }

    /// <summary>The symbol shown at (reel, row) for a given stop, wrapping cyclically.</summary>
    public Symbol At(int reel, int stop, int row) => _strips[reel][(stop + row) % _strips[reel].Length];
}
```

The constructor and the draw loop divide the work between setup and play.

Reel count, strip length, and window height arrive as constructor arguments. Orca
Dive uses 26/29/26/29/26 stops, so `StopCount(reel)` asks for a specific reel.
The same type also accepts 4- and 5-position windows.

The constructor's outer list contains the reels. Each inner list is one ordered
strip, and the inner lists may have different lengths. A caller can supply 26 stops
for reel 1 and 36 for reel 2.

The constructor copies those lists into a jagged `Symbol[][]`. Later changes to the
caller's arrays cannot alter an active game. A new set of strips requires a new
`StripReelSet` for the next run.

```csharp
IReadOnlyList<Symbol> reel26 = Build26StopStrip();
IReadOnlyList<Symbol> reel36 = Build36StopStrip();

var runA = new StripReelSet([reel26, reel26, reel26]);
var runB = new StripReelSet([reel36, reel26, reel36]);
```

Workers and analytic code therefore read the same fixed reel snapshot during a run.

## Extending the strip for faster window reads

A stop near the end of a reel wraps to the beginning. The direct formula is easy to
recognize:

```csharp
strip[(stop + position) % strip.Length]
```

The baseline calculation performs that remainder once for every visible cell. Ten
million spins with five reels and three visible positions produce 150 million such
calculations.

During construction, `StripReelSet` appends the first `Rows - 1` symbols to a private
drawing array:

```text
physical strip:  A B C D E
drawing array:   A B C D E A B
```

For a three-position window starting at D, the engine reads `D E A` as one contiguous
slice. The spin loop becomes `drawStrip[stop + position]`, with no wrap calculation.

A supported window is three to five positions tall, so each drawing array adds only
two to four symbol references. The original strip remains the source for stop counts
and probability calculations. The extended array is used only while drawing.

## Why this boundary fits CUPID

- **Composable.** Callers may combine any valid reel strips. JSON games, generated presets,
  and tests all meet at the same constructor.
- **Unix philosophy.** `StripReelSet` preserves and reads reel geometry. A feature that
  decides when to change reel sets belongs in game logic.
- **Predictable.** A run's strips cannot change behind its workers. The same reel snapshot,
  seed, and worker count reproduce the same draw sequence.
- **Idiomatic.** The public API accepts read-only collections. The implementation copies
  them into arrays suited to the hot path.
- **Domain-based.** Each reel owns one strip. The model does not invent a machine-wide
  `StopsPerReel` rule when actual reels may have different lengths.

## Exact PAR strips and generated demo strips

A PAR stop list and a symbol-count table provide different information.

When a PAR sheet lists every stop, the loader preserves that order and passes the
finished arrays to `StripReelSet`. Rearranging the strip would keep each symbol's
single-position probability but change which symbols appear together.

The older demo presets contain symbol counts but no stop order. For those presets,
`EvenlySpacedStripBuilder` must create an order. Suppose a 10-stop reel contains two
Pearls. Their probability in one position is fixed:

```text
P(Pearl) = 2 / 10
```

The count does not tell us whether the Pearls are neighbors or five stops apart.
The builder places copies at the centers of equal intervals on a temporary 0-to-1 ruler:

```text
temporary position = (copy number + 0.5) / number of copies
```

Two Pearl copies therefore receive positions 0.25 and 0.75. Three copies would receive
1/6, 3/6, and 5/6. The builder creates these marks for every symbol, sorts all marks,
and discards the marks. A symbol-id tie breaker makes the result deterministic.

Even spacing prevents an accidental block of identical demo symbols and produces the
same result every time. It does not recover a real reel's order. Only a stop list can
provide that. `EvenlySpacedStripBuilder` owns the demo policy, while `ReelPreset`
stores the completed strips.

A Strategy pattern would add an extra decision that the spin engine does not make.
PAR data arrives as a completed strip; demo counts go through one builder. Both paths
end as an ordered `Symbol[]`.

`MinRows`, `MaxRows`, and `DefaultRows` are compile-time constants because they state
which window heights this engine version supports. Changing a limit requires code,
tests, and a new build. `Millicents.ScaleFactor` from article 2 is `static readonly`
because consumers read it as a runtime value.

Raising `MaxRows` to 6 would extend the tested geometry. It is not a setting that a
deployed game can change.

`ProbabilityOf` counts one symbol on one reel and supplies the expected-value math in
article 4. `JointProbabilityOf` walks every stop and counts how often two selected
positions show a requested pair. That second result is needed for variance. Both
methods count exactly, although the returned `double` may contain the small binary
representation error described in article 2.

Separate method names keep the questions visible. `ProbabilityOf` asks about one
symbol. `JointProbabilityOf` asks about a pair of positions and symbols. An optional
second-symbol parameter would make callers inspect the arguments to learn which
calculation they requested.

`DrawWindow` fills a caller-owned `Span<Symbol>` and returns no array. Each worker can
reuse one window buffer for millions of spins instead of allocating an array per spin.

`Strip(int reel)` returns a `ReadOnlySpan<Symbol>`. A caller can inspect the existing
strip without copying it or changing it.

Symbol ids use a `byte`, which allows 256 distinct ids. The teaching overload fills a
`Symbol[]` so the lab can display names and flags. The production simulation calls
`DrawWindowIds` when it needs only the compact ids for evaluation. A `Symbol` value is
larger than one byte; the memory saving comes from the byte-id window, not from the
`Symbol[]` shown in the earlier listing.

`DrawWindow` and `ProbabilityOf` read the same strips. Simulation and analytic math
therefore use one copy of the geometry.

> The Chapter 3 page at <http://localhost:5090/#/ch03> draws windows from these
> strips. It also places the PAR symbol counts and payout table beside the selected
> game, so the visual model and the configured math can be checked together.

## From a stopped reel to a paid line, step by step

One spin moves through five steps:

1. **Draw the stops.** `DrawWindow` chooses one stop on each reel.
2. **Fill the window.** Each stop and its neighbors fill that reel's visible column.
3. **Read a payline.** The line selects one position from every reel, from left to
   right.
4. **Count the opening run.** The evaluator starts at reel 1 and counts matching
   symbols until the first mismatch.
5. **Apply the paytable.** A matching category and run length produce an award.
   Awards from winning lines are added to the spin total.

Lines are scored separately, but they are not necessarily independent. Two lines may
share visible positions and win together more often than two unrelated lines would.

Step 5 uses the finished payout multipliers. A loaded game's JSON file supplies them
from its PAR data. The stock demo presets begin with relative pay values that article
4 scales toward a target RTP. The examples below use finished multipliers.

## Paylines: configured shapes, with demo defaults

A payline chooses one visible position on each reel. On a 3-position window, Center
is `[1,1,1,1,1]` and V is `[0,1,2,1,0]`. This engine evaluates line wins from left
to right. Games that pay in both directions or use ways-to-win need a different
evaluator. The line path itself is configuration data:

```csharp
public sealed record Payline
{
    public Payline(string name, IReadOnlyList<int> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rows);
        Name = name;
        Rows = Array.AsReadOnly([.. rows]);   // construction-time snapshot
    }

    public string Name { get; }
    public IReadOnlyList<int> Rows { get; }

}

public static class StandardPaylines
{
    public static IReadOnlyList<Payline> For(
        int reels, int lineCount, int visiblePositions) { /* … */ }
}
```

A `Payline` stores a name and one position index per reel. Its constructor copies the
position list, so later changes to the caller's list cannot alter the line.

The project creates paylines in two ways:

- `GameDefinitionBuilder` validates each explicit path transcribed from a PAR sheet,
  then calls `new Payline(name, positions)`. A path such as `[0,2,1,0,2]` does not
  need to match a built-in shape.
- `StandardPaylines` generates Center, Top, Bottom, V, Hat, and zigzag paths for the
  old demo presets. It is a convenience catalog, not a restriction on loaded games.

Reel strips follow the same split. JSON games provide explicit ordered arrays, while
`StandardReelPresets` builds the older demos. After construction, both sources use
the same `Payline` and `StripReelSet` types.

### Orca Dive payout data used in this chapter

The worked examples below use the payout multipliers in `games/orca-dive.json`.
Each number is a multiple of the total spin wager. A dash means that run length does
not pay under that category.

| Pay category | 1 reel | 2 reels | 3 reels | 4 reels | 5 reels |
|---|---:|---:|---:|---:|---:|
| Red7 | - | - | 40× | 100× | 5000× |
| Green7 | - | - | 25× | 50× | 250× |
| Blue7 | - | - | 20× | 50× | 200× |
| Seal | - | - | 20× | 50× | 200× |
| WildOrca | 2× | 5× | 10× | 50× | 2000× |
| Salmon | - | - | 10× | 25× | 150× |
| Herring | - | - | 10× | 25× | 150× |
| Squid | - | - | 5× | 10× | 50× |
| Mackerel | - | - | 5× | 10× | 50× |
| MixedSeven | - | - | 5× | 40× | 100× |

The Chapter 3 lab reads this table and the symbol counts from the same loaded game
definition. Selecting another game changes the reel chart and payout chart together.

## A single line, paid

Orca Dive has one Center payline. Three Seals from the left pay 20 times the spin
wager. In this window, the arrow marks the positions read by that line:

```
reel:        1        2        3        4        5
row 0:    Mackerel  Squid     Herring  Green7   Mackerel
row 1:     Seal      Seal     Seal    Squid    Mackerel   ← Center payline
row 2:    Squid     Herring   Mackerel Blue7    Salmon
```

The grid labels reels 1–5 for reading; the code indexes them 0–4.

The Center line reads `Seal, Seal, Seal, Squid, Mackerel`. The first three symbols
form a Seal run, and Squid ends it on reel 4. The PAR payout table lists three Seals
at 20 times the wager, so a 1-credit spin pays 20 credits.

The symbols in screen rows 0 and 2 remain visible, but this line does not score them.
Orca Dive has only one line, so its line wager and total spin wager are the same.

## Why doesn't this pay? Two near-misses

Seeing three matching symbols does not always create a line win. Their positions and
their starting reel matter.

**The run starts too late.** If the line reads `Mackerel, Seal, Seal, Seal, Squid`,
the Seals begin on reel 2. This evaluator counts from reel 1, where Mackerel produces
a run length of 1. It does not skip ahead to search for another run.

**The symbols are outside the line.** Seals at `(reel 1, row 0)`, `(reel 3, row 2)`,
and `(reel 5, row 2)` are visible but do not occupy the Center line's positions.
Only the symbols in row 1 take part in that line's result.

Scatter rules are different. They may inspect the full window instead of a payline;
article 7 defines Orca Dive's Penguin scatter rule.

## Several lines, one spin

For this hypothetical multi-line example, use the five shapes generated by
`StandardPaylines.For(5, 5, 3)`: Center, Top, Bottom, V, and Hat. Let three B symbols
from the left pay 10 times the spin wager.

```
reel:        1     2     3     4     5
row 0:       B     A     C     A     A     ← Top payline
row 1:       B     B     B     Y     Z     ← Center payline
row 2:       C     B     B     D     C
```

The V payline reads `(reel 1, row 0), (reel 2, row 1), (reel 3, row 2), (reel 4,
row 1), (reel 5, row 0)`.

Each line reads a different path:

- **Top** (row 0): `B, A, C, A, A`. Reel 1 is B, reel 2 is A: mismatch immediately.
  Run length 1, so Top pays nothing.
- **Center** (row 1): `B, B, B, Y, Z`. Reels 1 through 3 match B; reel 4 breaks the
  run with Y. Run length 3. Center pays 10.
- **Bottom** (row 2): `C, B, B, D, C`. The first two symbols differ. Run length 1.
  Bottom pays nothing.
- **V** (`0,1,2,1,0`): reading those five cells gives `B, B, B, Y, A`. Reels 1
  through 3 (by position along the line, not by row) match B; the fourth position
  breaks the run with Y. Run length 3. V pays 10.
- **Hat** (`2,1,0,1,2`): `C, B, C, Y, C`. The first two symbols differ. Run
  length 1. Hat pays nothing.

Only two lines pay on this spin:

- Center pays 10 times the spin wager.
- V pays 10 times the spin wager.

The two awards add together, so the spin pays 20 times its wager. With this
simulator's standard 1-credit wager, the player receives 20 credits, or 2,000,000
millicents.

This engine uses the full spin wager when it calculates each line's award. It does
not divide the wager among the five paylines. Some traditional slot math divides
the wager among the lines first. Both systems are valid, but a paytable written for
one system will produce the wrong payouts if it is used with the other.

Center and V also pass through the same position on reels 2 and 4. On reel 4, both
lines read Y, so both winning runs stop there. Because the lines share positions,
their results are related rather than independent. Article 4 accounts for that
relationship when it calculates the variance of a game with several paylines.

## How many rows can this engine's window have?

The stock presets use 3-position windows. A JSON game may declare 3, 4, or 5 with
`windowRows`. The loader rejects values outside that tested range:

```
windowRows must be 3..5; got 2.
```

The minimum comes from the stock V and zigzag generator, which expects a position
between the top and bottom. The maximum is the tallest geometry covered by the
current definitions and tests. These are project limits, not limits of slot math.

## The geometry at 4 and 5 rows

`StandardPaylines.For` calculates three positions from the configured window height:

```
topRow = 0
bottomRow = rows - 1
middleRow = rows / 2        (integer division)
```

With **5 positions**, `middleRow` is 2. There are two positions above it and two
below it, so the top and bottom zigzags travel the same distance.

With **4 positions**, there is no single center. Integer division gives
`middleRow = 4 / 2 = 2`, the lower of the two central positions. A top zigzag travels
from 0 to 2, while a bottom zigzag travels from 3 to 2.

**5 positions, with a true center:**

```
row 0  ─┐
row 1   │  top zig-zag swings 2 rows (0→2)
row 2  ─┤← middleRow
row 3   │  bottom zig-zag swings 2 rows (4→2)
row 4  ─┘
```

**4 positions, with two central choices:**

```
row 0  ─┐
row 1   │  top zig-zag swings 2 rows (0→2)
row 2  ─┤← middleRow (rounds toward the bottom half)
row 3  ─┘  bottom zig-zag swings 1 row (3→2)
```

Choosing position 1 instead would move the shorter swing to the top side. Either
choice must be documented because a 4-position window has no true center.

V and Hat lines still span the full window from top to bottom. Their turning point
is the middle reel, so the `middleRow` rounding choice affects zigzags but not those
two shapes.

## Proof from a hand-built fixture

`MultiRowWindowTests.cs` checks the 4- and 5-position shapes against a small game.
The following calculation covers its 5-position version.

The fixture has three 20-stop reels. Each strip contains four A symbols and two Star
scatters. The Stars are far enough apart that one window cannot show both. Its Center
line reads position 2 and pays 5 times the wager for three A symbols.

**Line win.** One reel shows A on the line with probability `4/20 = 0.2`. All three
independent reels show A with probability `0.2³ = 0.008`. At a 5-times award, this
rule contributes `5 × 0.008 = 0.04`, or 4%, to RTP.

**Scatter trigger.** A 5-position window can reveal each Star from five starting
stops. With two separated Stars on a 20-stop strip, one reel contains a visible Star
with probability:

```
(Star count × rows) / strip length  =  (2 × 5) / 20  =  0.5
```

All three reels must contain a Star, so the trigger probability is `0.5³ = 0.125`.

**Bonus return.** The fixture has one prize worth 8 and one blank that ends the
round with a consolation award of 2. Article 7 derives the mean collected prize as
`8 × 1/(1+1) = 4`. The mean bonus award is therefore 6, and its RTP contribution is
`0.125 × 6 = 0.75`.

**Total return.** Line RTP plus bonus RTP is `0.04 + 0.75 = 0.79`. The test checks
the line probability, trigger probability, and total to 12 decimal places because
these decimal values do not all have exact binary floating-point representations.

The 4-position fixture uses 16-stop strips with the same four A symbols and two
Stars. Its line probability is `(4/16)³ = 0.015625`; line RTP is `0.078125`. Its
scatter probability remains `((2×4)/16)³ = 0.125`, so total RTP is `0.828125`.
`SyntheticGame_SimulatedRtp_ConvergesOnTheAnalyticValue` runs three million spins
for each height and checks that measured RTP falls inside the analytic band.

## Where the formulas stop

`StandardPaylines.For` generates Center, Top, Bottom, V, Hat, and zigzag shapes for
the stock presets. A JSON game instead supplies one position index per reel. The
loader checks every index against that game's `windowRows`. Orca Dive's Center line,
`[1,1,1,1,1]`, is loaded from JSON rather than generated. Both paths produce the
same `Payline` record for the evaluator.

## Evaluating a line, in code

The base-game evaluator is the five-step pipeline from earlier, written out:

```csharp
public sealed class LinePayEvaluator(IReadOnlyList<Payline> lines, ScaledPaytable paytable)
{
    private readonly Payline[] _lines = [.. lines];

    public Millicents Evaluate(ReadOnlySpan<Symbol> window, int reelCount, int rows)
    {
        var total = Millicents.Zero;
        foreach (var line in _lines)
        {
            var first = window[0 * rows + line.Rows[0]];
            var run = 1;
            for (var reel = 1; reel < reelCount; reel++)
            {
                if (window[reel * rows + line.Rows[reel]].Id != first.Id)
                    break;
                run++;
            }
            if (run >= Paytable.MinimumWinningRun)
                total += paytable.PayFor(first.Id, run);
        }
        return total;
    }
}
```

The stock preset pipeline sets `Paytable.MinimumWinningRun` to 3. Its paytable
generator and evaluator read the same constant. If they used different minimums,
the generator could create unreachable entries or the evaluator could request an
entry that was never created.

Loaded JSON games use the run lengths declared in their own paytable. Orca Dive, for
example, can pay a single Wild Orca.

CUPID from article 1 gives five checks for this evaluator:

- **Composable.** It takes lines and a paytable at construction, a window by
  `ReadOnlySpan`, and returns `Millicents`. It does not need logging or global state.
- **Unix philosophy.** It converts windows to money. It does not draw windows
  (`StripReelSet`), set pay amounts (`ScaledPaytable`), or schedule spins
  (`SimulationEngine`).
- **Predictable.** Left-to-right, run length gated by one named constant, match on
  symbol id. Loaded-game wild rules use the separate evaluator introduced in article 7.
- **Idiomatic.** `ReadOnlySpan` for the zero-copy window, a primary constructor, an
  array snapshot of the line list so the hot loop iterates a concrete type.
- **Domain-based.** Names such as `line`, `run`, and `pay` state the slot rule
  directly.

Line awards add. Therefore the expected return of the window is the sum of the
expected returns of its lines, even when those lines overlap. Overlap affects
variance because shared positions make line results related. `JointProbabilityOf`
provides the pair probabilities used by that calculation in article 4.

## Other ways to score a window

`LinePayEvaluator` converts a fixed-payline window into money. Ways-to-win and
cascading games need different evaluators. The simulation engine accepts the scoring
step as a delegate, so a different scoring model does not change its scheduler or
counters.

Article 4 uses these reel probabilities to solve a paytable and calculate RTP and
variance.

*Source files: `Reels/StripReelSet.cs`, `Reels/Payline.cs`, `Reels/StandardPaylines.cs`,
`Reels/ReelPreset.cs`, `Reels/StandardReelPresets.cs`,
`Paylines/LinePayEvaluator.cs`, `Paytables/Paytable.cs`,
`tests/MMP.SlotGame.Tests/MultiRowWindowTests.cs`,
`games/orca-dive.json`.*

## Optimization notebook

The baseline window formula is `(stop + position) % strip.Length`. The optimized path
uses the short wrapped extension described earlier and writes byte ids when the caller
does not need full `Symbol` values. Tests first confirm that both paths draw the same
symbols from the same RNG stream; article 9 then compares their speed.
