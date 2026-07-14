using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Guides;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    // A rotating backdrop of classic Fields of Justice art — same pool as the
    // Forums banner — for a bit of texture behind an otherwise text-heavy list.
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

    public List<Guide> Guides { get; private set; } = new();
    public string BannerBg { get; private set; } = BackgroundArt[0];

    public async Task OnGetAsync()
    {
        BannerBg = BackgroundArt[Random.Shared.Next(BackgroundArt.Length)];

        Guides = await _db.Guides
            .Where(g => g.Status == GuideStatus.Published)
            .Include(g => g.Champion)
            .Include(g => g.Author)
            .OrderByDescending(g => g.UpdatedAt)
            .AsNoTracking()
            .ToListAsync();
    }
}
