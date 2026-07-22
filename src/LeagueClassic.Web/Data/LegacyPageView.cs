namespace LeagueClassic.Web.Data;

// Frozen historical data from the original in-house hit counter, retired in
// favor of Plausible (see Program.cs / _Layout.cshtml). No new rows are
// written to this table — it's kept read-only for the Stats page's
// "before we had real analytics" chart. One row per recorded page request,
// deliberately minimal: no IP, no user-agent, no cookie/session id.
public class LegacyPageView
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string Path { get; set; }
}
