using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Guides;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<Guide> Guides { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Guides = await _db.Guides
            .Where(g => g.Status == GuideStatus.Published)
            .Include(g => g.Champion)
            .Include(g => g.Author)
            .OrderByDescending(g => g.UpdatedAt)
            .AsNoTracking()
            .ToListAsync();
    }
}
