using LeagueClassic.Web.Data;
using LeagueClassic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Guides;

[Authorize]
[EnableRateLimiting("post")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly GuideEditorService _editor;
    private readonly ContentModerationService _moderation;

    public EditModel(ApplicationDbContext db, UserManager<ApplicationUser> users,
        GuideEditorService editor, ContentModerationService moderation)
    {
        _db = db;
        _users = users;
        _editor = editor;
        _moderation = moderation;
    }

    public GuideEditorVm Vm { get; private set; } = default!;

    [BindProperty]
    public GuideInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var guide = await LoadGuideAsync(id, tracking: false);
        if (guide is null) return NotFound();
        if (!IsOwner(guide)) return Forbid();

        Input = new GuideInput
        {
            ChampionSlug = guide.Champion!.Slug,
            Title = guide.Title,
            SpellOneId = guide.SpellOneId,
            SpellTwoId = guide.SpellTwoId,
            SkillOrder = guide.SkillOrder,
            BuildOrderCsv = string.Join(",", guide.BuildOrder.OrderBy(b => b.Sort).Select(b => b.ItemId)),
            BodyMarkdown = guide.BodyMarkdown,
            Publish = guide.Status == GuideStatus.Published,
        };

        Vm = await _editor.BuildVmAsync(
            guide.Champion, Input, isEdit: true,
            heading: $"Edit: {guide.Title}",
            initialStateJson: _editor.InitialStateJson(guide));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var guide = await LoadGuideAsync(id, tracking: true);
        if (guide is null) return NotFound();
        if (!IsOwner(guide)) return Forbid();

        if (string.IsNullOrWhiteSpace(Input.Title))
            ModelState.AddModelError("Input.Title", "Give your guide a title.");
        if (string.IsNullOrWhiteSpace(Input.BodyMarkdown))
            ModelState.AddModelError("Input.BodyMarkdown", "Write something in the guide body.");
        if (_moderation.Validate(Input.Title, "Title", 200) is { } te)
            ModelState.AddModelError("Input.Title", te);
        if (_moderation.Validate(Input.BodyMarkdown, "Guide body", 100_000) is { } be)
            ModelState.AddModelError("Input.BodyMarkdown", be);

        if (!ModelState.IsValid)
        {
            Vm = await _editor.BuildVmAsync(
                guide.Champion!, Input, isEdit: true, heading: $"Edit: {guide.Title}",
                initialStateJson: _editor.InitialStateJson(guide));
            return Page();
        }

        await _editor.ApplyAsync(guide, Input, guide.Champion!); // champion isn't changed on edit
        guide.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return RedirectToPage("/Guides/Details", new { slug = guide.Slug });
    }

    private async Task<Guide?> LoadGuideAsync(int id, bool tracking)
    {
        IQueryable<Guide> q = _db.Guides
            .Include(g => g.Champion).ThenInclude(c => c!.Abilities)
            .Include(g => g.BuildOrder).ThenInclude(b => b.Item);
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(g => g.Id == id);
    }

    private bool IsOwner(Guide guide) => guide.AuthorId == _users.GetUserId(User);
}
