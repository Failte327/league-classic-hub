using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Data;

// Applies migrations on startup, then seeds reference data (champions, items,
// summoner spells from Data/seed/*.json) and a starter set of forum boards.
public static class DbSeeder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        await db.Database.MigrateAsync();

        await SeedReferenceAsync(db, env.ContentRootPath);
        await SeedBoardsAsync(db);
    }

    private static async Task SeedReferenceAsync(ApplicationDbContext db, string contentRoot)
    {
        var seedDir = Path.Combine(contentRoot, "Data", "seed");

        if (!await db.Champions.AnyAsync())
        {
            foreach (var c in Load<ChampionSeed>(seedDir, "champions.json"))
            {
                db.Champions.Add(new Champion
                {
                    Name = c.Name, Slug = c.Slug, IconPath = c.Icon,
                    IsAvailable = c.Available,
                });
            }
        }

        // Abilities depend on champions existing (need their ids). Persist champions first.
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();

        if (!await db.ChampionAbilities.AnyAsync())
        {
            var champBySlug = await db.Champions.ToDictionaryAsync(c => c.Slug, c => c.Id);
            foreach (var a in Load<AbilitySeed>(seedDir, "abilities.json"))
            {
                if (champBySlug.TryGetValue(a.Champ, out var champId))
                {
                    db.ChampionAbilities.Add(new ChampionAbility
                    {
                        ChampionId = champId, Slot = a.Slot, Name = a.Name, IconPath = a.Icon,
                    });
                }
            }
        }

        if (!await db.Items.AnyAsync())
        {
            foreach (var i in Load<ItemSeed>(seedDir, "items.json"))
            {
                db.Items.Add(new Item
                {
                    Name = i.Name, Slug = i.Slug, Category = i.Category, IconPath = i.Icon,
                });
            }
        }

        if (!await db.SummonerSpells.AnyAsync())
        {
            foreach (var s in Load<SpellSeed>(seedDir, "spells.json"))
            {
                db.SummonerSpells.Add(new SummonerSpell
                {
                    Name = s.Name, Slug = s.Slug, IconPath = s.Icon,
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static List<T> Load<T>(string seedDir, string file)
    {
        var path = Path.Combine(seedDir, file);
        if (!File.Exists(path)) return new List<T>();
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<T>>(stream, JsonOpts) ?? new List<T>();
    }

    private const string WelcomePost =
        "Welcome, Summoners! I am so glad to have all of you here, in a haven where all builds, " +
        "all champions, and all playstyles are welcome. A place where Teemo Mains and AP Sion " +
        "nostalgists can share a drink and a story. A place where AP Master Yis can Meditate under " +
        "tower for hours. A place where we can rejoice in the return of our beloved Fields of Justice. " +
        "I only ask that you keep the discourse respectful and safe for those of all ages.\n\n" +
        "Thank you, and once again, welcome!";

    private static async Task SeedBoardsAsync(ApplicationDbContext db)
    {
        // General starter boards — one-time (skipped once any category exists).
        if (!await db.Categories.AnyAsync())
        {
            var general = new Category { Name = "The Rift", Slug = "the-rift", SortOrder = 1 };
            var strategy = new Category { Name = "Strategy & Guides", Slug = "strategy", SortOrder = 2 };

            general.Boards.Add(new Board { Name = "Announcements", Slug = "announcements", Description = "Site news and League Classic updates.", SortOrder = 1 });
            general.Boards.Add(new Board { Name = "General Discussion", Slug = "general", Description = "Everything League Classic — plays, patches, hot takes.", SortOrder = 2 });
            strategy.Boards.Add(new Board { Name = "Champion Discussion", Slug = "champions", Description = "Deep dives on individual champions.", SortOrder = 1 });
            strategy.Boards.Add(new Board { Name = "Build & Guide Workshop", Slug = "guide-workshop", Description = "Share and critique builds and written guides.", SortOrder = 2 });

            db.Categories.AddRange(general, strategy);
            await db.SaveChangesAsync();
        }

        // Welcome board + pinned welcome thread — idempotent (added even to an
        // existing database), so the default landing board is always present.
        if (!await db.Boards.AnyAsync(b => b.Slug == "welcome"))
        {
            var welcomeCat = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "welcome");
            if (welcomeCat is null)
            {
                welcomeCat = new Category { Name = "Welcome", Slug = "welcome", SortOrder = 0 };
                db.Categories.Add(welcomeCat);
                await db.SaveChangesAsync();
            }

            var now = DateTimeOffset.UtcNow;
            var board = new Board
            {
                CategoryId = welcomeCat.Id,
                Name = "Welcome & Rules",
                Slug = "welcome",
                Description = "New here? Start with a warm welcome and the community guidelines.",
                SortOrder = 1,
                ThreadCount = 1,
                PostCount = 1,
                LastPostAt = now,
            };
            var thread = new ForumThread
            {
                Title = "Welcome to LeagueClassicGuides.net!",
                Slug = "welcome-to-leagueclassicguides-net",
                IsPinned = true,
                CreatedAt = now,
                LastPostAt = now,
            };
            thread.Posts.Add(new Post { BodyMarkdown = WelcomePost, CreatedAt = now }); // null author = staff
            board.Threads.Add(thread);
            db.Boards.Add(board);
            await db.SaveChangesAsync();
        }
    }

    // Seed DTOs matching Data/seed/*.json
    private sealed record ChampionSeed(string Name, string Slug, string? Icon, bool Available);
    private sealed record ItemSeed(string Name, string Slug, string Category, string? Icon);
    private sealed record SpellSeed(string Name, string Slug, string? Icon);
    private sealed record AbilitySeed(string Champ, string Slot, string Name, string? Icon);
}
