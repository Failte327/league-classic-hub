using System.Text.RegularExpressions;
using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace LeagueClassic.Web.Pages.Forums;

[Authorize]
[EnableRateLimiting("post")]
public class NewThreadModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly Services.ContentModerationService _moderation;

    public NewThreadModel(ApplicationDbContext db, UserManager<ApplicationUser> users, Services.ContentModerationService moderation)
    {
        _db = db;
        _users = users;
        _moderation = moderation;
    }

    [BindProperty] public string Title { get; set; } = "";
    [BindProperty] public string Body { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
            ModelState.AddModelError(nameof(Title), "Give your thread a title.");
        if (string.IsNullOrWhiteSpace(Body))
            ModelState.AddModelError(nameof(Body), "Write the opening post.");
        if (_moderation.Validate(Title, "Title", 200) is { } te)
            ModelState.AddModelError(nameof(Title), te);
        if (_moderation.Validate(Body, "Post", 20_000) is { } be)
            ModelState.AddModelError(nameof(Body), be);
        if (!ModelState.IsValid) return Page();

        var now = DateTimeOffset.UtcNow;
        var uid = _users.GetUserId(User);
        var thread = new ForumThread
        {
            AuthorId = uid,
            Title = Title.Trim(),
            Slug = Slugify(Title),
            Excerpt = DbSeeder.Excerpt(Body),
            CreatedAt = now,
            LastPostAt = now,
        };
        thread.Posts.Add(new Post { AuthorId = uid, BodyMarkdown = Body.Trim(), CreatedAt = now });

        var user = await _db.Users.FindAsync(uid);
        if (user is not null) user.PostCount += 1;

        _db.Threads.Add(thread);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Forums/Thread", new { id = thread.Id, slug = thread.Slug });
    }

    private static string Slugify(string s)
    {
        var slug = Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (slug.Length > 80) slug = slug[..80].Trim('-');
        return slug.Length == 0 ? "thread" : slug;
    }
}
