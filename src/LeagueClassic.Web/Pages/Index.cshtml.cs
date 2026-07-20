using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<Guide> LatestGuides { get; private set; } = new();
    public List<ForumThread> LatestThreads { get; private set; } = new();
    public string? HeroSplash { get; private set; }

    public async Task OnGetAsync()
    {
        // A rotating featured champion splash behind the welcome text.
        var splashSlugs = await _db.Champions
            .Where(c => c.IsAvailable)
            .Select(c => c.Slug)
            .ToListAsync();
        if (splashSlugs.Count > 0)
            HeroSplash = $"assets/splash/{splashSlugs[Random.Shared.Next(splashSlugs.Count)]}.jpg";

        LatestGuides = await _db.Guides
            .Where(g => g.Status == GuideStatus.Published)
            .Include(g => g.Champion)
            .Include(g => g.Author)
            .OrderByDescending(g => g.UpdatedAt)
            .Take(8)
            .AsNoTracking()
            .ToListAsync();

        // Pinned threads (e.g. Dev Updates) always lead the feed, regardless of
        // recency, then the rest fill in by latest activity.
        var pinned = await _db.Threads
            .Where(t => t.IsPinned)
            .Include(t => t.Author)
            .OrderByDescending(t => t.LastPostAt)
            .Take(2)
            .AsNoTracking()
            .ToListAsync();

        var rest = await _db.Threads
            .Where(t => !t.IsPinned)
            .Include(t => t.Author)
            .OrderByDescending(t => t.LastPostAt)
            .Take(6 - pinned.Count)
            .AsNoTracking()
            .ToListAsync();

        LatestThreads = pinned.Concat(rest).ToList();
    }
}
