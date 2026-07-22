using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Stats;

[Authorize(Roles = DbSeeder.ModeratorRole)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public int TotalViews { get; private set; }
    public int TodayViews { get; private set; }
    public int Last7DaysViews { get; private set; }
    public List<DailyCount> Last14Days { get; private set; } = new();
    public List<PageCount> TopPages { get; private set; } = new();

    public record DailyCount(DateOnly Date, int Count);
    public record PageCount(string Path, int Count);

    public async Task OnGetAsync()
    {
        // Keep every date comparison in plain DateTime (UTC calendar dates)
        // and only build a DateTimeOffset for the one value that crosses into
        // the SQL query. DateTimeOffset.UtcNow.Date returns an Unspecified-
        // kind DateTime; comparing that directly against a DateTimeOffset
        // column implicitly reinterprets it in the *server's local* time
        // zone, which Npgsql then rejects (the column only accepts UTC).
        var todayUtc = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var windowStartDate = todayUtc.AddDays(-13); // 14-day window including today
        var windowStart = new DateTimeOffset(windowStartDate, TimeSpan.Zero);

        TotalViews = await _db.LegacyPageViews.CountAsync();

        // One query for the whole 14-day window; both the daily breakdown and
        // the last-7-days figures are derived from it in memory. Fine at this
        // site's traffic volume and avoids fighting the Npgsql date-grouping
        // translator.
        var recent = await _db.LegacyPageViews
            .Where(v => v.OccurredAt >= windowStart)
            .Select(v => new { v.OccurredAt, v.Path })
            .ToListAsync();

        TodayViews = recent.Count(v => v.OccurredAt.UtcDateTime.Date == todayUtc);

        var sevenDaysAgo = todayUtc.AddDays(-6);
        var last7 = recent.Where(v => v.OccurredAt.UtcDateTime.Date >= sevenDaysAgo).ToList();
        Last7DaysViews = last7.Count;

        Last14Days = Enumerable.Range(0, 14)
            .Select(i => windowStartDate.AddDays(i))
            .Select(d => new DailyCount(DateOnly.FromDateTime(d), recent.Count(v => v.OccurredAt.UtcDateTime.Date == d)))
            .ToList();

        TopPages = last7
            .GroupBy(v => v.Path)
            .Select(g => new PageCount(g.Key, g.Count()))
            .OrderByDescending(p => p.Count)
            .ThenBy(p => p.Path)
            .Take(10)
            .ToList();
    }
}
