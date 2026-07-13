using LeagueClassic.Web.Data;
using LeagueClassic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages;

[Authorize]
[EnableRateLimiting("post")]
public class ReportModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ContentModerationService _moderation;

    public ReportModel(ApplicationDbContext db, UserManager<ApplicationUser> users, ContentModerationService moderation)
    {
        _db = db;
        _users = users;
        _moderation = moderation;
    }

    public string TargetLabel { get; private set; } = "";

    [BindProperty] public ReportTargetType Type { get; set; }
    [BindProperty] public int Id { get; set; }
    [BindProperty] public ReportReason Reason { get; set; }
    [BindProperty] public string? Details { get; set; }

    public async Task<IActionResult> OnGetAsync(ReportTargetType type, int id)
    {
        Type = type;
        Id = id;
        var label = await TargetLabelAsync(type, id);
        if (label is null) return NotFound();
        TargetLabel = label;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var label = await TargetLabelAsync(Type, Id);
        if (label is null) return NotFound();
        TargetLabel = label;

        if (_moderation.Validate(Details, "Details", 1000) is { } de)
            ModelState.AddModelError(nameof(Details), de);
        if (!ModelState.IsValid) return Page();

        var reporterId = _users.GetUserId(User);

        // Dedupe: ignore a second open report from the same user on the same target.
        var already = await _db.Reports.AnyAsync(r =>
            r.ReporterId == reporterId && r.TargetType == Type && r.TargetId == Id && r.Status == ReportStatus.Open);
        if (!already)
        {
            _db.Reports.Add(new Report
            {
                ReporterId = reporterId, TargetType = Type, TargetId = Id,
                Reason = Reason, Details = string.IsNullOrWhiteSpace(Details) ? null : Details.Trim(),
            });
            await _db.SaveChangesAsync();
        }

        TempData["Message"] = "Thanks — a moderator will take a look.";
        return await RedirectToTargetAsync(Type, Id);
    }

    private async Task<string?> TargetLabelAsync(ReportTargetType type, int id) => type switch
    {
        ReportTargetType.Guide => await _db.Guides.Where(g => g.Id == id).Select(g => "guide: " + g.Title).FirstOrDefaultAsync(),
        ReportTargetType.Post => await _db.Posts.Where(p => p.Id == id)
            .Select(p => "post by " + (p.Author!.DisplayName ?? "a summoner")).FirstOrDefaultAsync(),
        _ => null,
    };

    private async Task<IActionResult> RedirectToTargetAsync(ReportTargetType type, int id)
    {
        if (type == ReportTargetType.Guide)
        {
            var slug = await _db.Guides.Where(g => g.Id == id).Select(g => g.Slug).FirstOrDefaultAsync();
            return RedirectToPage("/Guides/Details", new { slug });
        }
        var thread = await _db.Posts.Where(p => p.Id == id)
            .Select(p => new { p.ThreadId, p.Thread!.Slug }).FirstOrDefaultAsync();
        return thread is null
            ? RedirectToPage("/Forums/Index")
            : RedirectToPage("/Forums/Thread", new { id = thread.ThreadId, slug = thread.Slug });
    }
}
