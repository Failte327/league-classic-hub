using LeagueClassic.Web.Data;
using LeagueClassic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace LeagueClassic.Web.Pages.Guides;

[Authorize]
[EnableRateLimiting("post")]
public class ComposeModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly GuideEditorService _editor;
    private readonly ContentModerationService _moderation;
    private readonly DiscordService _discord;

    public ComposeModel(ApplicationDbContext db, UserManager<ApplicationUser> users,
        GuideEditorService editor, ContentModerationService moderation, DiscordService discord)
    {
        _db = db;
        _users = users;
        _editor = editor;
        _moderation = moderation;
        _discord = discord;
    }

    public GuideEditorVm Vm { get; private set; } = default!;

    [BindProperty]
    public GuideInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string champion)
    {
        var champ = await _editor.LoadChampionAsync(champion);
        if (champ is null || !champ.IsAvailable)
            return RedirectToPage("/Guides/Create");

        Input.ChampionSlug = champ.Slug;
        Vm = await _editor.BuildVmAsync(champ, Input, isEdit: false, heading: $"New Guide: {champ.Name}");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var champ = await _editor.LoadChampionAsync(Input.ChampionSlug);
        if (champ is null || !champ.IsAvailable)
            return RedirectToPage("/Guides/Create");

        Validate();
        if (!ModelState.IsValid)
        {
            Vm = await _editor.BuildVmAsync(champ, Input, isEdit: false, heading: $"New Guide: {champ.Name}");
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        var guide = new Guide
        {
            AuthorId = _users.GetUserId(User),
            Title = Input.Title.Trim(),
            Slug = await _editor.UniqueSlugAsync(champ.Slug, Input.Title),
            BodyMarkdown = Input.BodyMarkdown,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _editor.ApplyAsync(guide, Input, champ);

        _db.Guides.Add(guide);
        await _db.SaveChangesAsync();

        if (guide.Status == GuideStatus.Published)
        {
            await _discord.NotifyGuidePublishedAsync(
                guide.Title, champ.Name, User.Identity?.Name ?? "a Summoner",
                $"https://leagueclassicarchive.net/Guides/Details/{guide.Slug}");
        }

        return RedirectToPage("/Guides/Details", new { slug = guide.Slug });
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Input.Title))
            ModelState.AddModelError("Input.Title", "Give your guide a title.");
        if (string.IsNullOrWhiteSpace(Input.BodyMarkdown))
            ModelState.AddModelError("Input.BodyMarkdown", "Write something in the guide body.");
        if (_moderation.Validate(Input.Title, "Title", 200) is { } te)
            ModelState.AddModelError("Input.Title", te);
        if (_moderation.Validate(Input.BodyMarkdown, "Guide body", 100_000) is { } be)
            ModelState.AddModelError("Input.BodyMarkdown", be);
    }
}
