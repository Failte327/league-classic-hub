using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Guides;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public DetailsModel(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public Guide Guide { get; private set; } = default!;
    public bool CanEdit { get; private set; }

    // Parsed per-level skill order, e.g. ["Q","W","E",...] (may be empty).
    public IReadOnlyList<string> SkillLevels { get; private set; } = Array.Empty<string>();

    // Slot ("P"/"Q"/"W"/"E"/"R") -> ability.
    public Dictionary<string, ChampionAbility> AbilityBySlot { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var guide = await _db.Guides
            .Include(g => g.Champion).ThenInclude(c => c!.Abilities)
            .Include(g => g.Author)
            .Include(g => g.SpellOne)
            .Include(g => g.SpellTwo)
            .Include(g => g.BuildOrder.OrderBy(b => b.Sort)).ThenInclude(b => b.Item)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Slug == slug);

        if (guide is null)
            return NotFound();

        Guide = guide;
        SkillLevels = string.IsNullOrEmpty(guide.SkillOrder)
            ? Array.Empty<string>()
            : guide.SkillOrder.Split(',');
        AbilityBySlot = guide.Champion?.Abilities.ToDictionary(a => a.Slot) ?? new();
        CanEdit = User.Identity?.IsAuthenticated == true && guide.AuthorId == _users.GetUserId(User);

        return Page();
    }
}
