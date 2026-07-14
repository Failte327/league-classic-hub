using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Resources;

public class MasteriesModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public MasteriesModel(ApplicationDbContext db) => _db = db;

    public List<Mastery> Masteries { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Masteries = await _db.Masteries.OrderBy(m => m.Row).ThenBy(m => m.Col).AsNoTracking().ToListAsync();
    }
}
