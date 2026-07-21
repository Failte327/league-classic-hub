using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Moderation;

[Authorize(Roles = DbSeeder.ModeratorRole)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public List<ReportGroup> Groups { get; private set; } = new();

    public class ReportGroup
    {
        public ReportTargetType Type { get; init; }
        public int TargetId { get; init; }
        public int Count { get; init; }
        public string Reasons { get; init; } = "";
        public string Reporters { get; init; } = "";
        public DateTimeOffset LatestAt { get; init; }
        public string Title { get; init; } = "";
        public string? Excerpt { get; init; }
        public bool Missing { get; init; }
        // Link targets
        public int? ThreadId { get; init; }
        public string? Slug { get; init; }
    }

    public async Task OnGetAsync()
    {
        var reports = await _db.Reports
            .Where(r => r.Status == ReportStatus.Open)
            .Include(r => r.Reporter)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var postIds = reports.Where(r => r.TargetType == ReportTargetType.Post).Select(r => r.TargetId).Distinct().ToList();
        var guideIds = reports.Where(r => r.TargetType == ReportTargetType.Guide).Select(r => r.TargetId).Distinct().ToList();
        var teamIds = reports.Where(r => r.TargetType == ReportTargetType.Team).Select(r => r.TargetId).Distinct().ToList();

        var posts = await _db.Posts.Where(p => postIds.Contains(p.Id))
            .Include(p => p.Thread).Include(p => p.Author).AsNoTracking()
            .ToDictionaryAsync(p => p.Id);
        var guides = await _db.Guides.Where(g => guideIds.Contains(g.Id)).AsNoTracking()
            .ToDictionaryAsync(g => g.Id);
        var teams = await _db.TournamentTeams.Where(t => teamIds.Contains(t.Id))
            .Include(t => t.Tournament).AsNoTracking()
            .ToDictionaryAsync(t => t.Id);

        Groups = reports
            .GroupBy(r => new { r.TargetType, r.TargetId })
            .Select(g =>
            {
                var reasons = string.Join(", ", g.Select(r => r.Reason).Distinct());
                var reporters = string.Join(", ", g.Select(r => r.Reporter.Display("?")).Distinct());
                if (g.Key.TargetType == ReportTargetType.Post)
                {
                    posts.TryGetValue(g.Key.TargetId, out var p);
                    return new ReportGroup
                    {
                        Type = ReportTargetType.Post, TargetId = g.Key.TargetId, Count = g.Count(),
                        Reasons = reasons, Reporters = reporters, LatestAt = g.Max(r => r.CreatedAt),
                        Missing = p is null,
                        Title = p is null ? "(deleted post)" : $"Post by {p.Author.Display("staff")} in “{p.Thread?.Title}”",
                        Excerpt = p is null ? null : DbSeeder.Excerpt(p.BodyMarkdown, 200),
                        ThreadId = p?.ThreadId, Slug = p?.Thread?.Slug,
                    };
                }
                if (g.Key.TargetType == ReportTargetType.Guide)
                {
                    guides.TryGetValue(g.Key.TargetId, out var gd);
                    return new ReportGroup
                    {
                        Type = ReportTargetType.Guide, TargetId = g.Key.TargetId, Count = g.Count(),
                        Reasons = reasons, Reporters = reporters, LatestAt = g.Max(r => r.CreatedAt),
                        Missing = gd is null,
                        Title = gd is null ? "(deleted guide)" : $"Guide: “{gd.Title}”",
                        Slug = gd?.Slug,
                    };
                }
                teams.TryGetValue(g.Key.TargetId, out var tm);
                return new ReportGroup
                {
                    Type = ReportTargetType.Team, TargetId = g.Key.TargetId, Count = g.Count(),
                    Reasons = reasons, Reporters = reporters, LatestAt = g.Max(r => r.CreatedAt),
                    Missing = tm is null,
                    Title = tm is null ? "(deleted team)" : $"Tournament team: “{tm.Name}” in {tm.Tournament?.Name}",
                    Slug = tm?.Tournament?.Slug,
                };
            })
            .OrderByDescending(g => g.Count).ThenByDescending(g => g.LatestAt)
            .ToList();
    }

    public async Task<IActionResult> OnPostDismissAsync(ReportTargetType type, int id)
    {
        await ResolveAsync(type, id, ReportStatus.Dismissed);
        TempData["Message"] = "Report dismissed.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteContentAsync(ReportTargetType type, int id)
    {
        if (type == ReportTargetType.Guide)
        {
            var guide = await _db.Guides.FirstOrDefaultAsync(g => g.Id == id);
            if (guide is not null) _db.Guides.Remove(guide);
        }
        else if (type == ReportTargetType.Team)
        {
            var team = await _db.TournamentTeams.Include(t => t.Players).FirstOrDefaultAsync(t => t.Id == id);
            if (team is not null)
            {
                var referenced = await _db.TournamentMatches.AnyAsync(m =>
                    m.TeamAId == id || m.TeamBId == id || m.WinnerTeamId == id);
                if (referenced)
                {
                    // A bracket already links to this team — redact in place rather than
                    // hard-deleting, so existing match links stay valid.
                    team.Name = "[removed]";
                    foreach (var p in team.Players) p.Name = "[removed]";
                }
                else
                {
                    var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == team.TournamentId);
                    if (tournament is not null) tournament.RegisteredTeamCount = Math.Max(0, tournament.RegisteredTeamCount - 1);
                    _db.TournamentTeams.Remove(team);
                }
            }
        }
        else
        {
            var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (post is not null)
            {
                var threadId = post.ThreadId;
                _db.Posts.Remove(post);
                await _db.SaveChangesAsync();
                var remaining = await _db.Posts.Where(p => p.ThreadId == threadId)
                    .OrderBy(p => p.CreatedAt).ToListAsync();
                var thread = await _db.Threads.FirstOrDefaultAsync(t => t.Id == threadId);
                if (thread is not null)
                {
                    if (remaining.Count == 0) _db.Threads.Remove(thread);
                    else { thread.ReplyCount = remaining.Count - 1; thread.LastPostAt = remaining[^1].CreatedAt; }
                }
            }
        }
        await ResolveAsync(type, id, ReportStatus.Resolved, save: false);
        await _db.SaveChangesAsync();
        TempData["Message"] = "Content deleted and reports resolved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLockThreadAsync(int id)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (post is not null)
        {
            var thread = await _db.Threads.FirstOrDefaultAsync(t => t.Id == post.ThreadId);
            if (thread is not null) thread.IsLocked = true;
        }
        await ResolveAsync(ReportTargetType.Post, id, ReportStatus.Resolved, save: false);
        await _db.SaveChangesAsync();
        TempData["Message"] = "Thread locked and reports resolved.";
        return RedirectToPage();
    }

    private async Task ResolveAsync(ReportTargetType type, int id, ReportStatus status, bool save = true)
    {
        var open = await _db.Reports
            .Where(r => r.TargetType == type && r.TargetId == id && r.Status == ReportStatus.Open)
            .ToListAsync();
        var now = DateTimeOffset.UtcNow;
        foreach (var r in open)
        {
            r.Status = status;
            r.ResolvedById = _users.GetUserId(User);
            r.ResolvedAt = now;
        }
        if (save) await _db.SaveChangesAsync();
    }
}
