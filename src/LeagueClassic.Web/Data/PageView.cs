namespace LeagueClassic.Web.Data;

// One row per recorded page request. Deliberately minimal — no IP, no
// user-agent, no cookie/session id — just when and what, so this stays
// truthful against the "no tracking" language in the privacy policy.
public class PageView
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string Path { get; set; }
}
