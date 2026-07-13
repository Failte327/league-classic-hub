using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Guides;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DetailsModel(ApplicationDbContext db) => _db = db;

    public Guide Guide { get; private set; } = default!;

    // Parsed per-level skill order, e.g. ["Q","W","E",...] (may be empty).
    public IReadOnlyList<string> SkillLevels { get; private set; } = Array.Empty<string>();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var guide = await _db.Guides
            .Include(g => g.Champion)
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

        return Page();
    }
}
