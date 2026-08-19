# Episode 5 — Counting Every Outcome Without Playing Every Spin

**Target:** 18–22 minutes.
**Companion article:** `docs/articles/05-weighted-enumeration.md`
**Code:** `CSharp/src/MMP.SlotGame.Core/Games/GameAnalyzer.cs`

## 0:00–2:30 — Start with 24 outcomes

**Scene:** a simple table or whiteboard, not the source code.

Open with: "The math is a little more complex in this episode because one spin can pay
several lines and start a bonus. These are standard probability formulas. We will take
them one question at a time: average, swinginess, and awards that happen together."

Draw three short reels:

- Reel 1: cherry, cherry, bell
- Reel 2: cherry, bell
- Reel 3: cherry, cherry, cherry, bell

There are 24 stop combinations. Group each reel by symbol. Three cherries represents
`2 × 1 × 3 = 6` of those combinations.

Say: "I can check the same cherry result six times, or check it once and write a six beside
it. This analyzer writes the six."

Use the coin-jar analogy from the article: count every coin, or group coins by denomination
and multiply count by value.

## 2:30–4:00 — Show the two exact calculators

**Scene:** project tree with `Games/GameAnalyzer.cs` and `Rtp/AnalyticMath.cs` visible.

Explain the boundary:

- `GameAnalyzer` handles loaded games with wilds and a scatter bonus. It uses weighted symbol
  groups for one line and physical-window entries for several lines.
- `AnalyticMath` handles the built-in model, including multiple paylines.
- Neither one runs random spins.

Do not teach the formulas yet. The audience only needs to know why both files exist.

## 4:00–5:00 — `Analyze`

**Scene:** `Analyze` method.

Read its comment, then show the two branches. One payline creates `Enumeration`, which groups
repeated line symbols. Several paylines call `AnalyzePhysicalOutcomes`, which squares the
combined line award already stored for each physical window.

## 5:00–7:30 — Constructor and `BuildWeights`

**Scene:** constructor, then `BuildWeights`.

Before opening the constructor, show the article's five-reel Blue7/Penguin window. Trace the
three Blue7 symbols across the center payline, stop at Seal, then point to Penguin on reels
1, 3, and 5. State that this one stopped window produces both a line award and a bonus
trigger.

Then show the Two-Line Tide window. Read Pearl across the Top line for 5X and Shell across
the Center line for 3X. Point to Starfish on reels 1 and 3. Write the lookup result beside
the window: `Top + Center = 8X; StarfishBonus triggered`. Explain that the spin loop plays
the bonus and returns either 8X or 10X for this fixture.

Put the article's `BuildWeights` diagram on screen. Follow one reel stop from left to right:
first check the payline symbol, then scan the visible positions for a scatter. Pause on the
two counters after each Yes branch.

Put these two labels on screen:

```text
anyStop     = payline shows this symbol
triggerStop = payline shows this symbol AND the window shows the scatter
```

Use the two-row Orca Dive table beside the diagram: "Two reel-1 stops put Salmon on the
center line. One of those windows also shows Penguin. The analyzer stores two and one."

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

Say: "If this reel has no lemon, there is no lemon branch. We do not spend time evaluating an
outcome the reel cannot produce."

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

Before the formula, show two ten-spin lists: ten 0.5X awards, then nine zeroes and one 5X
award. Both average 0.5X. Ask which game feels more swingy. Explain that squaring stops low
and high results from canceling and gives large jumps more weight. The square root turns
variance back into wager units; that result is standard deviation, written as sigma.

Then show the commented `meanLine`, `meanLineSquared`, `meanLineTimesTrigger`, `meanSquared`,
and `Math.Sqrt` excerpt from the article. Explain the purpose of the middle term: spins where
the line and bonus pay together must be included. Do not expand beyond that unless the
audience needs the algebra.

## 18:30–20:00 — Run the analyzer

**Scene:** the chapter 5 page (`#/ch05`) — Lab 1 "Build one weighted outcome" and
Lab 2 "Prove that no outcomes disappeared" — or a focused test.

Run Classic Three Reel, then Orca Dive. Show physical stop combinations beside weighted symbol
combinations and compare the RTP result with exhaustive enumeration.

Close by returning to the 24-outcome table: "We did not sample the jar. We counted every coin,
in stacks."

## Recording notes

- Keep the small reel table visible during the recursion section.
- Avoid opening with the word "tuple." Introduce "symbol combination" first; mention that the
  code and mathematics may call it a tuple afterward.
- If the episode runs long, shorten the variance derivation. Do not shorten the `Descend`
  walkthrough; that is the reason this episode exists.
- Episode 4 remains the detailed lesson on closed-form RTP and multi-line covariance. Episode 5
  teaches weighted enumeration and refers back to episode 4 for the alternate method.
