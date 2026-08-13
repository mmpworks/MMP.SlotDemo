# Episode 4 — The Par Sheet in Code: Exact RTP and Variance

**Target:** 26–29 min. **Format:** create the file, paste the finished source, then
walk it. The whiteboard carries the derivation; the paste carries the transcription.
**Subject:** the engine. The companion site appears three times, for under three
minutes total, and only to make an engine claim visible.
**Companion article:** `docs/articles/04-paytable-math.md`
**Companion site:** MMP.SlotDemo, branch `main`, page `#/ch04`
**Files created on camera:** `CSharp/src/MMP.SlotGame.Core/Paytables/Paytable.cs`,
`PaytableSolver.cs`, `CSharp/src/MMP.SlotGame.Core/Rtp/AnalyticMath.cs`.
**Shown, not created:** `CSharp/src/MMP.SlotGame.Core/Features/FeatureSchedule.cs`.

> **Discipline note for this recording.** This is the slowest episode of the series and
> the one that needs the most rehearsal. The labs illustrate; they do not carry it.
> Derive on the whiteboard, transcribe in Rider, and cut to the browser only where a
> number is easier to see than to describe.

---

## Prep checklist

**Repo — the subject**
- [ ] Rider on `CSharp/MMP.SlotDemo.slnx`, tree expanded to `MMP.SlotGame.Core`
- [ ] `Paytables/` and `Rtp/` folders present, the three target files moved aside so
      they get created on camera
- [ ] `Features/FeatureSchedule.cs` open in a background tab for the flash beat
- [ ] Test runner loaded: `SolverTests`, `FractionalPayUnitTests`,
      `StatisticalConvergenceTests`
- [ ] Clipboard manager staged with Block A, then Block B, then Block C
- [ ] Whiteboard with three areas planned in advance: exactly-k, expected value, and
      variance of a sum

**Companion site — the illustration**
- [ ] `E:\dev\MMP.SlotDemo`, branch `main`
- [ ] `cd CSharp/web && npm run build`, then `dotnet run --project CSharp/src/SlotDemo.Server`
- [ ] `http://localhost:5090/#/ch04`, all three labs run once — Lab 1 "Solve a paytable,"
      Lab 2 "The band, priced before any spin," Lab 3 "Orca Dive: the paytable that
      arrived fixed"
- [ ] `logs/` cleared so the viewer starts empty

**OBS**
- [ ] Scenes: `WHITEBOARD`, `RIDER`, `BROWSER`
- [ ] Zoom-to-mouse hotkey bound and tested at code-reading zoom
- [ ] Marker thick enough that subscripts survive 1080p

---

## 0:00–1:15 — Cold open

**Scene:** BROWSER, the convergence chart with its band, then cut to RIDER.

- "Every commercial slot machine ships with a par sheet. It is a closed-form proof of
  the machine's return, and there is no simulation anywhere on it."
- "Today we write that document as code. Exact expected return, and the exact standard
  deviation this shaded band is made of."
- "Three files. One holds the table, one solves it to a target, and one does the math.
  By the end, the target RTP is one division and the band is one square root."
- Set the format: "Whiteboard first for each idea, then the finished file goes in and
  we read the transcription."

## 1:15–6:30 — Expected value, derived on the whiteboard

**Scene:** WHITEBOARD.

1. **Setup.** "A payline reads one cell per reel. Episode 3's correlations live *within*
   a reel, and a line touches each reel once, so for expected value the marginals are
   enough. Hold that word — for expected value. Variance is a different story and we
   get to it."
2. **Write the event.** Exactly k leading Sevens means reels 0 through k−1 show the
   symbol and reel k does not.
   ```
   P(exactly k) = p₀ · p₁ · … · p_{k−1} · (1 − p_k)
   ```
   with the trailing factor dropping out when k equals the reel count.
3. **Dwell on that trailing factor.** "Leave it off and three-of-a-kind quietly includes
   every four and five as well. You pay some windows twice in expectation, and RTP
   comes out high by a few tenths of a percent. High enough to be wrong and small enough
   to look plausible."
4. **Sum it up.** Expected value is the sum over symbols, run lengths, and lines of the
   pay times its probability. Cost is on the order of reels times symbols per line.
   Microseconds.
5. **Land the scale.** "The five-reel stop space here is about 11.5 billion combinations.
   We just declined to visit them and got the exact answer anyway."
6. **Then the consequence:** the analytic result returns synchronously on
   every configuration change, so the chart knows its target before spin one.

## 6:30–7:15 — Create the first file

**Scene:** RIDER.

- New directory `Paytables`, new file. **Path on screen and said out loud:**
  `CSharp/src/MMP.SlotGame.Core/Paytables/Paytable.cs`
- Paste **Block A**. "Two records and a generator. The comments outweigh the code."

### Block A — `CSharp/src/MMP.SlotGame.Core/Paytables/Paytable.cs`

```csharp
using MMP.SlotGame.Core.Money;

namespace MMP.SlotGame.Core.Paytables;

/// <summary>
/// Canonical paytable: pay multipliers (of the TOTAL spin bet, not a single line's
/// share of it — see <see cref="PaytableSolver.Solve"/>'s wager doc comment) for
/// k-of-a-kind runs, k ≥ 3, left-to-right. Dimensionless — the solver turns this into
/// integer millicents via one scalar, <c>paytableScaleFactor</c>.
/// </summary>
public sealed record Paytable
{
    public Paytable(IReadOnlyDictionary<(byte SymbolId, int Count), double> pays)
    {
        ArgumentNullException.ThrowIfNull(pays);
        Pays = new System.Collections.ObjectModel.ReadOnlyDictionary<(byte SymbolId, int Count), double>(
            new Dictionary<(byte SymbolId, int Count), double>(pays));
    }

    public IReadOnlyDictionary<(byte SymbolId, int Count), double> Pays { get; }

    /// <summary>
    /// The v1 preset/solver pipeline's minimum k-of-a-kind that pays anything — the
    /// "no pair" rule of a classic/video slot. <see cref="CanonicalFor"/> never
    /// generates an entry below this, and <see cref="Paylines.LinePayEvaluator"/>'s
    /// own run-length gate must stay at the same value: lowering one without the
    /// other either creates pay entries that can never be reached (paytable ahead of
    /// the evaluator) or a run length the evaluator would pay with nothing in the
    /// table to look up (evaluator ahead of the paytable). This governs only the
    /// preset/solver pipeline, not the JSON <c>GameDefinition</c> path, where a pay
    /// table entry pays at whatever run length its own data declares.
    /// </summary>
    public const int MinimumWinningRun = 3;

    public double PayFor(byte symbolId, int count) =>
        Pays.GetValueOrDefault((symbolId, count));

    /// <summary>
    /// Canonical multipliers per symbol set: premiums pay steep, commons pay shallow.
    /// <see cref="PaytableSolver"/> scales the whole table to hit target RTP, so the
    /// ratios between these entries are what carry over.
    /// </summary>
    public static Paytable CanonicalFor(int reelCount, int symbolCount)
    {
        var pays = new Dictionary<(byte, int), double>();
        for (byte s = 0; s < symbolCount; s++)
        {
            // Teaching curve: symbol 0 is the premium and later symbols pay less.
            var basePay = 60.0 / Math.Pow(2.2, s);
            for (var k = MinimumWinningRun; k <= reelCount; k++)
            {
                // each extra matching reel roughly 5×
                pays[(s, k)] = basePay * Math.Pow(5, k - MinimumWinningRun);
            }
        }
        return new Paytable(pays);
    }
}

/// <summary>
/// Payout transform produced by the solver as a closure over
/// <c>paytableScaleFactor</c>.
/// </summary>
public delegate Millicents PayoutScaler(double rawPayMultiplier);

/// <summary>
/// The realized game: integer-millicent pays. The analytic calculator and the spin
/// evaluator both read this instance, so they share one rounding residual.
/// </summary>
public sealed record ScaledPaytable
{
    public ScaledPaytable(IReadOnlyDictionary<(byte SymbolId, int Count), Millicents> pays)
    {
        ArgumentNullException.ThrowIfNull(pays);
        Pays = new System.Collections.ObjectModel.ReadOnlyDictionary<(byte SymbolId, int Count), Millicents>(
            new Dictionary<(byte SymbolId, int Count), Millicents>(pays));
    }

    public IReadOnlyDictionary<(byte SymbolId, int Count), Millicents> Pays { get; }

    public Millicents PayFor(byte symbolId, int count) =>
        Pays.GetValueOrDefault((symbolId, count), Millicents.Zero);
}
```

## 7:15–12:00 — Walk `Paytable`

### Beat 1 — two types for two stages of the same table

`Paytable` holds dimensionless multipliers as `double`. `ScaledPaytable` holds integer
`Millicents`. Same information at two stages.

- Before the solver runs, only the ratios exist and floating point is the right
  representation for a ratio.
- After the solver runs, the numbers are money, and episode 2 said money is an integer.
  The type change is where that promotion happens, and it happens once.
- "If one type carried both stages, every reader would have to know which stage they
  were in."

### Beat 2 — the canonical table is a shape, not a price list

`CanonicalFor` builds a curve: symbol 0 is the premium, each later symbol pays about
2.2 times less, and each extra matching reel pays about five times more.

- **Then, slowly:** "Only the ratios here matter. Expected value is
  linear in the pays, so the solver can multiply the whole table by one number and hit
  any target it likes. Choosing the shape and choosing the return are separate
  decisions, and this method only makes the first one."
- The magic numbers 60, 2.2, and 5 are a teaching curve rather than a real par sheet,
  and the comment says so. A real game brings its own table through the JSON path in
  episode 7.
- One table per symbol set. The shape is generated from the geometry, so there is no
  per-preset table to keep in sync.

### Beat 3 — `MinimumWinningRun`, and a constant that names its own coupling

The longest comment in the file sits on a `const int` equal to 3. Read it aloud, then
say why it runs thirteen lines.

- The paytable generator and the line evaluator both need the same idea of the shortest
  paying run. Lower one and not the other and you get one of two silent failures: pay
  entries that can never be reached, or a run length the evaluator pays with nothing in
  the table to look up.
- Neither failure throws. The first wastes memory and the second pays zero, so a game
  quietly returns less than its par sheet claims.
- The comment also scopes the rule: it governs the preset pipeline, and the JSON path
  pays at whatever run length its own data declares.
- **The point:** "The coupling exists whether or not anyone writes it down. The constant
  is where a maintainer will be standing when it matters."

### Beat 4 — the read-only wrapper around a copy

Both records copy the incoming dictionary and then wrap it read-only.

- Copy first, because the caller still holds their dictionary and a paytable is shared
  by every worker for the whole run.
- Wrap second, because the copy is now this object's private state and the property
  hands it to callers.
- Same move as the reel set in episode 3, same reason. "Take ownership by copying, and
  afterwards there is nothing to synchronize."

### Beat 5 — invariant R1, stated where it lives

Read the `ScaledPaytable` doc comment out loud. The analytic calculator and the spin
evaluator both read *this instance*.

- Rounding leaves a residual, so realized RTP is not the target to infinite precision.
  That residual is shared by both computations, which makes it a resolution limit
  rather than a disagreement.
- "Apply the scale factor twice, in two places, and the fourth decimal starts
  disagreeing at ten million spins. It looks like a convergence bug, and you
  can lose a weekend to it."
- **The DRY reading:** repeated *knowledge* is the violation, even where no code is
  duplicated.

## 12:00–12:45 — Create the second file

**Scene:** RIDER.

- New file. **Path on screen:** `CSharp/src/MMP.SlotGame.Core/Paytables/PaytableSolver.cs`
- Paste **Block B**. "Thirty-nine lines, half of them comment, and one line carries the
  idea."

### Block B — `CSharp/src/MMP.SlotGame.Core/Paytables/PaytableSolver.cs`

```csharp
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Reels;
using MMP.SlotGame.Core.Rtp;

namespace MMP.SlotGame.Core.Paytables;

/// <summary>
/// Finds the scalar paytableScaleFactor = targetBaseRtp / unscaledBaseGameEv and applies
/// it once, at paytable construction, producing integer millicents. Rounding is half-even,
/// which removes the low bias uniform truncation would introduce.
///
/// Each pay rounds independently, so the realized total can drift a hair from
/// targetBaseRtp. Read <see cref="AnalyticMath.RealizedBaseRtp"/>, recomputed from the
/// rounded table, for the number the game pays.
/// </summary>
public sealed class PaytableSolver
{
    /// <summary>
    /// <paramref name="targetBaseRtp"/> is a fraction (e.g. 0.75) derived from integer
    /// basis points upstream. <paramref name="wager"/> is the total spin bet: every line's
    /// award is scaled against that same total, and a spin's payout is the sum across all
    /// paylines. RTP throughout this pipeline therefore means return relative to the total
    /// amount wagered per spin, not relative to one line's share of it.
    /// </summary>
    public static ScaledPaytable Solve(StripReelSet reels, IReadOnlyList<Payline> lines, Paytable canonical, double targetBaseRtp, Millicents wager)
    {
        var unscaledBaseGameEv = AnalyticMath.BaseEvMultiplier(reels, lines, canonical);
        if (unscaledBaseGameEv <= 0)
            throw new InvalidOperationException("Canonical paytable has zero EV; cannot scale.");

        var paytableScaleFactor = targetBaseRtp / unscaledBaseGameEv;
        PayoutScaler scale = raw => new Millicents(
            (long)Math.Round(raw * paytableScaleFactor * wager.Value, MidpointRounding.ToEven));

        var scaled = canonical.Pays.ToDictionary(kv => kv.Key, kv => scale(kv.Value));
        return new ScaledPaytable(scaled);
    }
}
```

## 12:45–17:30 — Walk `PaytableSolver`

### Beat 6 — one closed-form scalar, and the search that never happens

Point at `targetBaseRtp / unscaledBaseGameEv` and let it sit.

- Because expected value is linear in the pays, doubling every pay doubles the expected
  value. So the factor that turns the canonical table into a table hitting the target is
  one division.
- **Say what this replaces:** "The obvious implementation is a search. Guess a factor,
  simulate or compute, adjust, repeat until you are close enough. That version has a
  tolerance, a maximum iteration count, and a convergence failure mode. This version has
  none of those, because the relationship is linear."
- **And it is repeatable:** the same inputs give the same table every time, with no
  iteration order and no starting guess to influence the result.

### Beat 7 — the guard that comes before the division

`unscaledBaseGameEv <= 0` throws, naming the cause.

- A canonical table with zero expected value would produce a division by zero and then
  an infinity that propagates silently into every pay.
- The message says the paytable has zero EV and cannot be scaled, which points at the
  input rather than at the arithmetic.
- Same failure philosophy as every other boundary in this series: refuse the work,
  explain the refusal.

### Beat 8 — round half to even, and the bias it removes

`MidpointRounding.ToEven` on the one line where money is created.

- Truncation points every rounding error the same direction, so RTP comes out
  systematically low. Across a full paytable that bias accumulates rather than
  cancelling.
- Round-half-even scatters the ties both ways, so the errors cancel instead of adding.
- **Then the second half**, read from the class comment: this removes the *systematic*
  bias without guaranteeing the rounded table lands exactly on the target. Each pay
  rounds independently, so the realized total drifts a hair.
- "The comment goes on to say what banker's rounding does not buy, which is what keeps
  the next person from trusting the target as the answer."

### Beat 9 — realized versus target, and which one is authoritative

`RealizedBaseRtp` recomputes RTP from the rounded integer table, and that number is the
one the 99% cap is checked against.

- The failure mode this prevents: a solver that reports its own target back as its
  analytic RTP. "That solver looks perfect and never checked what the table it produced
  pays."
- Checking the number the game pays costs one extra computation on a config
  change, which is microseconds.

### Beat 10 — `PayoutScaler` as a delegate, and the type that was declined

`PayoutScaler` is a delegate, and `scale` is a closure over the factor and the wager.

- The reflex from a SOLID-first habit is an `IPayoutScaler` interface, an
  implementation, and a decorator so scalers can compose.
- Closures already compose: `x => outer(inner(x))`, for free, with nothing to register.
- In C#, one behavior with no identity and no lifetime is a delegate. The interface,
  the class, the file, and the registration would add four things to read and change
  nothing the code can do.
- The delegate still has a name and a doc comment, so the concept stays visible in the
  domain vocabulary.

> **Illustration (50 seconds, BROWSER).** Chapter 4 page, Lab 1 — "Solve a paytable."
> Leave the subject on Orca Dive and type a target line RTP into the box: solve at 5960,
> the shipped figure, then re-solve at 6500 and 7000. The whole integer paytable
> recomputes server-side through the engine's own `PaytableSolver.Solve`, and four
> readouts move together — unscaled EV, the single scale factor, the realized base RTP,
> and the drift. "One cabinet, several approved payback versions, one factor between
> them." Then point at the drift: it lands a hair off the target and it lands on both
> sides across the three solves. "Both directions is a resolution limit. One direction
> every time would be a bias — which is what banker's rounding is there to prevent."
> Cut back.
>
> The lab has no rounding-mode control; the endpoint always rounds half-to-even. The
> bias half of the beat is made on the whiteboard, out of the three solves' drift signs.

## 17:30–19:30 — Variance on the whiteboard: the strips come due

**Scene:** WHITEBOARD.

1. **Why sigma at all.** The band on the chart is z times sigma over the square root of
   N. "Without an exact sigma the band is a guess, and every statistical test in episode
   8 would need a hand-tuned tolerance instead."
2. **Write the identity.** Var of a sum equals the sum of the variances plus twice the
   sum of the covariances over distinct pairs.
3. **Per-line variance is easy:** the same probabilities that gave expected value.
4. **The covariance is where episode 3 collects.** Two lines cross the same reels, and
   their cells on a reel are strip neighbors. They win together. "This is where the
   weighted-die model gives a wrong answer while agreeing with reality on every mean you
   can check."
5. Do not derive the algebra on camera. Point at the identity and move to the code.
6. **Why not just measure sigma?** Run a million spins, take the sample standard
   deviation, use that for the band. It works, it is four lines, and it is circular: the
   band would be derived from the same simulator it is supposed to be judging. When the
   simulator is wrong, the band moves with it and the run lands inside a wrong band.
   Computing sigma from the strips costs us the next twenty minutes and buys a band the
   simulator cannot influence. "That is why `JointRowSymbolTables` exists."

## 19:30–20:15 — Create the third file

**Scene:** RIDER.

- New directory `Rtp`, new file. **Path on screen:**
  `CSharp/src/MMP.SlotGame.Core/Rtp/AnalyticMath.cs`
- Paste **Block C**. It is the longest file in the series so far. "We walk four methods
  and read past the rest."

### Block C — `CSharp/src/MMP.SlotGame.Core/Rtp/AnalyticMath.cs`

> **Recording note:** this is the full file, pasted verbatim from
> `CSharp/src/MMP.SlotGame.Core/Rtp/AnalyticMath.cs`. Paste it whole on camera; the
> walkthrough below covers `BaseEvMultiplier`, `ExactlyKLeading`,
> `SigmaPerUnitWagered`, and `JointRowSymbolTables`, and reads past the two private
> helpers between them.

```csharp
using MMP.SlotGame.Core.Money;
using MMP.SlotGame.Core.Paytables;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Rtp;

/// <summary>
/// Analytic paytable calculations. They enumerate or combine the modeled outcomes rather
/// than sampling them; probabilities and moments are represented as <see cref="double"/>:
///
///  - Line EV uses the closed form over per-reel marginals. Rows of one line sit on
///    different reels, and reels are independent, so marginals suffice for EV.
///  - Line variance needs more: two lines share reels, and rows within a reel are
///    correlated by strip adjacency. Cov(line i, line j) therefore uses the per-reel
///    joint row-pair distribution, obtained by enumerating the S stops per reel. Joint
///    probability across reels still factorizes, because reels are independent.
///
/// σ here is the analytic, configuration-derived source of the convergence band; the
/// empirical Welford estimate cross-checks it.
/// </summary>
public static class AnalyticMath
{
    /// <summary>
    /// The unscaled base-game EV: the canonical (dimensionless) paytable's expected
    /// payout, summed across every payline, in wager-multiplier units. "Unscaled"
    /// because this reads the canonical table directly, before <c>paytableScaleFactor</c>
    /// (<see cref="Paytables.PaytableSolver.Solve"/>) turns it into real millicents.
    /// Summing across lines here, and in <see cref="RealizedBaseRtp"/> on the scaled
    /// table, fixes the basis for every RTP number this pipeline produces: the total
    /// spin wager, not one line's share of it.
    /// </summary>
    public static double BaseEvMultiplier(StripReelSet reels, IReadOnlyList<Payline> lines, Paytable canonical)
    {
        var ev = 0.0;
        foreach (var line in lines)
        {
            foreach (var ((symbolId, count), pay) in canonical.Pays)
                ev += pay * ExactlyKLeading(reels, line, symbolId, count);
        }
        return ev;
    }

    /// <summary>
    /// Realized base RTP from the integer paytable actually shipped, recomputed here
    /// rather than trusted from <c>paytableScaleFactor</c>: round-half-even
    /// (<see cref="Paytables.PaytableSolver.Solve"/>) removes systematic rounding bias,
    /// but it does not guarantee the rounded table lands exactly on the target — each
    /// pay rounds independently, so the realized total can drift a hair. This recompute
    /// is the authoritative number; the target RTP is only ever a target.
    /// </summary>
    public static double RealizedBaseRtp(StripReelSet reels, IReadOnlyList<Payline> lines, ScaledPaytable scaled, Millicents wager)
    {
        var ev = 0.0;
        foreach (var line in lines)
        {
            foreach (var ((symbolId, count), pay) in scaled.Pays)
                ev += pay.Value * ExactlyKLeading(reels, line, symbolId, count);
        }
        return ev / wager.Value;
    }

    /// <summary>
    /// P(line shows exactly k leading copies of symbol s): match reels 0..k-1,
    /// mismatch reel k (or k == ReelCount). Reels independent → product of marginals.
    /// </summary>
    public static double ExactlyKLeading(StripReelSet reels, Payline line, byte symbolId, int k)
    {
        var p = 1.0;
        for (var reel = 0; reel < k; reel++)
            p *= reels.ProbabilityOf(reel, symbolId);
        if (k < reels.ReelCount)
            p *= 1.0 - reels.ProbabilityOf(k, symbolId);
        return p;
    }

    /// <summary>
    /// Variance of the total per-spin return (base + features), per unit wagered.
    /// Base: Var(Σ lines) = Σ Var + 2 Σ Cov over line pairs.
    /// Features trigger independently of the window and of each other in the v1
    /// model, so their variances simply add.
    /// </summary>
    public static double SigmaPerUnitWagered(
        StripReelSet reels,
        IReadOnlyList<Payline> lines,
        ScaledPaytable scaled,
        IReadOnlyList<Features.FeatureSchedule> features,
        Millicents wager)
    {
        var joints = JointRowSymbolTables.Build(reels);
        var w = (double)wager.Value;

        // Per-line pay distributions in millicents.
        var lineMean = new double[lines.Count];
        var lineMeanSq = new double[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            foreach (var ((symbolId, count), pay) in scaled.Pays)
            {
                var p = ExactlyKLeading(reels, lines[i], symbolId, count);
                lineMean[i] += pay.Value * p;
                lineMeanSq[i] += (double)pay.Value * pay.Value * p;
            }
        }

        var variance = 0.0;
        for (var i = 0; i < lines.Count; i++)
            variance += lineMeanSq[i] - lineMean[i] * lineMean[i];

        for (var i = 0; i < lines.Count; i++)
        {
            for (var j = i + 1; j < lines.Count; j++)
            {
                var eProduct = ExpectedPairProduct(reels, joints, lines[i], lines[j], scaled);
                variance += 2.0 * (eProduct - lineMean[i] * lineMean[j]);
            }
        }

        foreach (var f in features)
            variance += f.VarianceMillicentsSquared();

        return Math.Sqrt(variance) / w;
    }

    /// <summary>
    /// E[pay_i · pay_j]: sum over both lines' (symbol, exact-run) outcomes of
    /// pay·pay·P(joint). Joint P = product over reels of the per-reel probability that
    /// BOTH lines' cell conditions hold, read from the joint row-pair tables.
    /// </summary>
    private static double ExpectedPairProduct(
        StripReelSet reels,
        JointRowSymbolTables joints,
        Payline lineA,
        Payline lineB,
        ScaledPaytable scaled)
    {
        var total = 0.0;
        foreach (var ((symA, runA), payA) in scaled.Pays)
        {
            if (payA.Value == 0) continue;
            foreach (var ((symB, runB), payB) in scaled.Pays)
            {
                if (payB.Value == 0) continue;
                var p = JointRunProbability(reels, joints, lineA, symA, runA, lineB, symB, runB);
                if (p > 0)
                    total += (double)payA.Value * payB.Value * p;
            }
        }
        return total;
    }

    private static double JointRunProbability(
        StripReelSet reels,
        JointRowSymbolTables joints,
        Payline lineA, byte symA, int runA,
        Payline lineB, byte symB, int runB)
    {
        var p = 1.0;
        for (var reel = 0; reel < reels.ReelCount && p > 0; reel++)
        {
            // Cell condition per line on this reel: Match (reel < run),
            // Mismatch (reel == run), or Any (reel > run).
            var condA = reel < runA ? Cond.Match : reel == runA ? Cond.Mismatch : Cond.Any;
            var condB = reel < runB ? Cond.Match : reel == runB ? Cond.Mismatch : Cond.Any;
            p *= joints.Probability(reel, lineA.Rows[reel], lineB.Rows[reel], condA, symA, condB, symB);
        }
        return p;
    }

    internal enum Cond { Match, Mismatch, Any }

    /// <summary>
    /// Per reel, per (rowA, rowB) pair: the joint distribution of the two window
    /// cells' symbols, built by one O(S) stop enumeration each. 3×3 row pairs × R
    /// reels, tiny and exact. When rowA == rowB the two cells are the same cell and
    /// the table is automatically diagonal — no special case needed.
    /// </summary>
    internal sealed class JointRowSymbolTables
    {
        private readonly double[][,][,] _tables; // [reel][rowA,rowB][symA,symB] — jagged over reels
        private readonly double[][][] _marginals; // [reel][row][sym]
        private readonly int _symbolCount;

        private JointRowSymbolTables(double[][,][,] tables, double[][][] marginals, int symbolCount)
        {
            _tables = tables;
            _marginals = marginals;
            _symbolCount = symbolCount;
        }

        public static JointRowSymbolTables Build(StripReelSet reels)
        {
            var symbolCount = 0;
            for (var reel = 0; reel < reels.ReelCount; reel++)
            {
                foreach (var s in reels.Strip(reel))
                    symbolCount = Math.Max(symbolCount, s.Id + 1);
            }

            var rows = reels.Rows;
            var tables = new double[reels.ReelCount][,][,];
            var marginals = new double[reels.ReelCount][][];
            for (var reel = 0; reel < reels.ReelCount; reel++)
            {
                var strip = reels.Strip(reel);
                var n = strip.Length;
                tables[reel] = new double[rows, rows][,];
                marginals[reel] = new double[rows][];
                for (var rowA = 0; rowA < rows; rowA++)
                {
                    marginals[reel][rowA] = new double[symbolCount];
                    for (var rowB = 0; rowB < rows; rowB++)
                        tables[reel][rowA, rowB] = new double[symbolCount, symbolCount];
                }

                for (var stop = 0; stop < n; stop++)
                {
                    for (var rowA = 0; rowA < rows; rowA++)
                    {
                        var a = strip[(stop + rowA) % n].Id;
                        marginals[reel][rowA][a] += 1.0 / n;
                        for (var rowB = 0; rowB < rows; rowB++)
                        {
                            var b = strip[(stop + rowB) % n].Id;
                            tables[reel][rowA, rowB][a, b] += 1.0 / n;
                        }
                    }
                }
            }
            return new JointRowSymbolTables(tables, marginals, symbolCount);
        }

        /// <summary>P(cell@rowA satisfies condA vs symA AND cell@rowB satisfies condB vs symB) on one reel.</summary>
        public double Probability(int reel, int rowA, int rowB, Cond condA, byte symA, Cond condB, byte symB)
        {
            // Inclusion–exclusion over the joint == table; marginals cover the Any/Mismatch sides.
            return (condA, condB) switch
            {
                (Cond.Any, Cond.Any) => 1.0,
                (Cond.Any, _) => Single(reel, rowB, condB, symB),
                (_, Cond.Any) => Single(reel, rowA, condA, symA),
                (Cond.Match, Cond.Match) => Joint(reel, rowA, rowB, symA, symB),
                (Cond.Match, Cond.Mismatch) => _marginals[reel][rowA][symA] - Joint(reel, rowA, rowB, symA, symB),
                (Cond.Mismatch, Cond.Match) => _marginals[reel][rowB][symB] - Joint(reel, rowA, rowB, symA, symB),
                (Cond.Mismatch, Cond.Mismatch) =>
                    1.0 - _marginals[reel][rowA][symA] - _marginals[reel][rowB][symB] + Joint(reel, rowA, rowB, symA, symB),
                _ => throw new ArgumentOutOfRangeException(nameof(condA)),
            };
        }

        private double Single(int reel, int row, Cond cond, byte sym) =>
            cond == Cond.Match ? _marginals[reel][row][sym] : 1.0 - _marginals[reel][row][sym];

        private double Joint(int reel, int rowA, int rowB, byte symA, byte symB) =>
            symA < _symbolCount && symB < _symbolCount ? _tables[reel][rowA, rowB][symA, symB] : 0.0;
    }
}
```

## 20:15–25:00 — Walk `AnalyticMath`

### Beat 11 — the file comment is the derivation

The class comment states the split the whiteboard just made: marginals are enough for
expected value because a line touches each reel once, and variance needs joint row-pair
distributions because two lines share reels and rows within a reel are correlated.

- "The comment states the mathematical claim the methods depend on, so a reviewer can
  disagree with the claim rather than reverse-engineering it from loops."
- The last line names sigma's role: the analytic value is the band source, and the
  empirical estimate is a cross-check that never becomes the authority.

### Beat 12 — `ExactlyKLeading` is the whiteboard, transcribed

Put the board and the method on screen together if the layout allows.

- A product over the matching reels, one guard for the trailing mismatch factor, and a
  return. Six lines.
- "When the math and the code correspond line for line, review becomes reading."
- `BaseEvMultiplier` and `RealizedBaseRtp` are the same double loop over lines and pays,
  one reading the canonical table and one reading the scaled table. The duplication is
  two lines, and a shared generic helper over two different value types would be harder
  to read than either copy.

### Beat 13 — `SigmaPerUnitWagered`, in three parts

Walk the method as three sections rather than line by line.

1. **Per-line means and second moments.** One pass over the pays per line, using the
   same `ExactlyKLeading` from the expected-value path. Variance per line is the second
   moment minus the square of the mean.
2. **Covariance over distinct line pairs.** The nested `i`, `j` loop with `j` starting
   at `i + 1`, and the `2.0 *` in front matching the identity on the whiteboard.
3. **Features add.** Feature triggers are independent of the window and of each other in
   this model, so their variances add with no covariance term. Point at the loop and say
   that independence out loud, because it is a modeling assumption rather than a fact
   about arithmetic.

Then the return: a square root and a division by the wager, so sigma comes out per unit
wagered and the band works at any bet size.

### Beat 14 — `JointRowSymbolTables`, the exact enumeration

**Scene:** RIDER, zoomed on `Build`.

- Per reel, per row pair, the joint distribution of the two cells' symbols, built by one
  pass over the S stops. For a five-reel three-row machine that is nine row pairs per
  reel over a few dozen stops. Tiny, and exact.
- **The edge case that handles itself:** when `rowA` equals `rowB` the two cells are the
  same cell, and the enumeration produces a diagonal table without a special case.
  "There is no branch for it: the counting cannot produce two different symbols in one
  cell."
- The build is O(S × rows²) per reel and runs once per configuration change, so the
  cost never reaches the spin loop.

### Beat 15 — the switch expression over the three conditions

**Scene:** RIDER, zoomed on `Probability`.

- Each line imposes one of three conditions on a reel: Match, Mismatch, or Any.
- Seven cases of inclusion and exclusion over the joint table and the marginals, written
  as one switch expression that fits on a screen.
- Read two cases and say the rest are the same idea. Match/Match reads the joint
  directly. Mismatch/Mismatch is one minus each marginal plus the joint back, because
  subtracting both marginals removed the overlap twice.
- The alternative is nested ifs with early returns: the same seven outcomes spread over
  thirty lines, with no way to see at a glance that every combination is covered.

> **Illustration (45 seconds, BROWSER).** Chapter 4 page, Lab 2 — "The band, priced
> before any spin." Price it once and read the two figures at the top: total RTP, and
> sigma per unit wagered. Say that the sigma is the closed-form number, covariance
> included, and that nothing has spun yet. Then walk the ladder underneath — each factor
> of a hundred in spins buys one decimal place of certainty, because the square root is
> in the denominator. "That is why proving an RTP takes millions of spins, and why the
> band is priced before the run rather than measured after it." Cut back.
>
> The lab has no covariance toggle and runs no simulation of its own — it reports the
> analytic figure. The "ignore the correlation and the band comes out the wrong width"
> line is made on the whiteboard, above, where the covariance term is derived.

## 25:00–25:45 — Flash the feature schedule

**Scene:** RIDER, `Features/FeatureSchedule.cs`, no walkthrough.

- Show it for twenty seconds. The shape: trigger probability fixed by the preset, target
  contribution given, and the mean award solved as contribution times wager over
  probability. One unknown, one equation, so no search here either.
- The award table is built so the third value is derived rather than chosen, which makes
  the mean land on the declared contribution to the millicent after rounding.
- "Features trigger independently of the window, which is why their variances added
  without a covariance term two beats ago. That independence is a v1 modeling decision,
  and episode 7 shows what changes when a real game's scatter bonus breaks it."

## 25:45–27:45 — The tests are part of the design

**Scene:** RIDER test runner, then TERMINAL.

- **`SolverTests`** exists for one failure mode, and the class comment names it: a solver
  that reports its own target back as its analytic RTP. Every assertion in the file
  recomputes RTP from the realized `ScaledPaytable` and compares that. **Why this shape:**
  "A test that asks the solver what it achieved is a test the solver can pass by
  remembering what it was asked for."
- **`DefaultConfig_RealizedRtp_IsWithinBudgetOf98Percent`** and
  **`AtTheCap_RealizedRtp_IsWithinBudget_AndStaysUnder99Percent`** use a per-preset
  budget of half the acceptance criterion. **Why tighter than required:** the extra
  margin is where a real regression shows up before it becomes a failure anyone argues
  about.
- **`BaseRtp_ScalesLinearlyWithTheBaseTarget`** doubles the target and expects the
  realized base RTP to very nearly double. **Why "very nearly" is right here:** the
  comment does the arithmetic: each realized RTP carries its own rounding residual, so
  the ratio can drift by a few parts in ten thousand. A solver that renormalized or
  re-solved per symbol would miss by percent rather than by that. "The budget is derived
  from the rounding, rather than picked until the test went green.
- **`ZeroEvCanonicalPaytable_ThrowsInsteadOfDividingByZero`** is beat 7's guard, tested
  from the outside.
- **`EveryScaledPay_IsAWholePositiveMillicentCount`** pins the type promotion from beat
  1: after the solver runs, nothing on the pay path is fractional.
- **`FractionalPayUnitTests`** covers the other direction: a game that needs 2.25 times
  the bet declares it in hundredths, and
  **`PayUnitHundredths_TwoAndAQuarterXPay_RealizesExactMillicentsOnAForcedWin`** proves it
  realizes as exactly 225,000 millicents at a one-credit wager with nothing to round.
  **Why it matters here:** the fractional multiplier is a convenience at the JSON level
  and never a floating-point value on the pay path.
- **`AnalyticLineRtp_AgreesWithSimulatedLineRtp_ForFractionalPays`** builds a synthetic
  game where a disagreement between the analytic and simulated readings of a hundredths
  pay would show up as a large, unmistakable factor rather than a rounding-sized drift.
- **`StatisticalConvergenceTests.Coverage_32Seeds_3MSpins_LandInThe99PercentBand`** is
  the test of the band itself. Thirty-two independent seeds, and the assertion is on how
  many land inside. **Why coverage rather than a single run:** "One run landing in the
  band proves almost nothing. This assertion fails when the game is biased and it also
  fails when the analytic sigma is wrong, which is the property we want."
- Run the suites. Green.

## 27:45–28:30 — Wrap

- The par sheet is three files. A table that carries ratios, a solver that is one
  division and one rounding decision, and a math file whose comment is the derivation.
- Three claims to carry forward: expected value is a closed form over marginals, the
  target is hit by one scalar and then verified against the table that shipped, and the
  band comes from an exact sigma with covariance in it.
- "All of this could have been approximated by a billion spins, but simulation checking
  simulation is circular. A closed form derived by counting stops is an independent
  check."
- Next: "The engine. Ten million spins, every core busy, and the same answer bit for bit
  every time you run it."

---

## Recording notes

- This is the slowest episode of the series and the one with the most rehearsal cost.
  Budget roughly twenty-five minutes on the whiteboard and in Rider, under three in the
  browser.
- Strongest visuals in order: the missing `(1 − p_k)` term written and then crossed into
  place, the realized-RTP readout drifting one direction under truncation, and the band
  changing width when covariance is toggled off.
- Zoom hotkey belongs on: `targetBaseRtp / unscaledBaseGameEv`, the `MidpointRounding.ToEven`
  argument, the `Probability` switch expression, and the `2.0 *` covariance term.
- Do not derive the covariance algebra on camera. Point at the variance-of-a-sum identity
  and move to the code; the companion article carries the derivation.
- The three paste blocks are the initial-system files verbatim (the state before the
  episode-9 optimization branch; episode 9 shows the optimized versions side by side).
  If a paste lands wrong, cut and re-paste rather than hand-fixing: the file has to
  match that state.
- Running long? Show only the Match/Match and Mismatch/Mismatch cases of the switch and
  say inclusion and exclusion covers the rest. Drop the feature-schedule flash. Keep beat
  5 (invariant R1), beat 8 (rounding), and the test section whole.
