using System.Text;
using System.Text.RegularExpressions;

namespace LeagueClassic.Web.Services;

// First-line automated content filter: a curated slur/hate blocklist with
// leetspeak normalization, plus length limits. NOT a guarantee — it catches
// obvious cases and typical evasions; human moderation is the real backstop.
public class ContentModerationService
{
    private readonly List<Regex> _patterns = new();

    // Common leetspeak / lookalike substitutions collapsed before matching.
    private static readonly Dictionary<char, char> Leet = new()
    {
        ['0'] = 'o', ['1'] = 'i', ['3'] = 'e', ['4'] = 'a', ['5'] = 's',
        ['7'] = 't', ['8'] = 'b', ['9'] = 'g', ['@'] = 'a', ['$'] = 's', ['!'] = 'i',
    };

    public ContentModerationService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "content", "blocklist.txt");
        if (!File.Exists(path)) return;

        foreach (var raw in File.ReadAllLines(path))
        {
            var term = raw.Trim();
            if (term.Length == 0 || term.StartsWith('#')) continue;

            // Allow optional separators between characters so "n i g g e r" and
            // "n-i-g-g-e-r" are caught too, while \b keeps it to whole words.
            var body = string.Join(@"[\W_]*", term.Select(c => Regex.Escape(c.ToString())));
            _patterns.Add(new Regex($@"\b{body}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        }
    }

    public bool ContainsBlockedTerm(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var normalized = Normalize(text);
        return _patterns.Any(p => p.IsMatch(normalized));
    }

    // Returns an error message if the text is too long or contains blocked
    // terms; null if it's acceptable. (Emptiness is left to the caller.)
    public string? Validate(string? text, string fieldName, int maxLength)
    {
        if (text is not null && text.Length > maxLength)
            return $"{fieldName} is too long (max {maxLength:N0} characters).";
        if (ContainsBlockedTerm(text))
            return $"{fieldName} contains language that isn't allowed here. Please review the community rules.";
        return null;
    }

    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
            sb.Append(Leet.TryGetValue(ch, out var mapped) ? mapped : ch);
        return sb.ToString();
    }
}
