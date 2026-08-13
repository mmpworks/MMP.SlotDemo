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
    /// Multiplies an amount by a whole-number count. For example, wager * spinCount
    /// returns the total amount wagered. Use <see cref="ScaledMultiply"/> for fractional
    /// payout multipliers such as 2.25 times the wager.
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
