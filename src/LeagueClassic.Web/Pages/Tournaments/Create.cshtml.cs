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
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ContentModerationService _moderation;

    public CreateModel(ApplicationDbContext db, UserManager<ApplicationUser> users, ContentModerationService moderation)
    {
        _db = db;
        _users = users;
        _moderation = moderation;
    }

    [BindProperty]
    public CreateInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Validate();
        if (!ModelState.IsValid) return Page();

        var isRoundRobin = Input.Format is TournamentFormat.RoundRobinToSingleElim or TournamentFormat.RoundRobinToDoubleElim;

        var now = DateTimeOffset.UtcNow;
        var tournament = new Tournament
        {
            OrganizerId = _users.GetUserId(User),
            Name = Input.Name.Trim(),
            Slug = await UniqueSlugAsync(Input.Name),
            Format = Input.Format,
            MaxTeams = Input.MaxTeams,
            SeedingMode = Input.SeedingMode,
            TeamsPerGroup = isRoundRobin ? Input.TeamsPerGroup : null,
            AdvancePerGroup = isRoundRobin ? Input.AdvancePerGroup : null,
            PrizeType = Input.PrizeType,
            PrizeAmount = Input.PrizeType == PrizeType.Cash ? Input.PrizeAmount : null,
            PrizeCurrency = Input.PrizeType == PrizeType.Cash ? Input.PrizeCurrency?.Trim() : null,
            ScheduledAt = new DateTimeOffset(DateTime.SpecifyKind(Input.ScheduledAtDate, DateTimeKind.Utc)),
            DetailsMarkdown = string.IsNullOrWhiteSpace(Input.DetailsMarkdown) ? null : Input.DetailsMarkdown,
            CreatedAt = now,
        };

        _db.Tournaments.Add(tournament);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Tournaments/Details", new { slug = tournament.Slug });
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
            ModelState.AddModelError("Input.Name", "Give your tournament a name.");
        if (_moderation.Validate(Input.Name, "Name", 120) is { } ne)
            ModelState.AddModelError("Input.Name", ne);
        if (Input.MaxTeams is < 2 or > 64)
            ModelState.AddModelError("Input.MaxTeams", "Max teams must be between 2 and 64.");

        var isRoundRobin = Input.Format is TournamentFormat.RoundRobinToSingleElim or TournamentFormat.RoundRobinToDoubleElim;
        if (isRoundRobin)
        {
            if (Input.TeamsPerGroup is null or < 2)
                ModelState.AddModelError("Input.TeamsPerGroup", "Teams per group must be at least 2.");
            if (Input.AdvancePerGroup is null or < 1)
                ModelState.AddModelError("Input.AdvancePerGroup", "At least 1 team must advance per group.");
            if (Input.TeamsPerGroup is >= 2 && Input.AdvancePerGroup is >= 1 && Input.AdvancePerGroup >= Input.TeamsPerGroup)
                ModelState.AddModelError("Input.AdvancePerGroup", "Advance-per-group must be fewer than teams-per-group.");
        }

        if (Input.PrizeType == PrizeType.Cash)
        {
            if (Input.PrizeAmount is null or <= 0)
                ModelState.AddModelError("Input.PrizeAmount", "Enter a prize amount.");
            if (string.IsNullOrWhiteSpace(Input.PrizeCurrency))
                ModelState.AddModelError("Input.PrizeCurrency", "Enter a currency (e.g. USD).");
        }

        if (_moderation.Validate(Input.DetailsMarkdown, "Details", 20_000) is { } de)
            ModelState.AddModelError("Input.DetailsMarkdown", de);
    }

    private async Task<string> UniqueSlugAsync(string name)
    {
        var baseSlug = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (baseSlug.Length == 0) baseSlug = "tournament";
        if (baseSlug.Length > 120) baseSlug = baseSlug[..120].Trim('-');
        var slug = baseSlug;
        var n = 2;
        while (await _db.Tournaments.AnyAsync(t => t.Slug == slug))
            slug = $"{baseSlug}-{n++}";
        return slug;
    }
}

public class CreateInput
{
    public string Name { get; set; } = "";
    public TournamentFormat Format { get; set; } = TournamentFormat.SingleElimination;
    public int MaxTeams { get; set; } = 16;
    public SeedingMode SeedingMode { get; set; } = SeedingMode.Random;
    public int? TeamsPerGroup { get; set; }
    public int? AdvancePerGroup { get; set; }
    public PrizeType PrizeType { get; set; } = PrizeType.BraggingRights;
    public decimal? PrizeAmount { get; set; }
    public string? PrizeCurrency { get; set; }
    public DateTime ScheduledAtDate { get; set; } = DateTime.UtcNow.Date.AddDays(7);
    public string? DetailsMarkdown { get; set; }
}
