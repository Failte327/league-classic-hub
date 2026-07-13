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

    public string BodyMarkdown { get; set; } = "";
    public bool Publish { get; set; }
}
