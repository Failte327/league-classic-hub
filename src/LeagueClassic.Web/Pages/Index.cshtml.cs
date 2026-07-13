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

    public async Task OnGetAsync()
    {
        LatestGuides = await _db.Guides
            .Where(g => g.Status == GuideStatus.Published)
            .Include(g => g.Champion)
            .Include(g => g.Author)
            .OrderByDescending(g => g.UpdatedAt)
            .Take(8)
            .AsNoTracking()
            .ToListAsync();

        LatestThreads = await _db.Threads
            .Include(t => t.Board)
            .Include(t => t.Author)
            .OrderByDescending(t => t.LastPostAt)
            .Take(6)
            .AsNoTracking()
            .ToListAsync();
    }
}
