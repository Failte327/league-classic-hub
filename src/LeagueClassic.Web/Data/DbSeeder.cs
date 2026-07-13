using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
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
        await SeedWelcomeThreadAsync(db);
        await SeedModeratorsAsync(scope.ServiceProvider, env);
    }

    public const string ModeratorRole = "Moderator";

    // Ensures the Moderator role exists, promotes any configured moderator
    // emails, and (in Development only) makes the founder — the earliest user —
    // a moderator so the tooling is testable out of the box.
    private static async Task SeedModeratorsAsync(IServiceProvider sp, IWebHostEnvironment env)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var config = sp.GetRequiredService<IConfiguration>();

        if (!await roles.RoleExistsAsync(ModeratorRole))
            await roles.CreateAsync(new IdentityRole(ModeratorRole));

        foreach (var email in config.GetSection("Moderation:ModeratorEmails").Get<string[]>() ?? Array.Empty<string>())
        {
            var user = await users.FindByEmailAsync(email);
            if (user is not null && !await users.IsInRoleAsync(user, ModeratorRole))
                await users.AddToRoleAsync(user, ModeratorRole);
        }

        if (env.IsDevelopment() && !(await users.GetUsersInRoleAsync(ModeratorRole)).Any())
        {
            var founder = users.Users.OrderBy(u => u.CreatedAt).FirstOrDefault();
            if (founder is not null)
                await users.AddToRoleAsync(founder, ModeratorRole);
        }
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
                    Description = i.Desc,
                });
            }
        }

        if (!await db.SummonerSpells.AnyAsync())
        {
            foreach (var s in Load<SpellSeed>(seedDir, "spells.json"))
            {
                db.SummonerSpells.Add(new SummonerSpell
                {
                    Name = s.Name, Slug = s.Slug, IconPath = s.Icon, Description = s.Desc,
                });
            }
        }

        if (!await db.Runes.AnyAsync())
        {
            foreach (var r in Load<RuneSeed>(seedDir, "runes.json"))
            {
                db.Runes.Add(new Rune
                {
                    Name = r.Name, Slug = r.Slug, Slot = r.Slot,
                    IconPath = r.Icon, Description = r.Desc,
                });
            }
        }

        if (!await db.Masteries.AnyAsync())
        {
            foreach (var m in Load<MasterySeed>(seedDir, "masteries.json"))
            {
                db.Masteries.Add(new Mastery
                {
                    DdragonId = m.Id, Name = m.Name, Tree = m.Tree, Row = m.Row, Col = m.Col,
                    Ranks = m.Ranks, PrereqDdragonId = m.Prereq, IconPath = m.Icon, Description = m.Desc,
                });
            }
        }

        await db.SaveChangesAsync();

        await BackfillDescriptionsAsync(db, seedDir);
    }

    // Fills Description onto item/spell rows that were seeded before the column
    // existed. Non-destructive: only touches rows where Description is null.
    private static async Task BackfillDescriptionsAsync(ApplicationDbContext db, string seedDir)
    {
        var itemDesc = Load<ItemSeed>(seedDir, "items.json")
            .Where(i => i.Desc != null).ToDictionary(i => i.Slug, i => i.Desc!);
        var spellDesc = Load<SpellSeed>(seedDir, "spells.json")
            .Where(s => s.Desc != null).ToDictionary(s => s.Slug, s => s.Desc!);

        var changed = false;
        foreach (var item in await db.Items.Where(i => i.Description == null).ToListAsync())
            if (itemDesc.TryGetValue(item.Slug, out var d)) { item.Description = d; changed = true; }
        foreach (var spell in await db.SummonerSpells.Where(s => s.Description == null).ToListAsync())
            if (spellDesc.TryGetValue(spell.Slug, out var d)) { spell.Description = d; changed = true; }

        if (changed) await db.SaveChangesAsync();
    }

    private static List<T> Load<T>(string seedDir, string file)
    {
        var path = Path.Combine(seedDir, file);
        if (!File.Exists(path)) return new List<T>();
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<T>>(stream, JsonOpts) ?? new List<T>();
    }

    private const string WelcomePost =
        "Welcome, Summoners! I am so glad to have all of you here -- League Classic Hub is a haven where all builds, " +
        "all champions, and all playstyles are welcome. A place where Teemo Mains and AP Sion " +
        "nostalgists can share a drink and a story. A place where AP Master Yis can Meditate under " +
        "tower for hours. A place where we can rejoice in the return of our beloved Fields of Justice. " +
        "I only ask that you keep the discourse respectful and safe for those of all ages.\n\n" +
        "Thank you, and once again, welcome!";

    private static async Task SeedWelcomeThreadAsync(ApplicationDbContext db)
    {
        // Pinned welcome thread — idempotent, so the flat forum always has it.
        const string slug = "welcome-to-league-classic-hub";
        if (await db.Threads.AnyAsync(t => t.Slug == slug))
            return;

        var now = DateTimeOffset.UtcNow;
        var thread = new ForumThread
        {
            Title = "Welcome to League Classic Hub!",
            Slug = slug,
            Excerpt = Excerpt(WelcomePost),
            IsPinned = true,
            ReplyCount = 0,
            CreatedAt = now,
            LastPostAt = now,
        };
        thread.Posts.Add(new Post { BodyMarkdown = WelcomePost, CreatedAt = now }); // null author = staff
        db.Threads.Add(thread);
        await db.SaveChangesAsync();
    }

    // Plain-text peek at a post body for the thread list.
    public static string Excerpt(string markdown, int max = 160)
    {
        var text = Regex.Replace(markdown, @"[#*_`>\[\]]", "");         // strip common markdown
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }

    // Seed DTOs matching Data/seed/*.json
    private sealed record ChampionSeed(string Name, string Slug, string? Icon, bool Available);
    private sealed record ItemSeed(string Name, string Slug, string Category, string? Icon, string? Desc);
    private sealed record SpellSeed(string Name, string Slug, string? Icon, string? Desc);
    private sealed record AbilitySeed(string Champ, string Slot, string Name, string? Icon);
    private sealed record RuneSeed(string Name, string Slug, string Slot, string? Icon, string? Desc);
    private sealed record MasterySeed(int Id, string Name, string Tree, int Row, int Col, int Ranks, int? Prereq, string? Icon, string? Desc);
}
