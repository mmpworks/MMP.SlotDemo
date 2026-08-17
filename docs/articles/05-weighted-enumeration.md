# Counting Every Outcome Without Playing Every Spin

*Part 5 in a series on building a slot game engine in C#. This article explains
`GameAnalyzer`, one method at a time. The intended reader knows basic loops and arrays but
does not need a statistics background.*

A slot simulator answers a question by sampling: play many random spins, add the payouts,
and watch the average settle. `GameAnalyzer` takes a different route. It counts the possible
outcomes and calculates the exact average.

The direct method would try every stop on every reel. Orca Dive has 14,781,416 stop
combinations. That is possible, but much of the work repeats. If a cherry appears at four
different stops on reel 1, the payline evaluator sees the same cherry all four times.

`GameAnalyzer` evaluates that cherry once and gives it a weight of four.

## A small example

Imagine a game with three reels. We care about one payline.

| Reel | Symbols on its stops |
|---|---|
| 1 | Cherry, Cherry, Bell |
| 2 | Cherry, Bell |
| 3 | Cherry, Cherry, Cherry, Bell |

There are `3 × 2 × 4 = 24` physical stop combinations.

Now collapse repeated symbols into counts:

| Reel | Cherry count | Bell count |
|---|---:|---:|
| 1 | 2 | 1 |
| 2 | 1 | 1 |
| 3 | 3 | 1 |

Only eight symbol combinations remain: cherry or bell on each of three reels. Each one has
a weight. Three cherries has weight `2 × 1 × 3 = 6`, so it represents six of the 24 physical
outcomes. Bell, cherry, bell has weight `1 × 1 × 1 = 1`.

Add all eight weights and the total comes back to 24. Every outcome is still there,
counted rather than estimated. The repeated work is simply grouped.

### Check your understanding

The combination Cherry / Bell / Cherry has counts 2, 1, and 3. How many physical stop
combinations does it represent?

<details><summary>Answer</summary>

`2 × 1 × 3 = 6`. The weight counts where those same visible symbols came from on the
physical strips.

</details>

An everyday analogy is counting a jar of coins. You can list every coin separately, or make
one stack of quarters, one stack of dimes, and multiply each stack's count by its value. Both
methods produce the same total. `GameAnalyzer` makes stacks of identical reel outcomes.

> 🧪 **Try it live.** Open the companion site at <http://localhost:5090/#/ch05>.
> Build one weighted combination, then add all eight weights and confirm that they still
> represent the original 24 physical outcomes.

## The class has two layers

The public `Analyze` method checks whether the game is supported. The private `Enumeration`
object performs one calculation and holds its running totals.

```csharp
public static GameAnalysis Analyze(GameDefinition definition)
{
    ArgumentNullException.ThrowIfNull(definition);

    if (definition.Paylines.Count != 1)
        throw new NotSupportedException(...);

    return new Enumeration(definition).Run();
}
```

That one-payline limit has a reason behind it. Average returns from several lines add
together cleanly. Their variance does not, because two paylines can read different rows
of the same reel and their results then move together. `AnalyticMath` handles that
relationship for the built-in games. `GameAnalyzer` has yet to combine that work with
wild substitutions and window-based bonuses.

## Preparing the counts

The `Enumeration` constructor creates a `WinEvaluator`, a reusable payline buffer, and two
count tables by calling `BuildWeights`.

`BuildWeights` checks every physical stop once. For each symbol on each reel, it records:

- `anyStop`: how many stops place that symbol on the payline;
- `triggerStop`: how many of those stops also show the bonus scatter somewhere in the
  visible window.

Suppose five stops place a cherry on the payline. Two of those five positions also show a
scatter above or below it. The counts for that cherry are five and two.

Why keep both? A line win and a bonus trigger can happen on the same spin. The analyzer must
count their overlap to calculate variance correctly. Adding line variance and bonus variance
as if they were unrelated would lose that overlap.

`ScatterInWindow` performs the small check used while building the table:

```csharp
for (var row = 0; row < reels.Rows; row++)
{
    if (reels.At(reel, stop, row).Id == scatterId) return true;
}
return false;
```

It looks through the visible rows at one reel position and returns as soon as it finds the
scatter.

## Checking the amount of work

`GuardEnumerationSize` multiplies the number of distinct symbols found on each reel. A game
with 8 choices on each of 5 reels needs `8⁵`, or 32,768 branches. Repeated physical stops do
not add branches because their counts are stored in the weights.

The analyzer refuses more than 200 million branches. This guard prevents a bad or unusually
large definition from making the application appear frozen. It is a time and memory safety
limit, not a rule of slot mathematics.

## How `Descend` builds combinations

`Descend` performs the recursive walk:

```csharp
private void Descend(int reel, long weight, long triggerWeight)
{
    if (reel == _definition.ReelCount)
    {
        Accumulate(weight, triggerWeight);
        return;
    }

    var any = _anyStop[reel];
    var trigger = _triggerStop[reel];
    for (byte symbol = 0; symbol < any.Length; symbol++)
    {
        if (any[symbol] == 0) continue;
        _cells[reel] = symbol;
        Descend(reel + 1, weight * any[symbol], triggerWeight * trigger[symbol]);
    }
}
```

Think of filling three blank boxes, one box per reel.

1. Pick a possible symbol for reel 1 and put it in box 1.
2. For that choice, pick a possible symbol for reel 2 and put it in box 2.
3. For those two choices, pick a possible symbol for reel 3 and put it in box 3.
4. With every box filled, evaluate the payline.
5. Return to the last box and try its next symbol.

This is recursion: the method handles one reel, then calls itself for the next reel. When
`reel` equals the reel count, there are no blank boxes left.

The `weight` travels with the choices. If the selected symbols appear 2, 1, and 3 times,
the final weight is six. `Accumulate` therefore adds that result six times with one
multiplication.

`if (any[symbol] == 0) continue` skips any symbol the reel never carries, so a reel
branches only on symbols it can actually show.

## What `Accumulate` records

Once `_cells` contains one symbol for every reel, `Accumulate` asks `WinEvaluator` whether
the payline wins.

It records five kinds of totals:

| Field | What it counts |
|---|---|
| `_hits` | Physical stop combinations that produce a line win |
| `_payUnits` | Weighted sum of line-pay multipliers |
| `_paySquareUnits` | Weighted sum of squared line-pay multipliers |
| `_payTriggerUnits` | Line pay on combinations that also trigger the bonus |
| `_triggerWeight` | Physical stop combinations that trigger the bonus |

The squared pay is needed for variance. The combined pay-and-trigger count is needed because
the two events can occur together.

Payout multipliers stay as scaled integers during this loop. A multiplier of 2.25 is stored
as 225 when the scale is 100. This avoids rounding on every combination. `Summarize` converts
the totals to `double` once, after all counting is finished.

## What `Summarize` calculates

`Summarize` divides each weighted total by the number of physical stop combinations.

The line RTP is:

```text
weighted line payouts ÷ multiplier scale ÷ stop combinations
```

The bonus RTP is:

```text
chance of triggering × average bonus award
```

Total RTP is the sum of those two averages.

Standard deviation needs the average squared total payout as well as the average payout.
For a random payout `X`:

```text
variance = average(X²) - average(X)²
standard deviation = square root of variance
```

The total payout can contain both a line win and a bonus award. Expanding their square creates
a cross term, which is why `_payTriggerUnits` exists. In ordinary language, the calculation
must include spins where both parts pay together.

The final `GameAnalysis` reports exact combination counts, RTP values, trigger frequency, and
standard deviation. "Exact" here means that every modeled outcome is counted. The last
division still uses floating-point numbers for ratios.

## How this differs from the `Rtp` directory

The project has two exact calculation paths.

`GameAnalyzer` groups identical payline-symbol outcomes from a loaded game. It supports wilds
and a window-based scatter bonus, but currently requires one payline.

`AnalyticMath` uses probability formulas for built-in games. It handles several paylines by
calculating the covariance between each pair. Its version 1 features are independent of the
reel window.

Both avoid random sampling. They solve different game models.

## A walkthrough checklist

To re-read the code in order, hold one question in mind per method:

| Method | Question it answers |
|---|---|
| `Analyze` | Is this game supported, and where does analysis begin? |
| `Enumeration` constructor | What data must be prepared once? |
| `BuildWeights` | How many physical stops does each symbol choice represent? |
| `ScatterInWindow` | Does this reel position help trigger the bonus? |
| `GuardEnumerationSize` | Will this calculation finish in a reasonable amount of work? |
| `Run` | What are the two stages of the calculation? |
| `Descend` | How do we generate every symbol combination? |
| `Accumulate` | What does one completed combination contribute? |
| `Summarize` | How do counts become RTP and standard deviation? |

Start from the 24-outcome example. Once you see why three cherries carry a weight of
six, the production recursion turns into plain bookkeeping.

*Source files: `CSharp/src/MMP.SlotGame.Core/Games/GameAnalyzer.cs` and
`CSharp/src/MMP.SlotGame.Core/Rtp/AnalyticMath.cs`.*

## Optimization notebook

Weighted enumeration is already an algorithmic optimization: repeated physical stops share
one branch and carry a count. Resist micro-optimizing recursion until the combination guard
and independent exhaustive tests are in place. If analysis later becomes slow, profile branch
count, category evaluation, and dictionary accumulation separately.
