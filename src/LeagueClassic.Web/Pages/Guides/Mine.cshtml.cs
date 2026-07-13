using LeagueClassic.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueClassic.Web.Pages.Guides;

[Authorize]
public class MineModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public MineModel(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public List<Guide> Guides { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var uid = _users.GetUserId(User);
        Guides = await _db.Guides
            .Where(g => g.AuthorId == uid)
            .Include(g => g.Champion)
            .OrderByDescending(g => g.UpdatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var uid = _users.GetUserId(User);
        var guide = await _db.Guides.FirstOrDefaultAsync(g => g.Id == id);
        if (guide is null) return RedirectToPage();
        if (guide.AuthorId != uid) return Forbid();

        _db.Guides.Remove(guide);
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Deleted “{guide.Title}”.";
        return RedirectToPage();
    }
}
