using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Guides;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public CreateModel(ApplicationDbContext db) => _db = db;

    public List<Champion> Champions { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Champions = await _db.Champions
            .OrderBy(c => c.Slug == "generic" ? 0 : 1)
            .ThenBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync();
    }
}
