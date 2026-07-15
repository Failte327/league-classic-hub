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
    private readonly VotingService _voting;

    public ThreadModel(ApplicationDbContext db, UserManager<ApplicationUser> users, ContentModerationService moderation, VotingService voting)
    {
        _db = db;
        _users = users;
        _moderation = moderation;
        _voting = voting;
    }

    public ForumThread Thread { get; private set; } = default!;
    public List<Post> Posts { get; private set; } = new();
    public Dictionary<int, short> MyVotes { get; private set; } = new();

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

        var thread = await _db.Threads.FirstOrDefaultAsync(t => t.Id == id);
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
        var user = await _db.Users.FindAsync(_users.GetUserId(User));
        if (user is not null) user.PostCount += 1;

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id, slug = thread.Slug });
    }

    public async Task<IActionResult> OnPostUpvoteAsync(int id, int postId) => await VoteAsync(id, postId, 1);
    public async Task<IActionResult> OnPostDownvoteAsync(int id, int postId) => await VoteAsync(id, postId, -1);

    private async Task<IActionResult> VoteAsync(int id, int postId, short value)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();

        var thread = await _db.Threads.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return NotFound();
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.ThreadId == id);
        if (post is null) return NotFound();

        await _voting.CastAsync(VoteTargetType.Post, post.Id, _users.GetUserId(User)!, value);
        return RedirectToPage(new { id, slug = thread.Slug });
    }

    private async Task<bool> LoadAsync(int id)
    {
        var thread = await _db.Threads
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

        MyVotes = await _voting.MyVotesAsync(VoteTargetType.Post, Posts.Select(p => p.Id), _users.GetUserId(User));
        return true;
    }
}
