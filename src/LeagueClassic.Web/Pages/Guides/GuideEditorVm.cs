using LeagueClassic.Web.Data;

namespace LeagueClassic.Web.Pages.Guides;

// Everything the shared _EditorForm partial needs to render, for both the
// create (Compose) and edit (Edit) flows.
public class GuideEditorVm
{
    public required Champion Champion { get; init; }
    public required List<IGrouping<string, Item>> ItemsByCategory { get; init; }
    public required List<SummonerSpell> Spells { get; init; }
    public required Dictionary<string, ChampionAbility> AbilityBySlot { get; init; }
    public required GuideInput Input { get; init; }

    public bool IsEdit { get; init; }
    public string Heading { get; init; } = "New Guide";

    // Pre-populated selections for the JS widgets (null/empty on create):
    // { "spells": [id,id], "skill": "Q,W,...", "build": [{id,name,icon}, ...] }
    public string InitialStateJson { get; init; } = "null";
}
