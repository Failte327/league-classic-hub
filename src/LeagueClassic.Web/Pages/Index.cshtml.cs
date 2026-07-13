using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public List<Category> Categories { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Categories = await _db.Categories
            .OrderBy(c => c.SortOrder)
            .Include(c => c.Boards.OrderBy(b => b.SortOrder))
            .AsNoTracking()
            .ToListAsync();
    }
}
