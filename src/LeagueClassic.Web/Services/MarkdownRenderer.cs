using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Html;

namespace LeagueClassic.Web.Services;

// Renders user markdown to HTML, then sanitizes it against an allow-list so a
// safe subset of inline HTML (e.g. <b>, <i>, <u>) works alongside markdown,
// without opening an XSS hole.
public class MarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseSoftlineBreakAsHardlineBreak()
        // Raw HTML is allowed through Markdig here and then filtered by the sanitizer below.
        .Build();

    private readonly HtmlSanitizer _sanitizer;

    public MarkdownRenderer()
    {
        _sanitizer = new HtmlSanitizer();
        // Start from a conservative set: keep basic formatting + links + lists,
        // drop scripts, styles, iframes, event handlers, etc.
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
                 {
                     "b","strong","i","em","u","s","del","mark","small","sub","sup",
                     "p","br","hr","blockquote","pre","code",
                     "h1","h2","h3","h4","h5","h6",
                     "ul","ol","li","a","span","table","thead","tbody","tr","th","td",
                 })
            _sanitizer.AllowedTags.Add(tag);

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("http");
        // Force external links to be safe.
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Html.Dom.IHtmlAnchorElement a)
            {
                a.SetAttribute("rel", "nofollow noopener noreferrer");
                a.SetAttribute("target", "_blank");
            }
        };
    }

    public HtmlString ToHtml(string? markdown)
    {
        var rawHtml = Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
        return new HtmlString(_sanitizer.Sanitize(rawHtml));
    }
}
