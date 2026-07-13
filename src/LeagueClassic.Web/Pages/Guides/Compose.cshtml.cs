using System.Text.RegularExpressions;
using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Guides;

[Authorize]
public class ComposeModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ComposeModel(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public Champion Champion { get; private set; } = default!;
    public List<IGrouping<string, Item>> ItemsByCategory { get; private set; } = new();
    public List<SummonerSpell> Spells { get; private set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public string ChampionSlug { get; set; } = "";
        public string Title { get; set; } = "";
        public int? SpellOneId { get; set; }
        public int? SpellTwoId { get; set; }

        // "Q,W,E,Q,..." one slot per level (max 18).
        public string? SkillOrder { get; set; }

        // Ordered item ids, e.g. "12,4,4,88".
        public string? BuildOrderCsv { get; set; }

        public string BodyMarkdown { get; set; } = "";
        public bool Publish { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string champion)
    {
        var champ = await LoadChampionAsync(champion);
        if (champ is null || !champ.IsAvailable)
            return RedirectToPage("/Guides/Create");

        Champion = champ;
        Input.ChampionSlug = champ.Slug;
        await LoadPaletteAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var champ = await LoadChampionAsync(Input.ChampionSlug);
        if (champ is null || !champ.IsAvailable)
            return RedirectToPage("/Guides/Create");

        Champion = champ;
        await LoadPaletteAsync();

        if (string.IsNullOrWhiteSpace(Input.Title))
            ModelState.AddModelError("Input.Title", "Give your guide a title.");
        if (string.IsNullOrWhiteSpace(Input.BodyMarkdown))
            ModelState.AddModelError("Input.BodyMarkdown", "Write something in the guide body.");

        if (!ModelState.IsValid)
            return Page();

        var now = DateTimeOffset.UtcNow;
        var guide = new Guide
        {
            AuthorId = _users.GetUserId(User),
            ChampionId = champ.Id,
            Title = Input.Title.Trim(),
            Slug = await UniqueSlugAsync(champ.Slug, Input.Title),
            SpellOneId = ValidSpellId(Input.SpellOneId),
            SpellTwoId = ValidSpellId(Input.SpellTwoId),
            SkillOrder = NormalizeSkillOrder(Input.SkillOrder),
            BodyMarkdown = Input.BodyMarkdown,
            Status = Input.Publish ? GuideStatus.Published : GuideStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Build order — keep only ids that exist, preserve order & duplicates.
        var validItemIds = await _db.Items.Select(i => i.Id).ToListAsync();
        var idSet = validItemIds.ToHashSet();
        var sort = 0;
        foreach (var id in ParseCsvInts(Input.BuildOrderCsv))
        {
            if (idSet.Contains(id))
                guide.BuildOrder.Add(new GuideItem { ItemId = id, Sort = sort++ });
        }

        _db.Guides.Add(guide);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Guides/Details", new { slug = guide.Slug });
    }

    private async Task<Champion?> LoadChampionAsync(string? slug) =>
        string.IsNullOrEmpty(slug)
            ? null
            : await _db.Champions.FirstOrDefaultAsync(c => c.Slug == slug);

    private async Task LoadPaletteAsync()
    {
        var items = await _db.Items.OrderBy(i => i.Category).ThenBy(i => i.Name).AsNoTracking().ToListAsync();
        ItemsByCategory = items.GroupBy(i => i.Category).ToList();
        Spells = await _db.SummonerSpells.OrderBy(s => s.Name).AsNoTracking().ToListAsync();
    }

    private int? ValidSpellId(int? id) =>
        id.HasValue && Spells.Any(s => s.Id == id.Value) ? id : null;

    private static IEnumerable<int> ParseCsvInts(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                   .Where(n => n.HasValue).Select(n => n!.Value);

    private static string? NormalizeSkillOrder(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Keep per-level positions: empty/invalid slots become "-".
        var slots = raw.Split(',', StringSplitOptions.TrimEntries)
                       .Select(s => s.ToUpperInvariant())
                       .Select(s => s is "Q" or "W" or "E" or "R" ? s : "-")
                       .Take(18)
                       .ToList();
        // Trim trailing empties.
        while (slots.Count > 0 && slots[^1] == "-") slots.RemoveAt(slots.Count - 1);
        return slots.Count == 0 ? null : string.Join(',', slots);
    }

    private async Task<string> UniqueSlugAsync(string champSlug, string title)
    {
        var baseSlug = $"{champSlug}-{Slugify(title)}".Trim('-');
        if (baseSlug.Length > 120) baseSlug = baseSlug[..120].Trim('-');
        var slug = baseSlug;
        var n = 2;
        while (await _db.Guides.AnyAsync(g => g.Slug == slug))
            slug = $"{baseSlug}-{n++}";
        return slug;
    }

    private static string Slugify(string s)
    {
        s = s.ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", "-");
        return s.Trim('-');
    }
}
