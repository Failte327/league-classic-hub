using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Champions;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<Champion> Champions { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Champions = await _db.Champions
            .Where(c => c.IsAvailable && c.Slug != "generic")
            .OrderBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync();
    }
}
