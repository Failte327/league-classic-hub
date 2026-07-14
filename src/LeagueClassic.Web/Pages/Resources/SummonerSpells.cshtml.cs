using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Reference;

public class SummonerSpellsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public SummonerSpellsModel(ApplicationDbContext db) => _db = db;

    public List<SummonerSpell> Spells { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Spells = await _db.SummonerSpells.OrderBy(s => s.Name).AsNoTracking().ToListAsync();
    }
}
