using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Forums;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    // A rotating backdrop of classic Fields of Justice art — old golems, the old map, old
    // Nexus crystals — for a bit of texture behind an otherwise text-heavy thread list.
    private static readonly string[] BackgroundArt =
    {
        "map-summoners-rift.jpg",
        "map-parchment.jpg",
        "golem-ancient.jpg",
        "golem-lizard-elder.jpg",
        "nexus-crystal-order.jpg",
        "nexus-crystal-chaos.jpg",
    };

    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<ForumThread> Pinned { get; private set; } = new();
    public List<ForumThread> Threads { get; private set; } = new();
    public string ForumBg { get; private set; } = BackgroundArt[0];

    public async Task OnGetAsync()
    {
        ForumBg = BackgroundArt[Random.Shared.Next(BackgroundArt.Length)];

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
