namespace LeagueClassic.Web.Data;

// ---- Forum ----------------------------------------------------------------

// Top-level grouping shown as a header on the index (e.g. "General", "Strategy").
public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public int SortOrder { get; set; }

    public List<Board> Boards { get; set; } = new();
}

// A forum board within a category (e.g. "Champion Discussion", "Off Topic").
public class Board
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    // Denormalized counters — updated on write so board lists never aggregate.
    public int ThreadCount { get; set; }
    public int PostCount { get; set; }
    public DateTimeOffset? LastPostAt { get; set; }

    public List<ForumThread> Threads { get; set; } = new();
}

// A thread of discussion. Named ForumThread to avoid clashing with System.Threading.Thread.
public class ForumThread
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public Board? Board { get; set; }

    public string? AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }

    public required string Title { get; set; }
    public required string Slug { get; set; }

    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }

    public int ViewCount { get; set; }
    public int ReplyCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastPostAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Post> Posts { get; set; } = new();
}

// A single post/reply. The first post of a thread is the opening message.
public class Post
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public ForumThread? Thread { get; set; }

    public string? AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }

    public required string BodyMarkdown { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EditedAt { get; set; }
}

// ---- Guides ---------------------------------------------------------------

public enum GuideStatus { Draft = 0, Published = 1, Archived = 2 }

// A markdown guide/build write-up (the MOBAFire angle). Voting/build tables
// come post-launch; the body is markdown for now.
public class Guide
{
    public int Id { get; set; }

    public string? AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }

    public required string Title { get; set; }
    public required string Slug { get; set; }

    // The champion this guide is about.
    public int ChampionId { get; set; }
    public Champion? Champion { get; set; }

    // The two summoner spells (nullable while drafting).
    public int? SpellOneId { get; set; }
    public SummonerSpell? SpellOne { get; set; }
    public int? SpellTwoId { get; set; }
    public SummonerSpell? SpellTwo { get; set; }

    // Per-level skill order as a compact comma-separated list of ability slots,
    // one entry per character level, e.g. "Q,W,E,Q,Q,R,Q,...". Slots: Q/W/E/R.
    public string? SkillOrder { get; set; }

    // Ordered item build (see GuideItem.Sort).
    public List<GuideItem> BuildOrder { get; set; } = new();

    // Rune choices with counts.
    public List<GuideRune> Runes { get; set; } = new();

    // Mastery point allocation as "ddragonId:points,..." e.g. "4112:4,4113:4".
    public string? MasteryAllocations { get; set; }

    public required string BodyMarkdown { get; set; }
    public GuideStatus Status { get; set; } = GuideStatus.Draft;

    public int ViewCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Comment> Comments { get; set; } = new();
}

// A comment on a guide (reuses markdown rendering).
public class Comment
{
    public int Id { get; set; }
    public int GuideId { get; set; }
    public Guide? Guide { get; set; }

    public string? AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }

    public required string BodyMarkdown { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
