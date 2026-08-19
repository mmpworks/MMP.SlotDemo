# Games as Data: Loading a Third-Party Slot Deconstruction

*Part 7 of a series on building a slot game engine in C#. Parts 3 through 6 built a
configurable slot game and the engine that runs it. This one models Orca Dive
from a public third-party deconstruction by making the game itself a JSON file.*

Everything so far has run generated games: preset reel shapes, a canonical paytable
scaled to a target. That shows the engine and the math agreeing with *each other*,
which is useful, and a skeptic can fairly call it a closed loop. Stronger evidence
comes from game data and results derived outside this codebase. The public
third-party page Orca Dive's numbers are modeled on supplies reconstructed reel
strips, the visible paytable and bonus rules, and calculated returns. (The dated
citation lives in `docs/par-orca-dive.md`. This article is about the loader.)

Orca Dive is a fictional game invented for this series. Its math reproduces a published
PAR deconstruction of a real commercial machine: the source's author reconstructed the
strips from 212 recorded spins and read the awards off the machine's rule screens.
Reproducing that source's combination counts and returns is a valuable independent
cross-check of this engine; it certifies nothing (and the source is not an official
manufacturer PAR sheet).

The game can live in code or in data. This chapter puts it in data.

The vocabulary for this chapter:

| Term | Plain-language meaning |
|---|---|
| **Game definition** | The JSON description of symbols, strips, lines, awards, and features |
| **Loader** | Code that reads and validates that description |
| **Pay category** | One possible interpretation of a line, such as Mackerel or Mixed Seven |
| **Wild** | A symbol allowed to continue specified categories of wins |
| **Scatter** | A symbol checked in allowed window positions rather than only on a payline |
| **Provenance** | A record of where externally supplied game data came from |

## A validation example

Suppose a JSON file says a reel has 22 stops, but its strip lists only 21 symbols. It also
uses `Whale` on a payline even though no symbol named `Whale` was declared.

A parser can read that JSON successfully. The braces and commas are valid. The game is still
invalid. The loader should report both game problems:

```text
Reel 1 declares 22 stops but contains 21.
Payline "Center" refers to unknown symbol "Whale".
```

Reporting both lets the author fix the file in one editing pass.

### Check your understanding

What is the difference between parsing and validation?

<details><summary>Answer</summary>

Parsing turns JSON text into objects. Validation checks whether those objects describe a
game the engine can safely run.

</details>

## The game definition file

Orca Dive arrives as JSON:

```jsonc
{
  "name": "Orca Dive",
  "windowRows": 3,
  "symbols": [
    { "name": "Red7" },
    { "name": "WildOrca", "wild": true,
      "substitutesFor": ["Salmon", "Herring", "Squid", "Mackerel"] },
    { "name": "Penguin", "scatter": true }
    // … 10 symbols total
  ],
  "groups": { "AnySeven": ["Red7", "Green7", "Blue7"] },
  "reelStops": [26, 29, 26, 29, 26],
  "symbolCounts": { "Red7": [1, 2, 1, 2, 1], /* … */ },
  "reels": [
    ["Penguin", "Mackerel", "Herring", "Green7", /* … the actual 26-stop strip */],
    // … five strips, in published order
  ]
  // … paytable and bonus follow
}
```

Several fields deserve explanation:

- **`source` is provenance.** The schema carries an optional `source` string, and
  `GameDefinition` keeps it, so a file can say where its numbers came from. When a
  test fails, the first question is "us, the transcription, or the source model?",
  and a definition that names its origin answers it. The shipped
  `classic-three-reel.json` fills the field in; Orca Dive's dated citation lives in
  `docs/par-orca-dive.md` instead.
- **The strips follow the published reconstruction.** Unequal lengths,
  26/29/26/29/26, in the listed order,
  because order is what drives the adjacency correlations from article 3.
- **The redundancy is deliberate.** `reelStops` and `symbolCounts` restate what the
  strips imply. The loader cross-checks them and rejects the file on any
  disagreement.

The evaluator reads the strips at runtime; `reelStops` and `symbolCounts` are a
cross-check, the way a checksum published next to a download catches a bad transfer
rather than replacing the file. For hand-transcribed data, that checksum catches the
typo that would otherwise surface three layers later as a fourth-decimal RTP
mismatch.

The loader mirrors `SimulationConfig.TryCreate` from article 1: parse, validate
everything, report all errors at once, and only then construct. A
`GameDefinition` that exists is one that passed every check: unknown symbol names,
paylines off the window, paytable rows for symbols that don't exist, geometry
disagreeing with strips, all rejected with slot-domain messages rather than parser
stack traces.

The JSON is first deserialized into a plain matching shape, `internal sealed
class GameDocument` (and a handful of smaller classes alongside it for symbols,
paylines, pays, and features), before the loader turns it into the validated
`GameDefinition`. Every one of those classes is declared `internal`, meaning code
outside this assembly cannot reference the type at all, not even to catch it in a
variable. The JSON document types are not the public API. Engine users work with
validated types such as `GameDefinition` and `PayCategory`. The raw document classes
can change as the file format changes. Code outside this project cannot depend on them.

## How fractional pays enter a game file

A JSON document can contain decimals, but this schema deliberately compiles award
multipliers to integers. A source paytable may specify 1.5X or 2.25X. The `payUnit`
field names the unit every pay in that file uses: `"units"` (the default, whole multipliers only), `"tenths"`
(15 means 1.5X), or `"hundredths"` (225 means 2.25X, the finest unit the engine
supports).

Inside the engine those three choices are an `enum PayUnit { Units, Tenths,
Hundredths }` rather than a bare string carried around and compared at every use. An
`enum` gives the three valid choices names the compiler checks, so a typo like
`Hundreths` fails to compile instead of quietly behaving like `Units` at run time.

The JSON value is text supplied by the file's author,
and the loader reads it with
`string.Equals(trimmed, "units", StringComparison.OrdinalIgnoreCase)`.
`OrdinalIgnoreCase` earns its place here: it compares raw byte values and applies no
language's casing rules, so `"Hundredths"`, `"HUNDREDTHS"`, and `"hundredths"` all
match the same way whatever locale the loading machine is set to. A culture-aware
comparison could in principle treat certain letters differently depending on system
language settings. A JSON keyword should mean the same thing everywhere the file is
loaded, and an ordinal comparison guarantees it.

> 💡 **Quick picture.** A recipe written in whole cups can't ask for a cup and a
> half unless the cook also owns a half-cup measure. Declaring `payUnit: "tenths"`
> is buying the finer measuring cup for this one file: every pay in it is now read
> in tenths, and the loader checks that every number actually fits that cup.

Whichever unit a file declares, the loader rejects any pay written with more
precision than that unit can carry, and it names the finest unit the file could
still use:

```
paytable category 'Seven' pays 1.5 at a run of 3; "units" mode allows whole-number
multipliers only. Declare "payUnit": "tenths" or "hundredths" to express 1.5X.
```

`payUnit` applies to the whole file, not per category, so one game cannot mix
unit conventions across its own paytable. Internally the loader compiles every
declared pay to hundredths of the **total spin wager** before anything else touches
it, so the evaluator and analyzer downstream see one representation regardless of
which unit the JSON author chose. This total-wager basis is this engine's file
convention. Traditional multiline paytables often quote line-bet units and require
an explicit conversion when imported. Article 2 covers the money-type side of this
same conversion; the full schema for `payUnit` lives in
`docs/game-definition-schema.md`.

The loader receives declared pays as `Dictionary<int, decimal>`. It uses `decimal`
instead of `double` because a JSON author types a pay as ordinary decimal digits,
"1.5" or "2.25," and `decimal`
holds those digits with no representation error. That is a different job from the
accumulation path article 2 rules `double` out of, and the loader converts this
`decimal` to an integer well before the number reaches any hot path.

That conversion itself is `checked((int)(realMultiplier * Millicents.ScaleFactor))`.
Ordinarily, casting a value too large for an `int` silently wraps around to a
meaningless number; `checked` turns that wraparound into a thrown
`OverflowException` instead. A pay large enough to overflow an `int` would already
be an absurd paytable entry, and `checked` makes it surface as an exception instead
of a plausible-looking number.

> 🧪 **Try it live.** The companion site's chapter 7 page (<http://localhost:5090>,
> then `#/ch07`) hands the real loader whatever you give it. **Lab 1 — The shipped
> games, read by the real loader** compiles `orca-dive.json` and
> `classic-three-reel.json` and shows what came out; **Lab 2 — Feed the loader
> anything** lets you edit a definition and read back the whole error list at once.

## An evaluator that reads the game from data

Orca Dive needs things the generated games from earlier articles didn't have: a
wild that substitutes for the four fish (but not the sevens), a "mixed sevens"
group win, a wild that pays on its *own* line from one-of-a-kind, and a scatter
that triggers a bonus. Adding each rule to the evaluator as a special case would
leave it with a growing set of game-specific flags.

Instead, the loader **compiles** the game's rules into a neutral form, pay
categories, and the evaluator interprets those:

```csharp
public sealed record PayCategory
{
    private readonly bool[] _continuesRun;
    private readonly bool[] _requires;
    private readonly int[] _paysByCount;

    public PayCategory(int index, string name, PayCategoryKind kind,
        bool[] continuesRun, bool[] requires, int[] paysByCount)
    {
        Index = index; Name = name; Kind = kind;
        _continuesRun = [.. continuesRun];
        _requires = [.. requires];
        _paysByCount = [.. paysByCount];
    }

    public int Index { get; }
    public string Name { get; }
    public PayCategoryKind Kind { get; }

    public bool Continues(byte symbolId) => _continuesRun[symbolId];
    public bool IsRequired(byte symbolId) => _requires[symbolId];
}
```

`PayCategory` is a `record`, and like article 4's `ScaledPaytable` it spells
out a constructor instead of taking a one-line positional shape. The arrays stay
`private` here too. An array property is an ordinary reference, and handing a caller
the `bool[]` would let that caller flip one entry in place, changing what the
category pays without going through any of the loader's checks. The constructor
copies each array on the way in (`[.. continuesRun]`), and the category exposes its
data through `Continues(byte)` and `IsRequired(byte)`, single-symbol lookups that
never reach the backing array. Read through those two methods, "Mackerel" has
`Continues` true for Mackerel *and* WildOrca (the wild extends fish runs), and
`IsRequired` true for Mackerel alone.

Two flat `bool[]` arrays match where this data gets read: the
evaluator's inner loop, run once per line per spin, tens of millions of times a
run. `Continues(symbolId)` and `IsRequired(symbolId)` are each one array index, no
object to unwrap first, and a `bool[]` packs one byte per entry with nothing extra
attached. A single array of paired-flag objects would cost an indirection per
lookup for a package of data the hot loop doesn't need bundled together.

> 💡 **Quick picture.** A substitute teacher can run the class and keep the lesson
> going, but the semester's official grade report still credits the class to the
> assigned teacher of record, not the substitute. A wild is the substitute: it
> keeps a Mackerel run alive from reel to reel, but a run made of nothing but wilds
> has no assigned teacher of record for Mackerel, so it doesn't satisfy the Mackerel
> category. It falls through to the Wild category instead, which requires the
> wild and pays its own, much richer, schedule.

`AnySeven` is a category whose `ContinuesRun` covers three symbols and whose
`Requires` covers the same three symbols: a group win, from the same two arrays,
no separate code path needed.

The evaluator walks every category and keeps the best win:

```csharp
public LineWin Evaluate(ReadOnlySpan<byte> cells)
{
    var best = LineWin.None;
    foreach (var category in _categories)
    {
        var run = 0; var satisfied = false;
        while (run < cells.Length && category.Continues(cells[run]))
        {
            satisfied |= category.IsRequired(cells[run]);
            run++;
        }
        if (!satisfied) continue;

        var pay = category.PayFor(run);
        if (pay == 0 || pay < best.Multiplier) continue;
        if (pay == best.Multiplier && run <= best.Count) continue;  // tie -> longer run

        best = new LineWin(category.Index, run, pay);
    }
    return best;
}
```

The method is short, and its game knowledge is two engine-wide rules: runs are
left-aligned, and the best pay wins with ties going to the longer run. The source
combination table settles the tie. A Red7 four-of-a-kind and a Mixed-7
five-of-a-kind both pay 100 there, and tied cases are assigned to Mixed 7, so
choosing the longer run reproduces those category counts. Another deterministic
precedence rule could avoid randomness too, but it would not reproduce that
published breakdown.

Run length has no minimum in this evaluator: a category pays at a length exactly
when its pay array has a non-zero entry there. That's how a wild pays from
one-of-a-kind while everything else needs three; the *data* differs, the code does
not. New pay schedules change the data without requiring evaluator subclasses.

## The bonus, simulated pick by pick

In Orca Dive, scatters on reels 1, 3, and 5 open a pick
screen, 24 prizes and 6 blanks; pick until a blank ends the round.

```csharp
/// <summary>
/// A pick-until-you-lose bonus, configured from data: a pool of prizes plus some number
/// of blanks that end the round for a fixed consolation. Orca Dive fills it with 24
/// prizes and 6 blanks, but nothing here knows that.
///
/// <see cref="Play"/> draws entries without replacement. The closed forms below provide an
/// independent analytic check of that play path.
/// </summary>
public sealed class PickBonus { /* … */ }
```

The Orca Dive screen opens one treasure chest at a time, without replacement. Its *expected value*
comes from a symmetry argument. In a uniformly random ordering of `b` blanks and
however many prizes, any one prize is collected exactly when it precedes every blank
in the ordering, which by symmetry happens with probability `1 / (b + 1)`. Sum that
over every prize in the bag and the expected collected total falls out with no
permutation enumeration. Orca Dive's 6 blanks put that
probability at `1/7` for each of the 24 prizes; a pairwise version of the same
argument, `2 / ((b+2)(b+1))` for two prizes both preceding the blanks, yields the
second moment and hence the variance. The two paths answer the same question in
different ways, which makes disagreement useful evidence of a defect.

## Reusing the simulation machinery

Article 6's `SpinPlay` delegate lets a loaded game supply its scoring rules while
the engine keeps responsibility for scheduling, counters, and telemetry.
`GameRunner.CreatePlay` below returns a `SpinPlay` for the same reason article 6
gives: the engine asks every game, generated or loaded from JSON, for the same
one-behavior shape, so a data-loaded game plugs into the identical worker loop a
generated preset uses:

```csharp
private SpinPlay CreatePlay(ConcurrentBag<ComponentTally> tallies)
{
    var tally = new ComponentTally();
    tallies.Add(tally);

    var reels = definition.Reels;
    var evaluator = new WinEvaluator(definition);
    var bonus = definition.Bonus;
    var wager = SimulationConfig.Wager;
    var window = new Symbol[reels.WindowSize];
    var cells = new byte[definition.ReelCount];
    var scratch = new int[bonus?.Bonus.GiftCount ?? 0];
    var rows = reels.Rows;

    return (ref SpinRng rng) =>
    {
        reels.DrawWindow(ref rng, window);

        var multiplier = evaluator.EvaluateWindow(window, cells);
        var linePay = wager.ScaledMultiply(multiplier);

        var bonusPay = Millicents.Zero;
        if (bonus is not null && WinEvaluator.IsTriggered(window, rows, bonus))
        {
            bonusPay = wager * bonus.Bonus.Play(ref rng, scratch);
            tally.BonusTriggers++;
        }

        if (multiplier > 0) tally.LineHits++;
        tally.LineMillicents += linePay.Value;
        tally.BonusMillicents += bonusPay.Value;

        return new SpinOutcome(wager, linePay, bonusPay);
    };
}
```

`ComponentTally` is a small class with four plain `long` fields, one instance per
worker, added to a thread-safe `ConcurrentBag` when a worker starts. Its fields need
no `Interlocked`: each worker owns its own instance, and the run sums every tally
after every worker has joined, on a quiesced engine. `RunTotals` (article 6) remains
the interlocked, shared counter; the component split rides alongside it, per worker,
with no synchronization on the spin path.

`bonusPay = wager * bonus.Bonus.Play(ref rng, scratch)` draws from the *same*
worker stream as the reels. The bonus plays inline, on the worker's own stream, which
gives each worker one RNG-consumption order to reason about. A separately and deterministically
seeded bonus stream could also be replayable, but it would define a different
contract and require its own documented partitioning rules.

`Play` also takes a `scratch` buffer as a parameter, and `CreatePlay` allocates it
once, `new int[bonus?.Bonus.GiftCount ?? 0]`, outside the returned closure rather
than inside it. Playing the pick bonus needs somewhere to hold the shuffled gifts
while it draws them, and a function that allocated that array itself, fresh, on
every call, would allocate on every single spin that triggers the bonus, tens of
thousands of times across a run. Accepting the buffer as a parameter instead
means `CreatePlay` allocates it once per worker, when the worker starts, and
`Play` reuses it. The signature says who owns the memory and when it
gets created, which is the same reuse-over-allocation reasoning article 3 covers
for `DrawWindow`'s `Span<Symbol>` parameter, applied here to an `int[]` instead.

Quota partitioning, seeded streams, batched integer counters, and lossy telemetry
remain shared. Their existing tests still run against the common engine, so both
game paths use the same worker loop and determinism rules.

<!-- EXPORT: render this Mermaid block to PNG before publishing -->
```mermaid
flowchart LR
    JSON["games/orca-dive.json<br/>strips, paytable, wilds, bonus"] --> LOADER["GameDefinitionLoader<br/>validate everything, all errors at once"]
    LOADER --> DEF["GameDefinition<br/>compiled PayCategories<br/>(exists = valid)"]
    DEF --> RUNNER["GameRunner<br/>its own SpinPlay"]
    DEF --> AN["GameAnalyzer<br/>analytic enumeration"]
    RUNNER --> ENG["SimulationEngine<br/>shared scheduler and counters"]
    ENG --> MEAS["measured RTP"]
    AN --> EXPECTED["analytic RTP + sigma"]
    MEAS <-->|"compare statistically"| EXPECTED
    PAR["third-party deconstruction"] -.->|transcribed, cross-checked| JSON
    PAR -.->|"reported counts and returns"| EXPECTED
```

## Where the analytic side fits

`GameAnalyzer` computes the loaded game's analytic RTP by enumeration. It groups
outcomes by the symbol shown on each reel. The weight records how many stops produce
those symbols. This reduces Orca Dive from 14,781,416 stop combinations to tens of
thousands of symbol combinations. The exact answer stays the same because line scoring
does not use the stop number.

The scatter reads the whole window rather than a single cell, so it rides through the
enumeration as a second weight per symbol: stops showing this symbol on the payline
*and* a scatter somewhere in the window. That preserves the joint distribution of line
pay and bonus trigger, which are correlated, because a scatter occupying the window
costs that reel a payline symbol. Article 8 covers the accumulator this enumeration
uses and the overflow arithmetic behind it. A single-payline game's analytic
enumeration scales with distinct symbols per reel rather than stops per reel.

One limit: this analyzer covers single-payline games. Multi-line expected value is a
plain sum. Multi-line variance with wilds *and* a window-coupled scatter needs
line-pair covariance machinery this codebase has yet to build. Multi-line definitions
simulate correctly today, and `GameAnalyzer` has no way to check them.

## What the tests show

The deterministic analytic tests reproduce the deconstruction's 32 integer
line-win combination counts and its reported return components. Separately, the
statistical suite runs ten million spins and checks measured line return, bonus
return, **line** hit frequency (10.26% for Orca Dive — line wins only, rather than the
any-award union, which also counts bonus triggers and comes out at 11.45%), trigger
frequency, and total RTP against analytic bands.
Those are different claims: exact integer agreement for enumerated combination
counts, and probabilistic agreement for sampled simulation results.

For a game that fits the existing mechanics, most variation now lives in a validated
JSON definition. New mechanics still require code: a different evaluator or a
stateful feature needs a matching analysis path and tests.

Next, the final article: the test architecture, exhaustive ground truth,
statistical tiers, and the concurrency tests that assert bit-for-bit equality.

External comparison source: the dated, cited public third-party deconstruction in
`docs/par-orca-dive.md`, which explains that its strips were reconstructed from
recorded play. For contrast, an official laboratory submission includes percentage
calculations, reel-strip listings, paytables, source code, and other materials:
[GLI software-submission requirements](https://gaminglabs.com/getting-started/submit-new-software/).

*Source files: `games/orca-dive.json`, `Games/Definition/*.cs`,
`Games/WinEvaluator.cs`, `Games/GameRunner.cs`, `Games/GameAnalyzer.cs`.*

## Optimization notebook

**Summary:** preserve the complete game definition for people and tools, then compile a
smaller execution view for repeated spins.

- **Rich definition model:** keep symbol names, flags, validation details, and PAR data at
  the configuration boundary.
- **Compact execution model:** give workers byte ids and precomputed values when that is all
  evaluation requires.
- **Measured separation:** article 9 benchmarks the byte-id path while the full domain model
  remains available to analysis and the UI.
