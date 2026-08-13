# Episode 5 — Counting Every Outcome Without Playing Every Spin

**Target:** 18–22 minutes.
**Companion article:** `docs/articles/05-weighted-enumeration.md`
**Code:** `CSharp/src/MMP.SlotGame.Core/Games/GameAnalyzer.cs`

## 0:00–2:30 — Start with 24 outcomes

**Scene:** a simple table or whiteboard, not the source code.

Draw three short reels:

- Reel 1: cherry, cherry, bell
- Reel 2: cherry, bell
- Reel 3: cherry, cherry, cherry, bell

There are 24 stop combinations. Group each reel by symbol. Three cherries represents
`2 × 1 × 3 = 6` of those combinations.

Say: “I can check the same cherry result six times, or check it once and write a six beside
it. This analyzer writes the six.”

Use the coin-jar analogy from the article: count every coin, or group coins by denomination
and multiply count by value.

## 2:30–4:00 — Show the two exact calculators

**Scene:** project tree with `Games/GameAnalyzer.cs` and `Rtp/AnalyticMath.cs` visible.

Explain the boundary:

- `GameAnalyzer` handles a loaded, single-payline game with wilds and a scatter bonus.
- `AnalyticMath` handles the built-in model, including multiple paylines.
- Neither one runs random spins.

Do not teach the formulas yet. The audience only needs to know why both files exist.

## 4:00–5:00 — `Analyze`

**Scene:** `Analyze` method.

Read its comment, then show the one-payline guard. Explain that simulation still works for
multi-line games. Exact variance is the missing part because two lines can read the same reel
and influence each other.

## 5:00–7:30 — Constructor and `BuildWeights`

**Scene:** constructor, then `BuildWeights`.

Walk through one reel position. Point to the symbol on the payline, then scan the visible
rows for a scatter.

Put these two labels on screen:

```text
anyStop     = payline shows this symbol
triggerStop = payline shows this symbol AND the window shows the scatter
```

Use a concrete count: “Five positions put cherry on the line. Two of those five also show a
scatter. The analyzer stores five and two.”

## 7:30–8:30 — `ScatterInWindow`

The method checks each visible row and returns when it finds the scatter. Mention strip
wrapping only if the viewer asks how `At` handles the end of the reel; episode 3 owns that
explanation.

## 8:30–9:30 — `GuardEnumerationSize`

Show that the guard multiplies distinct symbol choices, not physical stop counts. Eight
symbols on five reels means 32,768 branches. The 200-million limit prevents an accidental
definition from tying up the application.

## 9:30–13:00 — `Descend`, one box at a time

**Scene:** keep the three blank reel boxes beside the source.

Step through the recursion:

1. Pick reel 1's symbol.
2. Call the same method for reel 2.
3. Call it again for reel 3.
4. When every box is filled, call `Accumulate`.
5. Return and try the next choice.

Trace cherry/cherry/cherry. Write its weight after each reel: 2, then 2, then 6.

Then point at the skip:

```csharp
if (any[symbol] == 0) continue;
```

Say: “If this reel has no lemon, there is no lemon branch. We do not spend time evaluating an
outcome the reel cannot produce.”

## 13:00–15:30 — `Accumulate`

Show the five running totals. Give each one its plain-language label from the article table.

Spend extra time on `_payTriggerUnits`: it counts line payout on spins that also trigger the
bonus. That overlap is required for total variance.

Explain scaled integers with one example: 2.25 is stored as 225 while counting. Conversion
happens once at the end.

## 15:30–18:30 — `Summarize`

Put these on screen one at a time:

```text
line RTP  = total weighted line pay / all outcomes
bonus RTP = trigger chance × average bonus award
total RTP = line RTP + bonus RTP
```

For variance, use `variance = average(X²) - average(X)²`. State what X means: the total payout
from one spin.

Do not derive the expanded line-plus-bonus equation on camera unless the audience needs it.
Explain its purpose: spins where the line and bonus pay together must be included.

## 18:30–20:00 — Run the analyzer

**Scene:** the chapter 8 enumeration lab (`#/ch08`) or a focused test.

Run Classic Three Reel, then Orca Dive. Show physical stop combinations beside weighted symbol
combinations and compare the RTP result with exhaustive enumeration.

Close by returning to the 24-outcome table: “We did not sample the jar. We counted every coin,
in stacks.”

## Recording notes

- Keep the small reel table visible during the recursion section.
- Avoid opening with the word “tuple.” Introduce “symbol combination” first; mention that the
  code and mathematics may call it a tuple afterward.
- If the episode runs long, shorten the variance derivation. Do not shorten the `Descend`
  walkthrough; that is the reason this episode exists.
- Episode 4 remains the detailed lesson on closed-form RTP and multi-line covariance. Episode 5
  teaches weighted enumeration and refers back to episode 4 for the alternate method.
