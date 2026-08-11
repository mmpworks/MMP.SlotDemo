using MMP.SlotGame.Core.Games;
using MMP.SlotGame.Core.Games.Definition;
using MMP.SlotGame.Core.Rtp;
using MMP.SlotGame.Core.Simulation;

namespace MMP.SlotGame.Tests;

/// <summary>
/// The `payUnit` opt-in (docs/game-definition-schema.md): a game declares its whole paytable
/// in tenths or hundredths of the total spin bet to express a fractional multiplier that
/// "units" cannot state — 1.5X needs tenths, 2.25X and 1.75X need hundredths, the finest unit the
/// engine supports. The loader still rejects a fractional-looking pay when the file never
/// opted into either.
///
/// Money stays 100% integer throughout (architecture invariant M1, <see cref="Millicents"/>):
/// these tests exist because the fractional multiplier is a JSON-level convenience, never a
/// floating-point value anywhere on the pay path.
/// </summary>
[Trait("Category", "Fast")]
public sealed class FractionalPayUnitTests
{
    private const string TenthsGame =
        """
        {
          "name": "Tenths Fixture",
          "windowRows": 3,
          "payUnit": "tenths",
          "symbols": [ { "name": "Seven" }, { "name": "Blank" } ],
          "reels": [ ["Seven", "Blank"], ["Seven", "Blank"], ["Seven", "Blank"] ],
          "paylines": [ { "name": "Centre", "rows": [0, 0, 0] } ],
          "paytable": [ { "symbol": "Seven", "pays": { "3": 15 } } ]
        }
        """;

    private const string HundredthsGame =
        """
        {
          "name": "Hundredths Fixture",
          "windowRows": 3,
          "payUnit": "hundredths",
          "symbols": [ { "name": "Seven" }, { "name": "Blank" } ],
          "reels": [ ["Seven", "Blank"], ["Seven", "Blank"], ["Seven", "Blank"] ],
          "paylines": [ { "name": "Centre", "rows": [0, 0, 0] } ],
          "paytable": [ { "symbol": "Seven", "pays": { "3": 225 } } ]
        }
        """;

    private const string UnitsGameWithFractionalPay =
        """
        {
          "name": "Units Fixture",
          "windowRows": 3,
          "symbols": [ { "name": "Seven" }, { "name": "Blank" } ],
          "reels": [ ["Seven", "Blank"], ["Seven", "Blank"], ["Seven", "Blank"] ],
          "paylines": [ { "name": "Centre", "rows": [0, 0, 0] } ],
          "paytable": [ { "symbol": "Seven", "pays": { "3": 1.5 } } ]
        }
        """;

    private static (GameDefinition Definition, LineWin Win) LoadAndForceWin(string json)
    {
        var loaded = GameDefinitionLoader.TryLoad(json, out var definition, out var errors);
        Assert.True(loaded, "Fixture failed to load:\n  " + string.Join("\n  ", errors));

        var evaluator = new WinEvaluator(definition!);
        var seven = (byte)definition!.SymbolId("Seven");
        return (definition, evaluator.Evaluate([seven, seven, seven]));
    }

    /// <summary>
    /// The number quoted throughout the design: 2.25X at a 1-credit (100,000 millicent) wager
    /// realizes as exactly 225,000 millicents, with nothing to round.
    /// </summary>
    [Fact]
    public void PayUnitHundredths_TwoAndAQuarterXPay_RealizesExactMillicentsOnAForcedWin()
    {
        var (_, win) = LoadAndForceWin(HundredthsGame);

        Assert.True(win.IsWin);
        Assert.Equal(225, win.Multiplier); // 2.25X, in hundredths

        var linePay = SimulationConfig.Wager.ScaledMultiply(win.Multiplier);
        Assert.Equal(225_000L, linePay.Value);
    }

    [Theory]
    [InlineData(125, 125_000L)] // 1.25X
    [InlineData(175, 175_000L)] // 1.75X — not expressible in tenths
    [InlineData(225, 225_000L)] // 2.25X — not expressible in tenths
    public void PayUnitHundredths_EveryQuarterStepPay_RealizesExactMillicents(int payHundredths, long expectedMillicents)
    {
        var json = HundredthsGame.Replace("\"3\": 225", $"\"3\": {payHundredths}");
        var (_, win) = LoadAndForceWin(json);

        Assert.Equal(payHundredths, win.Multiplier);

        var linePay = SimulationConfig.Wager.ScaledMultiply(win.Multiplier);
        Assert.Equal(expectedMillicents, linePay.Value);
    }

    /// <summary>
    /// The predecessor unit still works exactly as it did before hundredths existed: 1.5X
    /// stated as 15 tenths compiles to the same pay as before (150 hundredths internally, but
    /// the realized millicents are identical either way).
    /// </summary>
    [Fact]
    public void PayUnitTenths_StillLoadsAndPaysIdenticallyToBeforeHundredthsExisted()
    {
        var (_, win) = LoadAndForceWin(TenthsGame);

        Assert.True(win.IsWin);

        var linePay = SimulationConfig.Wager.ScaledMultiply(win.Multiplier);
        Assert.Equal(150_000L, linePay.Value); // 1.5X at 1 credit, same result as pre-hundredths
    }

    /// <summary>
    /// Without `payUnit: "tenths"` or `"hundredths"`, a fractional pay is a transcription
    /// mistake, not a valid 1.5X — and the loader says so, by name, rather than a bare JSON
    /// type error.
    /// </summary>
    [Fact]
    public void FractionalPayInUnitsMode_IsRejectedWithAMessageNamingTheFix()
    {
        var loaded = GameDefinitionLoader.TryLoad(UnitsGameWithFractionalPay, out _, out var errors);

        Assert.False(loaded);
        Assert.Contains(errors, e =>
            e.Contains("payUnit", StringComparison.Ordinal) && e.Contains("hundredths", StringComparison.Ordinal));
    }

    /// <summary>
    /// Hundredths is the finest unit the engine supports, so a value needing a third decimal
    /// place — 2.255X, stated as a fractional 225.5 under "hundredths" — is rejected in every
    /// mode, and the message names hundredths as the ceiling on precision.
    /// </summary>
    [Fact]
    public void PayNeedingMoreThanTwoDecimalPlaces_IsRejectedNamingTheFinestSupportedUnit()
    {
        var json = HundredthsGame.Replace("\"3\": 225", "\"3\": 225.5");

        var loaded = GameDefinitionLoader.TryLoad(json, out _, out var errors);

        Assert.False(loaded);
        Assert.Contains(errors, e =>
            e.Contains("hundredths", StringComparison.Ordinal) && e.Contains("precision", StringComparison.Ordinal));
    }

    [Fact]
    public void UnrecognisedPayUnit_IsReported()
    {
        var broken = TenthsGame.Replace("\"tenths\"", "\"fifths\"");

        var loaded = GameDefinitionLoader.TryLoad(broken, out _, out var errors);

        Assert.False(loaded);
        Assert.Contains(errors, e => e.Contains("payUnit", StringComparison.Ordinal) && e.Contains("fifths", StringComparison.Ordinal));
    }

    /// <summary>
    /// A single-payline synthetic game with 0.5X / 0.9X / 1.5X pays, all in one 8-stop-per-reel
    /// strip so <see cref="GameAnalyzer"/> can enumerate it exactly. If the analytic side and
    /// the simulated side disagreed about what a hundredths pay is worth, this would show up as
    /// a measured RTP off by a large, unmistakable factor — not a rounding-sized drift — well
    /// inside a modest spin budget.
    /// </summary>
    private const string ThreeFractionalPaysGame =
        """
        {
          "name": "Fractional Pays Fixture",
          "windowRows": 3,
          "payUnit": "hundredths",
          "symbols": [ { "name": "A" }, { "name": "B" }, { "name": "C" }, { "name": "Blank" } ],
          "reels": [
            ["A", "B", "C", "Blank", "Blank", "Blank", "Blank", "Blank"],
            ["A", "B", "C", "Blank", "Blank", "Blank", "Blank", "Blank"],
            ["A", "B", "C", "Blank", "Blank", "Blank", "Blank", "Blank"]
          ],
          "paylines": [ { "name": "Centre", "rows": [1, 1, 1] } ],
          "paytable": [
            { "symbol": "A", "pays": { "3": 50 } },
            { "symbol": "B", "pays": { "3": 90 } },
            { "symbol": "C", "pays": { "3": 150 } }
          ]
        }
        """;

    [Fact]
    public async Task AnalyticLineRtp_AgreesWithSimulatedLineRtp_ForFractionalPays()
    {
        var loaded = GameDefinitionLoader.TryLoad(ThreeFractionalPaysGame, out var definition, out var errors);
        Assert.True(loaded, "Fractional-pays fixture failed to load:\n  " + string.Join("\n  ", errors));

        var analytic = GameAnalyzer.Analyse(definition!);

        var plan = new RunPlan("fractional-pays", 0x0F0F_1234_5678_9ABCUL, WorkerCount: 4, TargetSpins: 2_000_000);
        var result = await new GameRunner(definition!, plan).RunAsync();

        // Two-sided 99.9% band, same construction as GameConvergenceTests: wide enough to
        // absorb sampling noise, tight enough that a missing /100 (a 100x error either way)
        // or a wrong /10000 on the squared term could never pass it.
        var band = NormalQuantile.TwoSided999 * analytic.LineSigma / Math.Sqrt(2_000_000);

        Assert.True(
            Math.Abs(result.LineRtp - analytic.LineRtp) <= band,
            $"Simulated line RTP {result.LineRtp:R} is outside the analytic band {analytic.LineRtp:R} +/-{band:R}.");
    }
}
