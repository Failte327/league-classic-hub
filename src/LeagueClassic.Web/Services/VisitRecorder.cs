using LeagueClassic.Web.Data;

namespace LeagueClassic.Web.Services;

// Backs the super-lightweight visitor counter: one row per page GET, no IP,
// no user-agent, no cookie. See Program.cs for where this gets called and
// Pages/Stats for where the counts get read back out.
public class VisitRecorder
{
    private readonly ApplicationDbContext _db;

    public VisitRecorder(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(string path)
    {
        _db.PageViews.Add(new PageView { OccurredAt = DateTimeOffset.UtcNow, Path = path });
        await _db.SaveChangesAsync();
    }
}
