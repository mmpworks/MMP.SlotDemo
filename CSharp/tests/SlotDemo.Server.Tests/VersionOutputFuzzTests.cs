using SlotDemo.Server;
using Xunit;

namespace SlotDemo.Server.Tests;

/// <summary>
/// Seeded fuzz over <see cref="AiSystemProbe.NormalizeVersionOutput"/>: arbitrary CLI
/// output (garbage bytes, unicode, control chars, huge blobs) must never throw and
/// must always yield null or a trimmed, single-line, non-empty string.
/// </summary>
public sealed class VersionOutputFuzzTests
{
    private const int Iterations = 10_000;
    private const int Seed = 0xC0FFEE; // fixed seed — failures reproduce

    [Fact]
    public void NormalizeVersionOutput_NeverThrows_AndKeepsInvariants()
    {
        var rng = new Random(Seed);
        for (var i = 0; i < Iterations; i++)
        {
            var input = RandomBlob(rng);
            var result = AiSystemProbe.NormalizeVersionOutput(input);

            if (result is null) continue;
            Assert.True(result.Length > 0, $"iteration {i}: empty result from {Show(input)}");
            Assert.Equal(result, result.Trim());
            Assert.DoesNotContain('\n', result);
            Assert.DoesNotContain('\r', result);
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   \r\n \r\n", null)]
    [InlineData("2.1.226 (Claude Code)\n", "2.1.226 (Claude Code)")]
    [InlineData("ollama version is 0.32.1", "0.32.1")]
    [InlineData("OLLAMA VERSION IS 9.9.9", "9.9.9")]
    [InlineData("\r\n\r\ncodex-cli 0.147.0\r\nextra line", "codex-cli 0.147.0")]
    public void NormalizeVersionOutput_KnownShapes(string? input, string? expected)
        => Assert.Equal(expected, AiSystemProbe.NormalizeVersionOutput(input));

    private static string? RandomBlob(Random rng)
    {
        if (rng.Next(20) == 0) return null;
        var length = rng.Next(0, rng.Next(10) == 0 ? 50_000 : 200);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = rng.Next(6) switch
            {
                0 => (char)rng.Next(0x00, 0x20),          // control chars incl. \r \n \t
                1 => ' ',
                2 => (char)rng.Next('a', 'z' + 1),
                3 => (char)rng.Next('0', '9' + 1),
                4 => (char)rng.Next(0x80, 0x3000),        // wide unicode
                _ => "ollama version is .%/\\\"'"[rng.Next("ollama version is .%/\\\"'".Length)],
            };
        }
        return new string(chars);
    }

    private static string Show(string? s) =>
        s is null ? "<null>" : $"len={s.Length} \"{s[..Math.Min(s.Length, 40)].Replace("\r", "\\r").Replace("\n", "\\n")}\"";
}
