using LeagueClassic.Web.Data;
using LeagueClassic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Moderation;

// Lets moderators post into the pinned, locked "Dev Updates" thread without
// going through the normal (locked-out) reply form. Every post here is
// authored anonymously (AuthorId = null, renders as "Staff") regardless of
// which moderator submitted it, so updates read as official rather than
// personal.
[Authorize(Roles = DbSeeder.ModeratorRole)]
[EnableRateLimiting("post")]
public class DevUpdatesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ContentModerationService _moderation;

    public DevUpdatesModel(ApplicationDbContext db, ContentModerationService moderation)
    {
        _db = db;
        _moderation = moderation;
    }

    public ForumThread Thread { get; private set; } = default!;
    public List<Post> Posts { get; private set; } = new();

    [BindProperty]
    public string Body { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var thread = await _db.Threads.FirstOrDefaultAsync(t => t.Slug == DbSeeder.DevUpdatesSlug);
        if (thread is null) return NotFound();

        if (string.IsNullOrWhiteSpace(Body))
            ModelState.AddModelError(nameof(Body), "Write the update first.");
        if (_moderation.Validate(Body, "Post", 20_000) is { } be)
            ModelState.AddModelError(nameof(Body), be);
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        _db.Posts.Add(new Post
        {
            ThreadId = thread.Id,
            AuthorId = null, // always staff, never the posting moderator's own account
            BodyMarkdown = Body.Trim(),
            CreatedAt = now,
        });

        thread.ReplyCount += 1;
        thread.LastPostAt = now;
        thread.Excerpt = DbSeeder.Excerpt(Body.Trim());

        await _db.SaveChangesAsync();
        return RedirectToPage("/Forums/Thread", new { id = thread.Id, slug = thread.Slug });
    }

    private async Task<bool> LoadAsync()
    {
        var thread = await _db.Threads.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == DbSeeder.DevUpdatesSlug);
        if (thread is null) return false;
        Thread = thread;

        Posts = await _db.Posts
            .Where(p => p.ThreadId == thread.Id)
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return true;
    }
}
