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
        await SeedStaffGuidesAsync(db);
        await BackfillDisplayNamesAsync(db);
        await SeedModeratorsAsync(scope.ServiceProvider, env);
    }

    // Gives pre-existing accounts a DisplayName (derived from the email local
    // part, de-duplicated) so nobody shows as the generic fallback.
    private static async Task BackfillDisplayNamesAsync(ApplicationDbContext db)
    {
        var users = await db.Users.Where(u => u.DisplayName == null).ToListAsync();
        if (users.Count == 0) return;

        var taken = (await db.Users.Where(u => u.DisplayName != null).Select(u => u.DisplayName!).ToListAsync())
            .Select(n => n.ToLowerInvariant()).ToHashSet();
        foreach (var u in users)
        {
            var baseName = Regex.Replace((u.Email ?? "summoner").Split('@')[0], @"[^A-Za-z0-9_.\-]", "");
            if (baseName.Length < 3) baseName = "summoner";
            if (baseName.Length > 20) baseName = baseName[..20];
            var name = baseName;
            var n = 2;
            while (taken.Contains(name.ToLowerInvariant())) name = $"{baseName}{n++}";
            u.DisplayName = name;
            taken.Add(name.ToLowerInvariant());
        }
        await db.SaveChangesAsync();
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
                    IsAvailable = c.Available, Title = c.Title, Blurb = c.Blurb, Lore = c.Lore,
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
                        ChampionId = champId, Slot = a.Slot, Name = a.Name, IconPath = a.Icon, Description = a.Desc,
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
                    DdragonId = r.DdragonId, Name = r.Name, Slug = r.Slug, Slot = r.Slot,
                    IconPath = r.Icon, Description = r.Desc, IsAvailable = true,
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
                    IsAvailable = true,
                });
            }
        }

        await db.SaveChangesAsync();

        await BackfillDescriptionsAsync(db, seedDir);
        await RenameLegacyGenericChampionAsync(db);
        await BackfillNewChampionsAsync(db, seedDir);
        await BackfillItemsAsync(db, seedDir);
        await BackfillChampionLoreAsync(db, seedDir);
        await BackfillAbilityNamesAsync(db, seedDir);
        await BackfillRunesAsync(db, seedDir);
        await BackfillMasteriesAsync(db, seedDir);
        await BackfillMasteryAllocationsAsync(db);
        await BackfillGuideRunesAsync(db);
    }

    // A handful of items were purely renamed when the real League Classic item pool
    // replaced the old guessed catalog (e.g. "BF Sword" -> "B. F. Sword"). Maps the
    // old guessed slug to the corrected real one so the row gets renamed in place
    // instead of the old slug being orphaned alongside a duplicate new row.
    private static readonly Dictionary<string, string> ItemSlugAliases = new()
    {
        ["bf-sword"] = "b-f-sword",
        ["bloodthirster"] = "the-bloodthirster",
        ["kages-lucky-pick"] = "lucky-pick",
    };

    // Reconciles the Items table against items.json: updates rows that match a real
    // item (by slug, or by the alias table above) in place, inserts real items with
    // no existing row, and marks any row no longer present in the real pool as
    // IsAvailable = false rather than deleting it — GuideItem has a cascading FK, so
    // deleting a row would silently truncate an existing guide's build order.
    private static async Task BackfillItemsAsync(ApplicationDbContext db, string seedDir)
    {
        var seedItems = Load<ItemSeed>(seedDir, "items.json");
        var seedBySlug = seedItems.ToDictionary(i => i.Slug);
        var oldSlugBySlug = ItemSlugAliases.ToDictionary(kv => kv.Value, kv => kv.Key);
        var existingBySlug = (await db.Items.ToListAsync()).ToDictionary(i => i.Slug);
        var changed = false;

        foreach (var i in seedItems)
        {
            Item? row = null;
            if (!existingBySlug.TryGetValue(i.Slug, out row) && oldSlugBySlug.TryGetValue(i.Slug, out var oldSlug))
                existingBySlug.TryGetValue(oldSlug, out row);

            if (row is null)
            {
                row = new Item
                {
                    Name = i.Name, Slug = i.Slug, Category = i.Category, IconPath = i.Icon,
                    Description = i.Desc, IsAvailable = true,
                };
                db.Items.Add(row);
                existingBySlug[i.Slug] = row;
                changed = true;
            }
            else if (row.Name != i.Name || row.Slug != i.Slug || row.Category != i.Category ||
                     row.Description != i.Desc || row.IconPath != i.Icon || !row.IsAvailable)
            {
                row.Name = i.Name; row.Slug = i.Slug; row.Category = i.Category;
                row.Description = i.Desc; row.IconPath = i.Icon; row.IsAvailable = true;
                changed = true;
            }
        }

        foreach (var row in existingBySlug.Values)
        {
            if (row.IsAvailable && !seedBySlug.ContainsKey(row.Slug))
            {
                row.IsAvailable = false;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    // One-time rename: the champion-agnostic pseudo-champion went through a couple of
    // names ("Any"/slug "any", then "General"/slug "general") before settling on
    // "Generic"/slug "generic". Renaming the existing row in place (rather than
    // delete+reinsert) keeps any guides already created under it correctly attached
    // via ChampionId, regardless of which of the earlier names an install still has.
    private static async Task RenameLegacyGenericChampionAsync(ApplicationDbContext db)
    {
        var legacy = await db.Champions.FirstOrDefaultAsync(c => c.Slug == "any" || c.Slug == "general");
        if (legacy is null) return;

        legacy.Name = "Generic";
        legacy.Slug = "generic";
        legacy.IconPath = "assets/champions/generic.png";
        await db.SaveChangesAsync();
    }

    // Inserts any champions.json entries added after the initial seed (e.g. the
    // "Generic" pseudo-champion for champion-agnostic guides) into an already-seeded
    // database. The champions table only bulk-seeds once, when empty, so a new
    // entry needs an explicit add-if-missing pass like this to reach existing installs.
    private static async Task BackfillNewChampionsAsync(ApplicationDbContext db, string seedDir)
    {
        var existingSlugs = (await db.Champions.Select(c => c.Slug).ToListAsync()).ToHashSet();
        var missing = Load<ChampionSeed>(seedDir, "champions.json")
            .Where(c => !existingSlugs.Contains(c.Slug)).ToList();
        if (missing.Count == 0) return;

        foreach (var c in missing)
        {
            db.Champions.Add(new Champion
            {
                Name = c.Name, Slug = c.Slug, IconPath = c.Icon,
                IsAvailable = c.Available, Title = c.Title, Blurb = c.Blurb, Lore = c.Lore,
            });
        }
        await db.SaveChangesAsync();
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

    // Fills Blurb/Lore onto champion rows that were seeded before those columns
    // existed. Non-destructive: only touches rows where Lore is null.
    private static async Task BackfillChampionLoreAsync(ApplicationDbContext db, string seedDir)
    {
        var bySlug = Load<ChampionSeed>(seedDir, "champions.json").ToDictionary(c => c.Slug, c => c);

        var changed = false;
        foreach (var champ in await db.Champions.Where(c => c.Lore == null).ToListAsync())
        {
            if (bySlug.TryGetValue(champ.Slug, out var c) && c.Lore != null)
            {
                champ.Lore = c.Lore;
                champ.Blurb = c.Blurb;
                champ.Title = c.Title;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    // Re-syncs ability Name against abilities.json by (champion slug, slot), for
    // rows seeded before a name correction (e.g. reverting a post-classic-era
    // rework's ability names back to their Season 1-3 originals). IconPath is
    // not touched here since the underlying PNG at the existing path is what
    // gets swapped when an icon is corrected. Also backfills Description onto
    // rows seeded before that column existed (only where currently null).
    private static async Task BackfillAbilityNamesAsync(ApplicationDbContext db, string seedDir)
    {
        var seedByKey = Load<AbilitySeed>(seedDir, "abilities.json")
            .ToDictionary(a => (a.Champ, a.Slot), a => a);
        var slugByChampId = await db.Champions.ToDictionaryAsync(c => c.Id, c => c.Slug);

        var changed = false;
        foreach (var ability in await db.ChampionAbilities.ToListAsync())
        {
            if (!slugByChampId.TryGetValue(ability.ChampionId, out var slug)) continue;
            if (!seedByKey.TryGetValue((slug, ability.Slot), out var seed)) continue;

            if (ability.Name != seed.Name)
            {
                ability.Name = seed.Name;
                changed = true;
            }
            if (ability.Description == null && seed.Desc != null)
            {
                ability.Description = seed.Desc;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    // Reconciles the Runes table against runes.json (matched by DdragonId, which only
    // the real League Classic rune set has — old guessed rows have none): inserts any
    // real rune missing a row, and re-marks a real rune's row IsAvailable if it drifted
    // false. Old guessed rows are never touched here; they're left as permanently
    // IsAvailable = false since none of them correspond to a real rune 1:1 (see
    // BackfillGuideRunesAsync for the best-effort GuideRune migration).
    private static async Task BackfillRunesAsync(ApplicationDbContext db, string seedDir)
    {
        var seedRunes = Load<RuneSeed>(seedDir, "runes.json");
        var existingByDdragonId = (await db.Runes.Where(r => r.DdragonId != null).ToListAsync())
            .ToDictionary(r => r.DdragonId!.Value);
        var changed = false;

        foreach (var r in seedRunes)
        {
            if (existingByDdragonId.TryGetValue(r.DdragonId, out var row))
            {
                if (row.Name != r.Name || row.Slug != r.Slug || row.Slot != r.Slot ||
                    row.Description != r.Desc || row.IconPath != r.Icon || !row.IsAvailable)
                {
                    row.Name = r.Name; row.Slug = r.Slug; row.Slot = r.Slot;
                    row.Description = r.Desc; row.IconPath = r.Icon; row.IsAvailable = true;
                    changed = true;
                }
            }
            else
            {
                db.Runes.Add(new Rune
                {
                    DdragonId = r.DdragonId, Name = r.Name, Slug = r.Slug, Slot = r.Slot,
                    IconPath = r.Icon, Description = r.Desc, IsAvailable = true,
                });
                changed = true;
            }
        }

        foreach (var row in await db.Runes.Where(r => r.DdragonId == null && r.IsAvailable).ToListAsync())
        {
            row.IsAvailable = false;
            changed = true;
        }

        if (changed) await db.SaveChangesAsync();
    }

    // Reconciles the Masteries table against masteries.json (matched by DdragonId —
    // old-guess ids are 4-digit 4xxx, real ids are 3-digit 5xx/6xx/7xx, so there's no
    // collision risk). Same non-destructive shape as BackfillRunesAsync/BackfillItemsAsync.
    private static async Task BackfillMasteriesAsync(ApplicationDbContext db, string seedDir)
    {
        var seedMasteries = Load<MasterySeed>(seedDir, "masteries.json");
        var seedIds = seedMasteries.Select(m => m.Id).ToHashSet();
        var existingByDdragonId = (await db.Masteries.ToListAsync()).ToDictionary(m => m.DdragonId);
        var changed = false;

        foreach (var m in seedMasteries)
        {
            if (existingByDdragonId.TryGetValue(m.Id, out var row))
            {
                if (row.Name != m.Name || row.Tree != m.Tree || row.Row != m.Row || row.Col != m.Col ||
                    row.Ranks != m.Ranks || row.PrereqDdragonId != m.Prereq ||
                    row.Description != m.Desc || row.IconPath != m.Icon || !row.IsAvailable)
                {
                    row.Name = m.Name; row.Tree = m.Tree; row.Row = m.Row; row.Col = m.Col;
                    row.Ranks = m.Ranks; row.PrereqDdragonId = m.Prereq;
                    row.Description = m.Desc; row.IconPath = m.Icon; row.IsAvailable = true;
                    changed = true;
                }
            }
            else
            {
                db.Masteries.Add(new Mastery
                {
                    DdragonId = m.Id, Name = m.Name, Tree = m.Tree, Row = m.Row, Col = m.Col,
                    Ranks = m.Ranks, PrereqDdragonId = m.Prereq, IconPath = m.Icon, Description = m.Desc,
                    IsAvailable = true,
                });
                changed = true;
            }
        }

        foreach (var row in existingByDdragonId.Values)
        {
            if (row.IsAvailable && !seedIds.Contains(row.DdragonId))
            {
                row.IsAvailable = false;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    // Old (pre-real-data) mastery id -> real League Classic mastery id, hand-built by
    // matching effect/name (grid position alone isn't reliable — about a third of
    // cells hold an unrelated mastery across the two trees). Ids with no real
    // equivalent are intentionally absent; a guide's points there are dropped rather
    // than guessed at. See docs in the PR/plan for the full reasoning per tree.
    private static readonly Dictionary<int, int> MasteryIdMap = new()
    {
        // Offense
        [4112] = 512, [4113] = 513, [4114] = 514, [4122] = 542, [4132] = 542,
        [4123] = 543, [4133] = 543, [4134] = 562, [4143] = 553, [4151] = 551,
        [4152] = 552, [4154] = 544, [4162] = 531,
        // Defense
        [4211] = 641, [4213] = 652, [4214] = 614, [4221] = 631, [4222] = 633,
        [4224] = 624, [4232] = 643, [4233] = 621, [4234] = 622, [4241] = 612,
        [4242] = 632, [4243] = 654, [4252] = 651, [4262] = 642,
        // Utility
        [4311] = 714, [4312] = 712, [4313] = 713, [4314] = 721, [4322] = 722,
        [4323] = 743, [4331] = 731, [4332] = 732, [4333] = 733, [4334] = 734,
        [4342] = 741, [4343] = 723, [4344] = 742, [4352] = 751, [4353] = 752,
        [4362] = 762,
    };

    // Rewrites every Guide's MasteryAllocations ("ddragonId:points,...") from the old
    // guessed mastery ids to the real ones via MasteryIdMap. Merges (two old ids
    // mapping to the same real id) sum their points; everything gets clamped to the
    // real mastery's rank cap and to a 30-point total, same rule as
    // GuideEditorService.NormalizeMasteriesAsync. Idempotent: once a guide's string is
    // fully on real ids, nothing in it matches a MasteryIdMap key or falls outside the
    // current mastery set, so the rewrite is a no-op.
    private static async Task BackfillMasteryAllocationsAsync(ApplicationDbContext db)
    {
        var current = await db.Masteries.Where(m => m.IsAvailable).ToListAsync();
        var currentIds = current.Select(m => m.DdragonId).ToHashSet();
        var ranksById = current.ToDictionary(m => m.DdragonId, m => m.Ranks);

        var guides = await db.Guides.Where(g => g.MasteryAllocations != null).ToListAsync();
        var changed = false;

        foreach (var guide in guides)
        {
            var merged = new Dictionary<int, int>();
            var touchedOldId = false;

            foreach (var part in guide.MasteryAllocations!.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = part.Split(':', 2);
                if (bits.Length != 2 || !int.TryParse(bits[0], out var id) || !int.TryParse(bits[1], out var pts))
                    continue;

                if (MasteryIdMap.TryGetValue(id, out var mapped))
                {
                    merged[mapped] = merged.GetValueOrDefault(mapped) + pts;
                    touchedOldId = true;
                }
                else if (currentIds.Contains(id))
                {
                    merged[id] = merged.GetValueOrDefault(id) + pts;
                }
                else
                {
                    touchedOldId = true; // an old id with no real equivalent — dropped
                }
            }

            if (!touchedOldId) continue;

            var kept = new List<string>();
            var total = 0;
            foreach (var (id, pts) in merged.OrderBy(kv => kv.Key))
            {
                if (!ranksById.TryGetValue(id, out var max)) continue;
                var p = Math.Min(pts, max);
                if (total + p > 30) p = 30 - total;
                if (p <= 0) continue;
                total += p;
                kept.Add($"{id}:{p}");
            }

            var newAllocations = kept.Count == 0 ? null : string.Join(',', kept);
            if (newAllocations != guide.MasteryAllocations)
            {
                guide.MasteryAllocations = newAllocations;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    // For each GuideRune still pointing at an old guessed Rune row, repoints it at the
    // real rune with the same (slot, stat) — e.g. "Greater Mark of Armor" ->
    // "Mark of Armor" — if one exists. About 60 of the 103 old rune types never had a
    // real equivalent (they were artifacts of the old guess dumping every historical
    // rune regardless of whether that stat ever actually existed on that slot), so
    // those picks are left pointing at their (IsAvailable = false) old row, which still
    // renders correctly. Idempotent: once RuneId points at a real (DdragonId != null)
    // row it's skipped on future runs.
    private static async Task BackfillGuideRunesAsync(ApplicationDbContext db)
    {
        var runes = await db.Runes.ToListAsync();
        var newByStatKey = runes.Where(r => r.DdragonId != null)
            .ToDictionary(r => StatKey(r.Name, r.Slot));

        var guideRunes = await db.GuideRunes.Include(gr => gr.Rune).ToListAsync();
        var changed = false;
        foreach (var gr in guideRunes)
        {
            if (gr.Rune is null || gr.Rune.DdragonId != null) continue;
            if (newByStatKey.TryGetValue(StatKey(gr.Rune.Name, gr.Rune.Slot), out var real))
            {
                gr.RuneId = real.Id;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    private static (string Slot, string Stat) StatKey(string name, string slot)
    {
        var stat = Regex.Replace(name, @"^(Greater|Minor)\s+", "");
        stat = Regex.Replace(stat, @"^(Mark|Seal|Glyph|Quintessence)\s+of\s+", "");
        return (slot, stat.Trim().ToLowerInvariant());
    }

    private static List<T> Load<T>(string seedDir, string file)
    {
        var path = Path.Combine(seedDir, file);
        if (!File.Exists(path)) return new List<T>();
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<T>>(stream, JsonOpts) ?? new List<T>();
    }

    private const string WelcomePost =
        "Welcome, Summoners! I am so glad to have all of you here -- League Classic Archive is a haven where all builds, " +
        "all champions, and all playstyles are welcome. A place where Teemo Mains and AP Sion " +
        "nostalgists can share a drink and a story. A place where AP Master Yis can Meditate under " +
        "tower for hours. A place where we can rejoice in the return of our beloved Fields of Justice. " +
        "I only ask that you keep the discourse respectful and safe for those of all ages.\n\n" +
        "Thank you, and once again, welcome!";

    private static async Task SeedWelcomeThreadAsync(ApplicationDbContext db)
    {
        // Pinned welcome thread — idempotent, so the flat forum always has it. The
        // slug is kept stable across the Hub->Archive rename (it's a permalink);
        // only the displayed title/body get updated, via the backfill below.
        const string slug = "welcome-to-league-classic-hub";
        var existing = await db.Threads.Include(t => t.Posts).FirstOrDefaultAsync(t => t.Slug == slug);
        if (existing is not null)
        {
            if (existing.Title == "Welcome to League Classic Hub!")
            {
                existing.Title = "Welcome to League Classic Archive!";
                existing.Excerpt = Excerpt(WelcomePost);
                var firstPost = existing.Posts.OrderBy(p => p.CreatedAt).FirstOrDefault();
                if (firstPost is not null) firstPost.BodyMarkdown = WelcomePost;
                await db.SaveChangesAsync();
            }
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var thread = new ForumThread
        {
            Title = "Welcome to League Classic Archive!",
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

    private const string StaffBuildBody = "## THIS WAS A DEFAULT BUILD CREATED BY THE STAFF";

    // Five staff-authored (AuthorId = null) example guides, so the site has real
    // content to browse before real users start posting. Idempotent by slug. Mastery
    // strings and rune/item slugs reference the corrected League Classic data seeded
    // above, which doubles as an end-to-end check that data resolves correctly.
    private static readonly (
        string ChampSlug, string Title, string Slug, string SpellOne, string SpellTwo,
        string SkillOrder, string Masteries, (string Slug, int Count)[] Runes, string[] Items, string Body
    )[] StaffGuides =
    {
        (
            "miss-fortune", "AD Carry Miss Fortune", "miss-fortune-ad-carry", "flash", "barrier",
            "Q,Q,Q,Q,Q,R,W,W,W,W,R,W,E,E,E,R,E,E",
            "512:4,522:4,531:3,532:1,541:2,542:2,551:1,552:3,562:1,613:4,614:2,621:3",
            new[] { ("mark-of-attack-damage", 9), ("minor-seal-of-attack-speed", 9), ("minor-glyph-of-armor", 9), ("quintessence-of-critical-damage", 3) },
            new[] { "dorans-blade", "berserkers-greaves", "the-bloodthirster", "infinity-edge", "phantom-dancer", "guardian-angel", "last-whisper" },
            StaffBuildBody
        ),
        (
            "nunu", "AP Jungle Nunu - Absolute Zero Tank Items", "nunu-ap-jungle", "flash", "smite",
            "E,E,E,E,E,R,Q,Q,Q,Q,R,Q,W,W,W,R,W,W",
            "513:4,523:4,531:3,533:1,543:3,544:1,553:4,562:1,613:4,614:2,621:3",
            new[] { ("mark-of-magic-penetration", 9), ("seal-of-armor", 9), ("glyph-of-cooldown-reduction", 9), ("quintessence-of-ability-power", 3) },
            new[] { "spirit-stone", "spirit-of-the-spectral-wraith", "sorcerers-shoes", "liandrys-torment", "rabadons-deathcap", "zhonyas-hourglass", "abyssal-scepter" },
            StaffBuildBody
        ),
        (
            "gragas", "AP Gragas \"If you're buyin', I'm in!\"", "gragas-ap-gragas", "flash", "ignite",
            "Q,Q,Q,Q,Q,R,E,E,E,E,R,E,W,W,W,R,W,W",
            "513:4,523:4,531:3,533:1,543:3,544:1,553:4,562:1,613:4,614:2,621:3",
            new[] { ("mark-of-magic-penetration", 9), ("minor-glyph-of-armor", 9), ("seal-of-health", 9), ("quintessence-of-ability-power", 3) },
            new[] { "dorans-ring", "dorans-ring", "dorans-ring", "sorcerers-shoes", "rod-of-ages", "rabadons-deathcap", "rylais-crystal-scepter", "void-staff", "abyssal-scepter" },
            StaffBuildBody
        ),
        (
            "gangplank", "Critplank", "gangplank-critplank", "flash", "ignite",
            "Q,Q,Q,Q,Q,R,E,E,E,E,R,E,W,W,W,R,W,W",
            "512:4,522:4,531:3,532:1,541:2,542:2,551:1,552:3,562:1,613:4,614:2,621:3",
            new[] { ("mark-of-attack-damage", 9), ("seal-of-armor", 9), ("glyph-of-cooldown-reduction", 9), ("quintessence-of-critical-damage", 3) },
            new[] { "dorans-blade", "boots-of-swiftness", "infinity-edge", "trinity-force", "phantom-dancer", "warmogs-armor" },
            StaffBuildBody
        ),
        (
            "soraka", "Support Soraka Banana Supreme", "soraka-support", "flash", "exhaust",
            "W,W,W,W,W,R,E,E,E,E,R,E,Q,Q,Q,R,Q,Q",
            "613:4,614:2,621:3,713:3,714:1,721:1,722:3,732:1,733:3,743:3,744:1,751:1,752:3,762:1",
            new[] { ("mark-of-magic-penetration", 9), ("minor-glyph-of-armor", 9), ("seal-of-mana-regeneration", 9), ("quintessence-of-ability-power", 3) },
            new[] { "philosophers-stone", "boots-of-mobility", "sightstone", "mikaels-crucible", "aegis-of-the-legion", "locket-of-the-iron-solari" },
            StaffBuildBody
        ),
    };

    private static async Task SeedStaffGuidesAsync(ApplicationDbContext db)
    {
        var champBySlug = await db.Champions.ToDictionaryAsync(c => c.Slug, c => c.Id);
        var itemBySlug = await db.Items.ToDictionaryAsync(i => i.Slug, i => i.Id);
        var runeBySlug = await db.Runes.Where(r => r.IsAvailable).ToDictionaryAsync(r => r.Slug, r => r.Id);
        var spellBySlug = await db.SummonerSpells.ToDictionaryAsync(s => s.Slug, s => s.Id);
        var now = DateTimeOffset.UtcNow;

        foreach (var spec in StaffGuides)
        {
            var existing = await db.Guides.FirstOrDefaultAsync(g => g.Slug == spec.Slug);
            if (existing is not null)
            {
                if (existing.Title != spec.Title || existing.BodyMarkdown != spec.Body)
                {
                    existing.Title = spec.Title;
                    existing.BodyMarkdown = spec.Body;
                    await db.SaveChangesAsync();
                }
                continue;
            }
            if (!champBySlug.TryGetValue(spec.ChampSlug, out var championId)) continue;

            var guide = new Guide
            {
                AuthorId = null,
                Title = spec.Title,
                Slug = spec.Slug,
                ChampionId = championId,
                SpellOneId = spellBySlug.GetValueOrDefault(spec.SpellOne),
                SpellTwoId = spellBySlug.GetValueOrDefault(spec.SpellTwo),
                SkillOrder = spec.SkillOrder,
                MasteryAllocations = spec.Masteries,
                BodyMarkdown = spec.Body,
                Status = GuideStatus.Published,
                CreatedAt = now,
                UpdatedAt = now,
            };

            for (var i = 0; i < spec.Items.Length; i++)
                if (itemBySlug.TryGetValue(spec.Items[i], out var itemId))
                    guide.BuildOrder.Add(new GuideItem { ItemId = itemId, Sort = i });

            foreach (var (slug, count) in spec.Runes)
                if (runeBySlug.TryGetValue(slug, out var runeId))
                    guide.Runes.Add(new GuideRune { RuneId = runeId, Count = count });

            db.Guides.Add(guide);
        }

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
    private sealed record ChampionSeed(string Name, string Slug, string? Icon, bool Available, string? Title, string? Blurb, string? Lore);
    private sealed record ItemSeed(string Name, string Slug, string Category, string? Icon, string? Desc);
    private sealed record SpellSeed(string Name, string Slug, string? Icon, string? Desc);
    private sealed record AbilitySeed(string Champ, string Slot, string Name, string? Icon, string? Desc);
    private sealed record RuneSeed(int DdragonId, string Name, string Slug, string Slot, string? Icon, string? Desc);
    private sealed record MasterySeed(int Id, string Name, string Tree, int Row, int Col, int Ranks, int? Prereq, string? Icon, string? Desc);
}
