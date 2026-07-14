using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Reference;

public class RunesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private static readonly string[] SlotOrder = { "mark", "seal", "glyph", "quintessence" };

    public RunesModel(ApplicationDbContext db) => _db = db;

    public List<IGrouping<string, Rune>> BySlot { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var runes = await _db.Runes.OrderBy(r => r.Name).AsNoTracking().ToListAsync();
        BySlot = runes.GroupBy(r => r.Slot)
            .OrderBy(g => Array.IndexOf(SlotOrder, g.Key))
            .ToList();
    }
}
