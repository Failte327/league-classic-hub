using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<ForumThread> Threads => Set<ForumThread>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Guide> Guides => Set<Guide>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Champion> Champions => Set<Champion>();
    public DbSet<ChampionAbility> ChampionAbilities => Set<ChampionAbility>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<SummonerSpell> SummonerSpells => Set<SummonerSpell>();
    public DbSet<GuideItem> GuideItems => Set<GuideItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables

        builder.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
            e.Property(c => c.Name).HasMaxLength(100);
            e.Property(c => c.Slug).HasMaxLength(120);
        });

        builder.Entity<Board>(e =>
        {
            e.HasIndex(b => b.Slug).IsUnique();
            e.Property(b => b.Name).HasMaxLength(120);
            e.Property(b => b.Slug).HasMaxLength(140);
            e.HasOne(b => b.Category)
                .WithMany(c => c.Boards)
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ForumThread>(e =>
        {
            // Board + last-post ordering is the hottest list query.
            e.HasIndex(t => new { t.BoardId, t.LastPostAt });
            e.HasIndex(t => t.Slug);
            e.Property(t => t.Title).HasMaxLength(250);
            e.HasOne(t => t.Board)
                .WithMany(b => b.Threads)
                .HasForeignKey(t => t.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Author)
                .WithMany()
                .HasForeignKey(t => t.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Post>(e =>
        {
            e.HasIndex(p => new { p.ThreadId, p.CreatedAt });
            e.HasOne(p => p.Thread)
                .WithMany(t => t.Posts)
                .HasForeignKey(p => p.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Author)
                .WithMany()
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Guide>(e =>
        {
            e.HasIndex(g => g.Slug).IsUnique();
            e.HasIndex(g => new { g.Status, g.UpdatedAt });
            e.HasIndex(g => g.ChampionId);
            e.Property(g => g.Title).HasMaxLength(250);
            e.Property(g => g.SkillOrder).HasMaxLength(80);
            e.HasOne(g => g.Author)
                .WithMany()
                .HasForeignKey(g => g.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(g => g.Champion)
                .WithMany()
                .HasForeignKey(g => g.ChampionId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.SpellOne)
                .WithMany()
                .HasForeignKey(g => g.SpellOneId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(g => g.SpellTwo)
                .WithMany()
                .HasForeignKey(g => g.SpellTwoId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Champion>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
            e.Property(c => c.Name).HasMaxLength(60);
            e.Property(c => c.Slug).HasMaxLength(60);
        });

        builder.Entity<ChampionAbility>(e =>
        {
            e.HasIndex(a => new { a.ChampionId, a.Slot });
            e.Property(a => a.Slot).HasMaxLength(1);
            e.Property(a => a.Name).HasMaxLength(80);
            e.HasOne(a => a.Champion)
                .WithMany(c => c.Abilities)
                .HasForeignKey(a => a.ChampionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Item>(e =>
        {
            e.HasIndex(i => i.Slug).IsUnique();
            e.HasIndex(i => i.Category);
            e.Property(i => i.Name).HasMaxLength(80);
            e.Property(i => i.Slug).HasMaxLength(80);
            e.Property(i => i.Category).HasMaxLength(40);
        });

        builder.Entity<SummonerSpell>(e =>
        {
            e.HasIndex(s => s.Slug).IsUnique();
            e.Property(s => s.Name).HasMaxLength(40);
            e.Property(s => s.Slug).HasMaxLength(40);
        });

        builder.Entity<GuideItem>(e =>
        {
            e.HasIndex(gi => new { gi.GuideId, gi.Sort });
            e.HasOne(gi => gi.Guide)
                .WithMany(g => g.BuildOrder)
                .HasForeignKey(gi => gi.GuideId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(gi => gi.Item)
                .WithMany()
                .HasForeignKey(gi => gi.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Comment>(e =>
        {
            e.HasIndex(c => new { c.GuideId, c.CreatedAt });
            e.HasOne(c => c.Guide)
                .WithMany(g => g.Comments)
                .HasForeignKey(c => c.GuideId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author)
                .WithMany()
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
