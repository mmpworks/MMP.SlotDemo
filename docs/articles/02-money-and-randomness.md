# Money You Can Trust: Integer Millicents and Deterministic Randomness

*Part 2 of a series on building a slot game engine in C#. Part 1 covered the system
design. This chapter builds the money and random-number types used by every spin.*

In C#, `0.1 + 0.2 == 0.3` returns `false`. The fix for what that hides is a change of
unit, and the safest place to put a change of unit is inside the types themselves,
where ordinary code obeys it and nobody has to remember a checklist.

## Why floating-point money fails quietly

Try it in any C# REPL:

```csharp
0.1 + 0.2 == 0.3   // false
```

One line, three separate failure modes. A slot simulator hits all three.

### Check your understanding

Imagine adding one million wagers of 0.1 credit. Which total is safer: a `double` that stores
`0.1`, or an integer that stores each wager as `10,000` millicents?

<details><summary>Answer</summary>

The integer total. Every addition uses a whole number. The `double` begins with an
approximation of 0.1, and repeated addition can collect rounding error.

</details>

**Failure 1: representation error.** Binary floating point stores numbers as
fractions with a power-of-two denominator. You can write 1/4 or 3/8 that way, and
1/3 you cannot. Most decimal fractions, 0.1 and 0.2 among them, have no exact binary
form. The computer keeps the closest binary value it can find, and that value is
already a hair off before any arithmetic starts.

**Failure 2: accumulated drift.** Picture a grocery receipt where every line carries
a tiny rounding error, a fraction of a cent high or low. One line, nobody notices.
Ten million lines, and the register total has walked away from the true total by a
measurable amount. A slot simulator runs millions of spins to check whether the game
pays back 86.111% of the money wagered or something else. Let the accounting drift by
a hundredth of a percent and that drift looks identical to the bug you are hunting.
You can no longer tell a real defect from your own arithmetic noise.

**Failure 3: order dependence.** `double` addition is not associative. `(a + b) + c`
can produce a different bit pattern than `a + (b + c)`, because each addition rounds,
and rounding twice in a different order lands in a different place. That sounds
academic right up until the totals come from parallel workers summing millions of
spins each. Two runs with the same seeds, the same spins, the same everything, can
finish on two different totals purely because the worker threads finished in a
different order.

## The floating point fix is a unit change

The fix is older than computers. Count in a unit small enough that every quantity you
care about is a whole number. Whole numbers add, subtract, and compare with none of
the three failures above. Banks have always done this. A price tag reads \$19.99, and
the register adds 1999 cents.

A slot game sometimes needs awards smaller than one cent, especially when a published
multiplier carries tenths or hundredths. So this engine counts **millicents**: one
credit equals 100,000 millicents. That scale sets payout resolution. It does not set
how many decimal places the RTP calculation can show, since RTP depends on outcome
probabilities too. Every wager, payout, and running total is an integer count of
millicents, and the separate analytic layer is where those integer awards meet
probabilities.

The simulator makes one additional choice: **every spin wagers 1 credit**, which is
100,000 millicents. `SimulationConfig.Wager` holds that value, and every simulated
spin uses it. A value of 1,000,000 millicents would be a 10-credit wager. Keeping the
wager fixed makes runs easy to compare; ten million spins always means ten million
credits wagered. The `Millicents` type can represent other wager sizes, but this
teaching simulator does not make the wager configurable.

## The Millicents type

```csharp
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
                + $"wager must divide evenly by {ScaleFactor} for a fractional multiplier to "
                + "convert to exact millicents.");

        return new Millicents(Value / ScaleFactor * scaledMultiplier);
    }

    /// <summary>The type's only conversion to floating point. Display and ratio math; run totals stay in millicents.</summary>
    public double ToCredits() => (double)Value / PerCredit;

    public override string ToString() => $"{ToCredits():0.#####}cr";
}
```

Four choices matter here:

**It is a `readonly record struct`.** Money behaves like a number, so value semantics
fit. Copying a `Millicents` copies its value, equality compares values, and an
ordinary local needs no object allocation. `readonly` keeps the amount fixed after
construction. What machine code comes out of that stays a JIT decision, so this
chapter quotes no instruction count.

**`IComparable<Millicents>` gives the type a normal ordering.** Framework methods
such as sorting, minimum, and maximum can compare two amounts without converting
them to another unit.

**Multiplication takes a `long`.** Money times money is dimensionally meaningless,
since millicents squared is not a currency. Money times a fraction is a rounding
decision, and somebody should have to make that decision out loud. Scaling by an
integer, a payout multiplier or a line count, is the only multiplication the domain
needs, so it is the only one plain `*` will compile.

**The missing conversion.** The type has no implicit conversion to `double`. Write
`total + spinPayout` and it compiles. Write `total * 0.98` and it doesn't. The
architecture document calls that invariant M1, no floating point anywhere in an
accumulation path, and the compiler enforces it on every build. The one exit,
`ToCredits()`, is named, documented as display-only, and easy to grep for.

> 🧪 **Try it live.** The companion site's chapter 2 page (<http://localhost:5090>,
> then `#/ch02`) opens with **Lab 1 — Money as an integer**: type a wager and a
> multiplier and watch the millicent arithmetic resolve, including the wagers that
> trip the divisibility guard. The same server code shown above answers every click.

Integer addition also lets workers contribute the same set of subtotals in different
orders without changing the sum. This is invariant **M2, partition invariance**.
It does not mean that changing the worker count produces the same spins; worker count
changes the RNG partition. Article 6 separates those two ideas.

## Fractional multipliers without fractional money

A payout multiplier like 1.25X or 2.25X is a fraction, and `Millicents` keeps
fractions out of the accounting. So we make the same move as before: when the
quantity in your hand isn't a whole number, change its unit until it is.

Measure the multiplier in **hundredths of the total spin wager** and 1.25X becomes
125. 1.75X becomes 175. 2.25X becomes 225. Now the multiplier is an integer, and
integers are what `Millicents` already handles.

This representation is fixed-point arithmetic: it stores an implied decimal place
instead of a fraction.
Applying a fraction p/100 to a wager W lands on a whole number of millicents only
when 100 divides W evenly. `ScaledMultiply` checks that once, in a guard clause, and
everything downstream of the check stays plain integer work. The 100 itself lives in
one named constant, `Millicents.ScaleFactor`, and every conversion in the engine
derives from it.

Changing that scale would still mean reviewing imported game data, wager
divisibility, accumulator limits, and tests. A shared constant prevents drift; it
does not make a domain change free. Hundredths covers the wager denominations this
project supports, down to a one-penny wager (1,000 millicents). Go finer and
denominations that no longer divide evenly would need a new rounding policy.

At a 1-credit wager, or 100,000 millicents, the arithmetic is:

```
1.25X:  100,000 / 100 * 125  =  1,000 * 125  =  125,000 millicents
1.75X:  100,000 / 100 * 175  =  1,000 * 175  =  175,000 millicents
2.25X:  100,000 / 100 * 225  =  1,000 * 225  =  225,000 millicents
```

The division in `100,000 / 100` is safe because the code checks its remainder first.
Integer division is dangerous in one specific way: it truncates. `7 / 2` gives 3 and
throws the 1 away without a word. So the engine performs only divisions it has
already proven will truncate nothing:

```csharp
if (Value % ScaleFactor != 0)        // remainder must be zero, or refuse to compute
    throw new InvalidOperationException(...);
return new Millicents(Value / ScaleFactor * scaledMultiplier);
```

The `%` operator returns the remainder. Divide 100,000 by 100 and you get a quotient
of 1,000 and a remainder of zero. That zero says the division to come is like
breaking a \$100 bill into a hundred \$1 bills: a change of form, with nothing lost.
A nonzero remainder means the wager will not cut into 100 equal millicent pieces, and
the guard throws. So the operation either returns an exact amount or reports that the
multiplier cannot be written in millicents at all.

Dividing first also keeps the intermediate value small. Both orders reach 125,000
here, but after the guard, `Value / 100` is a whole number of millicents, one
hundredth of the bet, and integer multiplication can introduce no error at all. In
that order the code reads like what it does: cut the bet into 100 exact pieces, take
125 of them.

No fraction ever forms, so there is nothing to round. `MillicentsTests` pins the
case. `ScaledMultiply_TwoAndAQuarterXAtOneCredit_IsExactMillicents` asserts the 2.25X
result above and stays green.

`ScaledMultiply` sits on `Millicents` itself and gets called as
`wager.ScaledMultiply(225)`, rather than as a static helper in some other class,
`MoneyMath.Scale(wager, 225)`. What changes is where the divisibility rule lives. Make
scaling a separate static function and any code anywhere can still build a
`Millicents`, add to it, multiply it, and compare it, without ever passing through
the one function that knows the guard clause exists. Put `ScaledMultiply` on the type
and the rule travels with the data. Skipping `Value % ScaleFactor != 0` means
skipping the method.

Games declare their multipliers in whatever unit reads naturally in the source PAR
sheet. "Units" for whole multipliers. "Tenths" for something like 1.5X, kept mainly
for files written before hundredths existed. "Hundredths" for a multiplier tenths
cannot state, such as 2.25X. Whichever unit a file declares, the loader rejects any
pay that needs more precision than that unit can carry, and its error names the
finest unit the file is allowed to declare instead of truncating in silence. The JSON
schema for `payUnit` and the reasoning behind it live in
`docs/game-definition-schema.md`. Inside the engine, every pay compiles to
multiplier × ScaleFactor before `ScaledMultiply` ever runs, so that guard clause is
the single place a fractional multiplier meets the money type, whatever unit the
source file used.

Every multiplier in this engine is a multiple of the total spin wager. The engine has no
concept of a per-line share of the bet. When several paylines win on one spin, each
line's multiplier scales against the full wager and the results add.
`LineBetVsTotalBetTests` pins that: two lines paying 5X and 3X on a 1-credit wager
award 800,000 millicents, the summed 8X, never a split.

Traditional multiline paytables often publish the other convention, quoting awards in
line-bet units where a 10-line machine divides the stake across lines first.
Transcribe such a PAR sheet's line pays straight into a multi-payline game file here
and the simulated RTP comes out many times too high. Real game data has to convert to
the total-wager basis on the way in. Orca Dive in article 7 is a single-line game,
where the two conventions coincide and the difference stays invisible. A multi-line
transcription is where it bites.

## Millicents always works in whole numbers

Every calculation inside the type resolves to a whole number. Two operations turn a
wager into a payout: `operator *(Millicents, long)` for whole-number scaling, such as
a wager times a whole-unit bonus award or a whole spin count, and
`ScaledMultiply(int)` for fractional pays, whose divisibility guard catches a
malformed wager before it can hand back a quietly wrong number. Addition,
subtraction, and comparison are plain integer operations, so the type has no way to
produce a fraction.

Floating point lives outside the type. `Millicents` offers no implicit conversion to
`double` (invariant M1), so other systems take exact integer values from it and
convert to floats at the last moment, usually when `ToCredits()` formats a payout for
display.

## Banker's rounding at the paytable boundary

Millicents refuses fractions everywhere but one place: building the paytable itself.
`PaytableSolver` starts from a canonical paytable, whose payouts are dimensionless
ratios, and scales it by a single factor, `paytableScaleFactor`, toward a target RTP
such as 86.111%. That scaling step produces a real number before it produces a
millicent, because the target RTP is itself a fraction. It is the one place in the
payout-construction path where a fractional value turns into money.

### What the paytable scale factor really means

The short version:

- `unscaledBaseGameEv` is the average amount the **unadjusted** paytable would return per spin.
  It is calculated from all possible payline wins and their chances, not from a
  payline hit that just happened.
- `paytableScaleFactor` is one global multiplier used to resize every prize in that paytable so its
  average return matches the requested RTP.
- Both numbers are calculated once while building the paytable. They are not
  recalculated on each spin.

Think of `unscaledBaseGameEv` as the answer to a giant imaginary experiment. Suppose we could
play the game a huge number of times using the original, unadjusted prizes. Some
spins would win and many would lose. If we added all the prizes and then divided
by the number of spins, the result would approach `unscaledBaseGameEv`.

For example, imagine an extremely simple one-line game with only two possible
results:

| Result | Chance | Unadjusted prize | Contribution to the average |
|---|---:|---:|---:|
| Hit three stars | 1 in 10, or 10% | 5 bets | 10% × 5 = 0.50 bets |
| No win | 9 in 10, or 90% | 0 bets | 90% × 0 = 0 bets |

Its `unscaledBaseGameEv` is `0.50`. That does not mean each spin pays half a bet. A real
spin still pays either 5 bets or 0 bets. It means that over many spins, the
unadjusted table averages half a bet returned for every bet made.

The real game has many symbols, match lengths, and paylines. The calculation does
the same job for every possible listed win:

```text
unscaledBaseGameEv = (chance of win A × prize A)
      + (chance of win B × prize B)
      + ...
      across every payline
```

So, yes, payline hits are part of the calculation, but only as **possible events
with probabilities**. `unscaledBaseGameEv` is not calculated in response to an actual hit. If
there are several paylines, their average contributions are added together. This
makes `unscaledBaseGameEv` the expected base-game return for the whole spin under the
unadjusted paytable.

Why is the table called unadjusted? The canonical paytable is only a prize
*shape*: perhaps the premium symbol pays 60, the next pays 27.3, and the cheapest
pays 1.2. Those numbers establish which wins are large or small compared with one
another, but the table may not yet have the desired RTP.

This is where `paytableScaleFactor` comes in: a correction factor, or, in
junior-high algebra, the scale factor in `new prize = old prize × factor`. The engine first calculates the
canonical table's `unscaledBaseGameEv`, and then computes:

```csharp
var paytableScaleFactor = targetBaseRtp / unscaledBaseGameEv;
```

Read that as:

```text
paytableScaleFactor = average we want / unadjusted average we currently have
```

It works like resizing a recipe. If a recipe feeds 24 people but you need food for
10, multiply every ingredient by `10 / 24`. Here the "recipe" is the whole
paytable. Multiplying every prize by the same number keeps the relative shape of
the prizes while changing their overall size.

Return to the simple star game. Its unadjusted `unscaledBaseGameEv` is 0.50, but suppose the
target RTP is 0.75, meaning 75 cents returned on average for each dollar bet:

```text
paytableScaleFactor = 0.75 / 0.50 = 1.5
new star prize = 5 bets × 1.5 = 7.5 bets
new average = 10% × 7.5 bets = 0.75 bets
```

Because the original table was too small, the factor is greater than 1 and
enlarges every prize. If the original table returned too much, it would be less
than 1 and shrink every prize. If it was already correct, it would equal 1 and
nothing would change.

This makes `paytableScaleFactor` a global paytable multiplier, but not a multiplier used on the
player's result after every spin. The engine applies it once to every canonical
paytable entry when constructing the final paytable. Actual spins use those final,
already-scaled prizes.

One scope note before the worked example. Scaling every prize by one global factor is
this engine's paytable-solving technique, and the math holds: multiply every award by
the factor and the average return multiplies by the same factor. It is a valid
construction method, and it is also a simplification of how commercial slot design
usually works. A studio moves several levers on their own: symbol frequencies on the
strips, individual award sizes, bonus frequency and bonus value, hit frequency,
volatility, jackpot behavior. One global factor preserves the relative shape of all
the prizes and gives up independent control of those qualities. Regulators such as
GLI evaluate the *resulting* theoretical percentage and leave the construction method
alone. This engine picks the one-factor method because it wants a provable target,
and article 4 covers what the method can and cannot tune.

For a fuller example, suppose the canonical table returns
`unscaledBaseGameEv = 2.4` bets per bet. That is 240%, useful as a shape but not as a
playable return. The game wants a 75% base RTP:

```
paytableScaleFactor = 0.75 / 2.4 = 0.3125

canonical 60    ->  60   × 0.3125 × 100,000  =  1,875,000 millicents   (18.75 bets)
canonical 27.3  ->  27.3 × 0.3125 × 100,000  =    853,125 millicents   (exact)
canonical 2.47  ->  2.47 × 0.3125 × 100,000  =     77,187.5            (a tie!)
```

The first two land on whole millicents on their own. The third lands between two
millicents, and the rounding rule is what decides which.

`PaytableSolver` rounds with `MidpointRounding.ToEven`, better known as banker's
rounding. The "to even" part controls midpoint ties.

The obvious rounding rule, always round a tie up, sounds harmless. 12.5 rounds to
13, 13.5 rounds to 14, and so on. But that rule always breaks the tie the same
direction: every midpoint in the paytable gets pushed high, a built-in upward
bias.

Round-half-to-even breaks each tie toward whichever neighbor is an even number:

| Value | Rounds to | Why |
|---|---|---|
| 12.5 | 12 | 12 is even |
| 13.5 | 14 | 14 is even |

The tie from the worked example above resolves this way: 77,187.5 rounds to
77,188, the even neighbor.

Round-half-to-even removes the built-in upward direction; it does not guarantee the
rounded paytable hits the target RTP. A paytable has a handful of entries, and they
contribute unequally: rounding a frequent small award by one millicent moves RTP far
more than rounding a rare jackpot by one, so there is no law of averages to lean on.
The engine treats rounding as a source of a small, known residual, then recomputes the
realized theoretical RTP from the final rounded table and validates that number.

That one rounding happens in one place, and the rest of the engine reads its result
rather than repeating it. That's invariant **R1**: the analytic RTP calculator and the
spin-by-spin evaluator both read the same already-rounded paytable. Neither one
re-rounds.

Article 8 covers the harder half of this story, the overflow headroom a
`Millicents` accumulator needs across a long run and the sum-of-squares math behind
variance.

## Randomness that can be replayed

Statistical simulation needs random-looking draws. Debugging needs the same run to
happen twice. A pseudorandom generator gives you both. A starting value called a
**seed** determines its output, and the sequence that comes out is built to behave
like random draws for simulation purposes.

The engine stores the recipe instead of the millions of values it produced. A
replay needs the game definition and code version, the master seed, the worker count,
and the target spin count. Hold those inputs fixed and each worker rebuilds the same
stream and consumes it in the same order.

That plan only works if nothing disturbs the sequence. Three tempting ideas each
break it.

**Ambient randomness.** `Random.Shared`,
`Guid.NewGuid()`, anything that pulls entropy from the clock or the operating
system. This is like asking strangers on the street for numbers: you will get
numbers, but you can never walk the same street tomorrow and get the same ones.
One such call anywhere in the engine and the seed no longer determines the run,
so replay is lost. Invariant R3 makes the dependency visible:
*randomness enters a function only through its signature*, as a `ref SpinRng`
parameter. No field stores a generator; no method creates one. Like a card dealer
who may only deal from the deck handed to them, never from a deck in their
pocket, a function can only use the deck its caller handed it. And because the
rule is structural, you can audit the entire assembly with one grep for `Random`.

**Neighboring worker seeds.** Eight workers need eight
different streams. The engine combines the master seed with each worker id, then
uses SplitMix64 to expand that value into the four state words required by
xoshiro256**. SplitMix64 is the seeding mechanism here; calling it "astronomically
far apart" would claim more than the code measures.

**Scheduler-owned RNG streams.** `Parallel.For` can be perfectly deterministic. The
trouble starts when mutable per-thread RNG streams meet dynamic work assignment,
because thread timing then decides which stream supplies a given iteration. This
engine sidesteps that contract. Each logical worker gets a fixed quota and one
private stream before the run starts. Article 6 shows the mechanics.

Together these rules produce N deterministic worker streams, each bound to a fixed
number of spins. The master seed and worker count are both required replay inputs,
and they are only part of the record. The game, the code, and the target spin count
matter too.

### What is a fixed quota?

A fixed quota is a work assignment made before the run starts and never revised.
With 8 workers and 10,000,000 spins, each worker receives 1,250,000 spins. When the
division has a remainder, worker 0 receives those extra spins. The engine does not
assign meaningful global spin numbers; it assigns counts.

A quota is a spin count, not a bankroll. Workers do not hold money, so no worker
can run out of it. Each spin is a self-contained experiment: assume a one-credit
bet, spin, record what came back. A worker's ledger reads like a tally sheet,
"spins: 1,250,000, wagered: 1,250,000 credits, returned: 1,076,000 credits,"
where "wagered" is a running count of assumed bets, never a balance that
decreases. The workers are pollsters, not gamblers: each is assigned 1.25
million households, knocks on every door, and writes down the answers. (A study
that does track a player's balance, betting until the money runs out, is
risk-of-ruin analysis. It would consume this engine's spin results and stop at
zero; the RTP check wants every spin counted regardless of any streak, because
it measures the machine, not one player's luck.)

### Why this is not a casino RNG

Regulated gaming RNGs must satisfy the rules of their jurisdiction and test lab.
Those rules cover matters such as statistical behavior, seeding, resistance to
outside influence, and when game outcomes may be drawn. Nevada Technical Standard 1,
for example, prohibits a static initialization seed and requires statistical tests
of the random-selection process.

This project's generator serves a narrower purpose: fast, repeatable simulation.
That makes it useful for tests and unsuitable as evidence that a gaming device is
certified. A casino implementation may also use a pseudorandom generator with seeds;
the difference is not simply "seeded" versus "true random." Certification depends on
the whole design, operating environment, and applicable standard.

Using a second, independently implemented generator can be a useful cross-check. If
two sound generators produce results consistent with the same analytic model, a bug
specific to one mapping or stream becomes less likely. That comparison is additional
evidence, not a replacement for RNG evaluation or game certification.


### The simulation RNG

`SpinRng` is a small test instrument with explicit state. The engine creates one
instance per logical worker with `SpinRng.ForWorker(masterSeed, workerId)` and passes
it by `ref` to code that consumes randomness. There is no runtime plug-in system;
tests can inject a different stream factory at the engine boundary when needed.

```csharp
/// <summary>
/// Deterministic per-worker RNG stream: xoshiro256** seeded via SplitMix64.
/// SplitMix64 expands masterSeed ^ workerId into the four state words.
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
        _s2 ^= _s0;  _s3 ^= _s1;
        _s1 ^= _s2;  _s0 ^= _s3;
        _s2 ^= t;    _s3 = ulong.RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform integer in [0, bound) using Lemire's rejection method.</summary>
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

<!-- EXPORT: render this Mermaid block to PNG before publishing -->
```mermaid
flowchart LR
    MS["masterSeed (ulong)"] --> X0["XOR worker 0"] --> SM0["SplitMix64 ×4"] --> S0["xoshiro256** stream 0"]
    MS --> X1["XOR worker 1"] --> SM1["SplitMix64 ×4"] --> S1["stream 1"]
    MS --> XN["XOR worker N-1"] --> SMN["SplitMix64 ×4"] --> SN["stream N-1"]
    S0 --> Q0["worker 0 quota"]
    S1 --> Q1["worker 1 quota"]
    SN --> QN["worker N-1 quota"]
```

> 🧪 **Try it live.** The same chapter 2 page carries **Lab 2 — One seed, many
> workers**, **Lab 3 — Roll a fair die from raw bits**, and **Lab 4 — Where modulo
> bias hides**. Lab 2 draws the per-worker streams from a seed you choose and shows
> that re-running it reproduces them. Lab 3 enumerates every raw value of a tiny
> source into die faces, so you watch the multiply pick the face and the trim remove
> the fat slices' openers, case by case. Lab 4 then runs the same arithmetic hot,
> charting a plain remainder mapping beside the rejection method so the bias becomes
> something you can see rather than something you take on faith.

Four details:

**The algorithm lives in the repository.** Tests do not depend on an undocumented
runtime implementation remaining unchanged. The project owns the sequence it uses
for replay.

**The state words are `ulong`.** xoshiro256** is defined on raw 64-bit patterns. It
shifts bits, rotates them around the end of the word, and combines two states with
XOR, and none of that cares what the pattern "means" as a number. A signed type like
`long` reserves its top bit to mean negative, so a shift or a rotate crossing that
bit would flip the sign along with the pattern and corrupt the very bits the
algorithm runs on. A `ulong` has no sign bit to protect. All 64 of its bits are just
pattern, which is what a bit-shuffling algorithm needs.

**The mutable struct is passed by `ref`.** Calling a random method advances the
caller's stream instead of a temporary copy. C# still permits someone to copy the
struct deliberately, so tests and source checks enforce the convention; the type
system does not make copying impossible.

**`NextInt` rejects the tiny biased tail.** A simple remainder mapping can favor
some stops when the generator's range is not evenly divisible by the reel length.
Lemire's multiply-and-reject method maps the accepted values evenly into
`[0, bound)`. Invalid bounds fail immediately.

The XML documentation labels this a simulation-grade RNG, not a certified gaming
RNG. That scope warning matters because repeatable simulation and regulated game
operation have different requirements.

## Modulo bias, counted

The following numbers show how remainder mapping can favor some stops. Discarding a
small, fixed part of the source range restores equal probabilities. The arithmetic
fits in an 8-bit example.

**Start with a 26-stop reel.** For a small example, imagine that the random source
can return only the numbers 0 through 255. That gives us 256 equally likely numbers.
We turn each number into a reel stop with `raw % 26`.

Now deal those 256 numbers to the 26 stops. The first number goes to stop 0, the next
to stop 1, and so on. After nine complete trips around the reel, each stop has nine
numbers assigned to it. But 22 numbers are still left. Those extras go to stops 0
through 21. As a result:

- Stops 0 through 21 can each be selected by 10 raw numbers.
- Stops 22 through 25 can each be selected by only 9 raw numbers.

That means stop 3 is selected about 11% more often than stop 24. The random source
is fair; the `% 26` conversion makes the reel unfair.

The general rule comes after the example. If the source has `S` possible values and
the reel has `B` stops, `S % B` tells us how many stops receive one extra raw value.
There is no bias only when `S % B` is zero.

Run that for ten million draws and count the bins, and the skew walks right out
of the noise (seeded run, NumPy, 10,000,000 draws; a fair share is 384,615 per
bin):

| Bins | Raw values each | Predicted count | Measured (10M draws) |
|---|---|---|---|
| 0–21 | 10 | 390,625 | ≈ 390,572 each |
| 22–25 | 9 | 351,562 | ≈ 351,852 each |

Measured max/min ratio: 1.114 against the predicted 10/9 ≈ 1.111. Four stops are
visibly starved. On a real reel those four stops would carry symbols whose true
probability the PAR sheet states exactly, and the game would quietly pay a different
RTP than the math package claims.

Apply the rejection method to the same source and the skew disappears. Discard any
raw value of 234 or above (234 = 9 × 26, the largest full lap), redraw, and map the
rest. In the same ten-million-draw run that discarded 8.6% of raw values, and the
bins flattened to a max/min ratio of 1.006, which is sampling noise centered on fair.

**Which bin counts are safe?** The source size is always a power of two (2⁸, 2³²,
2⁶⁴), and the only numbers that divide a power of two are smaller powers of two. So
the remainder mapping is exactly fair for 2, 4, 8, 16, 32 … stops and biased for
every other count: every odd number, every prime except 2, and every even number
carrying an odd factor, 6 and 10 and 26 included. Prime or composite is beside the
point. "Is it a power of two" is the question.

**A perfect random source has the same problem.** The bias lives in the *mapping*
rather than the generator. Take a certified hardware RNG producing flawlessly uniform
64-bit values, fold it with `% 26`, and it still favors 16 of the 26 stops (2⁶⁴ mod
26 = 16). On a 64-bit source that excess is about one part in 10¹⁸, too small to
measure in any simulation you could run. It is also provably nonzero, and game
certification asks for provably zero. Nevada's Technical Standard 1 requires each
outcome of a random selection to be equally probable, and "equal to eighteen decimal
places" falls short of equal. The rejection method makes the probabilities equal. On
a 64-bit source, the discard fires about once per 10¹⁸ draws.

**The rejected set is the leftover, fixed in advance.** Return to the deal: 256 raw
values, 26 bins, nine full laps, and 22 values left over that cannot
fill a tenth lap. Those leftovers are the rejected set. In the classic method they
are literally the top of the source range, 234 through 255, the same values every
time, on every machine, decided by nothing but S and B. So rejection is a fixed
partition of the source range, written down before any randomness exists: values
0–233 map, nine each per stop, and values 234–255 redraw. The result never enters
into it.

### Lemire's rejection method: one multiply, both jobs

The classic method above works, and it pays an integer division (`raw % 26`) on every
draw. Division is among the slowest things an ALU does. So the engine's
`SpinRng.NextInt` uses **Lemire's rejection method** (Daniel Lemire's "nearly
divisionless" technique, 2019), which gets the stop *and* the fairness check out of a
single multiply. Decimal shows the idea most clearly.

Roll the die first, since everybody already trusts a die. The job: turn one raw
random number into a fair face, 1 of 6. The move behind the multiply is to read the
raw value as a **fraction of the way through its range**. In decimal, a 3-digit raw
number 000–999 reads like a percentage, so raw 730 means "73.0% of the way along the
line." A fair die asks one question of that line: *which sixth did you land in?* The
first sixth is face 1, the second sixth face 2, on to the end. Multiplying by 6
answers it in whole numbers:

```
730 × 6 = 4,380           a 4-digit product
   high digit: 4          = floor(0.730 × 6) → the 5th sixth → face 5
   low 3 digits: 380      = how deep inside that sixth the value landed
```

The high part of the product is the face. It falls straight out of the multiply,
with no modulo anywhere. The low part is where the value landed inside that face's
slice of the line, and the fairness check lives there. A thousand values will not
split into six equal sixths (1000 = 6 × 166 + 4), so four of the sixths carry one
extra value. The rule "reject when the low digits are less than 004" shaves exactly
those four extras, the values sitting at the very start of a fat sixth. Raw 730's low
half is 380, nowhere near the trim zone, so face 5 stands.

The complete die fits in one table: a 5-bit source (32 raw values) onto the 6 faces,
every case visible. Threshold = 32 mod 6 = 2, so reject when low < 2. The low column
climbs by 6 per row and wraps at every face boundary (bins 0–5 are faces 1–6):

| raw | ×6 | bin | low | verdict | | raw | ×6 | bin | low | verdict |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 0 | 0 | **0** | ✗ reject | | 16 | 96 | 3 | **0** | ✗ reject |
| 1 | 6 | 0 | 6 | keep | | 17 | 102 | 3 | 6 | keep |
| 2 | 12 | 0 | 12 | keep | | 18 | 108 | 3 | 12 | keep |
| 3 | 18 | 0 | 18 | keep | | 19 | 114 | 3 | 18 | keep |
| 4 | 24 | 0 | 24 | keep | | 20 | 120 | 3 | 24 | keep |
| 5 | 30 | 0 | 30 | keep | | 21 | 126 | 3 | 30 | keep |
| 6 | 36 | 1 | 4 | keep | | 22 | 132 | 4 | 4 | keep |
| 7 | 42 | 1 | 10 | keep | | 23 | 138 | 4 | 10 | keep |
| 8 | 48 | 1 | 16 | keep | | 24 | 144 | 4 | 16 | keep |
| 9 | 54 | 1 | 22 | keep | | 25 | 150 | 4 | 22 | keep |
| 10 | 60 | 1 | 28 | keep | | 26 | 156 | 4 | 28 | keep |
| 11 | 66 | 2 | 2 | keep ← boundary | | 27 | 162 | 5 | 2 | keep ← boundary |
| 12 | 72 | 2 | 8 | keep | | 28 | 168 | 5 | 8 | keep |
| 13 | 78 | 2 | 14 | keep | | 29 | 174 | 5 | 14 | keep |
| 14 | 84 | 2 | 20 | keep | | 30 | 180 | 5 | 20 | keep |
| 15 | 90 | 2 | 26 | keep | | 31 | 186 | 5 | 26 | keep |

Before the trim the faces hold 6, 5, 5, 6, 5, 5, the same loaded die from the start
of this section, produced by the multiply the way `% 6` produced it. The two rejects
are the heavy faces' *openers*. Raw 0 opens bin 0 at low 0, and raw 16 opens bin 3 at
low 0 (96 is exactly 3 × 32, a perfect wrap). Bins 2 and 5 open at low 2, right on
the threshold, and that first fair seat is kept: the threshold counts *positions to
shave*, so "low < 2" removes positions 0 and 1 and stops there. After the trim every
face holds **5, 5, 5, 5, 5, 5**, a fair die by construction. (One wrinkle worth
noticing: 6 and 32 share a factor of 2, so every low lands even and the odd positions
never occur at all. The rule doesn't care. The trim still removes exactly the two
extras, which both happen to sit at low 0 here.)

A reel is a die with more faces. A 26-stop strip is a 26-sided die, and only the
multiplier changes: raw 730 × 26 = 18,980, high digits 18 = stop 18, low 980,
threshold 1000 mod 26 = 12. Now swap the 1,000 for the bit combinations of a 64-bit
number. The raw value is one of 2⁶⁴ patterns. Multiply by 26 and the product is 128
bits. The high 64 bits are the stop, and the low 64 bits face the threshold
`2⁶⁴ mod 26 = 16`.

The reject zone is 16
positions out of 2⁶⁴, about 9 × 10⁻¹⁹, one redraw per 10¹⁸ draws. The 8-bit
classroom example rejected 8.6% of draws. The 64-bit production source rejects so
rarely that a simulation drawing a billion stops per second would wait roughly thirty
years for its first redraw.

`SpinRng.NextInt` performs one 64×64→128 multiply; the high bits become
the stop, low bits checked against a per-reel precomputed threshold, redraw on the
astronomically rare miss. The one division in the whole scheme, computing
`2⁶⁴ mod B`, runs once per reel at construction and never per spin.

### One long division, two methods

Long division answers two questions at once: how many whole times does the
divisor fit, and what is left over. The quotient and the remainder. Both
mapping methods in this chapter run a single long division. They divide
different numbers, and they keep different halves of the answer.

The remainder method divides the raw value by the stop count:

```text
raw  =  quotient × 26  +  remainder
```

It keeps the remainder as the stop and throws the quotient away.

Lemire's method divides the product by the source size:

```text
raw × 26  =  bin × 2⁶⁴  +  low
```

It keeps the quotient (the bin) as the stop, and the remainder becomes the
fairness check. The die tables above say this in their column headers: low is
`product mod 32`, a remainder. Taking the high 64 bits performs the division,
and the low 64 bits are what that division leaves over.

One more remainder ties the two methods together. Lemire's rejection
threshold is `2⁶⁴ mod 26`: the same leftover the remainder method meets as
the 16 raw values at the top of its range that cannot finish a lap. Each
method discards those 16, in different places: the remainder method drops
them as one block at the top, Lemire's drops one from the start of each fat
slice.

The bin and the classic remainder are the same kind of number with the roles
traded. In the remainder method the
remainder names the stop and the quotient says how deep into the raw range
you were. In Lemire's method the quotient names the stop and the remainder
says how deep into the stop's slice you landed.

**Every reel carries its own threshold.** The leftover depends on the bin count, and
a real game mixes bin counts. Orca Dive's strips run 26, 29, 26, 29, 26 stops, so one
threshold cannot serve the whole game. A 26-stop reel rejects its own leftover
(2⁶⁴ mod 26 = 16 raw values) and a 29-stop reel rejects its own (2⁶⁴ mod 29 = 24).
`StripReelSet` computes each reel's range and rejection threshold once, at
construction, straight from that reel's strip length, and every window draw indexes
them per reel (`_rngRanges[reel]`, `_rngThresholds[reel]`). A 26-stop reel and a
29-stop reel drawn in the same spin each get exactly uniform stops from their own
arithmetic, and that correctness costs zero divisions per spin, which is how article
9 can precompute it and leave every probability untouched.

**Why discarding stays fair.** The discard decision reads the raw bit pattern alone,
before it becomes a stop, a symbol, or a payout. Wins are invisible to it. Every
*stop* stays reachable and every stop is exactly equally likely, and that is the
entire effect. It works like "the die bounced off the table, throw again": a
malformed throw gets rerolled, and a disliked result never does. Test labs accept
rejection-based mapping for that reason. What they forbid is bias in the outcome, and
rejection is the standard way to remove it.

## What these types guarantee

Neither type knows what a reel is. `Millicents` handles money arithmetic;
`SpinRng` advances a deterministic stream. The rest of the system can rely on these
smaller contracts:

- Within the checked accumulator range, payout totals use integer addition and do
  not change when the same contributions arrive in a different order.
- Spin logic declares its RNG dependency in its signature.
- A run can be replayed when its full input record is preserved.

The paytable rounding rule and worker-seeding recipe each have one implementation, so
a policy change has one authoritative place to edit.

Next in the series: what a reel actually is, and the modeling mistake that gets every
single-symbol probability right and every two-symbol probability wrong.

## References

- [Blackman and Vigna's xoshiro/xoroshiro reference](https://prng.di.unimi.it/)
  documents xoshiro256**, its statistical scope, and SplitMix64 seeding.
- [Lemire, "Fast Random Integer Generation in an Interval" (2019)](https://arxiv.org/abs/1805.10941)
  is the multiply-based rejection method `NextInt` implements.
- [Nevada Technical Standard 1](https://www.gaming.nv.gov/siteassets/content/home/features/TechnicalStandard1.pdf)
  provides one jurisdiction's RNG and random-selection requirements.
- [GLI software submission requirements](https://gaminglabs.com/getting-started/submit-new-software/)
  list percentage calculations, reel strips, paytables, source code, and RNG evidence
  among common submission materials.

*Source files: `Money/Millicents.cs`, `Simulation/SpinRng.cs`.*

## Optimization notebook

`NextInt` runs once per reel per spin, so its setup cost may matter at tens of millions of
spins. Keep the validated API while building the system. After reel lengths become stable
construction-time data, measure whether their Lemire ranges and rejection thresholds are
worth calculating once. Article 9 performs that experiment.
