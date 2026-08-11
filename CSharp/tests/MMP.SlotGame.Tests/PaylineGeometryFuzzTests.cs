using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Tests;

/// <summary>
/// Randomized geometry checks for <see cref="Payline.For"/> and <see cref="Payline.Center"/>
/// across window heights and reel counts, independent of any concrete preset.
/// </summary>
[Trait("Category", "Fast")]
public sealed class PaylineGeometryFuzzTests
{
    private const int Iterations = 5_000;

    [Fact]
    public void For_EveryGeneratedRow_StaysInsideTheWindow()
    {
        var rng = new Random(3001);
        for (var i = 0; i < Iterations; i++)
        {
            var rows = rng.Next(StripReelSet.MinRows, StripReelSet.MaxRows + 1);
            var reelCount = rng.Next(1, 21);
            var lineCount = rng.Next(2) == 0 ? 5 : 9;

            var paylines = Payline.For(reelCount, lineCount, rows);

            Assert.Equal(lineCount, paylines.Count);
            foreach (var line in paylines)
            {
                Assert.Equal(reelCount, line.Rows.Count);
                Assert.All(line.Rows, row => Assert.InRange(row, 0, rows - 1));
            }
        }
    }

    [Fact]
    public void For_UnsupportedLineCount_ThrowsRatherThanReturningAPartialSet()
    {
        var rng = new Random(3002);
        for (var i = 0; i < Iterations; i++)
        {
            var rows = rng.Next(StripReelSet.MinRows, StripReelSet.MaxRows + 1);
            var reelCount = rng.Next(1, 21);
            var badLineCount = rng.Next(2) == 0 ? rng.Next(-10, 5) : rng.Next(6, 9);

            Assert.Throws<ArgumentException>(() => Payline.For(reelCount, badLineCount, rows));
        }
    }

    [Fact]
    public void Center_AlwaysReturnsTheMiddleRowOnEveryReel()
    {
        var rng = new Random(3003);
        for (var i = 0; i < Iterations; i++)
        {
            var rows = rng.Next(StripReelSet.MinRows, StripReelSet.MaxRows + 1);
            var reelCount = rng.Next(1, 21);

            var line = Payline.Center(reelCount, rows);

            Assert.Equal(reelCount, line.Rows.Count);
            Assert.All(line.Rows, row => Assert.Equal(rows / 2, row));
        }
    }

    /// <summary>
    /// The "V" and "Hat" lines are constructed to span the full window height (0 to rows - 1)
    /// at the middle reel, by the class doc comment's own claim. This pins that claim across
    /// random reel counts and window heights instead of only the fixed shapes the stock
    /// presets happen to exercise.
    /// </summary>
    [Fact]
    public void NineLineSet_VAndHatLines_TouchBothWindowEdgesAtTheMiddleReel()
    {
        var rng = new Random(3004);
        for (var i = 0; i < Iterations; i++)
        {
            var rows = rng.Next(StripReelSet.MinRows, StripReelSet.MaxRows + 1);
            var reelCount = rng.Next(2, 21);

            var paylines = Payline.For(reelCount, 9, rows);
            var vee = paylines.Single(p => p.Name == "V");
            var hat = paylines.Single(p => p.Name == "Hat");
            var middleReel = reelCount / 2;

            Assert.Equal(rows - 1, vee.Rows[middleReel]);
            Assert.Equal(0, hat.Rows[middleReel]);
        }
    }
}
