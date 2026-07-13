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

    // Runes grouped by slot (mark/seal/glyph/quintessence).
    public List<IGrouping<string, GuideRune>> RunesBySlot { get; private set; } = new();

    // Full mastery layout + this guide's point allocation (ddragonId -> points).
    public List<Mastery> Masteries { get; private set; } = new();
    public Dictionary<int, int> MasteryPoints { get; private set; } = new();
    public bool HasMasteries => MasteryPoints.Count > 0;
    public int TreeTotal(string tree) =>
        Masteries.Where(m => m.Tree == tree && MasteryPoints.ContainsKey(m.DdragonId))
                 .Sum(m => MasteryPoints[m.DdragonId]);

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var guide = await _db.Guides
            .Include(g => g.Champion).ThenInclude(c => c!.Abilities)
            .Include(g => g.Author)
            .Include(g => g.SpellOne)
            .Include(g => g.SpellTwo)
            .Include(g => g.BuildOrder.OrderBy(b => b.Sort)).ThenInclude(b => b.Item)
            .Include(g => g.Runes).ThenInclude(r => r.Rune)
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

        var slotOrder = new[] { "mark", "seal", "glyph", "quintessence" };
        RunesBySlot = guide.Runes
            .Where(r => r.Rune is not null)
            .GroupBy(r => r.Rune!.Slot)
            .OrderBy(g => Array.IndexOf(slotOrder, g.Key))
            .ToList();

        MasteryPoints = ParseMasteries(guide.MasteryAllocations);
        if (MasteryPoints.Count > 0)
            Masteries = await _db.Masteries.OrderBy(m => m.Row).ThenBy(m => m.Col).AsNoTracking().ToListAsync();

        return Page();
    }

    private static Dictionary<int, int> ParseMasteries(string? raw)
    {
        var result = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split(':', 2);
            if (bits.Length == 2 && int.TryParse(bits[0], out var id) && int.TryParse(bits[1], out var p) && p > 0)
                result[id] = p;
        }
        return result;
    }
}
