using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Forums;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<ForumThread> Pinned { get; private set; } = new();
    public List<ForumThread> Threads { get; private set; } = new();

    public async Task OnGetAsync()
    {
        // Up to 3 pinned threads on top (welcome, patch discussion, etc.).
        Pinned = await _db.Threads
            .Where(t => t.IsPinned)
            .Include(t => t.Author)
            .OrderByDescending(t => t.LastPostAt)
            .Take(3)
            .AsNoTracking()
            .ToListAsync();

        // Everything else, most-recent activity first.
        Threads = await _db.Threads
            .Where(t => !t.IsPinned)
            .Include(t => t.Author)
            .OrderByDescending(t => t.LastPostAt)
            .Take(50)
            .AsNoTracking()
            .ToListAsync();
    }
}
