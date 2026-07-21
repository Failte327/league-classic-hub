using LeagueClassic.Web.Data;
using LeagueClassic.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Tournaments;

[EnableRateLimiting("post")]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly TournamentService _tournaments;

    public DetailsModel(ApplicationDbContext db, UserManager<ApplicationUser> users, TournamentService tournaments)
    {
        _db = db;
        _users = users;
        _tournaments = tournaments;
    }

    public Tournament Tournament { get; private set; } = default!;
    public List<TournamentMatch> Matches { get; private set; } = new();
    public bool CanManage { get; private set; }
    public bool AlreadyRegistered { get; private set; }
    public Dictionary<int, List<TournamentTeam>> GroupStandings { get; private set; } = new();

    [BindProperty]
    public Dictionary<int, int> Seeds { get; set; } = new();

    [BindProperty]
    public Dictionary<int, int> GroupRanks { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        if (!await LoadAsync(slug)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAssignSeedsAsync(string slug)
    {
        if (!await LoadAsync(slug)) return NotFound();
        if (!CanManage) return Forbid();

        await _tournaments.AssignSeedsAsync(Tournament.Id, Seeds);
        return RedirectToPage(new { slug });
    }

    public async Task<IActionResult> OnPostStartAsync(string slug)
    {
        if (!await LoadAsync(slug)) return NotFound();
        if (!CanManage) return Forbid();

        try
        {
            await _tournaments.StartTournamentAsync(Tournament.Id);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(slug);
            return Page();
        }
        return RedirectToPage(new { slug });
    }

    public async Task<IActionResult> OnPostRecordMatchResultAsync(string slug, int matchId, int winnerTeamId)
    {
        if (!await LoadAsync(slug)) return NotFound();
        if (!CanManage) return Forbid();

        try
        {
            await _tournaments.RecordMatchResultAsync(matchId, winnerTeamId, _users.GetUserId(User)!);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(slug);
            return Page();
        }
        return RedirectToPage(new { slug });
    }

    public async Task<IActionResult> OnPostConfirmGroupStandingsAsync(string slug, int groupId)
    {
        if (!await LoadAsync(slug)) return NotFound();
        if (!CanManage) return Forbid();

        var ordered = GroupRanks
            .Where(kv => Tournament.Groups.Any(g => g.Id == groupId && g.Teams.Any(t => t.Id == kv.Key)))
            .OrderBy(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToList();
        await _tournaments.ConfirmGroupStandingsAsync(groupId, ordered);
        return RedirectToPage(new { slug });
    }

    public async Task<IActionResult> OnPostGenerateBracketAsync(string slug)
    {
        if (!await LoadAsync(slug)) return NotFound();
        if (!CanManage) return Forbid();

        try
        {
            await _tournaments.GenerateBracketFromGroupsAsync(Tournament.Id);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(slug);
            return Page();
        }
        return RedirectToPage(new { slug });
    }

    private async Task<bool> LoadAsync(string slug)
    {
        var t = await _db.Tournaments
            .Include(x => x.Organizer)
            .Include(x => x.Teams).ThenInclude(tm => tm.Players)
            .Include(x => x.Groups).ThenInclude(g => g.Teams)
            .FirstOrDefaultAsync(x => x.Slug == slug);
        if (t is null) return false;

        Tournament = t;
        Matches = await _db.TournamentMatches
            .Where(m => m.TournamentId == t.Id)
            .Include(m => m.TeamA)
            .Include(m => m.TeamB)
            .Include(m => m.WinnerTeam)
            .Include(m => m.Group)
            .OrderBy(m => m.Stage).ThenBy(m => m.BracketSide).ThenBy(m => m.Round).ThenBy(m => m.SlotIndex)
            .ThenBy(m => m.GroupRound)
            .ToListAsync();

        var userId = _users.GetUserId(User);
        CanManage = userId is not null &&
            (Tournament.OrganizerId == userId || User.IsInRole(DbSeeder.ModeratorRole));
        AlreadyRegistered = userId is not null && Tournament.Teams.Any(tm => tm.CaptainId == userId);

        foreach (var g in Tournament.Groups.Where(g => !g.IsConcluded))
            GroupStandings[g.Id] = await _tournaments.ComputeGroupStandingsAsync(g.Id);

        return true;
    }
}
