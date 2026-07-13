using Markdig;
using Microsoft.AspNetCore.Html;

namespace LeagueClassic.Web.Services;

// Renders user markdown to HTML. Markdig's advanced-but-safe pipeline is used;
// note: for untrusted input you should HTML-sanitize the output before launch
// (e.g. Ganss.Xss / HtmlSanitizer). Left as a TODO so it's a conscious choice.
public class MarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml() // block raw HTML in user content — cheap XSS defense for now
        .Build();

    public HtmlString ToHtml(string? markdown)
        => new HtmlString(Markdown.ToHtml(markdown ?? string.Empty, _pipeline));
}
