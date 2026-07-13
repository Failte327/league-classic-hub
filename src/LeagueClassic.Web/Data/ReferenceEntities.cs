namespace LeagueClassic.Web.Data;

// Reference data seeded from Data/seed/*.json (sourced from the League Classic wiki datamine).
// These are the fixed sets a guide can reference.

public class Champion
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? IconPath { get; set; }      // e.g. "assets/champions/ahri.png"
    public bool IsAvailable { get; set; }       // false = confirmed-but-not-yet-playable

    public List<ChampionAbility> Abilities { get; set; } = new();
}

// One ability of a champion. Slot: "P" (passive) or "Q"/"W"/"E"/"R".
// Sourced from Data Dragon patch 5.1.1 — the oldest classic-era data available.
public class ChampionAbility
{
    public int Id { get; set; }
    public int ChampionId { get; set; }
    public Champion? Champion { get; set; }

    public required string Slot { get; set; }
    public required string Name { get; set; }
    public string? IconPath { get; set; }
}

public class Item
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Category { get; set; }
    public string? IconPath { get; set; }
    public string? Description { get; set; }   // classic stats/passive text (DDragon 5.1.1)
}

public class SummonerSpell
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? IconPath { get; set; }
    public string? Description { get; set; }
}

// Ordered build-order entry linking a Guide to an Item.
public class GuideItem
{
    public int Id { get; set; }
    public int GuideId { get; set; }
    public Guide? Guide { get; set; }

    public int ItemId { get; set; }
    public Item? Item { get; set; }

    public int Sort { get; set; }   // 0-based position in the build order
}
