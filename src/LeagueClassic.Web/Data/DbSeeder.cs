using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Data;

// Applies migrations on startup and seeds a starter set of categories/boards
// so the home page has content the moment you run it.
public static class DbSeeder
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.Database.MigrateAsync();

        if (await db.Categories.AnyAsync())
            return;

        var general = new Category { Name = "The Rift", Slug = "the-rift", SortOrder = 1 };
        var strategy = new Category { Name = "Strategy & Guides", Slug = "strategy", SortOrder = 2 };

        general.Boards.Add(new Board
        {
            Name = "Announcements",
            Slug = "announcements",
            Description = "Site news and League Classic updates.",
            SortOrder = 1,
        });
        general.Boards.Add(new Board
        {
            Name = "General Discussion",
            Slug = "general",
            Description = "Everything League Classic — plays, patches, hot takes.",
            SortOrder = 2,
        });
        strategy.Boards.Add(new Board
        {
            Name = "Champion Discussion",
            Slug = "champions",
            Description = "Deep dives on individual champions.",
            SortOrder = 1,
        });
        strategy.Boards.Add(new Board
        {
            Name = "Build & Guide Workshop",
            Slug = "guide-workshop",
            Description = "Share and critique builds and written guides.",
            SortOrder = 2,
        });

        db.Categories.AddRange(general, strategy);
        await db.SaveChangesAsync();
    }
}
