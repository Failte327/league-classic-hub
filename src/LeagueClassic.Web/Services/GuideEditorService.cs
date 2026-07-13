using System.Text.Json;
using System.Text.RegularExpressions;
using LeagueClassic.Web.Data;
using LeagueClassic.Web.Pages.Guides;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Services;

// Shared logic for the guide create + edit flows: loading the editor palette,
// building the view model, and applying posted input onto a Guide.
public class GuideEditorService
{
    private readonly ApplicationDbContext _db;

    public GuideEditorService(ApplicationDbContext db) => _db = db;

    public Task<Champion?> LoadChampionAsync(string? slug) =>
        string.IsNullOrEmpty(slug)
            ? Task.FromResult<Champion?>(null)
            : _db.Champions.Include(c => c.Abilities).FirstOrDefaultAsync(c => c.Slug == slug);

    public async Task<GuideEditorVm> BuildVmAsync(
        Champion champion, GuideInput input, bool isEdit, string heading, string initialStateJson = "null")
    {
        var items = await _db.Items.OrderBy(i => i.Category).ThenBy(i => i.Name).AsNoTracking().ToListAsync();
        var spells = await _db.SummonerSpells.OrderBy(s => s.Name).AsNoTracking().ToListAsync();
        return new GuideEditorVm
        {
            Champion = champion,
            ItemsByCategory = items.GroupBy(i => i.Category).ToList(),
            Spells = spells,
            AbilityBySlot = champion.Abilities.ToDictionary(a => a.Slot),
            Input = input,
            IsEdit = isEdit,
            Heading = heading,
            InitialStateJson = initialStateJson,
        };
    }

    // Applies posted input onto a guide (used by both create and edit). Rebuilds
    // the build order; validates spell ids; normalizes skill order.
    public async Task ApplyAsync(Guide guide, GuideInput input, Champion champ)
    {
        var spellIds = await _db.SummonerSpells.Select(s => s.Id).ToListAsync();
        var itemIds = (await _db.Items.Select(i => i.Id).ToListAsync()).ToHashSet();

        guide.ChampionId = champ.Id;
        guide.Title = input.Title.Trim();
        guide.SpellOneId = spellIds.Contains(input.SpellOneId ?? -1) ? input.SpellOneId : null;
        guide.SpellTwoId = spellIds.Contains(input.SpellTwoId ?? -1) ? input.SpellTwoId : null;
        guide.SkillOrder = NormalizeSkillOrder(input.SkillOrder);
        guide.BodyMarkdown = input.BodyMarkdown;
        guide.Status = input.Publish ? GuideStatus.Published : GuideStatus.Draft;

        guide.BuildOrder.Clear();
        var sort = 0;
        foreach (var id in ParseCsvInts(input.BuildOrderCsv))
            if (itemIds.Contains(id))
                guide.BuildOrder.Add(new GuideItem { ItemId = id, Sort = sort++ });
    }

    // Serializes an existing guide's selections for the JS widgets to pre-populate.
    public string InitialStateJson(Guide guide) =>
        JsonSerializer.Serialize(new
        {
            spells = new[] { guide.SpellOneId, guide.SpellTwoId }.Where(x => x.HasValue).Select(x => x!.Value),
            skill = guide.SkillOrder ?? "",
            build = guide.BuildOrder
                .OrderBy(b => b.Sort)
                .Select(b => new { id = b.ItemId, name = b.Item?.Name, icon = b.Item?.IconPath }),
        });

    public async Task<string> UniqueSlugAsync(string champSlug, string title, int? excludeGuideId = null)
    {
        var baseSlug = $"{champSlug}-{Slugify(title)}".Trim('-');
        if (baseSlug.Length > 120) baseSlug = baseSlug[..120].Trim('-');
        if (baseSlug.Length == 0) baseSlug = champSlug;
        var slug = baseSlug;
        var n = 2;
        while (await _db.Guides.AnyAsync(g => g.Slug == slug && g.Id != (excludeGuideId ?? 0)))
            slug = $"{baseSlug}-{n++}";
        return slug;
    }

    private static IEnumerable<int> ParseCsvInts(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                   .Where(n => n.HasValue).Select(n => n!.Value);

    private static string? NormalizeSkillOrder(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var slots = raw.Split(',', StringSplitOptions.TrimEntries)
                       .Select(s => s.ToUpperInvariant())
                       .Select(s => s is "Q" or "W" or "E" or "R" ? s : "-")
                       .Take(18)
                       .ToList();
        while (slots.Count > 0 && slots[^1] == "-") slots.RemoveAt(slots.Count - 1);
        return slots.Count == 0 ? null : string.Join(',', slots);
    }

    private static string Slugify(string s) =>
        Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
}
