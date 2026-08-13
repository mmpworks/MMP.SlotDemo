using System.Text.Json;
using MMP.SlotGame.Core.Reels;

namespace MMP.SlotGame.Core.Games.Definition;

/// <summary>Every problem found in a game file, reported together rather than one per run.</summary>
public sealed class GameDefinitionException(string path, IReadOnlyList<string> errors)
    : Exception(Describe(path, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;

    private static string Describe(string path, IReadOnlyList<string> errors) =>
        $"Game definition '{path}' is not valid ({errors.Count} problem(s)):{Environment.NewLine}  "
        + string.Join(Environment.NewLine + "  ", errors);
}

/// <summary>
/// Reads a game from JSON and compiles it into a validated <see cref="GameDefinition"/>.
///
/// Imported games are validated here and nowhere else, in the same spirit as
/// <see cref="Simulation.SimulationConfig.TryCreate"/>: a definition that comes out of here
/// satisfied every rule, so nothing downstream re-checks geometry. Errors are collected
/// and reported together, so someone hand-transcribing a PAR sheet fixes the file in one
/// pass.
///
/// The checks are the ones a PAR sheet transcription actually gets wrong: a strip that does
/// not match its declared length, a symbol count that does not match the published table, a
/// pay table naming a symbol that is not on any reel, a payline row off the bottom of the
/// window, a scatter on a reel that never carries it.
/// </summary>
public static class GameDefinitionLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static GameDefinition LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        if (!TryLoad(json, out var definition, out var errors))
            throw new GameDefinitionException(path, errors);

        // LoadFile is the deployment construction path, not a validation probe. Complete
        // the PAR-derived lookup now so the first spin never pays its construction cost.
        _ = definition!.WinningOutcomes;
        return definition;
    }

    public static GameDefinition Load(string json)
    {
        if (!TryLoad(json, out var definition, out var errors))
            throw new GameDefinitionException("(inline)", errors);

        _ = definition!.WinningOutcomes;
        return definition;
    }

    public static bool TryLoad(string json, out GameDefinition? definition, out IReadOnlyList<string> errors)
    {
        definition = null;

        GameDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<GameDocument>(json, Options);
        }
        catch (JsonException ex)
        {
            errors = [$"The file is not valid JSON: {ex.Message}"];
            return false;
        }

        if (document is null)
        {
            errors = ["The file parsed to nothing; a game definition must be a JSON object."];
            return false;
        }

        var builder = new GameDefinitionBuilder(document);
        var ok = builder.TryBuild(out definition);
        errors = builder.Errors;
        return ok;
    }
}
