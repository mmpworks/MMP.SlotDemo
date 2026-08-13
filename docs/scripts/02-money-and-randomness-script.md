# Episode 2 — Money You Can Trust: Millicents and SpinRng

**Target:** 24–26 min. **Format:** create the file, paste the finished source, then
walk it. The typing is a jump cut; the walkthrough is the episode.
**Subject:** the engine. The companion site appears four times, for under three
minutes total, and only to make an engine claim visible.
**Companion article:** `docs/articles/02-money-and-randomness.md`
**Companion site:** MMP.SlotDemo, branch `main`, page `#/ch02`
**Files created on camera:** `CSharp/src/MMP.SlotGame.Core/Money/Millicents.cs`,
`CSharp/src/MMP.SlotGame.Core/Simulation/SpinRng.cs`.

> **Discipline note for this recording.** The labs illustrate; they do not carry the
> episode. If a beat can be made in Rider, make it in Rider. Cut to the browser only
> where the engine's behavior is easier to see than to describe, and cut back inside
> a minute.

---

## Prep checklist

**Repo — the subject**
- [ ] Rider on `CSharp/MMP.SlotDemo.slnx`, tree expanded to `MMP.SlotGame.Core`
- [ ] `Money/` and `Simulation/` folders present, the two target files moved aside so
      they get created on camera
- [ ] Scratch file or C# Interactive ready for the float demonstrations
- [ ] Test runner loaded: `SlotDemo.Server.Tests/MillicentsTests` (the M1 reflection scan
      and the M2 shuffle live here), plus `MMP.SlotGame.Tests`: `MillicentsTests`,
      `MillicentsFuzzTests`, `DeterminismTests`, `NoAmbientRngTests`, `SpinRngFuzzTests`,
      `ConcurrencyTests`
- [ ] Clipboard manager staged with Block A then Block B

**Companion site — the illustration**
- [ ] `E:\dev\MMP.SlotDemo`, branch `main`
- [ ] `cd CSharp/web && npm run build`, then `dotnet run --project CSharp/src/SlotDemo.Server`
- [ ] `http://localhost:5090/#/ch02`, each lab run once so nothing pays first-request cost
- [ ] `logs/` cleared so the viewer starts empty

**OBS**
- [ ] Scenes: `RIDER`, `BROWSER`, `TERMINAL`
- [ ] Zoom-to-mouse hotkey bound and tested at code-reading zoom
- [ ] Rider font sized for capture

---

## 0:00–1:00 — Cold open

**Scene:** RIDER, the Core project tree, both target files absent.

- "Two files today. About a hundred lines between them. One holds money, one holds
  randomness, and between them they decide whether a ten-million-spin run means
  anything."
- "Everything downstream (reels, paytables, the engine, the proof in episode 8) gets
  to be ordinary because these two are strict."
- Set the format: "I paste each file finished, then we go through it and I tell you
  why every line is the way it is."

## 1:00–3:15 — Why integers, argued in code

**Scene:** RIDER, C# Interactive or a scratch method.

Type live:
```csharp
0.1 + 0.2 == 0.3          // false
0.1 + 0.2                 // 0.30000000000000004
```
- "In a renderer nobody notices. In a system that has to tell 97.99% from 98.01%
  after ten million spins, that noise is the same size as the answer."

Then the part that matters more, also typed live:
```csharp
(0.1 + 0.2) + 0.3         // 0.6000000000000001
0.1 + (0.2 + 0.3)         // 0.6
```
- "Same three numbers. Different grouping. Different answer. Floating-point addition
  does not care about grouping, which means it does not care about **order**."
- Land it on threads: "Sixteen workers finish in whatever order the scheduler picks.
  If the total is a `double`, the result depends on finish order. The arithmetic
  itself has a race in it, and no lock fixes that."
- "So count integers, the way banks do."

## 3:15–4:15 — Create the file

**Scene:** RIDER.

- Right-click `MMP.SlotGame.Core` → new directory `Money` → new file.
- **Path on screen and said out loud:** `CSharp/src/MMP.SlotGame.Core/Money/Millicents.cs`
- Paste **Block A**. Pause on the constructor, then trace one exact addition.

### Block A — `CSharp/src/MMP.SlotGame.Core/Money/Millicents.cs`

```csharp
namespace MMP.SlotGame.Core.Money;

/// <summary>
/// A monetary quantity stored as an integer count of millicents
/// (1 credit = 100,000 millicents). Run totals use this representation so addition and
/// comparison do not introduce floating-point rounding. Conversion to credits is reserved
/// for display and ratio calculations.
/// </summary>
public readonly record struct Millicents(long Value) : IComparable<Millicents>
{
    public const long PerCredit = 100_000;

    /// <summary>
    /// Pay multipliers are stored as the real multiplier times this scale. At 100,
    /// 225 represents 2.25 times the total spin wager. Parsers, analyzers, and payout code
    /// read the same value so the internal unit has one authority.
    /// </summary>
    public static readonly long ScaleFactor = 100;

    public static readonly Millicents Zero = new(0);

    public static Millicents FromCredits(long credits) => new(credits * PerCredit);

    public static Millicents operator +(Millicents a, Millicents b) => new(a.Value + b.Value);
    public static Millicents operator -(Millicents a, Millicents b) => new(a.Value - b.Value);

    /// <summary>
    /// A money amount taken so many whole times: a bonus worth 20 bets, a wager over
    /// 10M spins. The operand is dimensionless (money × money has no meaning), which is
    /// why it is a long and not another <see cref="Millicents"/>; fractional multipliers
    /// go through <see cref="ScaledMultiply"/> instead.
    /// </summary>
    public static Millicents operator *(Millicents a, long multiples) => new(a.Value * multiples);

    public static bool operator >(Millicents a, Millicents b) => a.Value > b.Value;
    public static bool operator <(Millicents a, Millicents b) => a.Value < b.Value;
    public static bool operator >=(Millicents a, Millicents b) => a.Value >= b.Value;
    public static bool operator <=(Millicents a, Millicents b) => a.Value <= b.Value;

    public int CompareTo(Millicents other) => Value.CompareTo(other.Value);

    /// <summary>
    /// Applies a multiplier expressed in <see cref="ScaleFactor"/>ths of the total spin
    /// wager. At the current scale, 225 means 2.25 times the wager. The wager must be
    /// divisible by the scale so the conversion has no remainder.
    /// </summary>
    public Millicents ScaledMultiply(int scaledMultiplier)
    {
        if (Value % ScaleFactor != 0)
            throw new InvalidOperationException(
                $"{this} ({Value} millicents) is not a multiple of {ScaleFactor} millicents. Pay "
                + $"multipliers are carried internally as the real multiplier × {ScaleFactor}, so the "
                + $"wager must divide evenly by {ScaleFactor} for a fractional multiplier to convert to "
                + "exact millicents.");

        return new Millicents(Value / ScaleFactor * scaledMultiplier);
    }

    /// <summary>The type's only conversion to floating point. Display and ratio math; run totals stay in millicents.</summary>
    public double ToCredits() => (double)Value / PerCredit;

    public override string ToString() => $"{ToCredits():0.#####}cr";
}
```

## 4:15–12:30 — Walk `Millicents`

**Scene:** RIDER throughout. Zoom on each region as it comes up.

### Beat 1 — why a struct and not a class

The question viewers ask most. Four reasons, then the counterweight.

1. **Identity is wrong here.** Two amounts of 2.25 credits are the same amount. A
   class gives every amount an identity it never wanted, and reference equality
   starts answering questions nobody asked.
2. **It is one `long`.** The struct is the same eight bytes as the number inside it.
   A class adds a header and a pointer to carry those same eight bytes, and every
   read chases the pointer.
3. **Allocation.** Ten million spins produce hundreds of millions of intermediate
   amounts. As structs they live on the stack and vanish. As classes they become
   collector work in the hot path.
4. **Copying is the safe default.** Passing a struct hands over a copy, so no caller
   can reach back and change an amount somebody else is holding.

The counterweight: structs get copied, so a **mutable** struct is a trap, because a
copy advances while the original stands still. `readonly` closes it. Every operation
returns a new value and nothing changes in place.

### Beat 2 — `record struct` for the parts you should not hand-write

`readonly record struct` supplies value equality, a matching `GetHashCode`, and
deconstruction, with no code to maintain and no chance of an equality bug. Point at
the one override: `ToString`, because the generated version prints the raw count and
a human wants credits.

### Beat 3 — the scale, and where it lives

- `PerCredit = 100_000`: five decimal digits below a credit. Enough resolution for a
  paytable solver to hit a target RTP with no remainder to lose.
- `ScaleFactor = 100`: pay multipliers travel as integers, so 2.25× is carried as
  `225`. Parser, analyzer, and payout code all read this one field, so the number 100
  lives in one place.
- **The seam:** naming it costs one line and leaves the resolution changeable later.

### Beat 4 — the operator that refuses to exist

Walk the list, then stop at the multiply.

- `+`, `-`, comparisons: ordinary, and they exist so call sites read like arithmetic
  rather than `new Millicents(a.Value + b.Value)`.
- `operator *(Millicents, long)`: money taken a whole number of times. Money times
  money means nothing, so that overload is absent by design.
- **Break it on camera.** In a scratch method, type `total * 0.98` and let the red
  squiggle sit for a beat. No implicit conversion to `double` exists anywhere in the
  type, so the line that would quietly lose money fails to compile.
- **The line:** "That missing feature is invariant M1. The compiler checks it on
  every build. A reviewer checks it on a good day."

### Beat 5 — one exit, and it has a name

`ToCredits()` is the only path to floating point. It says so in its own doc comment,
and it is greppable. An implicit conversion would let money slip into a double
anywhere; a named method makes every exit a search result you can audit before a
release.

### Beat 6 — the throw and its message

Read the `ScaledMultiply` message aloud. It names the amount, the scale, and why the
conversion cannot be exact.

- The type refuses work it cannot do exactly instead of rounding quietly.
- Error messages are part of the interface. One that explains the rule saves the next
  person an afternoon.

> **Illustration (30 seconds, BROWSER).** Chapter 2 page, money lab. Raw wager
> `12345` millicents, run. The refusal text appears on the page, and the same line
> appears in the log stream underneath at warning level. "This wager cannot carry a
> fractional multiplier without losing part of a millicent, and the type will not
> guess which way to round." Cut back.

### Beat 7 — M2, stated once so it can be tested later

Integer addition gives the same total in any order, so an N-worker total matches a
1-worker total bit for bit. Flag it: "Remember this. In episode 8 it becomes a test
with `==` in it."

> **Illustration (40 seconds, BROWSER).** Money lab, multiplier `110`, one million
> repeats: the integer column holds and the double column drifts by about `1.1e-05`
> credits. Switch to `225` and the drift goes to zero, because 2.25 is a sum of powers
> of two and binary holds it perfectly, while 1.1 rounds on every addition. Point at
> the 64-bit strip: "One integer, end to end. No mantissa, no exponent." Cut back.

## 12:30–14:00 — Determinism's three enemies

**Scene:** RIDER, comment block or whiteboard.

1. **Ambient randomness.** `Random.Shared`, `Guid.NewGuid()`, `DateTime.Now`. One call
   anywhere in the engine and the run stops being reproducible. Rule R3: randomness
   travels as `ref` parameters, which makes it greppable across the assembly.
2. **Correlated seeds.** `masterSeed + workerId` looks reasonable and produces streams
   that rhyme. We will see how badly in a few minutes.
3. **Dynamic scheduling.** `Parallel.For` steals work, so which worker handles which
   spin changes between runs. Episode 6's problem, named here so it does not surprise
   anyone later.

## 14:00–14:45 — Create the second file

**Scene:** RIDER.

- New directory `Simulation`, new file. **Path on screen:**
  `CSharp/src/MMP.SlotGame.Core/Simulation/SpinRng.cs`
- Paste **Block B**.

### Block B — `CSharp/src/MMP.SlotGame.Core/Simulation/SpinRng.cs`

```csharp
namespace MMP.SlotGame.Core.Simulation;

/// <summary>
/// Deterministic per-worker RNG stream — xoshiro256** seeded via SplitMix64.
///
/// Randomness in Core enters spin logic through <c>ref SpinRng</c> parameters. Keeping
/// the stream explicit makes a run replayable when the game, seed, worker count, spin
/// target, and code version are unchanged.
///
/// Each worker starts from a distinct value derived from the master seed and worker id;
/// SplitMix64 expands that value into the four xoshiro state words.
///
/// Simulation-grade RNG. Real-money play requires a certified gaming RNG; this is not one.
/// </summary>
public struct SpinRng
{
    private ulong _s0, _s1, _s2, _s3;

    public static SpinRng ForWorker(ulong masterSeed, int workerId)
    {
        // SplitMix64 both mixes the worker id and expands one seed into four
        // well-distributed xoshiro state words (the generator author's own recipe).
        var sm = masterSeed ^ (ulong)workerId;
        SpinRng r;
        r._s0 = SplitMix64(ref sm);
        r._s1 = SplitMix64(ref sm);
        r._s2 = SplitMix64(ref sm);
        r._s3 = SplitMix64(ref sm);
        return r;
    }

    private static ulong SplitMix64(ref ulong state)
    {
        var z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>xoshiro256** next value.</summary>
    public ulong NextUInt64()
    {
        var result = ulong.RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = ulong.RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform integer in [0, <paramref name="bound"/>) using Lemire's rejection method.</summary>
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);

        var range = (ulong)bound;
        var threshold = unchecked(0UL - range) % range;
        while (true)
        {
            var product = (UInt128)NextUInt64() * range;
            if ((ulong)product >= threshold)
                return (int)(product >> 64);
        }
    }

    /// <summary>Uniform double in [0, 1) with 53 random bits.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
}
```

## 14:45–21:30 — Walk `SpinRng`

### Beat 8 — a struct again, for different reasons

Four `ulong` fields: the entire generator is 32 bytes on the worker's stack. No
allocation, no pointer chase, and each worker's state stays in its own cache line
rather than being memory two cores fight over. In a loop that runs ten million times,
that is the difference between the generator costing nothing and the generator
dominating the profile.

### Beat 9 — the mutable struct, and the discipline around it

Say the anti-pattern before the comment section does: a mutable struct usually forks
its state silently, because copies advance independently.

Here that hazard is turned into discipline.
- The stream advances only through `ref`, so one live copy exists per worker.
- Any method consuming randomness declares it in its signature. Randomness becomes
  visible in the type system instead of hiding inside a field.
- `grep "ref SpinRng"` lists everything in the engine that can consume a random
  number. That is the R3 audit.

### Beat 10 — seeding, and when borrowed code is the engineering decision

- `masterSeed ^ workerId`, then SplitMix64 four times. One mixing function does two
  jobs: separates the workers, and expands one seed into four well-spread state words.
- The constants are published and canonical, from the generator's author. **The
  beat:** "Somebody proved these constants behave. Using their proof is cheaper than
  proving my own."
- Same argument for xoshiro256** itself: modern .NET uses it inside `Random`. Owning
  these twenty lines pins the byte stream across runtime versions, which is what lets
  a test assert an exact total three years from now.

> **Illustration (50 seconds, BROWSER).** RNG lab, the strongest visual in the
> episode. Click **seed + workerId**: workers 0 and 1 share **51 leading bits** of
> their first draw and every reduced stop comes out 0. "Fifty-one shared bits means
> those two workers are walking nearly the same stream." Click **SplitMix64**: shared
> prefix drops to 0 and the stops spread. Point at the replay row: same seed, same
> worker, same first value. Cut back.

### Beat 11 — `NextInt`, and paying for uniformity

- The obvious version is `NextUInt64() % bound`. It costs an integer division, and it
  is slightly wrong: when the range does not divide the space evenly, the low buckets
  each receive one extra source value.
- Lemire's version multiplies, takes the high half, and rejects the short tail. One
  multiply, one shift, and every bucket gets the same number of source values.
- Read the trade out loud from the code: the `while (true)` is the rejection loop, and
  the rejection rate is the price of the exactness.

> **Illustration (40 seconds, BROWSER).** Bias lab: 8-bit draw space, 37 buckets,
> 200,000 samples. Modulo's worst bucket lands about **15%** off expected with a
> visible step; multiply-shift stays down at sampling noise, with the rejection count
> showing the price. "At 64 bits this same skew is real and far too small to see. That
> is the kind of bug that passes review and turns up later as an RTP that misses its
> target by a hair." Cut back.

### Beat 12 — the warning label

Read the doc comment aloud: simulation-grade, and not certified-gaming.

- A regulator needs unpredictability: nobody can guess the next value, including
  someone holding the source.
- A simulator needs replayability: anybody holding the seed reproduces the run.
- Opposite requirements sharing one word, which is why the boundary is written into
  the type's own documentation.

## 21:30–24:30 — The tests are part of the design

**Scene:** RIDER test runner, then TERMINAL.

Give this section real time.

- **M1 has a test that looks for an absence.** Open
  `SlotDemo.Server.Tests/MillicentsTests.M1_the_type_exposes_no_conversion_to_a_floating_point_number`:
  it scans the type for any `op_Implicit` or `op_Explicit` returning `double`, `float`,
  or `decimal`, and expects to find nothing. "Somebody adds an implicit conversion in
  eight months because it made one call site tidier. This is what tells them no."
- **M2 has a test that shuffles.**
  `M2_a_total_does_not_depend_on_the_order_the_parts_arrive_in`, in the same class. Sum
  five hundred amounts forward, backward, and scrambled, and assert all three totals are
  equal. "One test carries the argument from the top of the episode."
- **Where these two live, and why.** Both sit in the companion site's own suite, over its
  copies of these files. If the demo ever drifts from the engine, its tests fail first.
- **R3 has two.** `SameSeedAndWorkerCount_ProducesIdenticalSnapshots` proves replay.
  `DifferentSeed_ProducesDifferentTotals` catches determinism achieved by ignoring
  the seed. **Say why the second exists:** "Test the negative space, or a stuck
  generator passes for a reliable one."
- **`NoAmbientRngTests`** enforces R3 by reading the assembly rather than by everyone
  remembering the rule.
- **Uniformity, checked at the right resolution.**
  `SpinRngFuzzTests.NextInt_DistributesRoughlyUniformlyAcrossTheBound` runs fifty
  iterations, each drawing a fresh seed and a fresh bound, and measures chi-square
  against a threshold its own comment calls deliberately loose. "It is there to catch a
  broken modulus or a biased rejection loop, and it says so. Tighten the threshold and
  it flakes on healthy noise, and a flaking test gets tuned until it stops, and a tuned
  test proves whatever it was tuned to."
- Run the suite. Green.
- Flash `ParallelRun_EqualsSequentialReplication_BitForBit` without opening it.
  "Episode 8 takes this one apart. It passes because of the three rules we set today."

## 24:30–25:30 — Wrap

- Two types, three invariants: **M1** no floating point in money paths, enforced by
  the compiler and guarded by a test. **M2** order-independent totals, which makes
  parallel provable. **R3** no ambient randomness, which makes runs replayable.
- "Everything after this episode is easier because these two are strict, and it cost
  about a hundred lines."
- Next: "What a reel actually is. It is not a weighted die, and the difference stays
  invisible until it costs you a weekend."

---

## Recording notes

- Engine-to-browser budget: roughly 22 minutes in Rider and the test runner, under
  three in the browser. If a take runs long, the browser time goes first.
- Strongest visuals in order: the red squiggle on `total * 0.98`, the 51-shared-bits
  correlation, the modulo histogram's step. Hold on each for a beat.
- Zoom hotkey belongs on: the compiler error, the exception message, the shared-prefix
  row, and the histogram step. Everything else reads at normal size.
- The two paste blocks are the finished files verbatim. If a paste lands wrong, cut
  and re-paste rather than hand-fixing: the file has to match the repo.
- Running long? Compress beat 2 (`record struct` freebies) to one sentence and drop
  `NextDouble`. Keep every beat that names an invariant, and keep the test section
  whole.
- The companion site's own suite covers the same guarantees over its copies of these
  two files, so if the demo ever disagrees with the engine, its tests fail first.
