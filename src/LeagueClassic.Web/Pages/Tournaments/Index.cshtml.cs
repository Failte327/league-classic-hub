using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Tournaments;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public string Tab { get; private set; } = "upcoming";
    public List<Tournament> Tournaments { get; private set; } = new();

    public async Task OnGetAsync(string? tab)
    {
        Tab = tab == "past" ? "past" : "upcoming";

        var query = _db.Tournaments.Include(t => t.Organizer).Include(t => t.Teams).AsNoTracking().AsQueryable();
        query = Tab == "past"
            ? query.Where(t => t.Status == TournamentStatus.Completed).OrderByDescending(t => t.CompletedAt)
            : query.Where(t => t.Status != TournamentStatus.Completed).OrderBy(t => t.ScheduledAt);

        Tournaments = await query.ToListAsync();
    }
}
