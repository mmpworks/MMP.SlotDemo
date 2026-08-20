using System.Text.RegularExpressions;
using MMP.Herald.Events;
using MMP.Herald.Pipeline;
using MMP.Herald.Templating;

namespace SlotDemo.Server.Chapters;

/// <summary>
/// Serves the written series to the TEACH ME section.
///
/// The articles live in docs/articles and are copied to the output directory at build time,
/// so the markdown files stay the single source. The alternative, pasting the prose into Vue
/// components, would give the site a second copy to drift from the one that gets edited.
/// </summary>
public static partial class ArticleEndpoints
{
    private static readonly LogCategory Category = new("Articles");

    /// <summary>Leading digits then a slug, which is how every article file is named.</summary>
    [GeneratedRegex(@"^(?<number>\d{2})-(?<slug>[a-z0-9-]+)\.md$")]
    private static partial Regex FileName();

    /// <summary>The first ATX heading in the file, which is the article's title.</summary>
    [GeneratedRegex(@"^#\s+(?<title>.+)$", RegexOptions.Multiline)]
    private static partial Regex FirstHeading();

    private static string Root => Path.Combine(AppContext.BaseDirectory, "articles");

    public static void MapArticles(this WebApplication app, StructuredLogger log)
    {
        app.MapGet("/api/articles", () => Results.Ok(List()));
        app.MapGet("/api/articles/{id}", (string id) => Read(id, log));
    }

    /// <summary>
    /// Every article, in series order, with the title read from the file rather than
    /// restated here. README.md is documentation about the series, not part of it.
    /// </summary>
    private static IEnumerable<object> List()
    {
        if (!Directory.Exists(Root)) return [];

        return Directory.EnumerateFiles(Root, "*.md")
            .Select(path => (Path: path, Match: FileName().Match(Path.GetFileName(path))))
            .Where(entry => entry.Match.Success)
            .OrderBy(entry => entry.Match.Groups["number"].Value, StringComparer.Ordinal)
            .Select(entry => new
            {
                id = Path.GetFileNameWithoutExtension(entry.Path),
                number = entry.Match.Groups["number"].Value,
                slug = entry.Match.Groups["slug"].Value,
                title = TitleOf(entry.Path),
            })
            .ToArray();
    }

    private static string TitleOf(string path)
    {
        var heading = FirstHeading().Match(File.ReadAllText(path));
        return heading.Success ? heading.Groups["title"].Value.Trim() : Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// One article's markdown. The id is matched against the known file names rather than
    /// joined onto a path, so a crafted id cannot reach outside the articles directory.
    /// </summary>
    private static IResult Read(string id, StructuredLogger log)
    {
        var fileName = $"{id}.md";
        if (!FileName().IsMatch(fileName))
            return Results.BadRequest(new { error = "Unknown article." });

        var path = Path.Combine(Root, fileName);
        if (!File.Exists(path))
            return Results.NotFound(new { error = $"No article '{id}'." });

        var markdown = File.ReadAllText(path);
        log.Information(Category, "Article {Id} served, {Bytes} bytes",
            new LogProperty("Id", id),
            new LogProperty("Bytes", markdown.Length));

        return Results.Ok(new { id, title = TitleOf(path), markdown });
    }
}
