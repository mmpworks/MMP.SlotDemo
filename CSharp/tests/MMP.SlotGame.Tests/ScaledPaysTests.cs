using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using Xunit;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The one lever that moves RTP without touching geometry.
///
/// Line RTP is the sum over combinations of probability times pay. Probabilities come from
/// the strips, so scaling every pay by k scales line RTP by k, exactly. These tests state
/// that property and the boundaries around it.
/// </summary>
public sealed class ScaledPaysTests
{
    private static GameDefinition Load(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "games", file);
        Assert.True(GameDefinitionLoader.TryLoad(File.ReadAllText(path), out var game, out var errors),
            $"{file} failed to load: {string.Join("; ", errors)}");
        return game!;
    }

    [Theory]
    [InlineData(1.25)]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void Scaling_the_pays_scales_line_rtp_by_the_same_factor(double factor)
    {
        var game = Load("orca-dive.json");
        var before = GameAnalyzer.Analyze(game);
        var after = GameAnalyzer.Analyze(game.WithScaledPays(factor));

        // Rounding to whole hundredths of the wager keeps this from being exact; a fraction
        // of a percent of the scaled value is the documented tolerance.
        var expected = before.LineRtp * factor;
        Assert.Equal(expected, after.LineRtp, expected * 0.001);
    }

    [Fact]
    public void Scaling_the_pays_leaves_hit_frequency_alone()
    {
        var game = Load("orca-dive.json");
        var before = GameAnalyzer.Analyze(game);
        var after = GameAnalyzer.Analyze(game.WithScaledPays(1.4));

        // Hit frequency is a property of the strips and paylines. Money never enters it.
        Assert.Equal(before.HitFrequency, after.HitFrequency, 12);
        Assert.Equal(before.StopCombinations, after.StopCombinations);
        Assert.Equal(before.HitCombinations, after.HitCombinations);
    }

    [Fact]
    public void Scaling_the_pays_leaves_the_feature_alone()
    {
        var game = Load("orca-dive.json");
        var before = GameAnalyzer.Analyze(game);
        var after = GameAnalyzer.Analyze(game.WithScaledPays(1.4));

        // The bonus is a separate lever with its own prize table, so its contribution and
        // its trigger rate must survive a line re-pricing untouched.
        Assert.Equal(before.TriggerProbability, after.TriggerProbability, 12);
        Assert.Equal(before.BonusRtp, after.BonusRtp, 12);
    }

    [Fact]
    public void A_run_length_that_pays_nothing_still_pays_nothing()
    {
        var game = Load("orca-dive.json");
        var scaled = game.WithScaledPays(3.0);

        for (var index = 0; index < game.Categories.Count; index++)
        {
            var original = game.Categories[index];
            var after = scaled.Categories[index];
            for (var count = 0; count <= game.ReelCount; count++)
            {
                if (original.PayFor(count) == 0)
                    Assert.Equal(0, after.PayFor(count));
            }
        }
    }

    [Fact]
    public void Scaling_preserves_the_identity_of_every_category()
    {
        var game = Load("orca-dive.json");
        var scaled = game.WithScaledPays(1.1);

        Assert.Equal(game.Categories.Count, scaled.Categories.Count);
        for (var index = 0; index < game.Categories.Count; index++)
        {
            Assert.Equal(game.Categories[index].Name, scaled.Categories[index].Name);
            Assert.Equal(game.Categories[index].Kind, scaled.Categories[index].Kind);
            Assert.Equal(game.Categories[index].Index, scaled.Categories[index].Index);
        }
    }

    [Fact]
    public void Scaling_returns_a_copy_and_leaves_the_original_priced_as_shipped()
    {
        var game = Load("orca-dive.json");
        var beforeRtp = GameAnalyzer.Analyze(game).LineRtp;

        _ = game.WithScaledPays(2.0);

        Assert.Equal(beforeRtp, GameAnalyzer.Analyze(game).LineRtp, 12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_factor_that_is_not_a_positive_number_is_rejected(double factor)
    {
        var game = Load("orca-dive.json");
        Assert.Throws<ArgumentOutOfRangeException>(() => game.WithScaledPays(factor));
    }
}
