using LeagueClassic.Web.Data;
using LeagueClassic.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Forums;

[EnableRateLimiting("post")]
public class ThreadModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ContentModerationService _moderation;

    public ThreadModel(ApplicationDbContext db, UserManager<ApplicationUser> users, ContentModerationService moderation)
    {
        _db = db;
        _users = users;
        _moderation = moderation;
    }

    public ForumThread Thread { get; private set; } = default!;
    public List<Post> Posts { get; private set; } = new();

    [BindProperty]
    public string ReplyBody { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();

        var thread = await _db.Threads.Include(t => t.Board).FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return NotFound();
        if (thread.IsLocked) return RedirectToPage(new { id, slug = thread.Slug });

        if (string.IsNullOrWhiteSpace(ReplyBody))
            ModelState.AddModelError(nameof(ReplyBody), "Write a reply first.");
        if (_moderation.Validate(ReplyBody, "Reply", 20_000) is { } re)
            ModelState.AddModelError(nameof(ReplyBody), re);
        if (!ModelState.IsValid)
        {
            await LoadAsync(id);
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        _db.Posts.Add(new Post
        {
            ThreadId = thread.Id,
            AuthorId = _users.GetUserId(User),
            BodyMarkdown = ReplyBody.Trim(),
            CreatedAt = now,
        });

        // Denormalized counters.
        thread.ReplyCount += 1;
        thread.LastPostAt = now;
        if (thread.Board is not null)
        {
            thread.Board.PostCount += 1;
            thread.Board.LastPostAt = now;
        }
        var user = await _db.Users.FindAsync(_users.GetUserId(User));
        if (user is not null) user.PostCount += 1;

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id, slug = thread.Slug });
    }

    private async Task<bool> LoadAsync(int id)
    {
        var thread = await _db.Threads
            .Include(t => t.Board)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return false;
        Thread = thread;

        Posts = await _db.Posts
            .Where(p => p.ThreadId == id)
            .Include(p => p.Author)
            .OrderBy(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
        return true;
    }
}
