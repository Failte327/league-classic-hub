using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ForumThread> Threads => Set<ForumThread>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Guide> Guides => Set<Guide>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Champion> Champions => Set<Champion>();
    public DbSet<ChampionAbility> ChampionAbilities => Set<ChampionAbility>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<SummonerSpell> SummonerSpells => Set<SummonerSpell>();
    public DbSet<GuideItem> GuideItems => Set<GuideItem>();
    public DbSet<Rune> Runes => Set<Rune>();
    public DbSet<Mastery> Masteries => Set<Mastery>();
    public DbSet<GuideRune> GuideRunes => Set<GuideRune>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<LegacyPageView> LegacyPageViews => Set<LegacyPageView>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentGroup> TournamentGroups => Set<TournamentGroup>();
    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<TournamentPlayer> TournamentPlayers => Set<TournamentPlayer>();
    public DbSet<TournamentMatch> TournamentMatches => Set<TournamentMatch>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(30);
            e.HasIndex(u => u.DisplayName).IsUnique();  // nulls allowed (pre-backfill)
        });

        builder.Entity<ForumThread>(e =>
        {
            // Pinned-first, then most-recent activity is the main list query.
            e.HasIndex(t => new { t.IsPinned, t.LastPostAt });
            e.HasIndex(t => t.Slug);
            e.Property(t => t.Title).HasMaxLength(250);
            e.Property(t => t.Excerpt).HasMaxLength(300);
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
            e.Property(i => i.IsAvailable).HasDefaultValue(true);
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

        builder.Entity<Rune>(e =>
        {
            e.HasIndex(r => r.Slug).IsUnique();
            e.HasIndex(r => r.Slot);
            e.HasIndex(r => r.DdragonId);
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.Slug).HasMaxLength(100);
            e.Property(r => r.Slot).HasMaxLength(20);
            e.Property(r => r.IsAvailable).HasDefaultValue(true);
        });

        builder.Entity<Mastery>(e =>
        {
            e.HasIndex(m => m.DdragonId).IsUnique();
            e.HasIndex(m => new { m.Tree, m.Row, m.Col });
            e.Property(m => m.Name).HasMaxLength(80);
            e.Property(m => m.Tree).HasMaxLength(20);
            e.Property(m => m.IsAvailable).HasDefaultValue(true);
        });

        builder.Entity<GuideRune>(e =>
        {
            e.HasIndex(gr => gr.GuideId);
            e.HasOne(gr => gr.Guide)
                .WithMany(g => g.Runes)
                .HasForeignKey(gr => gr.GuideId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(gr => gr.Rune)
                .WithMany()
                .HasForeignKey(gr => gr.RuneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Guide>().Property(g => g.MasteryAllocations).HasMaxLength(400);

        builder.Entity<Report>(e =>
        {
            e.HasIndex(r => new { r.Status, r.CreatedAt });
            e.HasIndex(r => new { r.TargetType, r.TargetId });
            e.Property(r => r.Details).HasMaxLength(1000);
            e.HasOne(r => r.Reporter)
                .WithMany()
                .HasForeignKey(r => r.ReporterId)
                .OnDelete(DeleteBehavior.SetNull);
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

        builder.Entity<Vote>(e =>
        {
            // One vote per user per target; also the lookup path VotingService uses.
            e.HasIndex(v => new { v.TargetType, v.TargetId, v.VoterId }).IsUnique();
            e.HasOne(v => v.Voter)
                .WithMany()
                .HasForeignKey(v => v.VoterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LegacyPageView>(e =>
        {
            e.ToTable("PageViewsLegacy");
            e.HasIndex(v => v.OccurredAt);
            e.Property(v => v.Path).HasMaxLength(300);
        });

        builder.Entity<Tournament>(e =>
        {
            e.HasIndex(t => t.Slug).IsUnique();
            e.HasIndex(t => new { t.Status, t.ScheduledAt });
            e.Property(t => t.Name).HasMaxLength(120);
            e.Property(t => t.Slug).HasMaxLength(140);
            e.Property(t => t.PrizeCurrency).HasMaxLength(20);
            e.Property(t => t.PrizeAmount).HasPrecision(12, 2);
            e.HasOne(t => t.Organizer)
                .WithMany()
                .HasForeignKey(t => t.OrganizerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TournamentGroup>(e =>
        {
            e.HasIndex(g => new { g.TournamentId, g.Index }).IsUnique();
            e.HasOne(g => g.Tournament)
                .WithMany(t => t.Groups)
                .HasForeignKey(g => g.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TournamentTeam>(e =>
        {
            e.HasIndex(t => t.TournamentId);
            e.HasIndex(t => new { t.TournamentId, t.Seed });
            e.Property(t => t.Name).HasMaxLength(60);
            e.HasOne(t => t.Tournament)
                .WithMany(tn => tn.Teams)
                .HasForeignKey(t => t.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Captain)
                .WithMany()
                .HasForeignKey(t => t.CaptainId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.Group)
                .WithMany(g => g.Teams)
                .HasForeignKey(t => t.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TournamentPlayer>(e =>
        {
            e.HasIndex(p => p.TeamId);
            e.Property(p => p.Name).HasMaxLength(60);
            e.HasOne(p => p.Team)
                .WithMany(t => t.Players)
                .HasForeignKey(p => p.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TournamentMatch>(e =>
        {
            e.HasIndex(m => new { m.TournamentId, m.Stage, m.BracketSide, m.Round, m.SlotIndex });
            e.HasIndex(m => new { m.TournamentId, m.GroupId, m.GroupRound });
            e.HasOne(m => m.Tournament)
                .WithMany(t => t.Matches)
                .HasForeignKey(m => m.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Group)
                .WithMany()
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.TeamA)
                .WithMany()
                .HasForeignKey(m => m.TeamAId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.TeamB)
                .WithMany()
                .HasForeignKey(m => m.TeamBId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.WinnerTeam)
                .WithMany()
                .HasForeignKey(m => m.WinnerTeamId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.NextMatch)
                .WithMany()
                .HasForeignKey(m => m.NextMatchId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.LoserNextMatch)
                .WithMany()
                .HasForeignKey(m => m.LoserNextMatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
