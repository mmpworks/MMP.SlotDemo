# Episode 7 — Games as Data: A Real Machine From a JSON File

**Target:** 24–26 min. **Format:** create the file, paste the finished source, then
walk it. Today one of the pastes is a game rather than a class.
**Subject:** the engine. The companion site appears three times, for under three
minutes total, and only to make an engine claim visible.
**Companion article:** `docs/articles/07-games-as-data.md`
**Companion site:** MMP.SlotDemo, branch `main`, page `#/ch07`
**Files created on camera:** `CSharp/games/classic-three-reel.json`,
`CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinition.cs`,
`GameDefinitionLoader.cs`. **Shown, not created:** `GameDefinitionBuilder.cs`,
`CSharp/games/orca-dive.json`.

> **Discipline note for this recording.** The labs illustrate; they do not carry the
> episode. If a beat can be made in Rider, make it in Rider. Cut to the browser only
> where the engine's behavior is easier to see than to describe, and cut back inside
> a minute.

---

## Prep checklist

**Repo — the subject**
- [ ] Rider on `CSharp/MMP.SlotDemo.slnx`, tree expanded to `MMP.SlotGame.Core`
- [ ] `Games/Definition/` present with `GameDocument.cs`, `GameDefinitionBuilder.cs`
      and `PickBonus.cs`; `GameDefinition.cs` and `GameDefinitionLoader.cs` moved aside
      so they get created on camera
- [ ] `CSharp/games/` folder present but `classic-three-reel.json` moved aside so it gets
      created on camera
- [ ] `CSharp/games/orca-dive.json` open in a background tab for the real-par-sheet beat
- [ ] A broken copy of a game file staged, with several problems in it at once
- [ ] Test runner loaded: `GameDefinitionLoaderTests`, `GameDefinitionFuzzTests`,
      `OrcaDiveParSheetTests`
- [ ] Clipboard manager staged with Block A, then Block B, then Block C

**Companion site — the illustration**
- [ ] `E:\dev\MMP.SlotDemo`, branch `main`
- [ ] `cd CSharp/web && npm run build`, then `dotnet run --project CSharp/src/SlotDemo.Server`
- [ ] `http://localhost:5090/#/ch07`, both labs run once — Lab 1 "The shipped games,
      read by the real loader" and Lab 2 "Feed the loader anything"
- [ ] `logs/` cleared so the viewer starts empty

**OBS**
- [ ] Scenes: `RIDER`, `BROWSER`, `TERMINAL`
- [ ] Zoom-to-mouse hotkey bound and tested at code-reading zoom
- [ ] Rider font sized for capture, JSON syntax colors checked against the background

---

## 0:00–1:15 — Cold open

**Scene:** RIDER, `CSharp/games/orca-dive.json` on screen, scrolled to the strips.

Before scrolling, show the article's source map. Trace one Center payline rule through
`GameDefinitionLoader`, `GameDefinitionBuilder`, `WinningOutcomeTable`,
`ProgressiveOutcomeTable`, and `GameRunner`. Name each file on screen. Make clear that
`WinningOutcomeTable.Build()` is the table builder; there is no separate table-builder
class.

- "This is a machine reconstructed from a published statistical analysis of a real one.
  Somebody recorded spins, worked out the combination counts and the returns, and
  published them. The engine has never heard of any of it."
- Then the distinction: "The strip *orderings* are ours. Only the counts were published.
  Line pays depend on counts alone, so the ordering is a free choice. The scatter is the
  exception, because it reads the whole window, so those we placed deliberately."
- "Every episode so far built a game out of C#. Today the game is a file, and the code's
  job is to refuse the ones that are wrong."
- "Three things go in on camera. A definition type, a loader, and a game."
- Set the format: "Same as always. Each one lands finished, then we walk it."

## 1:15–3:00 — Why a game stops being code

**Scene:** RIDER, the preset path from earlier episodes on one side.

Three reasons, said before any file appears.

1. **Real machines do not fit generated shapes.** Orca Dive has 26, 29, 26, 29, 26
   stops, a published paytable that was chosen rather than solved, and a scatter bonus.
   Nothing about it comes out of `CanonicalFor`.
2. **Transcription is where the errors are.** Typing a par sheet into a file is a
   mechanical task with a dozen ways to go wrong, and every one of them produces a game
   that runs and reports the wrong return.
3. **A game as data can be diffed, reviewed, and versioned.** A game as code can only be
   read by someone who reads C#.

"So the goal today is a file format that a person can hand-write and a loader that will
tell them everything they got wrong, all at once."

## 3:00–3:45 — Create the game

**Scene:** RIDER.

- New file in the `games` folder. **Path on screen and said out loud:**
  `CSharp/games/classic-three-reel.json`
- Paste **Block A**. "Ninety-seven lines and it is a complete machine."

### Block A — `CSharp/games/classic-three-reel.json`

```json
{
  "name": "Classic Three Reel",
  "source": "Hand-built example. Proves the loader and the analyzer are agnostic to reel count, symbol count and per-reel stop count.",
  "windowRows": 3,
  "symbols": [
    {
      "name": "Seven"
    },
    {
      "name": "Bar"
    },
    {
      "name": "Bell"
    },
    {
      "name": "Wild",
      "wild": true,
      "substitutesFor": ["Cherry", "Lemon"]
    },
    {
      "name": "Cherry"
    },
    {
      "name": "Lemon"
    }
  ],
  "groups": {
    "AnyFruit": ["Cherry", "Lemon"]
  },
  "reelStops": [22, 24, 22],
  "symbolCounts": {
    "Seven": [1, 2, 1],
    "Bar": [3, 3, 4],
    "Bell": [4, 5, 4],
    "Wild": [4, 2, 2],
    "Cherry": [5, 6, 5],
    "Lemon": [5, 6, 6]
  },
  "reels": [
    ["Cherry", "Lemon", "Bell", "Wild", "Bar", "Cherry", "Lemon", "Bell", "Wild", "Bar", "Cherry", "Lemon", "Seven", "Bell", "Wild", "Cherry", "Lemon", "Bar", "Bell", "Wild", "Cherry", "Lemon"],
    ["Cherry", "Lemon", "Bell", "Bar", "Cherry", "Lemon", "Seven", "Wild", "Bell", "Cherry", "Lemon", "Bar", "Bell", "Cherry", "Lemon", "Bell", "Cherry", "Lemon", "Seven", "Wild", "Bar", "Bell", "Cherry", "Lemon"],
    ["Lemon", "Cherry", "Bar", "Bell", "Lemon", "Wild", "Cherry", "Bar", "Bell", "Lemon", "Cherry", "Seven", "Lemon", "Bar", "Bell", "Cherry", "Lemon", "Wild", "Bar", "Bell", "Cherry", "Lemon"]
  ],
  "paylines": [
    {
      "name": "Center",
      "rows": [1, 1, 1]
    }
  ],
  "paytable": [
    {
      "symbol": "Seven",
      "pays": {
        "3": 500
      }
    },
    {
      "symbol": "Bar",
      "pays": {
        "3": 60
      }
    },
    {
      "symbol": "Bell",
      "pays": {
        "3": 30
      }
    },
    {
      "symbol": "Wild",
      "pays": {
        "3": 50,
        "2": 2,
        "1": 1
      }
    },
    {
      "symbol": "Cherry",
      "pays": {
        "3": 2
      }
    },
    {
      "symbol": "Lemon",
      "pays": {
        "3": 2
      }
    },
    {
      "group": "AnyFruit",
      "name": "MixedFruit",
      "pays": {
        "3": 1
      }
    }
  ]
}
```

## 3:45–9:30 — Walk the game file

### Beat 1 — `source`, and provenance as a first-class field

The second key in the file records where the numbers came from.

- For this game it says hand-built and says what it proves. Orca Dive deliberately
  leaves it out, because its provenance lives in `docs/par-orca-dive.md` and the
  citation belongs to the project rather than to the artifact. The field being
  *optional* is part of the design: provenance is carried where it can be maintained.
- **Why it ships in v1:** a number in a slot game is a claim about money, and a claim
  with no origin cannot be checked. The field costs one line and it is carried all the
  way through to `GameDefinition.Source`.
- Provenance ships before any tooling for it. There is no UI for this field yet, and
  adding one later needs no change to any file already written.

### Beat 2 — symbol ids come from position

The symbols array is ordered, and a symbol's id is its index. Seven is 0, Bar is 1, and
so on.

- The engine works in `byte` ids for speed, and a human works in names. This array is the
  one place the two vocabularies meet.
- **The consequence:** reordering the symbols array renumbers the game.
  Everything downstream reads ids, so it keeps working, and any hand-written test that
  hardcoded an id does not. The names are the stable contract; the ids are an internal
  encoding.

### Beat 3 — wilds arrive, and they arrive as data

`"wild": true` with an explicit `substitutesFor` list.

- Episode 3 shipped the `IsWild` flag on `Symbol` with nothing setting it. Here is the
  first game that sets it, and nothing changed shape to allow it.
- The substitution list is explicit rather than "substitutes for everything". This wild
  covers Cherry and Lemon, so a wild does not turn a near-miss on Seven into a jackpot.
- **Why explicit is right here:** "substitutes for everything" is a rule that has to be
  remembered. A list is a rule that can be read off the file and diffed when it changes.

### Beat 4 — groups, and a pay category that is not a symbol

`"groups": { "AnyFruit": ["Cherry", "Lemon"] }`, paying 1 for a mixed-fruit line.

- Real machines pay for "any fruit" or "any bar" all the time, and a group is how that
  is expressed without inventing a phantom symbol.
- Point ahead to the compiled form: a group becomes a `PayCategory` of kind `Group`,
  and the loader compiles both kinds into the same two lookups.

### Beat 5 — declared geometry, stated twice on purpose

`reelStops` and `symbolCounts` both restate information the `reels` arrays already
contain.

- This looks like a DRY violation. The strips are the game; the declarations are the par
  sheet's own claims about the game.
- The loader checks one against the other and reports every disagreement. "The
  redundancy is the test. Somebody transcribing 26 stops and typing 25 of them finds out
  from the loader rather than from an RTP that is off by a percent."
- When a human transcribes data, give them a way to state the same fact twice and let
  the machine compare.

### Beat 6 — pays are integers, and the unit is declared

Every pay in this file is a whole number of units of the total spin bet.

- Point at the Wild row paying at counts 1, 2, and 3. A single wild on the line returns
  the bet, and that is a real thing published machines do.
- Games needing 1.5 or 2.25 times the bet declare `payUnit` as tenths or hundredths and
  state the pay as an integer in that unit. "The fractional multiplier is a convenience
  in the file. It never becomes a floating-point number on the pay path."
- Episode 4's `MinimumWinningRun` scoping comment applies here: this JSON path pays at
  whatever run length its own data declares, which is why a one-of-a-kind wild pay is
  legal in a file and never generated by the preset solver.

> **Illustration (45 seconds, BROWSER).** Chapter 7 page, Lab 1 — "The shipped games,
> read by the real loader." Load this file and the site renders the compiled game
> (strips, marginals, and the analytic RTP), all from the engine's own loader running
> server-side. Then move down to Lab 2 — "Feed the loader anything" — and edit one strip
> entry so it disagrees with `symbolCounts`. The whole error list appears at once. "That
> list comes from the engine's own loader." Cut back.
>
> Lab 1 is read-only; the editor is Lab 2. Scroll between them rather than looking for
> an edit control in the first.

## 9:30–10:15 — Create the definition type

**Scene:** RIDER.

- New file. **Path on screen:**
  `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinition.cs`
- Paste **Block B**.

### Block B — `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinition.cs`

```csharp
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Games.Definition;

/// <summary>How a pay category decides what counts as a run.</summary>
public enum PayCategoryKind
{
    /// <summary>One symbol, extended by any wild that substitutes for it.</summary>
    Symbol,

    /// <summary>A named set of symbols, any of which continues the run. Wilds do not extend a group.</summary>
    Group,
}

/// <summary>
/// One row of the pay table, compiled into the two lookups the evaluator actually needs.
///
/// <see cref="Continues"/> answers "does this symbol keep the run going" and
/// <see cref="IsRequired"/> answers "does this symbol make the run count". The second one is
/// what stops an all-wild line from being read as a fruit win: the wild continues a fruit
/// run but does not satisfy it, so a line of nothing but wilds falls through to the wild
/// category, which requires the wild. The lookups are indexed by symbol id.
/// </summary>
public sealed record PayCategory
{
    private readonly bool[] _continuesRun;
    private readonly bool[] _requires;
    private readonly int[] _paysByCount;

    public PayCategory(
        int index,
        string name,
        PayCategoryKind kind,
        bool[] continuesRun,
        bool[] requires,
        int[] paysByCount)
    {
        Index = index;
        Name = name;
        Kind = kind;
        _continuesRun = [.. continuesRun];
        _requires = [.. requires];
        _paysByCount = [.. paysByCount];
    }

    public int Index { get; }
    public string Name { get; }
    public PayCategoryKind Kind { get; }

    public bool Continues(byte symbolId) => _continuesRun[symbolId];

    public bool IsRequired(byte symbolId) => _requires[symbolId];

    /// <summary>
    /// Pay multiplier for a run of <paramref name="count"/>, in hundredths of the TOTAL
    /// SPIN BET (225 = 2.25X of the whole wager — see <see cref="Games.WinEvaluator.EvaluateWindow"/>
    /// for why it is the total, not a single line's share), 0 for no pay. Always hundredths
    /// regardless of the game's declared payUnit: the loader compiles "units", "tenths" and
    /// "hundredths" pays to this one
    /// representation.
    /// </summary>
    public int PayFor(int count) => count >= 0 && count < _paysByCount.Length ? _paysByCount[count] : 0;

    /// <summary>The longest run this category can ever pay on. Useful for reporting, not for evaluation.</summary>
    public int MaxPayingCount
    {
        get
        {
            for (var count = _paysByCount.Length - 1; count >= 0; count--)
            {
                if (_paysByCount[count] != 0) return count;
            }
            return 0;
        }
    }

    /// <summary>The first reel count with a non-zero pay for this category.</summary>
    public int MinPayingCount
    {
        get
        {
            for (var count = 0; count < _paysByCount.Length; count++)
            {
                if (_paysByCount[count] != 0) return count;
            }
            return 0;
        }
    }
}

/// <summary>
/// A scatter-triggered pick bonus. Triggers when the scatter symbol is visible anywhere in
/// the window on EVERY reel in <see cref="RequiredReels"/>, then plays
/// <see cref="Bonus"/> once.
/// </summary>
public sealed record ScatterPickBonus
{
    public ScatterPickBonus(string name, byte scatterSymbolId, int[] requiredReels, PickBonus bonus)
    {
        Name = name;
        ScatterSymbolId = scatterSymbolId;
        RequiredReels = Array.AsReadOnly([.. requiredReels]);
        Bonus = bonus;
    }

    public string Name { get; }
    public byte ScatterSymbolId { get; }
    public IReadOnlyList<int> RequiredReels { get; }
    public PickBonus Bonus { get; }
}

/// <summary>
/// A complete, VALIDATED game. Reels, symbols, paylines, pay table and features all arrived
/// as data; nothing in this type or anything downstream of it knows how many reels, symbols
/// or stops a real game has.
///
/// Only <see cref="GameDefinitionLoader"/> creates instances, after all validation succeeds.
/// Downstream code can therefore use a GameDefinition without repeating validation or
/// depending on the JSON document types.
/// </summary>
public sealed class GameDefinition
{
    private readonly Lazy<WinningOutcomeTable> _winningOutcomes;
    private readonly Lazy<ProgressiveOutcomeTable> _progressiveOutcomes;

    internal GameDefinition(
        string name,
        string? source,
        IReadOnlyList<Symbol> symbols,
        StripReelSet reels,
        IReadOnlyList<Payline> paylines,
        IReadOnlyList<PayCategory> categories,
        ScatterPickBonus? bonus)
    {
        Name = name;
        Source = source;
        Symbols = Array.AsReadOnly([.. symbols]);
        Reels = reels;
        Paylines = Array.AsReadOnly([.. paylines]);
        Categories = Array.AsReadOnly([.. categories]);
        Bonus = bonus;
        _winningOutcomes = new Lazy<WinningOutcomeTable>(
            () => WinningOutcomeTable.Build(this),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _progressiveOutcomes = new Lazy<ProgressiveOutcomeTable>(
            () => ProgressiveOutcomeTable.Build(WinningOutcomes, Reels),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string Name { get; }

    /// <summary>Where the numbers came from, for example a PAR sheet URL. Carried for provenance.</summary>
    public string? Source { get; }

    /// <summary>Symbols indexed by id. Ids are assigned by position in the definition.</summary>
    public IReadOnlyList<Symbol> Symbols { get; }

    public StripReelSet Reels { get; }

    public IReadOnlyList<Payline> Paylines { get; }

    public IReadOnlyList<PayCategory> Categories { get; }

    public ScatterPickBonus? Bonus { get; }

    /// <summary>
    /// Every paying or feature-triggering combination implied by the PAR strips, paylines,
    /// paytable, and feature rules. The table is compiled once with the rest of the game;
    /// combinations that do nothing are absent.
    /// </summary>
    public WinningOutcomeTable WinningOutcomes => _winningOutcomes.Value;

    /// <summary>The same calculated outcomes arranged as reel-by-reel narrowing tables.</summary>
    public ProgressiveOutcomeTable ProgressiveOutcomes => _progressiveOutcomes.Value;

    public int ReelCount => Reels.ReelCount;

    /// <summary>The fewest leftmost payline symbols that can award money in this game.</summary>
    public int MinimumPayingReels => Categories.Min(category => category.MinPayingCount);

    /// <summary>Product of the per-reel stop counts: the size of the exhaustive outcome space.</summary>
    public long StopCombinations
    {
        get
        {
            long total = 1;
            for (var reel = 0; reel < Reels.ReelCount; reel++) total *= Reels.StopCount(reel);
            return total;
        }
    }

    public int SymbolId(string name)
    {
        for (var id = 0; id < Symbols.Count; id++)
        {
            if (string.Equals(Symbols[id].Name, name, StringComparison.Ordinal)) return id;
        }
        throw new ArgumentException($"Game '{Name}' has no symbol named '{name}'.", nameof(name));
    }

    public PayCategory Category(string name) =>
        Categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal))
        ?? throw new ArgumentException($"Game '{Name}' has no pay category named '{name}'.", nameof(name));
}
```

## 10:15–16:00 — Walk `GameDefinition`

### Beat 7 — `internal` constructor, and the invariant riding on the type

The only constructor is `internal`, and the loader is what calls it.

- A `GameDefinition` that exists passed every check. Same shape as
  `SimulationConfig.TryCreate` from episode 1, and the doc comment says so out loud.
- Nothing downstream re-checks geometry. The analyzer, the evaluator, and the engine all
  assume validity, and that assumption is safe because construction is gated.
- "Two very different subsystems reached the same shape independently."

### Beat 8 — `Continues` and `IsRequired`, two questions instead of one

Slow down here and read the doc comment aloud.

- The obvious model is one predicate: does this symbol count toward the run.
- Wilds break that. A wild keeps a fruit run going, and a line of nothing but wilds is
  not a fruit win.
- So a category answers two questions. `Continues` decides whether the run extends, and
  `IsRequired` decides whether the run counts. An all-wild line continues the fruit
  category and never satisfies it, so it falls through to the wild category, which
  requires the wild.
- "One predicate was hiding two ideas. The model had one question where the domain has
  two."

### Beat 9 — compiled lookups, indexed by id

Three arrays, indexed by symbol id, copied at construction.

- The evaluator asks these questions on every symbol of every line of every spin. An
  array index is the cheapest possible answer.
- The compile happens once at load. The file is written in names and the hot path runs on
  indices, and neither side has to think about the other.
- The copies in the constructor are the same defensive move as every other shared type in
  this series.

### Beat 10 — one representation for pays

`PayFor` returns hundredths of the total spin bet, always, whatever unit the file
declared.

- Units, tenths, and hundredths all compile to this one representation at load time.
- **Why that matters:** the evaluator has one arithmetic path rather than three, so there
  is no place for a unit to be misread at spin time. The unit is a property of the file,
  and it stops existing the moment the file is loaded.
- The comment also fixes the basis: hundredths of the *total* bet, matching episode 4's
  wager doc comment. "One authority for what RTP is relative to, restated at every place
  a reader might wonder."

### Beat 11 — the scatter bonus, and the assumption it breaks

`ScatterPickBonus` triggers when the scatter is visible anywhere in the window on every
required reel.

- Episode 4's features triggered on their own schedule, independent of the window. This
  one triggers on the symbols that came up.
- **Say the consequence:** episode 4 added feature variances with no covariance
  term because features were independent. A window-coupled bonus is a different model,
  and this game gets its return checked by exhaustive enumeration in episode 8 rather
  than by that closed form alone.
- "The seam on `Symbol` said scatter-count triggering couples features to the window.
  Here it is."

### Beat 12 — `StopCombinations`, a number that sets up episode 8

The product of the per-reel stop counts: the size of the whole outcome space.

- For this game it is 22 times 24 times 22, about 11,600 combinations. Small enough to
  visit every single one.
- "That property exists so the next episode can walk the entire space and referee both
  the simulator and the analytic math."

## 16:00–16:45 — Create the loader

**Scene:** RIDER.

- New file. **Path on screen:**
  `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinitionLoader.cs`
- Paste **Block C**. "Eighty-nine lines, and most of the work happens somewhere else on
  purpose."

### Block C — `CSharp/src/MMP.SlotGame.Core/Games/Definition/GameDefinitionLoader.cs`

```csharp
using System.Text.Json;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Games.Definition;

/// <summary>Every problem found in a game file, reported together rather than one per run.</summary>
public sealed class GameDefinitionException(string path, IReadOnlyList<string> errors)
    : Exception(Describe(path, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;

    private static string Describe(string path, IReadOnlyList<string> errors) =>
        $"Game definition '{path}' is not valid ({errors.Count} problem(s)):{Environment.NewLine}  "
        + string.Join(Environment.NewLine + "  ", errors);
}

/// <summary>
/// Reads a game from JSON and compiles it into a validated <see cref="GameDefinition"/>.
///
/// Imported games are validated here and nowhere else, in the same spirit as
/// <see cref="Simulation.SimulationConfig.TryCreate"/>: a definition that comes out of here
/// satisfied every rule, so nothing downstream re-checks geometry. Errors are collected
/// and reported together, so someone hand-transcribing a PAR sheet fixes the file in one
/// pass.
///
/// The checks are the ones a PAR sheet transcription actually gets wrong: a strip that does
/// not match its declared length, a symbol count that does not match the published table, a
/// pay table naming a symbol that is not on any reel, a payline row off the bottom of the
/// window, a scatter on a reel that never carries it.
/// </summary>
public static class GameDefinitionLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static GameDefinition LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        if (!TryLoad(json, out var definition, out var errors))
            throw new GameDefinitionException(path, errors);

        // LoadFile is the deployment construction path, not a validation probe. Complete
        // the PAR-derived lookup now so the first spin never pays its construction cost.
        _ = definition!.WinningOutcomes;
        _ = definition.ProgressiveOutcomes;
        return definition;
    }

    public static GameDefinition Load(string json)
    {
        if (!TryLoad(json, out var definition, out var errors))
            throw new GameDefinitionException("(inline)", errors);

        _ = definition!.WinningOutcomes;
        _ = definition.ProgressiveOutcomes;
        return definition;
    }

    public static bool TryLoad(string json, out GameDefinition? definition, out IReadOnlyList<string> errors)
    {
        definition = null;

        GameDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<GameDocument>(json, Options);
        }
        catch (JsonException ex)
        {
            errors = [$"The file is not valid JSON: {ex.Message}"];
            return false;
        }

        if (document is null)
        {
            errors = ["The file parsed to nothing; a game definition must be a JSON object."];
            return false;
        }

        var builder = new GameDefinitionBuilder(document);
        var ok = builder.TryBuild(out definition);
        errors = builder.Errors;
        return ok;
    }
}
```

## 16:45–21:00 — Walk `GameDefinitionLoader`

### Beat 13 — errors are collected, not thrown

The class comment gives the reason, so read it out loud: someone hand-transcribing a par
sheet fixes the file in one pass.

- `TryLoad` returns a bool and fills an error list. Nothing throws on the first problem.
- **Demonstrate it on camera.** Load the broken game file staged in the prep checklist.
  Six problems, one list, one run.
- **The framing to say:** "Fail-fast is the right default for a program talking to a
  program. A human editing a data file is a different consumer, and the right behavior
  for them is fail-completely."

### Beat 14 — three entry points, one implementation

`LoadFile`, `Load`, and `TryLoad`.

- `TryLoad` is the real one. The other two are conveniences that throw with the whole
  list attached.
- `GameDefinitionException` carries the errors as a property as well as formatting them
  into the message, so a caller can present them and a log can read them.
- "The exception message is designed to be read in a terminal. The `Errors` property is
  designed to be read by a web page. Both come from the same list."

### Beat 15 — the checks are chosen from real mistakes

Read the last paragraph of the class comment: a strip that does not match its declared
length, a symbol count that disagrees with the published table, a pay table naming a
symbol that is not on any reel, a payline row off the bottom of the window, a scatter on
a reel that never carries it.

- Every one of those is a transcription error rather than a schema error.
- "A JSON schema would catch none of these. They are all about the file being internally
  inconsistent, and internal consistency is the thing a person transcribing numbers
  gets wrong."
- The declared geometry from beat 5 exists so there is something to check the strips
  against.

### Beat 16 — the parse boundary, and errors that stay in the domain

The `try`/`catch` around deserialization turns a `JsonException` into a domain sentence.

- "The file is not valid JSON", followed by the parser's own detail. A caller never sees
  a serializer stack trace.
- The null check after it covers a file whose whole content is the literal `null`, which
  parses fine and means nothing.
- **The reader options:** case-insensitive properties, comments allowed, trailing commas
  allowed. All three exist because these files are written by hand.

### Beat 17 — why the builder is a separate class

The loader is eighty-nine lines and the builder is six hundred.

- The loader owns the boundary: parse, delegate, report. The builder owns the rules.
- **Why the split:** the boundary is the part every caller reads, and it stays readable
  because the rules live next door. `GameDefinitionBuilder` gets flashed on screen, not
  walked. "Six hundred lines of validation is the right size for that job, and it belongs
  next door rather than in front of a reader learning how loading works."
- Point at the line that does it: `builder.TryBuild(out definition)`, then
  `builder.Errors`. Every rule in the file reports through one accumulator.

> **Illustration (50 seconds, BROWSER).** Chapter 7 page, validation lab. It shows a
> game file with several deliberate errors and the loader's full error list beside it,
> live from the engine's `TryLoad`. Fix one error in the editor and the list shrinks by
> exactly one line. Fix the strip length and two entries vanish together, because the
> declared count and the symbol count were both disagreeing with the same strip. Cut
> back.

## 21:00–21:45 — Flash the builder and the shipped game

**Scene:** RIDER, twenty seconds each, no walkthrough.

Point out the boundary between the episode's initial implementation and the current source.
The initial branch rescored a visible symbol window on every spin. The optimized branch now
uses a reusable byte stop array and `ProgressiveOutcomeTable.TryGetValue`. The JSON schema
and validated `GameDefinition` stay the same; episode 9 owns the performance change.

- `GameDefinitionBuilder.cs`. Scroll it. Point at the `Fail` method that every rule calls
  and say the shape: check, accumulate, continue. "It never returns early."
- `CSharp/games/orca-dive.json`. Point at the ragged `reelStops`, the published paytable, and
  the penguin scatter. "That is a full commercial-scale machine, and the engine loading it
  has no code specific to it."

## 21:45–24:30 — The tests are part of the design

**Scene:** RIDER test runner, then TERMINAL.

- **`GameDefinitionLoaderTests.ShippedDefinitions_LoadWithoutErrors`** is the floor: both
  games in the repo load clean, every build.
- **`EveryProblemIsReportedTogether`** is beat 13 as an assertion. **Why it needs its own
  test:** every other error test would pass against a loader that stops at the first
  problem. This is the one that pins the behavior.
- The error suite maps one to one onto beat 15's list:
  **`StripsReferencingUnknownSymbols_AreReported`**,
  **`DeclaredGeometryThatDisagreesWithTheStrips_IsReported`**,
  **`BadPaylines_AreReported`**, **`BadPaytableEntries_AreReported`**,
  **`BadSubstitutions_AreReported`**, **`BadFeatures_AreReported`**, and
  **`DuplicateSymbolNames_AreReported`**. **Why one test per category:** each names a real
  transcription mistake, so the suite reads as a catalog of what the format protects
  against.
- **`MalformedJson_FailsWithASlotMessageNotAParserStackTrace`** is beat 16, and the test
  name is the requirement.
- **`GameDefinitionFuzzTests.RandomLeafMutations_NeverCrash_AndKeepInvariantsWhenTheyLoad`**
  mutates random values in a valid file and asserts two things at once: no crash, and any
  file that still loads still satisfies the invariants. **Why both halves:** "A loader
  that never crashes by accepting everything is worse than one that throws."
- **`TruncatedOrCorruptedJson_NeverCrashes_AndAlwaysReportsAnErrorWhenItFails`** and
  **`ExtremePayValues_AreRejectedWithAClearError_NeverCrashTheLoader`** cover the inputs
  nobody writes on purpose. This is a public-facing parse boundary, and fuzzing is what
  boundaries get.
- **`BadPayUnitStrings_AreAlwaysReportedByName`** pins beat 10 from the outside: an
  unrecognized unit is named rather than defaulted.
- **`OrcaDiveParSheetTests`** checks the loaded game against an outside document.
  **`CombinationTable_ReproducesThePublishedCountsExactly`** and
  **`ExpectedReturns_MatchThePublishedPercentages`** check the loaded game against numbers
  printed on somebody else's document. **Why that is different from every other test:**
  "Every other test in this repo checks our code against our expectations. This one checks
  it against an external authority that has never heard of us."
  **Read the numbers rather than gesturing at them:** total return 86.111%. Line pay
  59.601%, bonus 26.510%. Line hit frequency 10.258%, which is one paying line in
  roughly every 9.7 spins. All thirty-two combination counts reproduce exactly, as
  integers: 1,516,294 winning combinations out of 14,781,416, with no tolerance
  anywhere.
- **`PenguinScatter_HasDisjointWindowsAndThePublishedTriggerRate`** is beat 11 measured,
  and **`PickBonus_HonestPicks_ConvergeOnTheClosedForm`** checks the simulated bonus
  against its own closed form.
- Run all three classes. Green.

## 24:30–25:30 — Wrap

- A game is a file. The type that holds it can only be built by the loader, the loader
  reports everything wrong at once, and the checks were chosen from the mistakes people
  make transcribing a par sheet.
- Three claims: games as data can be diffed and reviewed, declared geometry is redundancy
  that earns its place, and the seams from episodes 3 and 5 absorbed wilds and a scatter
  bonus with no type changing shape.
- "This game has about 11,600 possible outcomes. Next episode we walk a space that size
  end to end. For the preset version of a three-reel game it comes out at 22 cubed,
  10,648: small enough to visit every one."
- Next: "Proving the machine. Three independent implementations, one of which visits every
  outcome the game can produce, and the ten-million-spin run that finishes the build."

---

## Recording notes

- Engine-to-browser budget: roughly twenty-two minutes in Rider and the test runner, under
  three in the browser. If a take runs long, browser time goes first.
- Strongest visuals in order: the six-error list appearing all at once from the broken
  file, the ragged `reelStops` in `orca-dive.json`, and the validation lab's list shrinking
  by exactly one line per fix.
- Zoom hotkey belongs on: the `substitutesFor` array, the `symbolCounts` block, the
  `Continues` and `IsRequired` pair with its doc comment, and the `internal` keyword on the
  `GameDefinition` constructor.
- The three paste blocks are the finished files verbatim, including the JSON. If a paste
  lands wrong, cut and re-paste rather than hand-fixing: the file has to match the repo.
- Running long? Drop the builder flash and compress beat 14 to one sentence. Keep beat 8
  (`Continues` versus `IsRequired`), beat 11 (the window-coupled bonus), beat 13 (collected
  errors), and the test section whole.
- The companion site runs the engine's own loader server-side, so if a lab ever disagrees
  with the walkthrough, the lab is reporting a real change in the repo.
