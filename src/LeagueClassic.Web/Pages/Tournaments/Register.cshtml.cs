using LeagueClassic.Web.Data;
using LeagueClassic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Tournaments;

[Authorize]
[EnableRateLimiting("post")]
public class RegisterModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly TournamentService _tournaments;
    private readonly ContentModerationService _moderation;

    public RegisterModel(ApplicationDbContext db, UserManager<ApplicationUser> users,
        TournamentService tournaments, ContentModerationService moderation)
    {
        _db = db;
        _users = users;
        _tournaments = tournaments;
        _moderation = moderation;
    }

    public Tournament Tournament { get; private set; } = default!;

    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var t = await _db.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug);
        if (t is null) return NotFound();
        if (t.Status != TournamentStatus.RegistrationOpen || t.RegisteredTeamCount >= t.MaxTeams)
            return RedirectToPage("/Tournaments/Details", new { slug });

        Tournament = t;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Slug == slug);
        if (t is null) return NotFound();
        Tournament = t;

        if (t.Status != TournamentStatus.RegistrationOpen || t.RegisteredTeamCount >= t.MaxTeams)
        {
            ModelState.AddModelError(string.Empty, "Registration is no longer open for this tournament.");
            return Page();
        }

        Validate();
        if (!ModelState.IsValid) return Page();

        var players = (Input.PlayerNamesCsv ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(20)
            .ToList();

        await _tournaments.RegisterTeamAsync(t.Id, _users.GetUserId(User)!, Input.TeamName.Trim(), players);

        return RedirectToPage("/Tournaments/Details", new { slug });
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Input.TeamName))
            ModelState.AddModelError("Input.TeamName", "Give your team a name.");
        if (_moderation.Validate(Input.TeamName, "Team name", 60) is { } te)
            ModelState.AddModelError("Input.TeamName", te);

        var players = (Input.PlayerNamesCsv ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in players)
            if (_moderation.Validate(p, "Player name", 60) is { } pe)
                ModelState.AddModelError("Input.PlayerNamesCsv", pe);
    }
}

public class RegisterInput
{
    public string TeamName { get; set; } = "";

    // Newline-separated, built by the page's JS from individual player-name rows on submit.
    public string? PlayerNamesCsv { get; set; }
}
