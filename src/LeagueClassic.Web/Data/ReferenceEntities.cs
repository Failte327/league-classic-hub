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

    // Classic lore, frozen at DDragon 5.1.1 — the last patch before Riot's ongoing
    // lore rewrites, so this is genuinely the old backstory rather than current canon.
    public string? Title { get; set; }          // e.g. "the Nine-Tailed Fox"
    public string? Blurb { get; set; }          // short teaser paragraph
    public string? Lore { get; set; }           // full backstory; paragraphs separated by "\n\n",
                                                 // a leading "> " paragraph is the closing flavor quote

    // Bullet points on how this champion's kit differs from the modern live game.
    // Sourced from Riot's own champ-select "Classic changes" tooltip text; one bullet
    // per "\n", no leading "- " (stripped at seed time). Null = not yet matched/seeded.
    public string? ClassicChanges { get; set; }

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
    public string? Description { get; set; }
}

public class Item
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Category { get; set; }
    public string? IconPath { get; set; }
    public string? Description { get; set; }   // classic stats/passive text (DDragon 5.1.1)
    public bool IsAvailable { get; set; } = true;   // false = no longer in the real Classic item pool
}

public class SummonerSpell
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? IconPath { get; set; }
    public string? Description { get; set; }
}

// A classic rune. DdragonId is the game's rune id (null for old guessed rows that
// predate League Classic's real PBE data and have no natural key to match on).
public class Rune
{
    public int Id { get; set; }
    public int? DdragonId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Slot { get; set; }   // mark / seal / glyph / quintessence
    public string? IconPath { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;   // false = no longer in the real Classic rune pool
}

// A classic mastery node in the Offense/Defense/Utility trees.
// DdragonId is the game's mastery id (used in guide allocations + prereqs).
public class Mastery
{
    public int Id { get; set; }
    public int DdragonId { get; set; }
    public required string Name { get; set; }
    public required string Tree { get; set; }    // Offense / Defense / Utility
    public int Row { get; set; }                 // 1-6
    public int Col { get; set; }                 // 0-3
    public int Ranks { get; set; }               // max points
    public int? PrereqDdragonId { get; set; }    // mastery that must be maxed first
    public string? IconPath { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;   // false = no longer in the real Classic mastery tree
}

// A rune choice on a guide, with a count (e.g. 9x Greater Mark of Attack Damage).
public class GuideRune
{
    public int Id { get; set; }
    public int GuideId { get; set; }
    public Guide? Guide { get; set; }

    public int RuneId { get; set; }
    public Rune? Rune { get; set; }

    public int Count { get; set; }
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
