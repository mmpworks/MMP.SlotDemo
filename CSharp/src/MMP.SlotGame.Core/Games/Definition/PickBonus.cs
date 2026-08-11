using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Core.Games.Definition;

/// <summary>
/// A pick-until-you-lose bonus, configured from data: a bag of prize gifts plus some number
/// of blanks that end the round for a fixed consolation. Orca Dive fills it with 24
/// prizes and 6 Party Poopers, but nothing here knows that.
///
/// <see cref="Play"/> draws gifts without replacement. The closed forms below provide an
/// independent analytic check of that play path.
///
/// The moments come out of one observation about a uniformly random order of all the gifts.
/// Prize i is collected exactly when it precedes every blank, which by symmetry happens with
/// probability 1/(b+1); prizes i and j are both collected when they lead the (b+2)-item
/// subset made of themselves and the blanks, probability 2/((b+2)(b+1)). Summing those over
/// the prize bag gives the mean and second moment without enumerating a permutation.
/// </summary>
public sealed class PickBonus
{
    private readonly int[] _prizes;

    internal PickBonus(int[] prizes, int blankCount, int consolation)
    {
        _prizes = [.. prizes];
        BlankCount = blankCount;
        Consolation = consolation;

        long prizeTotal = 0, prizeSquareTotal = 0;
        foreach (var prize in _prizes)
        {
            prizeTotal += prize;
            prizeSquareTotal += (long)prize * prize;
        }
        PrizeTotal = prizeTotal;

        var single = 1.0 / (blankCount + 1.0);
        var pair = 2.0 / ((blankCount + 2.0) * (blankCount + 1.0));
        var meanCollected = prizeTotal * single;
        var meanSquaredCollected = prizeSquareTotal * single
            + ((double)prizeTotal * prizeTotal - prizeSquareTotal) * pair;

        Mean = meanCollected + consolation;
        MeanSquared = meanSquaredCollected + 2.0 * consolation * meanCollected + (double)consolation * consolation;
    }

    public int PrizeCount => _prizes.Length;

    public int BlankCount { get; }

    /// <summary>Paid when a blank ends the round. Zero is a perfectly good value.</summary>
    public int Consolation { get; }

    public int GiftCount => _prizes.Length + BlankCount;

    public long PrizeTotal { get; }

    /// <summary>Largest award possible: every prize, then a blank.</summary>
    public long MaxAward => PrizeTotal + Consolation;

    /// <summary>Analytic expected award in total-spin-wager units.</summary>
    public double Mean { get; }

    /// <summary>Analytic second moment of the award.</summary>
    public double MeanSquared { get; }

    public double Variance => MeanSquared - Mean * Mean;

    /// <summary>
    /// Play one round for real. Draws uniformly from the gifts still in the bag by swapping
    /// the drawn slot with the last live one, which is a partial Fisher-Yates and therefore
    /// picking WITHOUT replacement rather than merely picking repeatedly. Always terminates:
    /// once only blanks remain the next draw must be one.
    ///
    /// <paramref name="scratch"/> is caller-owned and must hold <see cref="GiftCount"/>
    /// entries. The caller owns it so a worker can reuse one buffer for every bonus it
    /// plays and this type can stay shared and immutable.
    /// </summary>
    public int Play(ref SpinRng rng, Span<int> scratch)
    {
        if (scratch.Length < GiftCount)
            throw new ArgumentException($"Scratch needs {GiftCount} entries, got {scratch.Length}.", nameof(scratch));

        var pool = scratch[..GiftCount];
        _prizes.CopyTo(pool);
        pool[_prizes.Length..].Fill(BlankMarker);

        var remaining = GiftCount;
        var won = 0;
        while (true)
        {
            var slot = rng.NextInt(remaining);
            var gift = pool[slot];
            pool[slot] = pool[--remaining];
            if (gift == BlankMarker) return won + Consolation;
            won += gift;
        }
    }

    /// <summary>Marks a blank in the bag. Prize values are validated positive, so this cannot collide.</summary>
    private const int BlankMarker = -1;
}
