namespace LeagueClassic.Web.Pages.Guides;

// Shared bound input for the guide editor (create + edit).
public class GuideInput
{
    public string ChampionSlug { get; set; } = "";
    public string Title { get; set; } = "";
    public int? SpellOneId { get; set; }
    public int? SpellTwoId { get; set; }

    // "Q,W,E,Q,..." one slot per level (max 18).
    public string? SkillOrder { get; set; }

    // Ordered item ids, e.g. "12,4,4,88".
    public string? BuildOrderCsv { get; set; }

    // Rune choices as "runeId:count,...", e.g. "12:9,40:9,55:9,3:3".
    public string? RunesCsv { get; set; }

    // Mastery points as "ddragonId:points,...", e.g. "4112:4,4113:4".
    public string? MasteryAllocations { get; set; }

    public string BodyMarkdown { get; set; } = "";
    public bool Publish { get; set; }
}
