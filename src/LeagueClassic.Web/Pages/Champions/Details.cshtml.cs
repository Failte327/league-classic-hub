using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Champions;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DetailsModel(ApplicationDbContext db) => _db = db;

    public Champion Champion { get; private set; } = null!;
    public Dictionary<string, ChampionAbility> AbilityBySlot { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var champion = await _db.Champions
            .Include(c => c.Abilities)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug && c.IsAvailable);

        if (champion is null) return NotFound();

        Champion = champion;
        AbilityBySlot = champion.Abilities.ToDictionary(a => a.Slot, a => a);
        return Page();
    }
}
